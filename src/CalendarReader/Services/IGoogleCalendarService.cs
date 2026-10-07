namespace CalendarReader.Services;

using CalendarReader.Models;

public interface IGoogleCalendarService
{
    string GetAuthorizationUrl(string sessionId, string? prompt = null);
    Task<bool> HandleAuthorizationCallbackAsync(string code, string sessionId, CancellationToken cancellationToken = default);
    Task<bool> DisconnectAccountAsync(string sessionId, string accountId, CancellationToken cancellationToken = default);
    Task<bool> DisconnectAllAccountsAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(string sessionId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarTask>> GetTasksAsync(string sessionId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}
