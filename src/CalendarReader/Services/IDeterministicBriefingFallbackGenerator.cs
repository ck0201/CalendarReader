namespace CalendarReader.Services;

using CalendarReader.Models;

/// <summary>
/// Generates deterministic schedule briefings using verified calculated metrics
/// and observations without requiring an external AI provider or network access.
/// </summary>
public interface IDeterministicBriefingFallbackGenerator
{
    AiBriefingResult GenerateFallback(ScheduleSnapshot snapshot);
}
