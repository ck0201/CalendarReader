namespace CalendarReader.Services;

using CalendarReader.Models;

public class CalendarAnalysisService : ICalendarAnalysisService
{
    private static readonly TimeSpan DayStartHour = TimeSpan.FromHours(9);  // 9:00 AM
    private static readonly TimeSpan DayEndHour = TimeSpan.FromHours(18);   // 6:00 PM

    public CalendarInsightResult Analyze(IReadOnlyList<CalendarEvent> events, DateTimeOffset start, DateTimeOffset end, IReadOnlyList<CalendarTask>? tasks = null)
    {
        var taskList = new List<CalendarTask>();
        if (tasks != null)
        {
            taskList.AddRange(tasks);
        }

        var existingTaskIds = new HashSet<string>(taskList.Select(t => t.Id));
        foreach (var ev in events.Where(e => e.IsTask))
        {
            if (!existingTaskIds.Contains(ev.Id))
            {
                taskList.Add(ev.ToTask());
                existingTaskIds.Add(ev.Id);
            }
        }

        var result = new CalendarInsightResult
        {
            StartDate = start,
            EndDate = end,
            TotalEvents = events.Count,
            Events = events.OrderBy(e => e.Start).ToList(),
            Tasks = taskList.OrderBy(t => t.DueDate).ToList()
        };

        var timedEvents = events.Where(e => !e.IsAllDay).ToList();
        var allDayEvents = events.Where(e => e.IsAllDay).ToList();

        result.TimedEventsCount = timedEvents.Count;
        result.AllDayEventsCount = allDayEvents.Count;

        // 1. Calculate Total Scheduled Time (sum of timed events durations)
        var totalScheduled = TimeSpan.Zero;
        foreach (var ev in timedEvents)
        {
            totalScheduled += ev.Duration;
        }
        result.TotalScheduledTime = totalScheduled;
        result.FormattedScheduledTime = FormatDuration(totalScheduled);

        // 2. Calculate Meeting Time
        // V1 Rule: Timed events with 2 or more attendees are counted as collaborative meetings.
        // If no events have attendee information specified, all timed calendar events are treated as meeting time.
        var hasAttendeeData = timedEvents.Any(e => e.AttendeeCount > 1);
        var meetingEvents = hasAttendeeData
            ? timedEvents.Where(e => e.AttendeeCount > 1).ToList()
            : timedEvents;

        var totalMeeting = TimeSpan.Zero;
        foreach (var ev in meetingEvents)
        {
            totalMeeting += ev.Duration;
        }
        result.TotalMeetingTime = totalMeeting;
        result.FormattedMeetingTime = FormatDuration(totalMeeting);

        // 3. Calculate Recurring Meeting Time
        var recurringEvents = timedEvents.Where(e => e.IsRecurring).ToList();
        var totalRecurring = TimeSpan.Zero;
        foreach (var ev in recurringEvents)
        {
            totalRecurring += ev.Duration;
        }

        var recurringPercentage = totalScheduled > TimeSpan.Zero
            ? (int)Math.Round((double)totalRecurring.Ticks / totalScheduled.Ticks * 100)
            : 0;

        result.RecurringTime = new RecurringTimeInfo
        {
            Duration = totalRecurring,
            FormattedDuration = FormatDuration(totalRecurring),
            PercentageOfScheduledTime = recurringPercentage,
            RecurringEventCount = recurringEvents.Count
        };

        // 4. Calculate Busiest Day
        result.BusiestDay = CalculateBusiestDay(timedEvents);

        // 5. Calculate Longest Uninterrupted Focus Block
        result.LongestFocusBlock = CalculateLongestFocusBlock(timedEvents, start, end);

        // 6. Generate Deterministic Human-Readable Observations (3-5 observations)
        result.Observations = GenerateObservations(result, timedEvents, allDayEvents);

        return result;
    }

