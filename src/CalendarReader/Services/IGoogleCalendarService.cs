namespace CalendarReader.Services;

using CalendarReader.Models;

public interface IGoogleCalendarService
{
    string GetAuthorizationUrl(string sessionId);
    Task<bool> HandleAuthorizationCallbackAsync(string code, string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(string sessionId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarTask>> GetTasksAsync(string sessionId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}
