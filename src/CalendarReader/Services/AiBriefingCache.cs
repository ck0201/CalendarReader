namespace CalendarReader.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CalendarReader.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AiBriefingCache : IAiBriefingCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly AiBriefingConfig _config;
    private readonly ILogger<AiBriefingCache> _logger;

    public AiBriefingCache(
        IMemoryCache memoryCache,
        IOptions<AiBriefingConfig> config,
        ILogger<AiBriefingCache> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string CreateCacheKey(
        string sessionId,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<string>? calendarIds,
        ScheduleSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(snapshot);

        var normalizedStart = start.ToUniversalTime().ToString("yyyyMMddHHmmss");
        var normalizedEnd = end.ToUniversalTime().ToString("yyyyMMddHHmmss");

        // Pseudonymize session ID with SHA-256 to avoid raw session tokens in cache keys
        var sessionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))[..16];

        // Pseudonymize calendar IDs with SHA-256 to avoid raw email addresses in cache keys
        var rawCalendars = calendarIds != null && calendarIds.Count > 0
            ? string.Join(",", calendarIds.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            : "all";

        var calendarsHash = rawCalendars == "all"
            ? "all"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCalendars)))[..16];

        var fingerprint = ComputeSnapshotFingerprint(snapshot);

        return $"briefing:{sessionHash}:{normalizedStart}:{normalizedEnd}:{calendarsHash}:{fingerprint}";
    }

    public bool TryGet(string cacheKey, out AiBriefingResult result)
    {
        if (_memoryCache.TryGetValue(cacheKey, out AiBriefingResult? cached) && cached != null)
        {
            _logger.LogDebug("Briefing cache hit for entry tag {KeyTag}", ComputeKeyTag(cacheKey));
            result = CloneBriefing(cached, isCached: true);
            return true;
        }

        result = null!;
        return false;
    }

    public void Set(string cacheKey, AiBriefingResult result, TimeSpan? expiration = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var ttl = expiration ?? TimeSpan.FromMinutes(_config.CacheTtlMinutes > 0 ? _config.CacheTtlMinutes : 5);
        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1
        };

        var stored = CloneBriefing(result, isCached: false);
        _memoryCache.Set(cacheKey, stored, entryOptions);
        _logger.LogDebug("Briefing cached with entry tag {KeyTag} and TTL {TtlMinutes}m", ComputeKeyTag(cacheKey), ttl.TotalMinutes);
    }

    private static string ComputeKeyTag(string cacheKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)))[..8];
    }

    private static string ComputeSnapshotFingerprint(ScheduleSnapshot snapshot)
    {
        var keyData = new
        {
            snapshot.TotalEventsCount,
            snapshot.TimedEventsCount,
            snapshot.AllDayEventsCount,
            snapshot.TotalScheduledTimeFormatted,
            snapshot.TotalMeetingTimeFormatted,
            snapshot.RecurringMeetingPercentage,
            snapshot.FocusBlockDurationFormatted,
            snapshot.FocusBlockDayOfWeek,
            snapshot.FocusBlockWindowFormatted,
            snapshot.BusiestDayOfWeek,
            snapshot.BusiestDayScheduledTimeFormatted,
            snapshot.TotalConflictsCount,
            snapshot.TotalConflictDurationFormatted,
            snapshot.CrossAccountConflictsCount,
            snapshot.TotalTasksCount,
            snapshot.PendingTasksCount,
            snapshot.CompletedTasksCount,
            Days = snapshot.Days.Select(d => new
            {
                d.Date,
                d.EventCount,
                d.ScheduledTimeFormatted,
                d.ConflictCount,
                d.TaskCount,
                Blocks = d.TimeBlocks.Select(b => new { b.Start, b.End, b.IsAllDay, b.IsMeeting, b.IsConflicted })
            }),
            Tasks = snapshot.Tasks.Select(t => new { t.DueDate, t.IsCompleted, t.IsAllDay })
        };

        var json = JsonSerializer.Serialize(keyData);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16];
    }

    private static AiBriefingResult CloneBriefing(AiBriefingResult source, bool isCached)
    {
        return new AiBriefingResult
        {
            Headline = source.Headline,
            NarrativeSummary = source.NarrativeSummary,
            PreparationPriorities = [.. source.PreparationPriorities],
            SchedulingSuggestions = [.. source.SchedulingSuggestions],
            EnergyProfile = source.EnergyProfile,
            GeneratedAtUtc = source.GeneratedAtUtc,
            IsFallback = source.IsFallback,
            IsCached = isCached
        };
    }
}
