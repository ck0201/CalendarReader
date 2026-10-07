namespace CalendarReader.Services;

using System.Globalization;
using CalendarReader.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Tasks.v1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class GoogleCalendarService : IGoogleCalendarService
{
    private const string ReadonlyCalendarScope = "https://www.googleapis.com/auth/calendar.readonly";
    private const string ReadonlyTasksScope = "https://www.googleapis.com/auth/tasks.readonly";
    private static readonly string[] RequiredScopes = [ReadonlyCalendarScope, ReadonlyTasksScope];
    private readonly GoogleAuthConfig _config;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(
        IOptions<GoogleAuthConfig> config,
        ITokenStore tokenStore,
        ILogger<GoogleCalendarService> logger)
    {
        _config = config.Value;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    private GoogleAuthorizationCodeFlow CreateFlow()
    {
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _config.ClientId,
                ClientSecret = _config.ClientSecret
            },
            Scopes = RequiredScopes
        });
    }

    public string GetAuthorizationUrl(string sessionId)
    {
        if (!_config.IsConfigured)
        {
            throw new InvalidOperationException("Google OAuth is not configured. Missing ClientId, ClientSecret, or RedirectUri.");
        }

        var authUri = new GoogleAuthorizationCodeRequestUrl(new Uri("https://accounts.google.com/o/oauth2/v2/auth"))
        {
            ClientId = _config.ClientId,
            RedirectUri = _config.RedirectUri,
            Scope = string.Join(" ", RequiredScopes),
            AccessType = "offline",
            Prompt = "consent",
            State = sessionId
        }.Build();

        return authUri.ToString();
    }

    public async Task<bool> HandleAuthorizationCallbackAsync(string code, string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Authorization code cannot be empty.", nameof(code));
        }

        var flow = CreateFlow();
        _logger.LogInformation("Exchanging authorization code for tokens for session {SessionId}", sessionId);

        var tokenResponse = await flow.ExchangeCodeForTokenAsync(sessionId, code, _config.RedirectUri, cancellationToken);
        if (tokenResponse != null)
        {
            _tokenStore.StoreToken(sessionId, tokenResponse);
            _logger.LogInformation("Successfully stored OAuth tokens for session {SessionId}", sessionId);
            return true;
        }

        return false;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string sessionId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var token = _tokenStore.GetToken(sessionId);
        if (token == null)
        {
            throw new UnauthorizedAccessException("Not authenticated or session has expired.");
        }

        var flow = CreateFlow();
        var credential = new UserCredential(flow, sessionId, token);

        using var calendarService = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "CalendarReader"
        });

        var request = calendarService.Events.List("primary");
        request.TimeMinDateTimeOffset = start;
        request.TimeMaxDateTimeOffset = end;
        request.SingleEvents = true; // Expand recurring events into individual occurrences
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        _logger.LogInformation("Retrieving calendar events from {Start} to {End} for session {SessionId}", start, end, sessionId);
        var googleEvents = await request.ExecuteAsync(cancellationToken);

        var mappedEvents = new List<CalendarEvent>();
        if (googleEvents?.Items == null)
        {
            return mappedEvents;
        }

        foreach (var item in googleEvents.Items)
        {
            if (item.Status == "cancelled")
            {
                continue;
            }

            var isAllDay = item.Start?.DateTimeDateTimeOffset == null && !string.IsNullOrEmpty(item.Start?.Date);
            DateTimeOffset eventStart;
            DateTimeOffset eventEnd;

            if (isAllDay)
            {
                if (DateTime.TryParseExact(item.Start?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sDate))
                {
                    eventStart = new DateTimeOffset(sDate, TimeSpan.Zero);
                }
                else
                {
                    eventStart = start;
                }

                if (DateTime.TryParseExact(item.End?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var eDate))
                {
                    eventEnd = new DateTimeOffset(eDate, TimeSpan.Zero);
                }
                else
                {
                    eventEnd = eventStart.AddDays(1);
                }
            }
            else
            {
                eventStart = item.Start?.DateTimeDateTimeOffset ?? start;
                eventEnd = item.End?.DateTimeDateTimeOffset ?? eventStart.AddMinutes(30);
            }

            var title = string.IsNullOrWhiteSpace(item.Summary) ? "(No Title)" : item.Summary.Trim();
            var description = item.Description?.Trim();
            var isTrain = CalendarEvent.IsTrainJourneyMatch(title, description);

            mappedEvents.Add(new CalendarEvent
            {
                Id = item.Id ?? Guid.NewGuid().ToString(),
                Title = title,
                Description = description,
                IsTrainJourney = isTrain,
                Start = eventStart,
                End = eventEnd,
                IsAllDay = isAllDay,
                IsRecurring = !string.IsNullOrEmpty(item.RecurringEventId),
                AttendeeCount = item.Attendees?.Count ?? 0,
                Status = item.Status,
                HtmlLink = item.HtmlLink
            });
        }

        return mappedEvents;
    }

    public async Task<IReadOnlyList<CalendarTask>> GetTasksAsync(
        string sessionId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var token = _tokenStore.GetToken(sessionId);
        if (token == null)
        {
            throw new UnauthorizedAccessException("Not authenticated or session has expired.");
        }

        var flow = CreateFlow();
        var credential = new UserCredential(flow, sessionId, token);

        using var tasksService = new TasksService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "CalendarReader"
        });

        var mappedTasks = new List<CalendarTask>();

        try
        {
            var tasklistsRequest = tasksService.Tasklists.List();
            var tasklists = await tasklistsRequest.ExecuteAsync(cancellationToken);

            if (tasklists?.Items != null)
            {
                var rfcMin = start.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
                var rfcMax = end.UtcDateTime.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

                foreach (var list in tasklists.Items)
                {
                    var tasksRequest = tasksService.Tasks.List(list.Id);
                    tasksRequest.DueMin = rfcMin;
                    tasksRequest.DueMax = rfcMax;
                    tasksRequest.ShowCompleted = true;
                    tasksRequest.ShowHidden = true;

                    var googleTasks = await tasksRequest.ExecuteAsync(cancellationToken);
                    if (googleTasks?.Items != null)
                    {
                        foreach (var item in googleTasks.Items)
                        {
                            // "Only the tasks added in calendar" - must have a Due date
                            if (string.IsNullOrWhiteSpace(item.Due))
                            {
                                continue;
                            }

                            if (!DateTimeOffset.TryParse(item.Due, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueDate))
                            {
                                continue;
                            }

                            // Keep tasks within the selected date range
                            if (dueDate.Date < start.Date || dueDate.Date > end.Date)
                            {
                                continue;
                            }

                            mappedTasks.Add(new CalendarTask
                            {
                                Id = item.Id ?? Guid.NewGuid().ToString(),
                                Title = string.IsNullOrWhiteSpace(item.Title) ? "(No Title)" : item.Title.Trim(),
                                DueDate = dueDate,
                                IsAllDay = true,
                                IsCompleted = string.Equals(item.Status, "completed", StringComparison.OrdinalIgnoreCase),
                                Description = item.Notes?.Trim(),
                                HtmlLink = item.WebViewLink
                            });
                        }
                    }
                }
            }
        }
        catch (Google.GoogleApiException gEx)
        {
            // If tasks scope was not granted or Tasks API is disabled, gracefully log warning
            _logger.LogWarning(gEx, "Google Tasks API call failed ({StatusCode}): {Message}", gEx.HttpStatusCode, gEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error retrieving tasks: {Message}", ex.Message);
        }

        return mappedTasks;
    }
}
