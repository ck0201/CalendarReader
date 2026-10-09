namespace CalendarReader.Services;

using CalendarReader.Models;

public class DeterministicBriefingFallbackGenerator : IDeterministicBriefingFallbackGenerator
{
    public AiBriefingResult GenerateFallback(ScheduleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var headline = BuildHeadline(snapshot);
        var narrative = BuildNarrativeSummary(snapshot);
        var priorities = BuildPreparationPriorities(snapshot);
        var suggestions = BuildSchedulingSuggestions(snapshot);
        var energyProfile = BuildEnergyProfile(snapshot);

        return new AiBriefingResult
        {
            Headline = headline,
            NarrativeSummary = narrative,
            PreparationPriorities = priorities,
            SchedulingSuggestions = suggestions,
            EnergyProfile = energyProfile,
            IsFallback = true,
            IsCached = false,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string BuildHeadline(ScheduleSnapshot snapshot)
    {
        if (snapshot.TotalEventsCount == 0)
        {
            return "Open Schedule Ahead";
        }

        if (snapshot.TotalConflictsCount > 0)
        {
            var conflictWord = snapshot.TotalConflictsCount == 1 ? "Conflict" : "Conflicts";
            return $"Schedule Notice: {snapshot.TotalConflictsCount} {conflictWord} Detected";
        }

        if (snapshot.RecurringMeetingPercentage >= 50)
        {
            return "Meeting-Intensive Schedule with High Recurring Load";
        }

        if (snapshot.TimedEventsCount > 0)
        {
            return $"Schedule Overview: {snapshot.TotalEventsCount} Events ({snapshot.TotalScheduledTimeFormatted})";
        }

        return "Schedule Overview";
    }

    private static string BuildNarrativeSummary(ScheduleSnapshot snapshot)
    {
        if (snapshot.TotalEventsCount == 0)
        {
            return "You have no scheduled commitments in this period. Your calendar is clear for proactive planning or uninterrupted deep work.";
        }

        var parts = new List<string>
        {
            $"You have {snapshot.TotalScheduledTimeFormatted} of scheduled commitments across {snapshot.TotalEventsCount} events ({snapshot.TotalMeetingTimeFormatted} in meetings)."
        };

        if (snapshot.BusiestDayEventCount > 0 &&
            !string.IsNullOrWhiteSpace(snapshot.BusiestDayOfWeek) &&
            snapshot.BusiestDayOfWeek != "None")
        {
            parts.Add($"Your busiest day is {snapshot.BusiestDayOfWeek} with {snapshot.BusiestDayScheduledTimeFormatted} scheduled across {snapshot.BusiestDayEventCount} events.");
        }

        if (snapshot.FocusBlockDurationFormatted != "0m" &&
            !string.IsNullOrWhiteSpace(snapshot.FocusBlockDayOfWeek) &&
            snapshot.FocusBlockDayOfWeek != "None")
        {
            var windowSuffix = !string.IsNullOrWhiteSpace(snapshot.FocusBlockWindowFormatted)
                ? $" ({snapshot.FocusBlockWindowFormatted})"
                : string.Empty;
            parts.Add($"Your longest uninterrupted focus block is {snapshot.FocusBlockDurationFormatted} on {snapshot.FocusBlockDayOfWeek}{windowSuffix}.");
        }

        if (snapshot.TotalConflictsCount > 0)
        {
            var conflictWord = snapshot.TotalConflictsCount == 1 ? "conflict" : "conflicts";
            parts.Add($"You have {snapshot.TotalConflictsCount} overlapping {conflictWord} totaling {snapshot.TotalConflictDurationFormatted}.");
        }

        return string.Join(" ", parts);
    }

    private static List<string> BuildPreparationPriorities(ScheduleSnapshot snapshot)
    {
        var priorities = new List<string>();

        if (snapshot.PendingTasksCount > 0)
        {
            var taskWord = snapshot.PendingTasksCount == 1 ? "task" : "tasks";
            priorities.Add($"Address {snapshot.PendingTasksCount} pending {taskWord} due during this period.");
        }

        if (snapshot.HasTodayConflict)
        {
            priorities.Add("Resolve active schedule conflicts occurring today.");
        }
        else if (snapshot.TotalConflictsCount > 0)
        {
            if (snapshot.CrossAccountConflictsCount > 0)
            {
                var conflictWord = snapshot.CrossAccountConflictsCount == 1 ? "conflict" : "conflicts";
                priorities.Add($"Resolve {snapshot.CrossAccountConflictsCount} cross-account {conflictWord} between personal and work calendars.");
            }
            else
            {
                var conflictWord = snapshot.TotalConflictsCount == 1 ? "conflict" : "conflicts";
                priorities.Add($"Resolve {snapshot.TotalConflictsCount} overlapping schedule {conflictWord}.");
            }
        }

        if (snapshot.TotalEventsCount > 0 && priorities.Count == 0)
        {
            priorities.Add("Review upcoming agenda items and allocate focus blocks for deep work.");
        }

        if (snapshot.TotalEventsCount == 0 && priorities.Count == 0)
        {
            priorities.Add("Plan priorities and key milestones for the upcoming period.");
        }

        return priorities;
    }

    private static List<string> BuildSchedulingSuggestions(ScheduleSnapshot snapshot)
    {
        var suggestions = new List<string>();

        if (snapshot.TotalConflictsCount > 0)
        {
            suggestions.Add("Adjust start or end times of overlapping events to eliminate double-booking.");
        }

        if (snapshot.RecurringMeetingPercentage >= 40)
        {
            suggestions.Add($"Recurring meetings consume {snapshot.RecurringMeetingPercentage}% of scheduled time ({snapshot.RecurringMeetingTimeFormatted}). Consider auditing recurring series for potential cadence reductions.");
        }

        if (snapshot.FocusBlockDurationFormatted != "0m" &&
            !string.IsNullOrWhiteSpace(snapshot.FocusBlockDayOfWeek) &&
            snapshot.FocusBlockDayOfWeek != "None")
        {
            suggestions.Add($"Protect your {snapshot.FocusBlockDurationFormatted} focus block on {snapshot.FocusBlockDayOfWeek} from new meeting bookings.");
        }

        if (snapshot.TotalConflictsCount == 0 && snapshot.TotalEventsCount > 0)
        {
            suggestions.Add("Maintain current schedule boundaries to preserve conflict-free days.");
        }

        if (snapshot.TotalEventsCount == 0)
        {
            suggestions.Add("Block dedicated focus time on your primary calendar before new invites arrive.");
        }

        return suggestions;
    }

    private static string BuildEnergyProfile(ScheduleSnapshot snapshot)
    {
        if (snapshot.TotalConflictsCount > 0)
        {
            return "High Contention — Schedule requires active conflict resolution";
        }

        if (snapshot.RecurringMeetingPercentage >= 50)
        {
            return "Meeting Intensive — High recurring meeting load with fragmented focus";
        }

        if (snapshot.TimedEventsCount == 0)
        {
            return "Wide Open — Full autonomy over working hours";
        }

        return "Balanced — Structured schedule with available focus windows";
    }
}
