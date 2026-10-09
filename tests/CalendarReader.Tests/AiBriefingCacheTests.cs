namespace CalendarReader.Tests;

using CalendarReader.Models;
using CalendarReader.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class AiBriefingCacheTests
{
    private readonly MemoryCache _memoryCache;
    private readonly AiBriefingCache _cache;
    private readonly CalendarAnalysisService _analysisService = new();

    public AiBriefingCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        var config = Options.Create(new AiBriefingConfig
        {
            CacheTtlMinutes = 5
        });
        _cache = new AiBriefingCache(_memoryCache, config, NullLogger<AiBriefingCache>.Instance);
    }

    private ScheduleSnapshot CreateSnapshot(int eventCount = 1)
    {
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 7, 23, 59, 59, TimeSpan.Zero);
        var events = new List<CalendarEvent>();

        for (var i = 0; i < eventCount; i++)
        {
            events.Add(new CalendarEvent
            {
                Id = $"ev-{i}",
                Title = "Meeting",
                Start = start.AddHours(9 + i),
                End = start.AddHours(10 + i),
                AttendeeCount = 2
            });
        }

        var insights = _analysisService.Analyze(events, start, end);
        return _analysisService.CreateScheduleSnapshot(insights, includeTitles: false);
    }

    [Fact]
    public void CreateCacheKey_DifferentSessions_ProduceDifferentKeys()
    {
        var snapshot = CreateSnapshot();
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        var key1 = _cache.CreateCacheKey("session-user-1", start, end, null, snapshot);
        var key2 = _cache.CreateCacheKey("session-user-2", start, end, null, snapshot);

        Assert.NotEqual(key1, key2);
        Assert.StartsWith("briefing:session-user-1:", key1);
        Assert.StartsWith("briefing:session-user-2:", key2);
    }

    [Fact]
    public void CreateCacheKey_SortedCalendarIds_ProducesIdenticalKeyRegardlessOfOrder()
    {
        var snapshot = CreateSnapshot();
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        var keyA = _cache.CreateCacheKey("session-1", start, end, ["cal-z", "cal-a", "cal-m"], snapshot);
        var keyB = _cache.CreateCacheKey("session-1", start, end, ["cal-a", "cal-m", "cal-z"], snapshot);

        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void CreateCacheKey_DifferentDateRanges_ProduceDifferentKeys()
    {
        var snapshot = CreateSnapshot();
        var start1 = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end1 = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        var start2 = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        var end2 = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

        var key1 = _cache.CreateCacheKey("session-1", start1, end1, null, snapshot);
        var key2 = _cache.CreateCacheKey("session-1", start2, end2, null, snapshot);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void CreateCacheKey_ScheduleChange_ProducesDifferentFingerprint()
    {
        var snapshot1 = CreateSnapshot(eventCount: 1);
        var snapshot2 = CreateSnapshot(eventCount: 3);
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        var key1 = _cache.CreateCacheKey("session-1", start, end, null, snapshot1);
        var key2 = _cache.CreateCacheKey("session-1", start, end, null, snapshot2);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void TryGet_AfterSet_ReturnsCachedBriefingWithIsCachedTrue()
    {
        var key = "test-key-123";
        var original = new AiBriefingResult
        {
            Headline = "High Priority Week",
            NarrativeSummary = "Lots of work",
            PreparationPriorities = ["Item 1"],
            SchedulingSuggestions = ["Suggestion 1"],
            EnergyProfile = "Focused",
            IsFallback = false,
            IsCached = false
        };

        _cache.Set(key, original);

        var found = _cache.TryGet(key, out var cached);

        Assert.True(found);
        Assert.NotNull(cached);
        Assert.True(cached.IsCached); // Marked as cached on retrieval
        Assert.False(cached.IsFallback);
        Assert.Equal("High Priority Week", cached.Headline);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var found = _cache.TryGet("non-existent-key", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public async Task Set_WithShortExpiration_ExpiresCorrectly()
    {
        var key = "expiring-key";
        var original = new AiBriefingResult
        {
            Headline = "Quick Briefing",
            NarrativeSummary = "Expires soon",
            IsFallback = false
        };

        _cache.Set(key, original, TimeSpan.FromMilliseconds(50));

        // Immediately available
        Assert.True(_cache.TryGet(key, out _));

        // Wait past TTL
        await Task.Delay(100);

        Assert.False(_cache.TryGet(key, out _));
    }

    [Fact]
    public void CreateCacheKey_TaskCompletionChange_ProducesDifferentFingerprint()
    {
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        var snapshotPending = new ScheduleSnapshot
        {
            StartDate = start,
            EndDate = end,
            TotalTasksCount = 1,
            PendingTasksCount = 1,
            CompletedTasksCount = 0,
            Tasks = [new TaskSummarySnapshot { DueDate = start.AddHours(12), IsCompleted = false }]
        };

        var snapshotCompleted = new ScheduleSnapshot
        {
            StartDate = start,
            EndDate = end,
            TotalTasksCount = 1,
            PendingTasksCount = 0,
            CompletedTasksCount = 1,
            Tasks = [new TaskSummarySnapshot { DueDate = start.AddHours(12), IsCompleted = true }]
        };

        var keyPending = _cache.CreateCacheKey("session-1", start, end, null, snapshotPending);
        var keyCompleted = _cache.CreateCacheKey("session-1", start, end, null, snapshotCompleted);

        Assert.NotEqual(keyPending, keyCompleted);
    }

    [Fact]
    public void CreateCacheKey_CrossAccountConflictChange_ProducesDifferentFingerprint()
    {
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        var snapshot1 = new ScheduleSnapshot
        {
            StartDate = start,
            EndDate = end,
            TotalConflictsCount = 1,
            CrossAccountConflictsCount = 1,
            Conflicts = [new ConflictSummarySnapshot { OverlapStart = start.AddHours(9), OverlapEnd = start.AddHours(10), IsCrossAccount = true }]
        };

        var snapshot2 = new ScheduleSnapshot
        {
            StartDate = start,
            EndDate = end,
            TotalConflictsCount = 1,
            CrossAccountConflictsCount = 0,
            SameAccountConflictsCount = 1,
            Conflicts = [new ConflictSummarySnapshot { OverlapStart = start.AddHours(9), OverlapEnd = start.AddHours(10), IsCrossAccount = false }]
        };

        var key1 = _cache.CreateCacheKey("session-1", start, end, null, snapshot1);
        var key2 = _cache.CreateCacheKey("session-1", start, end, null, snapshot2);

        Assert.NotEqual(key1, key2);
    }
}

