namespace CalendarReader.Services;

using CalendarReader.Models;

/// <summary>
/// Service abstraction for generating calendar schedule briefings.
/// Handles external AI provider integration with deterministic fallback.
/// </summary>
public interface IAiBriefingService
{
    Task<AiBriefingResult> GenerateBriefingAsync(ScheduleSnapshot snapshot, CancellationToken cancellationToken = default);
}
