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
}