    private static BusiestDayInfo CalculateBusiestDay(List<CalendarEvent> timedEvents)
    {
        if (timedEvents.Count == 0)
        {
            return new BusiestDayInfo
            {
                DayOfWeek = "None",
                DateFormatted = "-",
                ScheduledTime = TimeSpan.Zero,
                FormattedScheduledTime = "0m",
                EventCount = 0
            };
        }

        var groups = timedEvents
            .GroupBy(e => e.Start.Date)
            .Select(g => new
            {
                Date = g.Key,
                DayOfWeek = g.First().Start.ToString("dddd"),
                DateFormatted = g.First().Start.ToString("MMM d"),
                TotalDuration = g.Aggregate(TimeSpan.Zero, (acc, e) => acc + e.Duration),
                Count = g.Count()
            })
            .OrderByDescending(g => g.TotalDuration)
            .ThenByDescending(g => g.Count)
            .First();

        return new BusiestDayInfo
        {
            DayOfWeek = groups.DayOfWeek,
            DateFormatted = groups.DateFormatted,
            ScheduledTime = groups.TotalDuration,
            FormattedScheduledTime = FormatDuration(groups.TotalDuration),
            EventCount = groups.Count
        };
    }

    private static FocusBlockInfo CalculateLongestFocusBlock(List<CalendarEvent> timedEvents, DateTimeOffset start, DateTimeOffset end)
    {
        // Search through each calendar day in the range [start, end]
        // Focus window is 9:00 AM - 6:00 PM for each day.
        var maxDuration = TimeSpan.Zero;
        DateTimeOffset? bestStart = null;
        DateTimeOffset? bestEnd = null;
        string bestDayOfWeek = "None";

        var currentDay = start.Date;
        var lastDay = end.Date;

        while (currentDay <= lastDay)
        {
            var dayOffset = start.Offset;
            var windowStart = new DateTimeOffset(currentDay, dayOffset).Add(DayStartHour);
            var windowEnd = new DateTimeOffset(currentDay, dayOffset).Add(DayEndHour);

            // Clamp window to analysis period [start, end]
            if (windowStart < start) windowStart = start;
            if (windowEnd > end) windowEnd = end;

            if (windowStart < windowEnd)
            {
                // Find all events intersecting this day's focus window
                var dayEvents = timedEvents
                    .Where(e => e.End > windowStart && e.Start < windowEnd)
                    .Select(e => new
                    {
                        Start = e.Start < windowStart ? windowStart : e.Start,
                        End = e.End > windowEnd ? windowEnd : e.End
                    })
                    .OrderBy(e => e.Start)
                    .ToList();

                // Merge overlapping or contiguous intervals
                var merged = new List<(DateTimeOffset Start, DateTimeOffset End)>();
                foreach (var ev in dayEvents)
                {
                    if (merged.Count == 0)
                    {
                        merged.Add((ev.Start, ev.End));
                    }
                    else
                    {
                        var last = merged[^1];
                        if (ev.Start <= last.End)
                        {
                            var newEnd = ev.End > last.End ? ev.End : last.End;
                            merged[^1] = (last.Start, newEnd);
                        }
                        else
                        {
                            merged.Add((ev.Start, ev.End));
                        }
                    }
                }

                // Compute free gaps
                var cursor = windowStart;
                foreach (var busy in merged)
                {
                    if (busy.Start > cursor)
                    {
                        var gap = busy.Start - cursor;
                        if (gap > maxDuration)
                        {
                            maxDuration = gap;
                            bestStart = cursor;
                            bestEnd = busy.Start;
                            bestDayOfWeek = cursor.ToString("dddd");
                        }
                    }
                    if (busy.End > cursor)
                    {
                        cursor = busy.End;
                    }
                }

                // Final gap between last busy block and windowEnd
                if (cursor < windowEnd)
                {
                    var finalGap = windowEnd - cursor;
                    if (finalGap > maxDuration)
                    {
                        maxDuration = finalGap;
                        bestStart = cursor;
                        bestEnd = windowEnd;
                        bestDayOfWeek = cursor.ToString("dddd");
                    }
                }
            }

            currentDay = currentDay.AddDays(1);
        }

        return new FocusBlockInfo
        {
            Duration = maxDuration,
            FormattedDuration = FormatDuration(maxDuration),
            DayOfWeek = bestDayOfWeek,
            Start = bestStart,
            End = bestEnd
        };
    }

