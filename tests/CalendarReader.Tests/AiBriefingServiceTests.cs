namespace CalendarReader.Tests;

using System.Net;
using System.Text.Json;
using CalendarReader.Models;
using CalendarReader.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class AiBriefingServiceTests
{
    private readonly DeterministicBriefingFallbackGenerator _fallbackGenerator = new();
    private readonly CalendarAnalysisService _analysisService = new();

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public int RequestCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return await _handler(request, cancellationToken);
        }
    }

    private ScheduleSnapshot CreateSampleSnapshot(bool includeTitles = false)
    {
        var start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 7, 23, 59, 59, TimeSpan.Zero);

        var ev1 = new CalendarEvent
        {
            Id = "ev-1",
            Title = "Confidential Board Meeting",
            Description = "Secret discussion notes",
            AccountEmail = "exec@work.com",
            Start = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
            AttendeeCount = 5,
            IsRecurring = true
        };

        var ev2 = new CalendarEvent
        {
            Id = "ev-2",
            Title = "Confidential Conflict Session",
            Description = "Overlapping secret",
            AccountEmail = "personal@gmail.com",
            Start = new DateTimeOffset(2026, 10, 5, 11, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 5, 12, 30, 0, TimeSpan.Zero),
            AttendeeCount = 2
        };

        var task = new CalendarTask
        {
            Id = "t-1",
            Title = "Confidential Task Review",
            Description = "Secret spreadsheet",
            AccountEmail = "exec@work.com",
            DueDate = new DateTimeOffset(2026, 10, 6, 15, 0, 0, TimeSpan.Zero),
            IsCompleted = false
        };

        var insights = _analysisService.Analyze([ev1, ev2], start, end, [task]);
        return _analysisService.CreateScheduleSnapshot(insights, includeTitles);
    }

    [Fact]
    public async Task GenerateBriefingAsync_MissingApiKey_ReturnsFallbackWithoutHttpRequest()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        var result = await service.GenerateBriefingAsync(snapshot);

        Assert.Equal(0, handler.RequestCount);
        Assert.True(result.IsFallback);
        Assert.False(string.IsNullOrWhiteSpace(result.Headline));
        Assert.False(string.IsNullOrWhiteSpace(result.NarrativeSummary));
        Assert.NotEmpty(result.PreparationPriorities);
        Assert.NotEmpty(result.SchedulingSuggestions);
    }

    [Fact]
    public async Task GenerateBriefingAsync_ValidProviderResponse_ReturnsMappedAiResult()
    {
        const string mockGeminiResponse = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{\n  \"headline\": \"Focused Start with Mid-Week Pressure\",\n  \"narrativeSummary\": \"Your week begins with moderate meeting commitments and an active conflict on Monday. Thursday offers strong focus opportunities.\",\n  \"preparationPriorities\": [\n    \"Resolve Monday midday calendar conflict\",\n    \"Complete task review due Tuesday\"\n  ],\n  \"schedulingSuggestions\": [\n    \"Protect Wednesday afternoon focus window\",\n    \"Audit recurring series to reduce fatigue\"\n  ],\n  \"energyProfile\": \"High Contention — Resolve Monday Overlap\"\n}"
                  }
                ]
              }
            }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mockGeminiResponse, System.Text.Encoding.UTF8, "application/json")
            }));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        var result = await service.GenerateBriefingAsync(snapshot);

        Assert.Equal(1, handler.RequestCount);
        Assert.False(result.IsFallback);
        Assert.Equal("Focused Start with Mid-Week Pressure", result.Headline);
        Assert.Equal("Your week begins with moderate meeting commitments and an active conflict on Monday. Thursday offers strong focus opportunities.", result.NarrativeSummary);
        Assert.Equal(2, result.PreparationPriorities.Count);
        Assert.Equal("Resolve Monday midday calendar conflict", result.PreparationPriorities[0]);
        Assert.Equal(2, result.SchedulingSuggestions.Count);
        Assert.Equal("High Contention — Resolve Monday Overlap", result.EnergyProfile);
    }

    [Fact]
    public async Task GenerateBriefingAsync_MalformedJson_ReturnsFallbackResult()
    {
        const string malformedResponse = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "This is raw text instead of valid json {"
                  }
                ]
              }
            }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(malformedResponse, System.Text.Encoding.UTF8, "application/json")
            }));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        var result = await service.GenerateBriefingAsync(snapshot);

        Assert.Equal(1, handler.RequestCount);
        Assert.True(result.IsFallback);
        Assert.False(string.IsNullOrWhiteSpace(result.Headline));
    }

    [Fact]
    public async Task GenerateBriefingAsync_IncompletePayload_ReturnsFallbackResult()
    {
        const string incompletePayload = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{\n  \"headline\": \"\",\n  \"narrativeSummary\": \"Summary without headline\"\n}"
                  }
                ]
              }
            }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(incompletePayload, System.Text.Encoding.UTF8, "application/json")
            }));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        var result = await service.GenerateBriefingAsync(snapshot);

        Assert.Equal(1, handler.RequestCount);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task GenerateBriefingAsync_EmptyCandidates_ReturnsFallbackResult()
    {
        const string emptyCandidates = """
        {
          "candidates": []
        }
        """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(emptyCandidates, System.Text.Encoding.UTF8, "application/json")
            }));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        var result = await service.GenerateBriefingAsync(snapshot);

        Assert.Equal(1, handler.RequestCount);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task GenerateBriefingAsync_Http500Error_ReturnsFallbackResult()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        var result = await service.GenerateBriefingAsync(snapshot);

        Assert.Equal(1, handler.RequestCount);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task GenerateBriefingAsync_InvalidModelNotFound404_ReturnsFallbackResult()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""
                {
                  "error": {
                    "code": 404,
                    "message": "models/nonexistent-model is not found for API version v1beta",
                    "status": "NOT_FOUND"
                  }
                }
                """, System.Text.Encoding.UTF8, "application/json")
            }));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "nonexistent-model"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        var result = await service.GenerateBriefingAsync(snapshot);

        Assert.Equal(1, handler.RequestCount);
        Assert.True(result.IsFallback);
        Assert.False(string.IsNullOrWhiteSpace(result.Headline));
    }

    [Fact]
    public async Task GenerateBriefingAsync_Http429RateLimit_ReturnsFallbackResult()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        var result = await service.GenerateBriefingAsync(snapshot);

        Assert.Equal(1, handler.RequestCount);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task GenerateBriefingAsync_HttpTimeout_ReturnsFallbackResult()
    {
        var handler = new MockHttpMessageHandler(async (_, ct) =>
        {
            // Simulate timeout cancellation by waiting on a token or throwing TaskCanceledException
            await Task.Delay(500, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-3.8-flash",
            TimeoutSeconds = 1 // 1 second timeout
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        // Pass a mock handler that simulates timeout without caller cancellation
        var timeoutHandler = new MockHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("Simulated HTTP client timeout"));

        var timeoutService = new AiBriefingService(
            new HttpClient(timeoutHandler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var result = await timeoutService.GenerateBriefingAsync(snapshot);

        Assert.Equal(1, timeoutHandler.RequestCount);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task GenerateBriefingAsync_CallerCancellation_PropagatesOperationCanceledException()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        var snapshot = CreateSampleSnapshot();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GenerateBriefingAsync(snapshot, cts.Token));
    }

    [Fact]
    public async Task GenerateBriefingAsync_PrivacyMinimization_OutboundRequestContainsNoTitlesOrSensitiveData()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "{\"headline\":\"Test\",\"narrativeSummary\":\"Test\",\"preparationPriorities\":[],\"schedulingSuggestions\":[],\"energyProfile\":\"Balanced\"}"
                          }
                        ]
                      }
                    }
                  ]
                }
                """, System.Text.Encoding.UTF8, "application/json")
            }));

        var config = Options.Create(new AiBriefingConfig
        {
            ApiKey = "my-secret-key-12345",
            Model = "gemini-3.8-flash"
        });

        var service = new AiBriefingService(
            new HttpClient(handler),
            config,
            _fallbackGenerator,
            NullLogger<AiBriefingService>.Instance);

        // Even if snapshot was created with includeTitles: true
        var snapshotWithTitles = CreateSampleSnapshot(includeTitles: true);

        await service.GenerateBriefingAsync(snapshotWithTitles);

        Assert.Equal(1, handler.RequestCount);
        Assert.NotNull(handler.LastRequest);
        Assert.NotNull(handler.LastRequestBody);

        // Header check
        Assert.True(handler.LastRequest.Headers.Contains("x-goog-api-key"));
        Assert.Equal("my-secret-key-12345", handler.LastRequest.Headers.GetValues("x-goog-api-key").First());

        // Target URL check: key is NOT in query string
        Assert.DoesNotContain("key=", handler.LastRequest.RequestUri!.Query);
        Assert.EndsWith("/models/gemini-3.8-flash:generateContent", handler.LastRequest.RequestUri.AbsolutePath);

        // Content privacy check
        var body = handler.LastRequestBody;
        Assert.DoesNotContain("Confidential Board Meeting", body);
        Assert.DoesNotContain("Confidential Conflict Session", body);
        Assert.DoesNotContain("Confidential Task Review", body);
        Assert.DoesNotContain("Secret discussion notes", body);
        Assert.DoesNotContain("Overlapping secret", body);
        Assert.DoesNotContain("Secret spreadsheet", body);
        Assert.DoesNotContain("exec@work.com", body);
        Assert.DoesNotContain("personal@gmail.com", body);
        Assert.DoesNotContain("my-secret-key-12345", body); // API key must NOT be in JSON body
    }

    [Fact]
    public void DeterministicFallback_EmptySchedule_ProducesValidFallback()
    {
        var snapshot = new ScheduleSnapshot
        {
            TotalEventsCount = 0,
            TimedEventsCount = 0,
            AllDayEventsCount = 0,
            TotalScheduledTimeFormatted = "0m",
            TotalMeetingTimeFormatted = "0m"
        };

        var fallback = _fallbackGenerator.GenerateFallback(snapshot);

        Assert.True(fallback.IsFallback);
        Assert.Equal("Open Schedule Ahead", fallback.Headline);
        Assert.Contains("no scheduled commitments", fallback.NarrativeSummary);
        Assert.Equal("Wide Open — Full autonomy over working hours", fallback.EnergyProfile);
        Assert.NotEmpty(fallback.PreparationPriorities);
        Assert.NotEmpty(fallback.SchedulingSuggestions);
    }

    [Fact]
    public void DeterministicFallback_ConflictSchedule_HighlightsConflicts()
    {
        var snapshot = new ScheduleSnapshot
        {
            TotalEventsCount = 4,
            TimedEventsCount = 4,
            TotalConflictsCount = 2,
            CrossAccountConflictsCount = 1,
            TotalConflictDurationFormatted = "1h 0m",
            TotalScheduledTimeFormatted = "4h 0m",
            TotalMeetingTimeFormatted = "3h 0m",
            BusiestDayOfWeek = "Monday",
            BusiestDayScheduledTimeFormatted = "4h 0m",
            BusiestDayEventCount = 4
        };

        var fallback = _fallbackGenerator.GenerateFallback(snapshot);

        Assert.True(fallback.IsFallback);
        Assert.Contains("2 Conflicts Detected", fallback.Headline);
        Assert.Contains("cross-account conflict", fallback.PreparationPriorities[0]);
        Assert.Contains("overlapping", fallback.SchedulingSuggestions[0]);
        Assert.Equal("High Contention — Schedule requires active conflict resolution", fallback.EnergyProfile);
    }

    [Fact]
    public void DeterministicFallback_MeetingHeavySchedule_HighlightsRecurringLoad()
    {
        var snapshot = new ScheduleSnapshot
        {
            TotalEventsCount = 8,
            TimedEventsCount = 8,
            RecurringMeetingPercentage = 65,
            RecurringMeetingTimeFormatted = "6h 30m",
            TotalScheduledTimeFormatted = "10h 0m",
            TotalMeetingTimeFormatted = "8h 0m",
            FocusBlockDurationFormatted = "1h 0m",
            FocusBlockDayOfWeek = "Friday"
        };

        var fallback = _fallbackGenerator.GenerateFallback(snapshot);

        Assert.True(fallback.IsFallback);
        Assert.Equal("Meeting-Intensive Schedule with High Recurring Load", fallback.Headline);
        Assert.Equal("Meeting Intensive — High recurring meeting load with fragmented focus", fallback.EnergyProfile);
        Assert.Contains("Recurring meetings consume 65%", fallback.SchedulingSuggestions[0]);
        Assert.Contains("Protect your 1h 0m focus block on Friday", fallback.SchedulingSuggestions[1]);
    }
}
