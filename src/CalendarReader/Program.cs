using CalendarReader.Models;
using CalendarReader.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Configure Google OAuth settings from configuration, user secrets, or environment variables
builder.Services.Configure<GoogleAuthConfig>(options =>
{
    var configSection = builder.Configuration.GetSection("Google");

    string GetConfigValue(string flatKey, string sectionKey, string defaultValue = "")
    {
        var val = builder.Configuration[flatKey];
        if (string.IsNullOrWhiteSpace(val))
        {
            val = configSection[sectionKey];
        }
        if (string.IsNullOrWhiteSpace(val))
        {
            val = Environment.GetEnvironmentVariable(flatKey);
        }
        return string.IsNullOrWhiteSpace(val) ? defaultValue : val;
    }

    options.ClientId = GetConfigValue("GOOGLE_CLIENT_ID", "ClientId");
    options.ClientSecret = GetConfigValue("GOOGLE_CLIENT_SECRET", "ClientSecret");
    options.RedirectUri = GetConfigValue("GOOGLE_REDIRECT_URI", "RedirectUri", "http://localhost:5000/api/auth/callback");
});

// Register Application Services
builder.Services.AddDataProtection();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAccountTokenStore, SqliteAccountTokenStore>();
builder.Services.AddSingleton<ICalendarAnalysisService, CalendarAnalysisService>();
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();

var app = builder.Build();

const string SessionCookieName = "cr_session";

string GetOrCreateSessionId(HttpContext context)
{
    if (context.Request.Cookies.TryGetValue(SessionCookieName, out var existing) && !string.IsNullOrWhiteSpace(existing))
    {
        return existing;
    }

    var newId = Guid.NewGuid().ToString("N");
    context.Response.Cookies.Append(SessionCookieName, newId, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Secure = context.Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddDays(7)
    });
    return newId;
}

// Static Files & Default Page (serves wwwroot/index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// -------------------------------------------------------------
// Authentication Endpoints
// -------------------------------------------------------------

app.MapGet("/api/auth/status", async (
    HttpContext context,
    IAccountTokenStore tokenStore,
    IOptions<GoogleAuthConfig> config,
    CancellationToken ct) =>
{
    var cfg = config.Value;
    var sessionId = context.Request.Cookies[SessionCookieName];
    var accounts = !string.IsNullOrEmpty(sessionId)
        ? await tokenStore.GetAccountsAsync(sessionId, ct)
        : (IReadOnlyList<ConnectedAccount>)[];

    var isAuthenticated = accounts.Any(a => !a.RequiresReauth);
    var isConfigured = cfg.IsConfigured;

    return Results.Ok(new AuthStatusResponse
    {
        IsAuthenticated = isAuthenticated,
        IsConfigured = isConfigured,
        MissingConfigMessage = isConfigured
            ? null
            : "Google OAuth is not configured. Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET to enable Google connection.",
        Accounts = accounts.ToList()
    });
});

app.MapGet("/api/auth/login", (
    HttpContext context,
    IGoogleCalendarService calendarService,
    IOptions<GoogleAuthConfig> config,
    string? prompt) =>
{
    if (!config.Value.IsConfigured)
    {
        return Results.BadRequest(new
        {
            error = "Google OAuth credentials are not configured.",
            details = "Please set GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET, and GOOGLE_REDIRECT_URI."
        });
    }

    var sessionId = GetOrCreateSessionId(context);
    var authUrl = calendarService.GetAuthorizationUrl(sessionId, prompt);
    return Results.Redirect(authUrl);
});

app.MapGet("/api/auth/callback", async (
    HttpContext context,
    string? code,
    string? state,
    string? error,
    IGoogleCalendarService calendarService,
    CancellationToken ct) =>
{
    if (!string.IsNullOrEmpty(error))
    {
        return Results.Redirect($"/?error={Uri.EscapeDataString(error)}");
    }

    if (string.IsNullOrEmpty(code))
    {
        return Results.Redirect("/?error=missing_code");
    }

    var sessionId = !string.IsNullOrWhiteSpace(state) ? state : GetOrCreateSessionId(context);
    context.Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Secure = context.Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddDays(7)
    });

    var success = await calendarService.HandleAuthorizationCallbackAsync(code, sessionId, ct);
    if (!success)
    {
        return Results.Redirect("/?error=token_exchange_failed");
    }

    return Results.Redirect("/");
});

app.MapDelete("/api/auth/accounts/{id}", async (
    HttpContext context,
    string id,
    IGoogleCalendarService calendarService,
    IAccountTokenStore tokenStore,
    CancellationToken ct) =>
{
    var sessionId = context.Request.Cookies[SessionCookieName];
    if (string.IsNullOrEmpty(sessionId))
    {
        return Results.Unauthorized();
    }

    var success = await calendarService.DisconnectAccountAsync(sessionId, id, ct);
    var remaining = await tokenStore.GetAccountsAsync(sessionId, ct);
    if (remaining.Count == 0)
    {
        context.Response.Cookies.Delete(SessionCookieName);
    }

    return Results.Ok(new { success });
});

app.MapPost("/api/auth/logout", async (
    HttpContext context,
    IGoogleCalendarService calendarService,
    CancellationToken ct) =>
{
    if (context.Request.Cookies.TryGetValue(SessionCookieName, out var sessionId) && !string.IsNullOrWhiteSpace(sessionId))
    {
        await calendarService.DisconnectAllAccountsAsync(sessionId, ct);
    }
    context.Response.Cookies.Delete(SessionCookieName);
    return Results.Ok(new { success = true });
});

