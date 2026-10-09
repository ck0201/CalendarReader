namespace CalendarReader.Models;

/// <summary>
/// Represents the structure of an AI-generated calendar briefing.
/// </summary>
public class AiBriefingResult
{
    public string Headline { get; set; } = string.Empty;
    public string NarrativeSummary { get; set; } = string.Empty;
    public List<string> PreparationPriorities { get; set; } = [];
    public List<string> SchedulingSuggestions { get; set; } = [];
    public string EnergyProfile { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsCached { get; set; }
    public bool IsFallback { get; set; }
}

/// <summary>
/// Privacy-minimized schedule snapshot derived from verified calendar metrics.
/// Excludes attendee emails/names, event descriptions, account IDs, session identifiers, and URLs.
/// By default, raw event and task titles are also excluded.
/// </summary>
public class ScheduleSnapshot
{
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    // Aggregate schedule metrics (directly sourced from verified CalendarInsightResult)
    public int TotalEventsCount { get; set; }
    public int TimedEventsCount { get; set; }
    public int AllDayEventsCount { get; set; }
    public string TotalScheduledTimeFormatted { get; set; } = string.Empty;
    public string TotalMeetingTimeFormatted { get; set; } = string.Empty;
    public int RecurringMeetingPercentage { get; set; }
    public string RecurringMeetingTimeFormatted { get; set; } = string.Empty;

    // Focus block info (directly sourced from verified CalendarInsightResult)
    public string FocusBlockDurationFormatted { get; set; } = string.Empty;
    public string FocusBlockDayOfWeek { get; set; } = string.Empty;
    public string? FocusBlockWindowFormatted { get; set; }

    // Busiest day info (directly sourced from verified CalendarInsightResult)
    public string BusiestDayOfWeek { get; set; } = string.Empty;
    public string BusiestDayScheduledTimeFormatted { get; set; } = string.Empty;
    public int BusiestDayEventCount { get; set; }

    // Conflict metrics (directly sourced from verified ScheduleHealthInfo)
    public int TotalConflictsCount { get; set; }
    public string TotalConflictDurationFormatted { get; set; } = "0m";
    public int CrossAccountConflictsCount { get; set; }
    public int SameAccountConflictsCount { get; set; }
    public int ConflictFreeDaysCount { get; set; }
    public string MostConflictedDay { get; set; } = "None";
    public bool HasTodayConflict { get; set; }
    public List<ConflictSummarySnapshot> Conflicts { get; set; } = [];

    // Task metrics (directly sourced from verified CalendarInsightResult)
    public int TotalTasksCount { get; set; }
    public int PendingTasksCount { get; set; }
    public int CompletedTasksCount { get; set; }
    public List<TaskSummarySnapshot> Tasks { get; set; } = [];

    // Daily temporal breakdown
    public List<DayScheduleSnapshot> Days { get; set; } = [];

    // Indicates whether titles were explicitly requested and included
    public bool TitlesIncluded { get; set; }
}

public class DayScheduleSnapshot
{
    public DateTimeOffset Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public string DateFormatted { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public int TimedEventCount { get; set; }
    public int AllDayEventCount { get; set; }
    public string ScheduledTimeFormatted { get; set; } = "0m";
    public string MeetingTimeFormatted { get; set; } = "0m";
    public int ConflictCount { get; set; }
    public int TaskCount { get; set; }
    public List<EventBlockSnapshot> TimeBlocks { get; set; } = [];
}

public class EventBlockSnapshot
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string DurationFormatted { get; set; } = string.Empty;
    public string TimeRangeFormatted { get; set; } = string.Empty;
    public bool IsAllDay { get; set; }
    public bool IsMeeting { get; set; }
    public bool IsRecurring { get; set; }
    public bool IsConflicted { get; set; }
    public string? Title { get; set; } // Null by default; only populated when TitlesIncluded is true
}

public class ConflictSummarySnapshot
{
    public DateTimeOffset OverlapStart { get; set; }
    public DateTimeOffset OverlapEnd { get; set; }
    public string OverlapDurationFormatted { get; set; } = string.Empty;
    public bool IsCrossAccount { get; set; }
    public string? EventATitle { get; set; } // Null by default; only populated when TitlesIncluded is true
    public string? EventBTitle { get; set; } // Null by default; only populated when TitlesIncluded is true
}

public class TaskSummarySnapshot
{
    public DateTimeOffset? DueDate { get; set; }
    public string? DueDateFormatted { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsAllDay { get; set; }
    public string? Title { get; set; } // Null by default; only populated when TitlesIncluded is true
}
