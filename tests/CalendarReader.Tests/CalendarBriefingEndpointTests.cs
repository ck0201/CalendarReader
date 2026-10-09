namespace CalendarReader.Tests;

using CalendarReader.Endpoints;
using CalendarReader.Models;
using CalendarReader.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class CalendarBriefingEndpointTests
{
    private class FakeAccountTokenStore : IAccountTokenStore
    {
        private readonly bool _hasAccounts;

        public FakeAccountTokenStore(bool hasAccounts = true)
        {
            _hasAccounts = hasAccounts;
        }

        public Task<bool> HasAnyAccountAsync(string sessionId, CancellationToken ct = default) =>
            Task.FromResult(_hasAccounts);

        public Task<IReadOnlyList<ConnectedAccount>> GetAccountsAsync(string sessionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ConnectedAccount>>([]);

        public Task<IReadOnlyList<AccountTokenData>> GetAllAccountTokensAsync(string sessionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AccountTokenData>>([]);

        public Task<AccountTokenData?> GetAccountTokenAsync(string sessionId, string accountId, CancellationToken ct = default) =>
            Task.FromResult<AccountTokenData?>(null);

        public Task SaveAccountTokenAsync(string sessionId, AccountTokenData account, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAccountAsync(string sessionId, string accountId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAllAccountsAsync(string sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task MarkRequiresReauthAsync(string sessionId, string accountId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private class FakeGoogleCalendarService : IGoogleCalendarService
    {
        public List<CalendarEvent> EventsToReturn { get; set; } = [];
        public List<CalendarTask> TasksToReturn { get; set; } = [];
        public bool ThrowUnauthorized { get; set; }
        public bool ThrowGenericException { get; set; }
        public IReadOnlyList<string>? LastRequestedCalendarIds { get; private set; }

        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            string sessionId,
            DateTimeOffset start,
            DateTimeOffset end,
            IReadOnlyList<string>? calendarIds = null,
            CancellationToken ct = default)
        {
            if (ThrowUnauthorized)
            {
                throw new UnauthorizedAccessException("Session or calendar unauthorized.");
            }

            if (ThrowGenericException)
            {
                throw new InvalidOperationException("Failed to call upstream calendar provider.");
            }

            LastRequestedCalendarIds = calendarIds;
            return Task.FromResult<IReadOnlyList<CalendarEvent>>(EventsToReturn);
        }

        public Task<IReadOnlyList<CalendarTask>> GetTasksAsync(
            string sessionId,
            DateTimeOffset start,
            DateTimeOffset end,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<CalendarTask>>(TasksToReturn);
        }

        public string GetAuthorizationUrl(string sessionId, string? prompt = null) => string.Empty;
        public Task<bool> HandleAuthorizationCallbackAsync(string code, string sessionId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<UserCalendar>> GetCalendarsAsync(string sessionId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserCalendar>>([]);
        public Task<bool> DisconnectAccountAsync(string sessionId, string accountId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> DisconnectAllAccountsAsync(string sessionId, CancellationToken ct = default) => Task.FromResult(true);
    }

    private class FakeAiBriefingService : IAiBriefingService
    {
        public int CallCount { get; private set; }
        public AiBriefingResult ResultToReturn { get; set; } = new()
        {
            Headline = "Focused Week Ahead",
            NarrativeSummary = "You have balanced meetings.",
            PreparationPriorities = ["Prep for demo"],
            SchedulingSuggestions = ["Protect Thursday focus"],
            EnergyProfile = "Balanced",
            IsFallback = false,
            IsCached = false
        };

        public Task<AiBriefingResult> GenerateBriefingAsync(ScheduleSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(ResultToReturn);
        }
    }

    private readonly CalendarAnalysisService _analysisService = new();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions { SizeLimit = 1000 });
    private readonly AiBriefingCache _briefingCache;

    public CalendarBriefingEndpointTests()
    {
        var config = Options.Create(new AiBriefingConfig { CacheTtlMinutes = 5 });
        _briefingCache = new AiBriefingCache(_memoryCache, config, NullLogger<AiBriefingCache>.Instance);
    }

    private static HttpContext CreateContextWithSession(string sessionId = "test-session-123")
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{CalendarBriefingEndpoint.SessionCookieName}={sessionId}";
        return context;
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_Returns200WithStructuredBriefing()
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 11, 23, 59, 59, TimeSpan.Zero);

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context,
            start,
            end,
            calendars: null,
            calendarService,
            _analysisService,
            briefingService,
            _briefingCache,
            tokenStore,
            NullLogger.Instance,
            CancellationToken.None);

        var okResult = Assert.IsType<Ok<AiBriefingResult>>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(okResult.Value);
        Assert.Equal("Focused Week Ahead", okResult.Value.Headline);
        Assert.False(okResult.Value.IsFallback);
        Assert.False(okResult.Value.IsCached);
        Assert.Equal(1, briefingService.CallCount);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task HandleAsync_MissingStartOrEnd_Returns400BadRequest(bool hasStart, bool hasEnd)
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        DateTimeOffset? start = hasStart ? DateTimeOffset.UtcNow : null;
        DateTimeOffset? end = hasEnd ? DateTimeOffset.UtcNow.AddDays(7) : null;

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context,
            start,
            end,
            calendars: null,
            calendarService,
            _analysisService,
            briefingService,
            _briefingCache,
            tokenStore,
            NullLogger.Instance,
            CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvertedDateRange_Returns400BadRequest()
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context,
            start,
            end,
            calendars: null,
            calendarService,
            _analysisService,
            briefingService,
            _briefingCache,
            tokenStore,
            NullLogger.Instance,
            CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_DateRangeExceeds31Days_Returns400BadRequest()
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero); // 45 days

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context,
            start,
            end,
            calendars: null,
            calendarService,
            _analysisService,
            briefingService,
            _briefingCache,
            tokenStore,
            NullLogger.Instance,
            CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_MissingSessionCookie_Returns401Unauthorized()
    {
        var context = new DefaultHttpContext(); // No cookies
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context,
            start,
            end,
            calendars: null,
            calendarService,
            _analysisService,
            briefingService,
            _briefingCache,
            tokenStore,
            NullLogger.Instance,
            CancellationToken.None);

        var unauth = Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Equal(401, unauth.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SessionWithNoAccounts_Returns401Unauthorized()
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: false); // Zero accounts

        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context,
            start,
            end,
            calendars: null,
            calendarService,
            _analysisService,
            briefingService,
            _briefingCache,
            tokenStore,
            NullLogger.Instance,
            CancellationToken.None);

        var unauth = Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Equal(401, unauth.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_CalendarServiceThrowsUnauthorized_Returns401Unauthorized()
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService { ThrowUnauthorized = true };
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context,
            start,
            end,
            calendars: null,
            calendarService,
            _analysisService,
            briefingService,
            _briefingCache,
            tokenStore,
            NullLogger.Instance,
            CancellationToken.None);

        var unauth = Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Equal(401, unauth.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SelectedCalendars_PassedToCalendarService()
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);

        await CalendarBriefingEndpoint.HandleAsync(
            context,
            start,
            end,
            calendars: "cal-1, cal-2",
            calendarService,
            _analysisService,
            briefingService,
            _briefingCache,
            tokenStore,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.NotNull(calendarService.LastRequestedCalendarIds);
        Assert.Equal(2, calendarService.LastRequestedCalendarIds.Count);
        Assert.Contains("cal-1", calendarService.LastRequestedCalendarIds);
        Assert.Contains("cal-2", calendarService.LastRequestedCalendarIds);
    }

    [Fact]
    public async Task HandleAsync_RepeatedRequest_UsesCacheAndAvoidsSecondAiCall()
    {
        var context = CreateContextWithSession("session-user-cached");
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 11, 23, 59, 59, TimeSpan.Zero);

        // First call: cache miss
        var result1 = await CalendarBriefingEndpoint.HandleAsync(
            context, start, end, null, calendarService, _analysisService, briefingService, _briefingCache, tokenStore, NullLogger.Instance, CancellationToken.None);

        var ok1 = Assert.IsType<Ok<AiBriefingResult>>(result1);
        Assert.False(ok1.Value!.IsCached);
        Assert.Equal(1, briefingService.CallCount);

        // Second call: cache hit
        var result2 = await CalendarBriefingEndpoint.HandleAsync(
            context, start, end, null, calendarService, _analysisService, briefingService, _briefingCache, tokenStore, NullLogger.Instance, CancellationToken.None);

        var ok2 = Assert.IsType<Ok<AiBriefingResult>>(result2);
        Assert.True(ok2.Value!.IsCached);
        Assert.Equal(1, briefingService.CallCount); // Still 1! No second AI call!
        Assert.Equal(ok1.Value.Headline, ok2.Value.Headline);
    }

    [Fact]
    public async Task HandleAsync_DifferentSessions_DoNotShareCache()
    {
        var contextUser1 = CreateContextWithSession("session-user-1");
        var contextUser2 = CreateContextWithSession("session-user-2");
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 11, 23, 59, 59, TimeSpan.Zero);

        // Request from User 1
        await CalendarBriefingEndpoint.HandleAsync(
            contextUser1, start, end, null, calendarService, _analysisService, briefingService, _briefingCache, tokenStore, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(1, briefingService.CallCount);

        // Request from User 2
        var resultUser2 = await CalendarBriefingEndpoint.HandleAsync(
            contextUser2, start, end, null, calendarService, _analysisService, briefingService, _briefingCache, tokenStore, NullLogger.Instance, CancellationToken.None);

        var ok2 = Assert.IsType<Ok<AiBriefingResult>>(resultUser2);
        Assert.False(ok2.Value!.IsCached);
        Assert.Equal(2, briefingService.CallCount); // User 2 does NOT hit User 1's cache
    }

    [Fact]
    public async Task HandleAsync_FallbackResult_PreservesIsFallbackTrue()
    {
        var context = CreateContextWithSession("session-fallback");
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService
        {
            ResultToReturn = new AiBriefingResult
            {
                Headline = "Fallback Schedule Notice",
                NarrativeSummary = "Deterministic summary",
                IsFallback = true,
                IsCached = false
            }
        };
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 11, 23, 59, 59, TimeSpan.Zero);

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context, start, end, null, calendarService, _analysisService, briefingService, _briefingCache, tokenStore, NullLogger.Instance, CancellationToken.None);

        var okResult = Assert.IsType<Ok<AiBriefingResult>>(result);
        Assert.True(okResult.Value!.IsFallback);
    }

    [Fact]
    public async Task HandleAsync_CallerCancellation_PropagatesOperationCanceledException()
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService();
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CalendarBriefingEndpoint.HandleAsync(
                context, start, end, null, calendarService, _analysisService, briefingService, _briefingCache, tokenStore, NullLogger.Instance, cts.Token));
    }

    [Fact]
    public async Task HandleAsync_UnexpectedException_Returns500ProblemDetailsWithoutSensitiveData()
    {
        var context = CreateContextWithSession();
        var calendarService = new FakeGoogleCalendarService { ThrowGenericException = true };
        var briefingService = new FakeAiBriefingService();
        var tokenStore = new FakeAccountTokenStore(hasAccounts: true);

        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);

        var result = await CalendarBriefingEndpoint.HandleAsync(
            context, start, end, null, calendarService, _analysisService, briefingService, _briefingCache, tokenStore, NullLogger.Instance, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(500, problem.StatusCode);
        Assert.NotNull(problem.ProblemDetails.Detail);
        // Ensure sensitive internal exception text is NOT exposed in detail
        Assert.DoesNotContain("upstream calendar provider", problem.ProblemDetails.Detail);
        Assert.Equal("An unexpected error occurred while generating the calendar briefing.", problem.ProblemDetails.Detail);
    }
}
