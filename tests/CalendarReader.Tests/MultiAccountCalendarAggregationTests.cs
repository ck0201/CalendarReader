namespace CalendarReader.Tests;

using CalendarReader.Models;
using CalendarReader.Services;
using Xunit;

public class MultiAccountCalendarAggregationTests
{
    private readonly CalendarAnalysisService _analysisService = new();

    [Fact]
    public void MultiAccount_CombinesEventsAndCalculatesConsolidatedMetrics()
    {
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 11, 23, 59, 59, TimeSpan.Zero);

        // Account 1: Personal events
        var personalEvent = new CalendarEvent
        {
            Id = "acc-personal_ev1",
            Title = "Doctor Appointment",
            AccountEmail = "personal@gmail.com",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AttendeeCount = 1
        };

        // Account 2: Work events
        var workEvent = new CalendarEvent
        {
            Id = "acc-work_ev1",
            Title = "Sprint Planning Meeting",
            AccountEmail = "work@gmail.com",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 30, 0, TimeSpan.Zero),
            AttendeeCount = 5
        };

        var combinedEvents = new List<CalendarEvent> { personalEvent, workEvent };

        var personalTask = new CalendarTask
        {
            Id = "acc-personal_task1",
            Title = "Pick up prescription",
            DueDate = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
            AccountEmail = "personal@gmail.com"
        };

        var workTask = new CalendarTask
        {
            Id = "acc-work_task1",
            Title = "Submit release report",
            DueDate = new DateTimeOffset(2026, 10, 7, 17, 0, 0, TimeSpan.Zero),
            AccountEmail = "work@gmail.com"
        };

        var combinedTasks = new List<CalendarTask> { personalTask, workTask };

        var result = _analysisService.Analyze(combinedEvents, start, end, combinedTasks);

        // Verify aggregation
        Assert.Equal(2, result.TotalEvents);
        Assert.Equal(TimeSpan.FromMinutes(150), result.TotalScheduledTime); // 60 + 90 min
        Assert.Equal("2h 30m", result.FormattedScheduledTime);
        Assert.Equal(TimeSpan.FromMinutes(90), result.TotalMeetingTime); // 90 min meeting (5 attendees)
        Assert.Equal(2, result.Tasks.Count);

        // Verify AccountEmail is preserved
        Assert.Equal("personal@gmail.com", result.Events[0].AccountEmail);
        Assert.Equal("work@gmail.com", result.Events[1].AccountEmail);
        Assert.Equal("personal@gmail.com", result.Tasks[0].AccountEmail);
        Assert.Equal("work@gmail.com", result.Tasks[1].AccountEmail);
    }

    [Fact]
    public void MultiAccount_TrainJourneyFilter_DetectsTrainJourneysFromAnyAccount()
    {
        var personalTrainEvent = new CalendarEvent
        {
            Id = "acc-personal_train",
            Title = "Train Journey 12951 Mumbai Rajdhani",
            Description = "PNR: 2451234567, Coach B2, Seat 45",
            AccountEmail = "personal@gmail.com",
            IsTrainJourney = CalendarEvent.IsTrainJourneyMatch("Train Journey 12951 Mumbai Rajdhani", "PNR: 2451234567, Coach B2, Seat 45")
        };

        var workTrainEvent = new CalendarEvent
        {
            Id = "acc-work_train",
            Title = "IRCTC Corporate Booking: Delhi to Jaipur",
            Description = "Railway travel for client meeting",
            AccountEmail = "work@gmail.com",
            IsTrainJourney = CalendarEvent.IsTrainJourneyMatch("IRCTC Corporate Booking: Delhi to Jaipur", "Railway travel for client meeting")
        };

        var workRegularMeeting = new CalendarEvent
        {
            Id = "acc-work_meeting",
            Title = "Product Review",
            Description = "Weekly product roadmap discussion",
            AccountEmail = "work@gmail.com",
            IsTrainJourney = CalendarEvent.IsTrainJourneyMatch("Product Review", "Weekly product roadmap discussion")
        };

        var allEvents = new List<CalendarEvent> { personalTrainEvent, workTrainEvent, workRegularMeeting };

        var trainJourneys = allEvents.Where(e => e.IsTrainJourney).ToList();
        var otherEvents = allEvents.Where(e => !e.IsTrainJourney).ToList();

        Assert.Equal(2, trainJourneys.Count);
        Assert.Contains(trainJourneys, e => e.AccountEmail == "personal@gmail.com");
        Assert.Contains(trainJourneys, e => e.AccountEmail == "work@gmail.com");

        Assert.Single(otherEvents);
        Assert.Equal("acc-work_meeting", otherEvents[0].Id);
    }

    [Fact]
    public void MultiAccount_ToTaskConversion_PreservesAccountEmail()
    {
        var ev = new CalendarEvent
        {
            Id = "ev-task-1",
            Title = "[Task] Prepare presentation deck",
            Start = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero),
            AccountEmail = "work@gmail.com"
        };

        Assert.True(ev.IsTask);
        var task = ev.ToTask();

        Assert.Equal("Prepare presentation deck", task.Title);
        Assert.Equal("work@gmail.com", task.AccountEmail);
    }
}
