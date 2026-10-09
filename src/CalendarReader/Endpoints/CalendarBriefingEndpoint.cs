namespace CalendarReader.Endpoints;

using CalendarReader.Models;
using CalendarReader.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public static class CalendarBriefingEndpoint
{
    public const string SessionCookieName = "cr_session";
    public const int MaxBriefingDays = 31;

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        DateTimeOffset? start,
        DateTimeOffset? end,
        string? calendars,
        IGoogleCalendarService calendarService,
        ICalendarAnalysisService analysisService,
        IAiBriefingService briefingService,
        IAiBriefingCache briefingCache,
        IAccountTokenStore tokenStore,
        ILogger logger,
        CancellationToken ct)
    {
        var sessionId = context.Request.Cookies[SessionCookieName];
        if (string.IsNullOrEmpty(sessionId) || !await tokenStore.HasAnyAccountAsync(sessionId, ct))
        {
            return Results.Unauthorized();
        }

        // Explicit start and end query parameters are strictly required for the AI briefing endpoint
        // to prevent unintended AI provider invocations and uncontrolled token consumption.
        if (!start.HasValue || !end.HasValue)
        {
            return Results.BadRequest(new { error = "Both 'start' and 'end' date query parameters are required." });
        }

        var rangeStart = start.Value;
        var rangeEnd = end.Value;

        if (rangeEnd <= rangeStart)
        {
            return Results.BadRequest(new { error = "End date must be greater than start date." });
        }

        if ((rangeEnd - rangeStart).TotalDays > MaxBriefingDays)
        {
            return Results.BadRequest(new { error = $"Date range cannot exceed {MaxBriefingDays} days." });
        }

        var calendarIds = !string.IsNullOrWhiteSpace(calendars)
            ? calendars.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        try
        {
            var events = await calendarService.GetEventsAsync(sessionId, rangeStart, rangeEnd, calendarIds, ct);
            var tasks = await calendarService.GetTasksAsync(sessionId, rangeStart, rangeEnd, ct);
            var insights = analysisService.Analyze(events, rangeStart, rangeEnd, tasks);
            var snapshot = analysisService.CreateScheduleSnapshot(insights, includeTitles: false);

            var cacheKey = briefingCache.CreateCacheKey(sessionId, rangeStart, rangeEnd, calendarIds, snapshot);
            if (briefingCache.TryGet(cacheKey, out var cachedResult))
            {
                return Results.Ok(cachedResult);
            }

            var briefing = await briefingService.GenerateBriefingAsync(snapshot, ct);
            briefingCache.Set(cacheKey, briefing);

            return Results.Ok(briefing);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error generating calendar briefing.");
            return Results.Problem(
                detail: "An unexpected error occurred while generating the calendar briefing.",
                title: "Failed to generate calendar briefing.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
