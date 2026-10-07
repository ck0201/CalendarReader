namespace CalendarReader.Tests;

using CalendarReader.Models;
using CalendarReader.Services;
using Xunit;

public class MultiCalendarSelectionTests
{
    private readonly CalendarAnalysisService _service = new();

    [Fact]
    public void UserCalendar_Metadata_FlagsPrimaryAndSharedCorrectly()
    {
        var primaryCal = new UserCalendar
        {
            Id = "primary",
            AccountId = "acc1",
            AccountEmail = "user@gmail.com",
            Summary = "user@gmail.com",
            IsPrimary = true,
            AccessRole = "owner"
        };

        var ownedSecondary = new UserCalendar
        {
            Id = "cal-side-project",
            AccountId = "acc1",
            AccountEmail = "user@gmail.com",
            Summary = "Side Projects",
            IsPrimary = false,
            AccessRole = "owner"
        };

        var sharedTeamCal = new UserCalendar
        {
            Id = "team-cal-123",
            AccountId = "acc1",
            AccountEmail = "user@gmail.com",
            Summary = "Engineering Team Schedule",
            IsPrimary = false,
            AccessRole = "reader"
        };

        Assert.True(primaryCal.IsPrimary);
        Assert.False(primaryCal.IsShared);

        Assert.False(ownedSecondary.IsPrimary);
        Assert.False(ownedSecondary.IsShared);

        Assert.False(sharedTeamCal.IsPrimary);
        Assert.True(sharedTeamCal.IsShared);
    }

    [Fact]
    public void MultiCalendar_EventsCarryCalendarMetadataIntoConflicts()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var personalFamilyEvent = new CalendarEvent
        {
            Id = "p_fam_1",
            Title = "Family Lunch",
            Start = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 13, 30, 0, TimeSpan.Zero),
            AccountEmail = "personal@gmail.com",
            CalendarId = "cal_family",
            CalendarName = "Family & Home"
        };

        var workProjectEvent = new CalendarEvent
        {
            Id = "w_proj_1",
            Title = "Architecture Sync",
            Start = new DateTimeOffset(2026, 10, 6, 13, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            AccountEmail = "work@company.com",
            CalendarId = "cal_project_apollo",
            CalendarName = "Project Apollo"
        };

        var result = _service.Analyze([personalFamilyEvent, workProjectEvent], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(1, health.TotalConflicts);
        var conflict = Assert.Single(health.Conflicts);
        Assert.True(conflict.IsCrossAccount);

        Assert.Equal("Family & Home", conflict.EventA.CalendarName);
        Assert.Equal("Project Apollo", conflict.EventB.CalendarName);
        Assert.Equal(TimeSpan.FromMinutes(30), conflict.OverlapDuration);
    }

    [Fact]
    public void MultiCalendar_SameAccountDifferentCalendars_DetectsOverlapWithDistinctCalendarNames()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var primaryEvent = new CalendarEvent
        {
            Id = "work_main_1",
            Title = "All-Hands Meeting",
            Start = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 16, 0, 0, TimeSpan.Zero),
            AccountEmail = "work@company.com",
            CalendarId = "primary",
            CalendarName = "Work Primary"
        };

        var onCallEvent = new CalendarEvent
        {
            Id = "work_oncall_1",
            Title = "Incident Response Handover",
            Start = new DateTimeOffset(2026, 10, 6, 15, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 16, 30, 0, TimeSpan.Zero),
            AccountEmail = "work@company.com",
            CalendarId = "cal_oncall",
            CalendarName = "DevOps On-Call"
        };

        var result = _service.Analyze([primaryEvent, onCallEvent], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(1, health.TotalConflicts);
        var conflict = Assert.Single(health.Conflicts);
        Assert.False(conflict.IsCrossAccount);
        Assert.Equal("work@company.com", conflict.AccountEmail);

        Assert.Equal("Work Primary", conflict.EventA.CalendarName);
        Assert.Equal("DevOps On-Call", conflict.EventB.CalendarName);
    }
}
