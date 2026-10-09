namespace CalendarReader.Services;

using CalendarReader.Models;

/// <summary>
/// Cache abstraction for storing and retrieving AI calendar briefings.
/// Isolates entries by authenticated session, date range, calendar IDs, and schedule fingerprint.
/// </summary>
public interface IAiBriefingCache
{
    string CreateCacheKey(
        string sessionId,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<string>? calendarIds,
        ScheduleSnapshot snapshot);

    bool TryGet(string cacheKey, out AiBriefingResult result);

    void Set(string cacheKey, AiBriefingResult result, TimeSpan? expiration = null);
}
