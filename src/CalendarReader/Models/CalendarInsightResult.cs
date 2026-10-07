namespace CalendarReader.Models;

public class CalendarInsightResult
{
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public int TotalEvents { get; set; }
    public int TimedEventsCount { get; set; }
    public int AllDayEventsCount { get; set; }
    public TimeSpan TotalScheduledTime { get; set; }
    public string FormattedScheduledTime { get; set; } = string.Empty;
    public TimeSpan TotalMeetingTime { get; set; }
    public string FormattedMeetingTime { get; set; } = string.Empty;
    public FocusBlockInfo LongestFocusBlock { get; set; } = new();
    public BusiestDayInfo BusiestDay { get; set; } = new();
    public RecurringTimeInfo RecurringTime { get; set; } = new();
    public List<string> Observations { get; set; } = new();
    public List<CalendarEvent> Events { get; set; } = new();
    public List<CalendarTask> Tasks { get; set; } = new();
    public ScheduleHealthInfo ScheduleHealth { get; set; } = new();
}

public class FocusBlockInfo
{
    public TimeSpan Duration { get; set; }
    public string FormattedDuration { get; set; } = string.Empty;
    public string DayOfWeek { get; set; } = string.Empty;
    public DateTimeOffset? Start { get; set; }
    public DateTimeOffset? End { get; set; }
}

public class BusiestDayInfo
{
    public string DayOfWeek { get; set; } = string.Empty;
    public string DateFormatted { get; set; } = string.Empty;
    public TimeSpan ScheduledTime { get; set; }
    public string FormattedScheduledTime { get; set; } = string.Empty;
    public int EventCount { get; set; }
}

public class RecurringTimeInfo
{
    public TimeSpan Duration { get; set; }
    public string FormattedDuration { get; set; } = string.Empty;
    public int PercentageOfScheduledTime { get; set; }
    public int RecurringEventCount { get; set; }
}

public class AuthStatusResponse
{
    public bool IsAuthenticated { get; set; }
    public bool IsConfigured { get; set; }
    public string? MissingConfigMessage { get; set; }
    public List<ConnectedAccount> Accounts { get; set; } = [];
}

public class ScheduleHealthInfo
{
    public int TotalConflicts { get; set; }
    public TimeSpan TotalConflictDuration { get; set; }
    public string FormattedConflictDuration { get; set; } = "0m";
    public int AffectedEventsCount { get; set; }
    public int ConflictFreeDaysCount { get; set; }
    public string MostConflictedDay { get; set; } = "None";
    public bool HasTodayConflict { get; set; }
    public int CrossAccountConflictsCount { get; set; }
    public int SameAccountConflictsCount { get; set; }
    public List<ScheduleConflict> Conflicts { get; set; } = [];
    public List<ScheduleSuggestion> Suggestions { get; set; } = [];
}

public class ScheduleConflict
{
    public string Id { get; set; } = string.Empty;
    public ConflictEventSummary EventA { get; set; } = new();
    public ConflictEventSummary EventB { get; set; } = new();
    public DateTimeOffset OverlapStart { get; set; }
    public DateTimeOffset OverlapEnd { get; set; }
    public TimeSpan OverlapDuration { get; set; }
    public string FormattedOverlapDuration { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public bool IsCrossAccount { get; set; }
}

public class ConflictEventSummary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? HtmlLink { get; set; }
    public string AccountEmail { get; set; } = string.Empty;
}

public class ScheduleSuggestion
{
    public string Type { get; set; } = string.Empty; // "conflict", "back-to-back", "heavy-day", "healthy"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning"; // "warning", "info", "success"
}

