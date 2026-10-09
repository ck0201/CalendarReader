namespace CalendarReader.Services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalendarReader.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AiBriefingService : IAiBriefingService
{
    public const long MaxResponseSizeBytes = 1024 * 1024; // 1 MiB

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly AiBriefingConfig _config;
    private readonly IDeterministicBriefingFallbackGenerator _fallbackGenerator;
    private readonly ILogger<AiBriefingService> _logger;

    public AiBriefingService(
        HttpClient httpClient,
        IOptions<AiBriefingConfig> config,
        IDeterministicBriefingFallbackGenerator fallbackGenerator,
        ILogger<AiBriefingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _fallbackGenerator = fallbackGenerator ?? throw new ArgumentNullException(nameof(fallbackGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiBriefingResult> GenerateBriefingAsync(ScheduleSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_config.IsConfigured)
        {
            _logger.LogInformation("AI API key is not configured. Returning deterministic fallback briefing.");
            return _fallbackGenerator.GenerateFallback(snapshot);
        }

        try
        {
            var sanitizedData = CreateSanitizedSnapshotPayload(snapshot);
            var snapshotJson = JsonSerializer.Serialize(sanitizedData, JsonOptions);

            var promptText = "You are an executive calendar assistant. Analyze the following privacy-sanitized schedule metrics and generate a concise, actionable schedule briefing.\n\n" +
                $"Schedule Data:\n{snapshotJson}\n\n" +
                "Instructions:\n" +
                "1. Provide a clear, high-level headline summarizing schedule density.\n" +
                "2. Write a concise 2-3 sentence narrative summary highlighting total load, busiest days, and focus windows.\n" +
                "3. List 1-3 practical preparation priorities based on pending tasks or schedule conflicts.\n" +
                "4. List 1-3 actionable scheduling suggestions (e.g. protecting focus blocks, managing recurring meeting overhead, or resolving overlaps).\n" +
                "5. State the energy profile (e.g. 'Balanced', 'Meeting Intensive', 'High Contention', 'Open Focus').\n" +
                "Return strictly valid JSON matching the requested schema.";

            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = promptText }
                        }
                    }
                },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = "You are an executive calendar assistant. Provide a concise, actionable schedule briefing and recommendations based strictly on the provided sanitized schedule metrics. Return valid JSON adhering to the schema." }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            headline = new { type = "string" },
                            narrativeSummary = new { type = "string" },
                            preparationPriorities = new
                            {
                                type = "array",
                                items = new { type = "string" }
                            },
                            schedulingSuggestions = new
                            {
                                type = "array",
                                items = new { type = "string" }
                            },
                            energyProfile = new { type = "string" }
                        },
                        required = new[] { "headline", "narrativeSummary", "preparationPriorities", "schedulingSuggestions", "energyProfile" }
                    }
                }
            };

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_config.Model}:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("x-goog-api-key", _config.ApiKey);
            request.Content = JsonContent.Create(requestPayload, options: JsonOptions);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Explicit caller cancellation must propagate
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("AI briefing request timed out after {TimeoutSeconds}s. Using deterministic fallback.", _config.TimeoutSeconds);
                return _fallbackGenerator.GenerateFallback(snapshot);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AI provider returned HTTP status {StatusCode}. Using deterministic fallback.", (int)response.StatusCode);
                    return _fallbackGenerator.GenerateFallback(snapshot);
                }

                var responseBody = await ReadResponseBodyBoundedAsync(response, MaxResponseSizeBytes, linkedCts.Token);
                if (responseBody == null)
                {
                    return _fallbackGenerator.GenerateFallback(snapshot);
                }

                return ParseProviderResponse(responseBody, snapshot);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("AI briefing HTTP request failed ({Message}). Using deterministic fallback.", ex.Message);
            return _fallbackGenerator.GenerateFallback(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during AI briefing generation. Using deterministic fallback.");
            return _fallbackGenerator.GenerateFallback(snapshot);
        }
    }

    private AiBriefingResult ParseProviderResponse(string responseBody, ScheduleSnapshot snapshot)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("AI provider response contained no candidates. Using deterministic fallback.");
                return _fallbackGenerator.GenerateFallback(snapshot);
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                _logger.LogWarning("AI provider candidate contained no content parts. Using deterministic fallback.");
                return _fallbackGenerator.GenerateFallback(snapshot);
            }

            var jsonText = parts[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                _logger.LogWarning("AI provider returned empty text payload. Using deterministic fallback.");
                return _fallbackGenerator.GenerateFallback(snapshot);
            }

            var payload = JsonSerializer.Deserialize<AiBriefingPayload>(jsonText, JsonOptions);
            if (!TryValidatePayload(payload, out var result))
            {
                _logger.LogWarning("AI provider response payload failed validation. Using deterministic fallback.");
                return _fallbackGenerator.GenerateFallback(snapshot);
            }

            _logger.LogInformation("AI briefing generated successfully using provider model {Model}.", _config.Model);
            return result;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to parse JSON response from AI provider. Using deterministic fallback.");
            return _fallbackGenerator.GenerateFallback(snapshot);
        }
    }

    private async Task<string?> ReadResponseBodyBoundedAsync(HttpResponseMessage response, long maxBytes, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maxBytes)
        {
            _logger.LogWarning("AI provider response Content-Length ({Length} bytes) exceeds maximum limit of {MaxBytes} bytes. Using deterministic fallback.",
                response.Content.Headers.ContentLength.Value, maxBytes);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memoryStream = new MemoryStream();
        var buffer = new byte[16384];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            if (memoryStream.Length + bytesRead > maxBytes)
            {
                _logger.LogWarning("AI provider response stream exceeded maximum size limit of {MaxBytes} bytes. Using deterministic fallback.", maxBytes);
                return null;
            }

            memoryStream.Write(buffer, 0, bytesRead);
        }

        return System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    private static bool TryValidatePayload(AiBriefingPayload? payload, out AiBriefingResult result)
    {
        result = null!;
        if (payload == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.Headline) ||
            string.IsNullOrWhiteSpace(payload.NarrativeSummary) ||
            string.IsNullOrWhiteSpace(payload.EnergyProfile) ||
            payload.PreparationPriorities == null ||
            payload.SchedulingSuggestions == null)
        {
            return false;
        }

        result = new AiBriefingResult
        {
            Headline = payload.Headline.Trim(),
            NarrativeSummary = payload.NarrativeSummary.Trim(),
            PreparationPriorities = payload.PreparationPriorities
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList(),
            SchedulingSuggestions = payload.SchedulingSuggestions
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList(),
            EnergyProfile = payload.EnergyProfile.Trim(),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            IsFallback = false,
            IsCached = false
        };

        return true;
    }

    /// <summary>
    /// Explicitly maps only privacy-sanitized metrics and temporal distribution to an anonymous object.
    /// Strictly guarantees that no event/task titles, descriptions, attendee details, account emails,
    /// calendar IDs, or URLs are serialized over the wire even if present on the input snapshot.
    /// </summary>
    private static object CreateSanitizedSnapshotPayload(ScheduleSnapshot snapshot)
    {
        return new
        {
            snapshot.StartDate,
            snapshot.EndDate,
            snapshot.PeriodLabel,
            snapshot.TotalEventsCount,
            snapshot.TimedEventsCount,
            snapshot.AllDayEventsCount,
            snapshot.TotalScheduledTimeFormatted,
            snapshot.TotalMeetingTimeFormatted,
            snapshot.RecurringMeetingPercentage,
            snapshot.RecurringMeetingTimeFormatted,
            snapshot.FocusBlockDurationFormatted,
            snapshot.FocusBlockDayOfWeek,
            snapshot.FocusBlockWindowFormatted,
            snapshot.BusiestDayOfWeek,
            snapshot.BusiestDayScheduledTimeFormatted,
            snapshot.BusiestDayEventCount,
            snapshot.TotalConflictsCount,
            snapshot.TotalConflictDurationFormatted,
            snapshot.CrossAccountConflictsCount,
            snapshot.SameAccountConflictsCount,
            snapshot.ConflictFreeDaysCount,
            snapshot.MostConflictedDay,
            snapshot.HasTodayConflict,
            Conflicts = snapshot.Conflicts.Select(c => new
            {
                c.OverlapStart,
                c.OverlapEnd,
                c.OverlapDurationFormatted,
                c.IsCrossAccount
            }),
            snapshot.TotalTasksCount,
            snapshot.PendingTasksCount,
            snapshot.CompletedTasksCount,
            Tasks = snapshot.Tasks.Select(t => new
            {
                t.DueDate,
                t.DueDateFormatted,
                t.IsCompleted,
                t.IsAllDay
            }),
            Days = snapshot.Days.Select(d => new
            {
                d.Date,
                d.DayOfWeek,
                d.DateFormatted,
                d.EventCount,
                d.TimedEventCount,
                d.AllDayEventCount,
                d.ScheduledTimeFormatted,
                d.MeetingTimeFormatted,
                d.ConflictCount,
                d.TaskCount,
                TimeBlocks = d.TimeBlocks.Select(b => new
                {
                    b.Start,
                    b.End,
                    b.DurationFormatted,
                    b.TimeRangeFormatted,
                    b.IsAllDay,
                    b.IsMeeting,
                    b.IsRecurring,
                    b.IsConflicted
                })
            })
        };
    }

    internal class AiBriefingPayload
    {
        [JsonPropertyName("headline")]
        public string? Headline { get; set; }

        [JsonPropertyName("narrativeSummary")]
        public string? NarrativeSummary { get; set; }

        [JsonPropertyName("preparationPriorities")]
        public List<string>? PreparationPriorities { get; set; }

        [JsonPropertyName("schedulingSuggestions")]
        public List<string>? SchedulingSuggestions { get; set; }

        [JsonPropertyName("energyProfile")]
        public string? EnergyProfile { get; set; }
    }
}
