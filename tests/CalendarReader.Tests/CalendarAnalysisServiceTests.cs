namespace CalendarReader.Tests;

using CalendarReader.Models;
using CalendarReader.Services;
using Xunit;

public class CalendarAnalysisServiceTests
{
    private readonly CalendarAnalysisService _service = new();

    [Fact]
    public void Analyze_EmptyEvents_ReturnsZeroMetricsAndSafeObservations()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 11, 23, 59, 59, TimeSpan.Zero);
        var events = new List<CalendarEvent>();

        // Act
        var result = _service.Analyze(events, start, end);

        // Assert
        Assert.Equal(0, result.TotalEvents);
        Assert.Equal(TimeSpan.Zero, result.TotalScheduledTime);
        Assert.Equal("0m", result.FormattedScheduledTime);
        Assert.Equal(TimeSpan.Zero, result.TotalMeetingTime);
        Assert.Equal(0, result.RecurringTime.PercentageOfScheduledTime);
        Assert.Equal("None", result.BusiestDay.DayOfWeek);
        Assert.NotEmpty(result.Observations);
        Assert.InRange(result.Observations.Count, 3, 5);
    }

    [Fact]
    public void Analyze_SingleEvent_CalculatesScheduledTimeAndBusiestDay()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero); // Tuesday
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);
        var events = new List<CalendarEvent>
        {
            new()
            {
                Id = "1",
                Title = "Sprint Planning",
                Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 6, 11, 30, 0, TimeSpan.Zero),
                IsAllDay = false,
                IsRecurring = false,
                AttendeeCount = 3
            }
        };

        // Act
        var result = _service.Analyze(events, start, end);

        // Assert
        Assert.Equal(1, result.TotalEvents);
        Assert.Equal(TimeSpan.FromMinutes(90), result.TotalScheduledTime);
        Assert.Equal("1h 30m", result.FormattedScheduledTime);
        Assert.Equal(TimeSpan.FromMinutes(90), result.TotalMeetingTime);
        Assert.Equal("Tuesday", result.BusiestDay.DayOfWeek);
        Assert.Equal(1, result.BusiestDay.EventCount);
    }

    [Fact]
    public void Analyze_MultipleDays_IdentifiesCorrectBusiestDay()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 9, 23, 59, 59, TimeSpan.Zero);
        var events = new List<CalendarEvent>
        {
            // Monday: 1h
            new()
            {
                Id = "1",
                Title = "Quick Sync",
                Start = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero),
                IsAllDay = false
            },
            // Tuesday: 3h total (2 events)
            new()
            {
                Id = "2",
                Title = "Architecture Review",
                Start = new DateTimeOffset(2026, 10, 6, 9, 30, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 6, 11, 30, 0, TimeSpan.Zero),
                IsAllDay = false
            },
            new()
            {
                Id = "3",
                Title = "1:1 Sync",
                Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
                IsAllDay = false
            },
            // Wednesday: 45m
            new()
            {
                Id = "4",
                Title = "Standup",
                Start = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 7, 9, 45, 0, TimeSpan.Zero),
                IsAllDay = false
            }
        };

        // Act
        var result = _service.Analyze(events, start, end);

        // Assert
        Assert.Equal("Tuesday", result.BusiestDay.DayOfWeek);
        Assert.Equal(TimeSpan.FromHours(3), result.BusiestDay.ScheduledTime);
        Assert.Equal("3h", result.BusiestDay.FormattedScheduledTime);
        Assert.Equal(2, result.BusiestDay.EventCount);
    }

    [Fact]
    public void Analyze_CalculatesLongestFocusBlockCorrectly()
    {
        // Daytime window is 9:00 - 18:00 (9 hours total)
        // Day: 2026-10-06
        // Event 1: 9:00 - 10:00 (1h)
        // Event 2: 15:00 - 16:00 (1h)
        // Free blocks:
        // 10:00 - 15:00 -> 5 hours!
        // 16:00 - 18:00 -> 2 hours
        // Max focus block should be 5 hours.
        var start = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 18, 0, 0, TimeSpan.Zero);
        var events = new List<CalendarEvent>
        {
            new()
            {
                Id = "1",
                Title = "Morning Standup",
                Start = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
                IsAllDay = false
            },
            new()
            {
                Id = "2",
                Title = "Afternoon Check-in",
                Start = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 6, 16, 0, 0, TimeSpan.Zero),
                IsAllDay = false
            }
        };

        // Act
        var result = _service.Analyze(events, start, end);

        // Assert
        Assert.Equal(TimeSpan.FromHours(5), result.LongestFocusBlock.Duration);
        Assert.Equal("5h", result.LongestFocusBlock.FormattedDuration);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero), result.LongestFocusBlock.Start);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero), result.LongestFocusBlock.End);
    }

    [Fact]
    public void Analyze_RecurringEvents_CalculatesPercentageCorrectly()
    {
        // 2 hours scheduled total: 1 hour recurring, 1 hour non-recurring -> 50%
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 5, 23, 59, 59, TimeSpan.Zero);
        var events = new List<CalendarEvent>
        {
            new()
            {
                Id = "rec1",
                Title = "Weekly Team Sync",
                Start = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero),
                IsAllDay = false,
                IsRecurring = true
            },
            new()
            {
                Id = "one-off",
                Title = "Client Demo",
                Start = new DateTimeOffset(2026, 10, 5, 14, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero),
                IsAllDay = false,
                IsRecurring = false
            }
        };

        // Act
        var result = _service.Analyze(events, start, end);

        // Assert
        Assert.Equal(TimeSpan.FromHours(2), result.TotalScheduledTime);
        Assert.Equal(TimeSpan.FromHours(1), result.RecurringTime.Duration);
        Assert.Equal(50, result.RecurringTime.PercentageOfScheduledTime);
        Assert.Equal(1, result.RecurringTime.RecurringEventCount);
    }

    [Theory]
    [InlineData(0, "0m")]
    [InlineData(45, "45m")]
    [InlineData(60, "1h")]
    [InlineData(90, "1h 30m")]
    [InlineData(150, "2h 30m")]
    public void FormatDuration_FormatsExpectedStrings(int minutes, string expected)
    {
        var duration = TimeSpan.FromMinutes(minutes);
        var formatted = CalendarAnalysisService.FormatDuration(duration);
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("Train Journey to Mumbai", null, true)]
    [InlineData("IRCTC Ticket Confirmation", "PNR: 1234567890", true)]
    [InlineData("Trip Details", "Booked train ticket via Railway portal", true)]
    [InlineData("Train Departure: Platform 3", null, true)]
    [InlineData("Arrival in Delhi", "Train 12004", true)]
    [InlineData("Coach B3, Seat 21", "Train departure to Pune", true)]
    [InlineData("PNR: 8945231100", null, true)]
    [InlineData("Train #12951 Express", null, true)]
    [InlineData("Catching Train to Boston", "Departing South Station", true)]
    // False positive prevention tests:
    [InlineData("Departure: Platform 3", null, false)]
    [InlineData("Flight Departure: Terminal 2", "Gate 14", false)]
    [InlineData("Guest Arrival at Office", null, false)]
    [InlineData("VIP Arrival", "Scheduled at main entrance", false)]
    [InlineData("Project Departure meeting", null, false)]
    [InlineData("Training Session: Advanced C#", "Employee workshop", false)]
    [InlineData("Executive Trainer Briefing", null, false)]
    [InlineData("Coach & Mentoring 1:1", "Personal development", false)]
    [InlineData("Weekly 1:1 with Alex", "Discuss quarterly OKRs", false)]
    [InlineData("Doctor Appointment", null, false)]
    [InlineData("", "", false)]
    [InlineData(null, null, false)]
    public void IsTrainJourneyMatch_CorrectlyIdentifiesTrainEvents(string? title, string? description, bool expected)
    {
        var result = CalendarEvent.IsTrainJourneyMatch(title, description);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FilterUpcomingTrainJourneys_ReturnsOnlyUpcomingTrainJourneysWithinTwoMonthsAscending()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

        var pastTrain = new CalendarEvent
        {
            Id = "past",
            Title = "Train Journey to Mumbai",
            Start = now.AddDays(-5),
            End = now.AddDays(-4),
            IsTrainJourney = true
        };

        var upcomingTrain1 = new CalendarEvent
        {
            Id = "train-1",
            Title = "Train Journey to Delhi (Train 12004)",
            Start = now.AddDays(10),
            End = now.AddDays(10).AddHours(6),
            IsTrainJourney = true
        };

        var upcomingTrain2 = new CalendarEvent
        {
            Id = "train-2",
            Title = "IRCTC Ticket to Goa",
            Start = now.AddDays(2),
            End = now.AddDays(2).AddHours(8),
            IsTrainJourney = true
        };

        var futureTrainBeyond2Months = new CalendarEvent
        {
            Id = "far-future",
            Title = "Train Journey across Europe",
            Start = now.AddMonths(3),
            End = now.AddMonths(3).AddHours(5),
            IsTrainJourney = true
        };

        var upcomingRegularEvent = new CalendarEvent
        {
            Id = "meeting",
            Title = "Sprint Planning Meeting",
            Start = now.AddDays(3),
            End = now.AddDays(3).AddHours(1),
            IsTrainJourney = false
        };

        var allEvents = new List<CalendarEvent>
        {
            pastTrain,
            upcomingTrain1,
            upcomingTrain2,
            futureTrainBeyond2Months,
            upcomingRegularEvent
        };

        // Act
        var result = CalendarEvent.FilterUpcomingTrainJourneys(allEvents, now);

        // Assert
        Assert.Equal(2, result.Count);
        // Sorted ascending: train-2 (in 2 days) comes before train-1 (in 10 days)
        Assert.Equal("train-2", result[0].Id);
        Assert.Equal("train-1", result[1].Id);
    }

    [Theory]
    [InlineData("[Task] Complete slides", null, true)]
    [InlineData("Task: Review pull request", null, true)]
    [InlineData("Task - Fix bug", null, true)]
    [InlineData("[TODO] Buy milk", null, true)]
    [InlineData("TODO: File taxes", null, true)]
    [InlineData("[To-Do] Prepare report", null, true)]
    [InlineData("To-Do: Call accountant", null, true)]
    [InlineData("[Action] Submit paperwork", null, true)]
    [InlineData("Action: Send invoice", null, true)]
    [InlineData("Important note", "Follow up on this item #task", true)]
    [InlineData("Meeting notes", "Make sure to deploy #todo", true)]
    [InlineData("Task", null, true)]
    [InlineData("task", null, true)]
    [InlineData("Task 1", null, true)]
    [InlineData("Task force meeting", null, false)]
    [InlineData("Sprint Planning Meeting", null, false)]
    [InlineData("Doctor Appointment", "Annual checkup", false)]
    [InlineData("Flight to New York", "Terminal 4", false)]
    public void IsTaskMatch_DetectsTaskIndicatorsAccurately(string title, string? description, bool expected)
    {
        var actual = CalendarEvent.IsTaskMatch(title, description);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToTask_ConvertsCalendarEventAndCleansPrefix()
    {
        var ev = new CalendarEvent
        {
            Id = "task-1",
            Title = "Task: Write unit tests",
            Start = new DateTimeOffset(2026, 10, 8, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero),
            IsAllDay = false,
            Status = "confirmed",
            Description = "Add coverage for task models",
            HtmlLink = "https://calendar.google.com/event?id=task-1"
        };

        var task = ev.ToTask();

        Assert.Equal("task-1", task.Id);
        Assert.Equal("Write unit tests", task.Title);
        Assert.Equal(ev.Start, task.DueDate);
        Assert.False(task.IsCompleted);
        Assert.Equal("Add coverage for task models", task.Description);
        Assert.Equal("https://calendar.google.com/event?id=task-1", task.HtmlLink);
    }

    [Fact]
    public void ToTask_MarksCompletedWhenStatusOrTitleIndicates()
    {
        var completedByStatus = new CalendarEvent
        {
            Id = "task-completed-status",
            Title = "[TODO] Submit tax forms",
            Status = "completed"
        };

        var completedByTitle = new CalendarEvent
        {
            Id = "task-completed-title",
            Title = "[x] [Task] Send weekly update",
            Status = "confirmed"
        };

        var task1 = completedByStatus.ToTask();
        var task2 = completedByTitle.ToTask();

        Assert.True(task1.IsCompleted);
        Assert.True(task2.IsCompleted);
    }

    [Fact]
    public void Analyze_PopulatesTasksOrderedByDueDate()
    {
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 11, 23, 59, 59, TimeSpan.Zero);

        var taskLater = new CalendarEvent
        {
            Id = "t2",
            Title = "Task: Deploy to staging",
            Start = new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 9, 11, 0, 0, TimeSpan.Zero)
        };

        var taskEarlier = new CalendarEvent
        {
            Id = "t1",
            Title = "[TODO] Review schema",
            Start = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 9, 30, 0, TimeSpan.Zero)
        };

        var regularMeeting = new CalendarEvent
        {
            Id = "m1",
            Title = "Weekly Sync",
            Start = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 7, 11, 0, 0, TimeSpan.Zero)
        };

        var events = new List<CalendarEvent> { taskLater, regularMeeting, taskEarlier };

        var result = _service.Analyze(events, start, end);

        Assert.Equal(2, result.Tasks.Count);
        Assert.Equal("Review schema", result.Tasks[0].Title);
        Assert.Equal("Deploy to staging", result.Tasks[1].Title);
    }

    [Fact]
    public void GoogleTasksType_VerifyProperties()
    {
        var task = new Google.Apis.Tasks.v1.Data.Task
        {
            Id = "1",
            Title = "Test Task",
            Status = "needsAction",
            Due = "2026-10-07T00:00:00.000Z",
            Notes = "Note text",
            WebViewLink = "https://tasks.google.com"
        };
        Assert.Equal("Test Task", task.Title);
        Assert.Equal("2026-10-07T00:00:00.000Z", task.Due);
    }

    [Fact]
    public void Analyze_CombinesGoogleTasksAndCalendarEventTasks()
    {
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 11, 23, 59, 59, TimeSpan.Zero);

        var calendarTask = new CalendarEvent
        {
            Id = "cal-task-1",
            Title = "Task: Write report",
            Start = new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 8, 11, 0, 0, TimeSpan.Zero)
        };

        var regularEvent = new CalendarEvent
        {
            Id = "meeting-1",
            Title = "Weekly Sync",
            Start = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 7, 11, 0, 0, TimeSpan.Zero)
        };

        var googleTasks = new List<CalendarTask>
        {
            new()
            {
                Id = "gtask-1",
                Title = "Buy groceries",
                DueDate = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero),
                IsAllDay = true,
                IsCompleted = false
            }
        };

        var events = new List<CalendarEvent> { calendarTask, regularEvent };

        var result = _service.Analyze(events, start, end, googleTasks);

        Assert.Equal(2, result.Tasks.Count);
        // Sorted by DueDate: gtask-1 (Oct 6) then cal-task-1 (Oct 8)
        Assert.Equal("Buy groceries", result.Tasks[0].Title);
        Assert.Equal("Write report", result.Tasks[1].Title);
    }
}