    private static List<string> GenerateObservations(CalendarInsightResult result, List<CalendarEvent> timedEvents, List<CalendarEvent> allDayEvents)
    {
        var observations = new List<string>();

        // Observation 1: Busiest Day observation
        if (result.BusiestDay.EventCount > 0)
        {
            observations.Add(
                $"{result.BusiestDay.DayOfWeek} is your busiest day with {result.BusiestDay.FormattedScheduledTime} scheduled across {result.BusiestDay.EventCount} event{(result.BusiestDay.EventCount == 1 ? "" : "s")}."
            );
        }
        else
        {
            observations.Add("You have no scheduled commitments in this period.");
        }

        // Observation 2: Focus Block observation
        if (result.LongestFocusBlock.Duration >= TimeSpan.FromMinutes(30) && result.LongestFocusBlock.Start.HasValue && result.LongestFocusBlock.End.HasValue)
        {
            var startFormatted = result.LongestFocusBlock.Start.Value.ToString("h:mm tt");
            var endFormatted = result.LongestFocusBlock.End.Value.ToString("h:mm tt");
            observations.Add(
                $"{result.LongestFocusBlock.DayOfWeek} offers your longest uninterrupted focus block ({result.LongestFocusBlock.FormattedDuration} from {startFormatted} to {endFormatted})."
            );
        }
        else if (result.LongestFocusBlock.Duration > TimeSpan.Zero)
        {
            observations.Add(
                $"Your longest uninterrupted focus block is {result.LongestFocusBlock.FormattedDuration} on {result.LongestFocusBlock.DayOfWeek}."
            );
        }
        else
        {
            observations.Add("No daytime focus blocks of 30 minutes or more were found in this period.");
        }

        // Observation 3: Recurring meetings observation
        if (result.RecurringTime.RecurringEventCount > 0)
        {
            observations.Add(
                $"Recurring meetings take up {result.RecurringTime.PercentageOfScheduledTime}% ({result.RecurringTime.FormattedDuration}) of your scheduled time across {result.RecurringTime.RecurringEventCount} recurring session{(result.RecurringTime.RecurringEventCount == 1 ? "" : "s")}."
            );
        }
        else
        {
            observations.Add("No recurring meetings are scheduled in this period, providing maximum schedule adaptability.");
        }

        // Observation 4: Meeting / Event duration density
        if (timedEvents.Count > 0)
        {
            var avgMinutes = (int)Math.Round(result.TotalScheduledTime.TotalMinutes / timedEvents.Count);
            observations.Add(
                $"Average event length is {avgMinutes} minutes across {timedEvents.Count} scheduled commitment{(timedEvents.Count == 1 ? "" : "s")}."
            );
        }
        else
        {
            observations.Add("Your schedule is wide open with 0 timed commitments.");
        }

        // Observation 5: All-day events or daily workload
        if (allDayEvents.Count > 0)
        {
            observations.Add(
                $"You have {allDayEvents.Count} all-day event{(allDayEvents.Count == 1 ? "" : "s")} marked on your calendar during this timeframe."
            );
        }
        else
        {
            var totalDays = Math.Max(1, (int)Math.Ceiling((result.EndDate - result.StartDate).TotalDays));
            if (totalDays > 1 && result.TotalScheduledTime > TimeSpan.Zero)
            {
                var avgDaily = TimeSpan.FromTicks(result.TotalScheduledTime.Ticks / totalDays);
                observations.Add(
                    $"You average approximately {FormatDuration(avgDaily)} of scheduled time per day across this {totalDays}-day window."
                );
            }
        }

        return observations;
    }

    public static string FormatDuration(TimeSpan time)
    {
        if (time <= TimeSpan.Zero)
        {
            return "0m";
        }

        var totalHours = (int)time.TotalHours;
        var minutes = time.Minutes;

        if (totalHours == 0 && minutes == 0)
        {
            return "0m";
        }

        if (totalHours == 0)
        {
            return $"{minutes}m";
        }

        if (minutes == 0)
        {
            return $"{totalHours}h";
        }

        return $"{totalHours}h {minutes}m";
    }
}
