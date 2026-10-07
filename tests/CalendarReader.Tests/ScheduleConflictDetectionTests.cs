namespace CalendarReader.Tests;

using CalendarReader.Models;
using CalendarReader.Services;
using Xunit;

public class ScheduleConflictDetectionTests
{
    private readonly CalendarAnalysisService _service = new();

    [Fact]
    public void DetectConflicts_BasicOverlap_IdentifiesConflictAndOverlapDuration()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var eventA = new CalendarEvent
        {
            Id = "ev1",
            Title = "Sprint Planning",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var eventB = new CalendarEvent
        {
            Id = "ev2",
            Title = "Client Check-in",
            Start = new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 30, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([eventA, eventB], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(1, health.TotalConflicts);
        Assert.Equal(TimeSpan.FromMinutes(30), health.TotalConflictDuration);
        Assert.Equal("30m", health.FormattedConflictDuration);
        Assert.Equal(2, health.AffectedEventsCount);
        Assert.Single(health.Conflicts);

        var conflict = health.Conflicts[0];
        Assert.Equal("Sprint Planning", conflict.EventA.Title);
        Assert.Equal("Client Check-in", conflict.EventB.Title);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero), conflict.OverlapStart);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero), conflict.OverlapEnd);
        Assert.Equal(TimeSpan.FromMinutes(30), conflict.OverlapDuration);
        Assert.Equal("30m", conflict.FormattedOverlapDuration);
        Assert.Equal("user@example.com", conflict.AccountEmail);
    }

    [Fact]
    public void DetectConflicts_AdjacentEvents_AreNotConflicts()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var eventA = new CalendarEvent
        {
            Id = "ev1",
            Title = "Design Review",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var eventB = new CalendarEvent
        {
            Id = "ev2",
            Title = "Team Standup",
            Start = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([eventA, eventB], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(0, health.TotalConflicts);
        Assert.Equal(TimeSpan.Zero, health.TotalConflictDuration);
        Assert.Empty(health.Conflicts);

        // However, it should generate a deterministic back-to-back suggestion
        Assert.Contains(health.Suggestions, s => s.Type == "back-to-back" && s.Description.Contains("Team Standup"));
    }

    [Fact]
    public void DetectConflicts_ContainedEvent_DetectsFullOverlap()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var longEvent = new CalendarEvent
        {
            Id = "long",
            Title = "Workshop",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var shortEvent = new CalendarEvent
        {
            Id = "short",
            Title = "Quick Sync",
            Start = new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([longEvent, shortEvent], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(1, health.TotalConflicts);
        Assert.Equal(TimeSpan.FromMinutes(30), health.TotalConflictDuration);
        Assert.Equal(shortEvent.Start, health.Conflicts[0].OverlapStart);
        Assert.Equal(shortEvent.End, health.Conflicts[0].OverlapEnd);
    }

    [Fact]
    public void DetectConflicts_IdenticalStartAndEndTime_DetectsConflict()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var eventA = new CalendarEvent
        {
            Id = "ev1",
            Title = "Meeting A",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var eventB = new CalendarEvent
        {
            Id = "ev2",
            Title = "Meeting B",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([eventA, eventB], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(1, health.TotalConflicts);
        Assert.Equal(TimeSpan.FromHours(1), health.TotalConflictDuration);
        Assert.Equal("1h", health.FormattedConflictDuration);
    }

    [Fact]
    public void DetectConflicts_MultipleOverlaps_CalculatesMergedWallClockDuration()
    {
        // Event A: 10:00 - 11:00
        // Event B: 10:30 - 11:30
        // Event C: 10:45 - 12:00
        // Pairwise overlaps:
        // A & B: 10:30 - 11:00 (30m)
        // B & C: 10:45 - 11:30 (45m)
        // A & C: 10:45 - 11:00 (15m)
        // Pairwise sum: 30 + 45 + 15 = 90m
        // Merged wall-clock double-booked window: 10:30 to 11:30 = 60m (1h)
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var eventA = new CalendarEvent
        {
            Id = "A",
            Title = "Event A",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var eventB = new CalendarEvent
        {
            Id = "B",
            Title = "Event B",
            Start = new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 30, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var eventC = new CalendarEvent
        {
            Id = "C",
            Title = "Event C",
            Start = new DateTimeOffset(2026, 10, 6, 10, 45, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([eventA, eventB, eventC], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(3, health.TotalConflicts); // A-B, A-C, B-C
        Assert.Equal(TimeSpan.FromHours(1), health.TotalConflictDuration); // Merged wall-clock: 10:30-11:30
        Assert.Equal("1h", health.FormattedConflictDuration);
        Assert.Equal(3, health.AffectedEventsCount);
    }

    [Fact]
    public void DetectConflicts_Phase2_CrossAccount_DetectsOverlapBetweenDifferentAccounts()
    {
        // Account A: personal@gmail.com
        // Account B: work@gmail.com
        // Overlapping time ranges across different accounts are detected as cross-account conflicts.
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var personalEvent = new CalendarEvent
        {
            Id = "personal_ev1",
            Title = "Dentist Appointment",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "personal@gmail.com"
        };

        var workEvent = new CalendarEvent
        {
            Id = "work_ev1",
            Title = "Client Pitch",
            Start = new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 30, 0, TimeSpan.Zero),
            AccountEmail = "work@gmail.com"
        };

        var result = _service.Analyze([personalEvent, workEvent], start, end);
        var health = result.ScheduleHealth;

        // Phase 2 assertions
        Assert.Equal(1, health.TotalConflicts);
        Assert.Equal(TimeSpan.FromMinutes(30), health.TotalConflictDuration);
        Assert.Equal("30m", health.FormattedConflictDuration);
        Assert.Equal(1, health.CrossAccountConflictsCount);
        Assert.Equal(0, health.SameAccountConflictsCount);

        var conflict = Assert.Single(health.Conflicts);
        Assert.True(conflict.IsCrossAccount);
        Assert.Equal("Multiple Accounts", conflict.AccountEmail);
        Assert.Equal("Dentist Appointment", conflict.EventA.Title);
        Assert.Equal("personal@gmail.com", conflict.EventA.AccountEmail);
        Assert.Equal("Client Pitch", conflict.EventB.Title);
        Assert.Equal("work@gmail.com", conflict.EventB.AccountEmail);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero), conflict.OverlapStart);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero), conflict.OverlapEnd);
        Assert.Equal(TimeSpan.FromMinutes(30), conflict.OverlapDuration);
        Assert.Equal("30m", conflict.FormattedOverlapDuration);

        // Verify generated suggestion uses "Cross-Account Double Booking" and includes both account emails
        var suggestion = Assert.Single(health.Suggestions, s => s.Type == "conflict");
        Assert.Equal("Cross-Account Double Booking", suggestion.Title);
        Assert.Contains("personal@gmail.com", suggestion.Description);
        Assert.Contains("work@gmail.com", suggestion.Description);
        Assert.Contains("Dentist Appointment", suggestion.Description);
        Assert.Contains("Client Pitch", suggestion.Description);
    }

    [Fact]
    public void DetectConflicts_Phase2_CrossAccount_MirroredOrSharedInvite_SuppressesFalseConflict()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        // The user was invited to the same meeting on both work and personal accounts.
        // Same title (case-insensitive, trimmed) and exactly matching start/end times across two accounts:
        var workEvent = new CalendarEvent
        {
            Id = "work_shared_1",
            Title = "  Quarterly Company All-Hands  ",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
            AccountEmail = "work@company.com"
        };

        var personalEvent = new CalendarEvent
        {
            Id = "personal_shared_1",
            Title = "quarterly company all-hands",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
            AccountEmail = "personal@gmail.com"
        };

        var result = _service.Analyze([workEvent, personalEvent], start, end);
        var health = result.ScheduleHealth;

        // Mirrored shared invite is suppressed
        Assert.Equal(0, health.TotalConflicts);
        Assert.Equal(TimeSpan.Zero, health.TotalConflictDuration);
        Assert.Empty(health.Conflicts);
        Assert.Contains(health.Suggestions, s => s.Type == "healthy");
    }

    [Fact]
    public void DetectConflicts_Phase2_CrossAccount_MergedWallClockDuration_MultipleOverlappingAccounts()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var personalEvent = new CalendarEvent
        {
            Id = "pers1",
            Title = "Personal Errand",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "personal@gmail.com"
        };

        var workEvent = new CalendarEvent
        {
            Id = "work1",
            Title = "Client Call",
            Start = new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 30, 0, TimeSpan.Zero),
            AccountEmail = "work@gmail.com"
        };

        var consultingEvent = new CalendarEvent
        {
            Id = "cons1",
            Title = "Project Sync",
            Start = new DateTimeOffset(2026, 10, 6, 10, 45, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
            AccountEmail = "consulting@client.com"
        };

        var result = _service.Analyze([personalEvent, workEvent, consultingEvent], start, end);
        var health = result.ScheduleHealth;

        // 3 pairwise conflicts across the 3 accounts
        Assert.Equal(3, health.TotalConflicts);
        Assert.Equal(3, health.CrossAccountConflictsCount);
        Assert.Equal(0, health.SameAccountConflictsCount);
        // Merged wall-clock double-booked duration is 10:30 to 11:30 = 1 hour
        Assert.Equal(TimeSpan.FromHours(1), health.TotalConflictDuration);
        Assert.Equal("1h", health.FormattedConflictDuration);
    }

    [Fact]
    public void DetectConflicts_MultipleAccounts_DetectsConflictsWithinEachAccountIndependently()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        // Account 1: 2 overlapping events -> 1 conflict
        var p1 = new CalendarEvent
        {
            Id = "p1",
            Title = "Doctor",
            Start = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            AccountEmail = "personal@gmail.com"
        };
        var p2 = new CalendarEvent
        {
            Id = "p2",
            Title = "Pharmacy",
            Start = new DateTimeOffset(2026, 10, 6, 9, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 10, 15, 0, TimeSpan.Zero),
            AccountEmail = "personal@gmail.com"
        };

        // Account 2: 2 non-overlapping events -> 0 conflicts
        var w1 = new CalendarEvent
        {
            Id = "w1",
            Title = "Sprint Sync",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
            AccountEmail = "work@gmail.com"
        };
        var w2 = new CalendarEvent
        {
            Id = "w2",
            Title = "1:1",
            Start = new DateTimeOffset(2026, 10, 6, 15, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 16, 0, 0, TimeSpan.Zero),
            AccountEmail = "work@gmail.com"
        };

        var result = _service.Analyze([p1, p2, w1, w2], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(1, health.TotalConflicts);
        Assert.Equal("personal@gmail.com", health.Conflicts[0].AccountEmail);
    }

    [Fact]
    public void DetectConflicts_ExcludesAllDayEvents()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var allDayEvent = new CalendarEvent
        {
            Id = "allday1",
            Title = "National Holiday",
            Start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero),
            IsAllDay = true,
            AccountEmail = "user@example.com"
        };

        var timedEvent = new CalendarEvent
        {
            Id = "timed1",
            Title = "Client Call",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            IsAllDay = false,
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([allDayEvent, timedEvent], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(0, health.TotalConflicts);
        Assert.Empty(health.Conflicts);
    }

    [Fact]
    public void DetectConflicts_ExcludesZeroDurationEvents()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        var zeroDuration = new CalendarEvent
        {
            Id = "zero",
            Title = "Reminder Milestone",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var timedEvent = new CalendarEvent
        {
            Id = "timed",
            Title = "Meeting",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([zeroDuration, timedEvent], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(0, health.TotalConflicts);
    }

    [Fact]
    public void ScheduleHealth_Statistics_CalculatesMostConflictedDayAndConflictFreeDays()
    {
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 7, 23, 59, 59, TimeSpan.Zero);

        // Monday (2026-10-05): 2 overlapping events -> 1 conflict
        var mon1 = new CalendarEvent
        {
            Id = "m1",
            Title = "Sync 1",
            Start = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };
        var mon2 = new CalendarEvent
        {
            Id = "m2",
            Title = "Sync 2",
            Start = new DateTimeOffset(2026, 10, 5, 10, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 11, 30, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        // Tuesday (2026-10-06): 2 events, NO conflicts (conflict-free day)
        var tue1 = new CalendarEvent
        {
            Id = "t1",
            Title = "Planning",
            Start = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };
        var tue2 = new CalendarEvent
        {
            Id = "t2",
            Title = "Retro",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([mon1, mon2, tue1, tue2], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(1, health.TotalConflicts);
        Assert.Equal("Monday", health.MostConflictedDay);
        Assert.Equal(1, health.ConflictFreeDaysCount); // Tuesday is conflict-free
    }

    [Fact]
    public void ScheduleHealth_HeavyScheduleDay_GeneratesDeterministicSuggestion()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        // Tuesday has 3 meetings: 2h + 2h + 1.5h = 5.5 hours scheduled
        var ev1 = new CalendarEvent
        {
            Id = "h1",
            Title = "Morning Workshop",
            Start = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };
        var ev2 = new CalendarEvent
        {
            Id = "h2",
            Title = "Strategy Session",
            Start = new DateTimeOffset(2026, 10, 6, 11, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 13, 30, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };
        var ev3 = new CalendarEvent
        {
            Id = "h3",
            Title = "Architecture Deep Dive",
            Start = new DateTimeOffset(2026, 10, 6, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 15, 30, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([ev1, ev2, ev3], start, end);
        var health = result.ScheduleHealth;

        Assert.Contains(health.Suggestions, s => s.Type == "heavy-day" && s.Description.Contains("5h 30m"));
    }

    [Fact]
    public void DetectConflicts_DifferentTimeZones_ComparesAbsoluteInstantsAccurately()
    {
        var start = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero);

        // Event A: 10:00 - 11:00 UTC
        var eventA = new CalendarEvent
        {
            Id = "utc_ev",
            Title = "London Office Sync",
            Start = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 6, 11, 0, 0, TimeSpan.Zero),
            AccountEmail = "user@example.com"
        };

        // Event B: 15:30 - 16:30 with UTC+5:30 (India Standard Time).
        // 15:30 IST is exactly 10:00 UTC! So they completely overlap.
        var eventB = new CalendarEvent
        {
            Id = "ist_ev",
            Title = "Mumbai Office Sync",
            Start = new DateTimeOffset(2026, 10, 6, 15, 30, 0, TimeSpan.FromHours(5.5)),
            End = new DateTimeOffset(2026, 10, 6, 16, 30, 0, TimeSpan.FromHours(5.5)),
            AccountEmail = "user@example.com"
        };

        var result = _service.Analyze([eventA, eventB], start, end);
        var health = result.ScheduleHealth;

        Assert.Equal(1, health.TotalConflicts);
        Assert.Equal(TimeSpan.FromHours(1), health.TotalConflictDuration);
    }
}
