namespace CalendarReader.Services;

using System.Globalization;
using System.Text.Json;
using CalendarReader.Models;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class GoogleCalendarService : IGoogleCalendarService
{
    private const string OpenIdScope = "openid";
    private const string EmailScope = "https://www.googleapis.com/auth/userinfo.email";
    private const string ProfileScope = "https://www.googleapis.com/auth/userinfo.profile";
    private const string ReadonlyCalendarScope = "https://www.googleapis.com/auth/calendar.readonly";
    private static readonly string[] RequiredScopes =
    [
        OpenIdScope,
        EmailScope,
        ProfileScope,
        ReadonlyCalendarScope
    ];

    private readonly GoogleAuthConfig _config;
    private readonly IAccountTokenStore _tokenStore;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(
        IOptions<GoogleAuthConfig> config,
        IAccountTokenStore tokenStore,
        HttpClient httpClient,
        ILogger<GoogleCalendarService> logger)
    {
        _config = config.Value;
        _tokenStore = tokenStore;
        _httpClient = httpClient;
        _logger = logger;
    }

    private GoogleAuthorizationCodeFlow CreateFlow()
    {
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _config.ClientId,
                ClientSecret = _config.ClientSecret
            },
            Scopes = RequiredScopes
        });
    }

    public string GetAuthorizationUrl(string sessionId, string? prompt = null)
    {
        if (!_config.IsConfigured)
        {
            throw new InvalidOperationException("Google OAuth is not configured. Missing ClientId, ClientSecret, or RedirectUri.");
        }

        var authPrompt = string.IsNullOrWhiteSpace(prompt) ? "select_account consent" : $"{prompt} consent";

        var authUri = new GoogleAuthorizationCodeRequestUrl(new Uri("https://accounts.google.com/o/oauth2/v2/auth"))
        {
            ClientId = _config.ClientId,
            RedirectUri = _config.RedirectUri,
            Scope = string.Join(" ", RequiredScopes),
            AccessType = "offline",
            Prompt = authPrompt,
            State = sessionId
        }.Build();

        return authUri.ToString();
    }

    public async Task<bool> HandleAuthorizationCallbackAsync(string code, string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Authorization code cannot be empty.", nameof(code));
        }

        var flow = CreateFlow();
        _logger.LogInformation("Exchanging authorization code for tokens for session {SessionId}", sessionId);

        var tokenResponse = await flow.ExchangeCodeForTokenAsync(sessionId, code, _config.RedirectUri, cancellationToken);
        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            _logger.LogWarning("Token exchange returned empty or null token response for session {SessionId}", sessionId);
            return false;
        }

        var (sub, email, name, picture) = await GetGoogleUserInfoAsync(tokenResponse.AccessToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Failed to resolve Google user identity for session {SessionId}", sessionId);
            return false;
        }

        var accountToken = new AccountTokenData
        {
            SessionId = sessionId,
            AccountId = sub,
            Email = email,
            DisplayName = !string.IsNullOrWhiteSpace(name) ? name : email,
            AvatarUrl = picture,
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            IssuedUtc = tokenResponse.IssuedUtc,
            ExpiresInSeconds = tokenResponse.ExpiresInSeconds,
            ConnectedAt = DateTimeOffset.UtcNow,
            RequiresReauth = false
        };

        await _tokenStore.SaveAccountTokenAsync(sessionId, accountToken, cancellationToken);
        _logger.LogInformation("Successfully stored OAuth tokens for account {Email} ({AccountId}) in session {SessionId}", email, sub, sessionId);
        return true;
    }

    public async Task<bool> DisconnectAccountAsync(string sessionId, string accountId, CancellationToken cancellationToken = default)
    {
        var account = await _tokenStore.GetAccountTokenAsync(sessionId, accountId, cancellationToken);
        if (account == null)
        {
            return false;
        }

        // Revoke token with Google (best-effort)
        var tokenToRevoke = !string.IsNullOrWhiteSpace(account.RefreshToken) ? account.RefreshToken : account.AccessToken;
        if (!string.IsNullOrWhiteSpace(tokenToRevoke))
        {
            await RevokeGoogleTokenAsync(tokenToRevoke, cancellationToken);
        }

        await _tokenStore.RemoveAccountAsync(sessionId, accountId, cancellationToken);
        _logger.LogInformation("Disconnected account {Email} ({AccountId}) from session {SessionId}", account.Email, accountId, sessionId);
        return true;
    }

    public async Task<bool> DisconnectAllAccountsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var accounts = await _tokenStore.GetAllAccountTokensAsync(sessionId, cancellationToken);
        foreach (var acc in accounts)
        {
            var tokenToRevoke = !string.IsNullOrWhiteSpace(acc.RefreshToken) ? acc.RefreshToken : acc.AccessToken;
            if (!string.IsNullOrWhiteSpace(tokenToRevoke))
            {
                await RevokeGoogleTokenAsync(tokenToRevoke, cancellationToken);
            }
        }

        await _tokenStore.RemoveAllAccountsAsync(sessionId, cancellationToken);
        _logger.LogInformation("Disconnected all accounts for session {SessionId}", sessionId);
        return true;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string sessionId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _tokenStore.GetAllAccountTokensAsync(sessionId, cancellationToken);
        if (accounts.Count == 0)
        {
            throw new UnauthorizedAccessException("Not authenticated or session has expired.");
        }

        var activeAccounts = accounts.Where(a => !a.RequiresReauth).ToList();
        if (activeAccounts.Count == 0)
        {
            throw new UnauthorizedAccessException("All connected accounts require re-authorization.");
        }

        var fetchTasks = activeAccounts.Select(acc => FetchEventsForAccountAsync(sessionId, acc, start, end, cancellationToken));
        var results = await Task.WhenAll(fetchTasks);

        return results
            .SelectMany(events => events)
            .OrderBy(e => e.Start)
            .ToList();
    }

    public Task<IReadOnlyList<CalendarTask>> GetTasksAsync(
        string sessionId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<CalendarTask>>([]);
    }

    private async Task<IReadOnlyList<CalendarEvent>> FetchEventsForAccountAsync(
        string sessionId,
        AccountTokenData account,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct)
    {
        try
        {
            var tokenResponse = new TokenResponse
            {
                AccessToken = account.AccessToken,
                RefreshToken = account.RefreshToken,
                IssuedUtc = account.IssuedUtc?.UtcDateTime ?? DateTime.UtcNow,
                ExpiresInSeconds = account.ExpiresInSeconds
            };

            var flow = CreateFlow();
            var credential = new UserCredential(flow, account.AccountId, tokenResponse);

            using var calendarService = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "CalendarReader"
            });

            var request = calendarService.Events.List("primary");
            request.TimeMinDateTimeOffset = start;
            request.TimeMaxDateTimeOffset = end;
            request.SingleEvents = true; // Expand recurring events into individual occurrences
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            _logger.LogInformation("Retrieving calendar events for account {Email} from {Start} to {End}", account.Email, start, end);
            var googleEvents = await request.ExecuteAsync(ct);

            // If token refreshed automatically during the call, persist updated tokens
            if (!string.Equals(tokenResponse.AccessToken, account.AccessToken, StringComparison.Ordinal))
            {
                account.AccessToken = tokenResponse.AccessToken;
                account.IssuedUtc = tokenResponse.IssuedUtc;
                account.ExpiresInSeconds = tokenResponse.ExpiresInSeconds;
                await _tokenStore.SaveAccountTokenAsync(sessionId, account, ct);
            }

            var mappedEvents = new List<CalendarEvent>();
            if (googleEvents?.Items == null)
            {
                return mappedEvents;
            }

            foreach (var item in googleEvents.Items)
            {
                if (item.Status == "cancelled")
                {
                    continue;
                }

                var isAllDay = item.Start?.DateTimeDateTimeOffset == null && !string.IsNullOrEmpty(item.Start?.Date);
                DateTimeOffset eventStart;
                DateTimeOffset eventEnd;

                if (isAllDay)
                {
                    if (DateTime.TryParseExact(item.Start?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sDate))
                    {
                        eventStart = new DateTimeOffset(sDate, TimeSpan.Zero);
                    }
                    else
                    {
                        eventStart = start;
                    }

                    if (DateTime.TryParseExact(item.End?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var eDate))
                    {
                        eventEnd = new DateTimeOffset(eDate, TimeSpan.Zero);
                    }
                    else
                    {
                        eventEnd = eventStart.AddDays(1);
                    }
                }
                else
                {
                    eventStart = item.Start?.DateTimeDateTimeOffset ?? start;
                    eventEnd = item.End?.DateTimeDateTimeOffset ?? eventStart.AddMinutes(30);
                }

                var title = string.IsNullOrWhiteSpace(item.Summary) ? "(No Title)" : item.Summary.Trim();
                var description = item.Description?.Trim();
                var isTrain = CalendarEvent.IsTrainJourneyMatch(title, description);

                mappedEvents.Add(new CalendarEvent
                {
                    Id = $"{account.AccountId}_{item.Id ?? Guid.NewGuid().ToString()}",
                    Title = title,
                    Description = description,
                    IsTrainJourney = isTrain,
                    Start = eventStart,
                    End = eventEnd,
                    IsAllDay = isAllDay,
                    IsRecurring = !string.IsNullOrEmpty(item.RecurringEventId),
                    AttendeeCount = item.Attendees?.Count ?? 0,
                    Status = item.Status,
                    HtmlLink = item.HtmlLink,
                    AccountEmail = account.Email
                });
            }

            return mappedEvents;
        }
        catch (TokenResponseException trEx)
        {
            _logger.LogWarning("Token refresh failed for account {Email}: {Error}", account.Email, trEx.Error?.ErrorDescription ?? trEx.Message);
            await _tokenStore.MarkRequiresReauthAsync(sessionId, account.AccountId, ct);
            return [];
        }
        catch (GoogleApiException gEx) when (gEx.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Google Calendar API returned 401 Unauthorized for account {Email}: {Message}", account.Email, gEx.Message);
            await _tokenStore.MarkRequiresReauthAsync(sessionId, account.AccountId, ct);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve calendar events for account {Email}", account.Email);
            return [];
        }
    }


    private async Task<(string Sub, string Email, string Name, string? Picture)> GetGoogleUserInfoAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var root = doc.RootElement;
            var sub = root.TryGetProperty("sub", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() ?? string.Empty : string.Empty;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? email : email;
            var picture = root.TryGetProperty("picture", out var p) ? p.GetString() : null;

            return (sub, email, name, picture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch userinfo from Google");
            return (string.Empty, string.Empty, string.Empty, null);
        }
    }

    private async Task RevokeGoogleTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token
            });

            using var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/revoke", content, ct);
            _logger.LogInformation("Google token revocation returned HTTP {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to revoke token with Google revocation endpoint");
        }
    }
}