// -------------------------------------------------------------
// Calendar Endpoints
// -------------------------------------------------------------

app.MapGet("/api/calendar/calendars", async (
    HttpContext context,
    IGoogleCalendarService calendarService,
    IAccountTokenStore tokenStore,
    CancellationToken ct) =>
{
    var sessionId = context.Request.Cookies[SessionCookieName];
    if (string.IsNullOrEmpty(sessionId) || !await tokenStore.HasAnyAccountAsync(sessionId, ct))
    {
        return Results.Unauthorized();
    }

    try
    {
        var calendars = await calendarService.GetCalendarsAsync(sessionId, ct);
        return Results.Ok(calendars);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Failed to retrieve user calendars.");
    }
});

app.MapGet("/api/calendar/events", async (
    HttpContext context,
    DateTimeOffset? start,
    DateTimeOffset? end,
    string? filter,
    string? calendars,
    IGoogleCalendarService calendarService,
    IAccountTokenStore tokenStore,
    CancellationToken ct) =>
{
    var sessionId = context.Request.Cookies[SessionCookieName];
    if (string.IsNullOrEmpty(sessionId) || !await tokenStore.HasAnyAccountAsync(sessionId, ct))
    {
        return Results.Unauthorized();
    }

    var rangeStart = start ?? DateTimeOffset.UtcNow.Date;
    var rangeEnd = end ?? rangeStart.AddDays(7);

    if (rangeEnd <= rangeStart)
    {
        return Results.BadRequest(new { error = "End date must be greater than start date." });
    }

    var calendarIds = !string.IsNullOrWhiteSpace(calendars)
        ? calendars.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : null;

    try
    {
        var events = await calendarService.GetEventsAsync(sessionId, rangeStart, rangeEnd, calendarIds, ct);
        if (string.Equals(filter, "train", StringComparison.OrdinalIgnoreCase))
        {
            events = events.Where(e => e.IsTrainJourney).ToList();
        }
        else if (string.Equals(filter, "other", StringComparison.OrdinalIgnoreCase))
        {
            events = events.Where(e => !e.IsTrainJourney).ToList();
        }
        return Results.Ok(events);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Failed to retrieve calendar events.");
    }
});

app.MapGet("/api/calendar/tasks", async (
    HttpContext context,
    DateTimeOffset? start,
    DateTimeOffset? end,
    IGoogleCalendarService calendarService,
    IAccountTokenStore tokenStore,
    CancellationToken ct) =>
{
    var sessionId = context.Request.Cookies[SessionCookieName];
    if (string.IsNullOrEmpty(sessionId) || !await tokenStore.HasAnyAccountAsync(sessionId, ct))
    {
        return Results.Unauthorized();
    }

    var rangeStart = start ?? DateTimeOffset.UtcNow.Date;
    var rangeEnd = end ?? rangeStart.AddDays(7);

    if (rangeEnd <= rangeStart)
    {
        return Results.BadRequest(new { error = "End date must be greater than start date." });
    }

    try
    {
        var googleTasks = await calendarService.GetTasksAsync(sessionId, rangeStart, rangeEnd, ct);
        var events = await calendarService.GetEventsAsync(sessionId, rangeStart, rangeEnd, null, ct);
        var eventTasks = events.Where(e => e.IsTask).Select(e => e.ToTask());

        var allTasks = new List<CalendarTask>(googleTasks);
        var existingIds = new HashSet<string>(allTasks.Select(t => t.Id));
        foreach (var t in eventTasks)
        {
            if (!existingIds.Contains(t.Id))
            {
                allTasks.Add(t);
                existingIds.Add(t.Id);
            }
        }

        return Results.Ok(allTasks.OrderBy(t => t.DueDate).ToList());
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Failed to retrieve calendar tasks.");
    }
});

app.MapGet("/api/calendar/insights", async (
    HttpContext context,
    DateTimeOffset? start,
    DateTimeOffset? end,
    string? calendars,
    IGoogleCalendarService calendarService,
    ICalendarAnalysisService analysisService,
    IAccountTokenStore tokenStore,
    CancellationToken ct) =>
{
    var sessionId = context.Request.Cookies[SessionCookieName];
    if (string.IsNullOrEmpty(sessionId) || !await tokenStore.HasAnyAccountAsync(sessionId, ct))
    {
        return Results.Unauthorized();
    }

    var rangeStart = start ?? DateTimeOffset.UtcNow.Date;
    var rangeEnd = end ?? rangeStart.AddDays(7);

    if (rangeEnd <= rangeStart)
    {
        return Results.BadRequest(new { error = "End date must be greater than start date." });
    }

    var calendarIds = !string.IsNullOrWhiteSpace(calendars)
        ? calendars.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : null;

    try
    {
        var events = await calendarService.GetEventsAsync(sessionId, rangeStart, rangeEnd, calendarIds, ct);
        var tasks = await calendarService.GetTasksAsync(sessionId, rangeStart, rangeEnd, ct);
        var insights = analysisService.Analyze(events, rangeStart, rangeEnd, tasks);
        return Results.Ok(insights);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Failed to generate calendar insights.");
    }
});

app.Run();
