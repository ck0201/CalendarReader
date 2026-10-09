namespace CalendarReader.Tests;

using CalendarReader.Models;
using CalendarReader.Services;
using Xunit;

public class ScheduleSnapshotTests
{
    private readonly CalendarAnalysisService _service = new();

    [Fact]
    public void CreateScheduleSnapshot_NullInsights_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.CreateScheduleSnapshot(null!));
    }

    [Fact]
    public void CreateScheduleSnapshot_DefaultOptions_ExcludesSensitiveFieldsAndTitles()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 5, 23, 59, 59, TimeSpan.Zero);

        var ev1 = new CalendarEvent
        {
            Id = "ev-1",
            Title = "Confidential Executive Strategy",
            Description = "Secret discussion about M&A, attendee: alice@partner.corp",
            AccountEmail = "user@work.com",
            CalendarId = "work-cal",
            CalendarName = "Work Calendar",
            HtmlLink = "https://calendar.google.com/event?eid=123",
            Start = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero),
            AttendeeCount = 4
        };

        var ev2 = new CalendarEvent
        {
            Id = "ev-2",
            Title = "Doctor Appointment - Oncology",
            Description = "Medical notes and clinic room #402",
            AccountEmail = "personal@gmail.com",
            CalendarId = "personal-cal",
            CalendarName = "Personal",
            HtmlLink = "https://calendar.google.com/event?eid=456",
            Start = new DateTimeOffset(2026, 10, 5, 9, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 10, 30, 0, TimeSpan.Zero),
            AttendeeCount = 1
        };

        var task = new CalendarTask
        {
            Id = "task-1",
            Title = "Prepare financial forecast for Board",
            Description = "Confidential metrics spreadsheet",
            AccountEmail = "user@work.com",
            HtmlLink = "https://tasks.google.com/task-1",
            DueDate = new DateTimeOffset(2026, 10, 5, 14, 0, 0, TimeSpan.Zero),
            IsCompleted = false
        };

        var insights = _service.Analyze([ev1, ev2], start, end, [task]);

        // Act - Call default without includeTitles (or includeTitles: false)
        var snapshot = _service.CreateScheduleSnapshot(insights);

        // Assert - Titles are excluded by default
        Assert.False(snapshot.TitlesIncluded);

        // Time blocks in days
        Assert.NotEmpty(snapshot.Days);
        var day = snapshot.Days[0];
        Assert.Equal(2, day.TimeBlocks.Count);
        foreach (var block in day.TimeBlocks)
        {
            Assert.Null(block.Title);
        }

        // Conflicts
        Assert.NotEmpty(snapshot.Conflicts);
        foreach (var conflict in snapshot.Conflicts)
        {
            Assert.Null(conflict.EventATitle);
            Assert.Null(conflict.EventBTitle);
        }

        // Tasks
        Assert.NotEmpty(snapshot.Tasks);
        foreach (var t in snapshot.Tasks)
        {
            Assert.Null(t.Title);
        }
    }

    [Fact]
    public void CreateScheduleSnapshot_WithIncludeTitlesTrue_PopulatesTitles()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var ev1 = new CalendarEvent
        {
            Id = "e1",
            Title = "Quarterly Review",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AttendeeCount = 2
        };

        var ev2 = new CalendarEvent
        {
            Id = "e2",
            Title = "Team Standup",
            Start = new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 30, 0, TimeSpan.Zero),
            AttendeeCount = 5
        };

        var task = new CalendarTask
        {
            Id = "t1",
            Title = "Send meeting notes",
            DueDate = new DateTimeOffset(2026, 10, 6, 17, 0, 0, TimeSpan.Zero),
            IsCompleted = true
        };

        var insights = _service.Analyze([ev1, ev2], start, end, [task]);

        // Act
        var snapshot = _service.CreateScheduleSnapshot(insights, includeTitles: true);

        // Assert
        Assert.True(snapshot.TitlesIncluded);

        var day = snapshot.Days[0];
        Assert.Equal("Quarterly Review", day.TimeBlocks[0].Title);
        Assert.Equal("Team Standup", day.TimeBlocks[1].Title);

        Assert.NotEmpty(snapshot.Conflicts);
        Assert.Equal("Quarterly Review", snapshot.Conflicts[0].EventATitle);
        Assert.Equal("Team Standup", snapshot.Conflicts[0].EventBTitle);

        Assert.NotEmpty(snapshot.Tasks);
        Assert.Equal("Send meeting notes", snapshot.Tasks[0].Title);
    }

    [Fact]
    public void CreateScheduleSnapshot_PreservesAuthoritativeCalculatedMetrics()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero); // Monday
        var end = new DateTimeOffset(2026, 10, 7, 23, 59, 59, TimeSpan.Zero); // Wednesday

        var ev1 = new CalendarEvent
        {
            Id = "1",
            Title = "Design Sprint",
            Start = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero), // 3 hours
            AttendeeCount = 4,
            IsRecurring = true
        };

        var ev2 = new CalendarEvent
        {
            Id = "2",
            Title = "Focus Work",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero), // 1 hour
            AttendeeCount = 1
        };

        var task = new CalendarTask
        {
            Id = "t1",
            Title = "Write Specs",
            DueDate = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero),
            IsCompleted = false
        };

        var insights = _service.Analyze([ev1, ev2], start, end, [task]);

        // Act
        var snapshot = _service.CreateScheduleSnapshot(insights);

        // Assert - Authoritative aggregate metrics match insights exactly
        Assert.Equal(insights.StartDate, snapshot.StartDate);
        Assert.Equal(insights.EndDate, snapshot.EndDate);
        Assert.Equal(insights.TotalEvents, snapshot.TotalEventsCount);
        Assert.Equal(insights.TimedEventsCount, snapshot.TimedEventsCount);
        Assert.Equal(insights.AllDayEventsCount, snapshot.AllDayEventsCount);
        Assert.Equal(insights.FormattedScheduledTime, snapshot.TotalScheduledTimeFormatted);
        Assert.Equal(insights.FormattedMeetingTime, snapshot.TotalMeetingTimeFormatted);
        Assert.Equal(insights.RecurringTime.PercentageOfScheduledTime, snapshot.RecurringMeetingPercentage);
        Assert.Equal(insights.RecurringTime.FormattedDuration, snapshot.RecurringMeetingTimeFormatted);

        // Focus block & Busiest day
        Assert.Equal(insights.LongestFocusBlock?.FormattedDuration ?? "0m", snapshot.FocusBlockDurationFormatted);
        Assert.Equal(insights.LongestFocusBlock?.DayOfWeek ?? "None", snapshot.FocusBlockDayOfWeek);
        Assert.Equal(insights.BusiestDay?.DayOfWeek ?? "None", snapshot.BusiestDayOfWeek);
        Assert.Equal(insights.BusiestDay?.FormattedScheduledTime ?? "0m", snapshot.BusiestDayScheduledTimeFormatted);
        Assert.Equal(insights.BusiestDay?.EventCount ?? 0, snapshot.BusiestDayEventCount);

        // Tasks & Conflicts
        Assert.Equal(1, snapshot.TotalTasksCount);
        Assert.Equal(1, snapshot.PendingTasksCount);
        Assert.Equal(0, snapshot.CompletedTasksCount);
        Assert.Equal(insights.ScheduleHealth?.TotalConflicts ?? 0, snapshot.TotalConflictsCount);
        Assert.Equal(insights.ScheduleHealth?.FormattedConflictDuration ?? "0m", snapshot.TotalConflictDurationFormatted);
    }

    [Fact]
    public void CreateScheduleSnapshot_MultiDayBreakdown_AccuratelyDistributesDaysAndFlagsConflicts()
    {
        // Arrange
        var monday = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var wednesday = new DateTimeOffset(2026, 10, 7, 23, 59, 59, TimeSpan.Zero);

        // Monday: 2 overlapping events (conflict)
        var monA = new CalendarEvent
        {
            Id = "mon-1",
            Title = "Conflicting A",
            Start = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero),
            AttendeeCount = 2
        };
        var monB = new CalendarEvent
        {
            Id = "mon-2",
            Title = "Conflicting B",
            Start = new DateTimeOffset(2026, 10, 5, 9, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 10, 30, 0, TimeSpan.Zero),
            AttendeeCount = 2
        };

        // Tuesday: No events (empty day)

        // Wednesday: 1 all-day event and 1 task
        var wedAllDay = new CalendarEvent
        {
            Id = "wed-1",
            Title = "Company Holiday",
            Start = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 7, 23, 59, 59, TimeSpan.Zero),
            IsAllDay = true
        };

        var wedTask = new CalendarTask
        {
            Id = "task-wed",
            Title = "Holiday prep",
            DueDate = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero),
            IsCompleted = true
        };

        var insights = _service.Analyze([monA, monB, wedAllDay], monday, wednesday, [wedTask]);

        // Act
        var snapshot = _service.CreateScheduleSnapshot(insights);

        // Assert - Exactly 3 days spanned
        Assert.Equal(3, snapshot.Days.Count);

        // Monday
        var dayMon = snapshot.Days[0];
        Assert.Equal("Monday", dayMon.DayOfWeek);
        Assert.Equal(2, dayMon.EventCount);
        Assert.Equal(2, dayMon.TimedEventCount);
        Assert.Equal(0, dayMon.AllDayEventCount);
        Assert.Equal(1, dayMon.ConflictCount);
        Assert.All(dayMon.TimeBlocks, b => Assert.True(b.IsConflicted));

        // Tuesday
        var dayTue = snapshot.Days[1];
        Assert.Equal("Tuesday", dayTue.DayOfWeek);
        Assert.Equal(0, dayTue.EventCount);
        Assert.Equal(0, dayTue.ConflictCount);
        Assert.Equal("0m", dayTue.ScheduledTimeFormatted);
        Assert.Empty(dayTue.TimeBlocks);

        // Wednesday
        var dayWed = snapshot.Days[2];
        Assert.Equal("Wednesday", dayWed.DayOfWeek);
        Assert.Equal(1, dayWed.EventCount);
        Assert.Equal(0, dayWed.TimedEventCount);
        Assert.Equal(1, dayWed.AllDayEventCount);
        Assert.Equal(1, dayWed.TaskCount);
        Assert.Single(dayWed.TimeBlocks);
        Assert.True(dayWed.TimeBlocks[0].IsAllDay);
        Assert.False(dayWed.TimeBlocks[0].IsConflicted);
    }

    [Fact]
    public void CreateScheduleSnapshot_EmptyInsights_ReturnsSafeDefaults()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 5, 23, 59, 59, TimeSpan.Zero);
        var insights = _service.Analyze([], start, end);

        // Act
        var snapshot = _service.CreateScheduleSnapshot(insights);

        // Assert
        Assert.Equal(0, snapshot.TotalEventsCount);
        Assert.Equal("0m", snapshot.TotalScheduledTimeFormatted);
        Assert.Equal("0m", snapshot.TotalMeetingTimeFormatted);
        Assert.Equal("None", snapshot.BusiestDayOfWeek);
        Assert.Equal(insights.LongestFocusBlock?.DayOfWeek ?? "None", snapshot.FocusBlockDayOfWeek);
        Assert.Equal(0, snapshot.TotalConflictsCount);
        Assert.Equal(0, snapshot.TotalTasksCount);
        Assert.Empty(snapshot.Conflicts);
        Assert.Empty(snapshot.Tasks);
        Assert.Single(snapshot.Days);
        Assert.Equal(0, snapshot.Days[0].EventCount);
        Assert.Empty(snapshot.Days[0].TimeBlocks);
    }
}
