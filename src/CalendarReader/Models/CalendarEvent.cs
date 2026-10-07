namespace CalendarReader.Models;

using System.Text.RegularExpressions;

public class CalendarEvent
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public bool IsAllDay { get; set; }
    public bool IsRecurring { get; set; }
    public int AttendeeCount { get; set; }
    public string? Description { get; set; }
    public bool IsTrainJourney { get; set; }
    public string? Status { get; set; }
    public string? HtmlLink { get; set; }

    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;

    // High-confidence railway keywords and phrases
    private static readonly string[] HighConfidenceKeywords =
    [
        "train journey",
        "train ticket",
        "train booking",
        "train ride",
        "irctc",
        "railway",
        "railways"
    ];

    // Strong railway patterns
    private static readonly Regex TrainNumberRegex = new(@"\btrain\s*(?:no\.?|#)?\s*\d{3,5}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PnrRegex = new(@"\bpnr(?:\s*[:\-#]?\s*\w+)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RailwayCoachSeatRegex = new(@"\b(coach\s*[a-z]?\d{1,2}|berth\s*\d{1,2}|seat\s*(?:no\.?)?\s*\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StandaloneTrainRegex = new(@"\btrain\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StationPlatformRegex = new(@"\b(railway\s*station|station\s*platform|platform\s*\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsTrainJourneyMatch(string? title, string? description)
    {
        var combined = $"{title} {description}".Trim();
        if (string.IsNullOrEmpty(combined))
        {
            return false;
        }

        // 1. High-confidence explicit railway terms
        foreach (var keyword in HighConfidenceKeywords)
        {
            if (combined.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 2. Explicit PNR match (e.g. "PNR 1234567890", "PNR: ABC123")
        if (PnrRegex.IsMatch(combined))
        {
            return true;
        }

        // 3. Train number patterns (e.g. "Train 12004", "Train No. 12951", "Train #22436")
        if (TrainNumberRegex.IsMatch(combined))
        {
            return true;
        }

        // 4. Standalone "train" word combined with journey/transit context (strictly avoids "training", "trainer", etc.)
        if (StandaloneTrainRegex.IsMatch(combined))
        {
            if (combined.Contains("to ", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("from ", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("trip", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("journey", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("boarding", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("ticket", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("booking", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("express", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("departure", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("arrival", StringComparison.OrdinalIgnoreCase) ||
                StationPlatformRegex.IsMatch(combined) ||
                RailwayCoachSeatRegex.IsMatch(combined))
            {
                return true;
            }
        }

        // 5. Railway Coach/Berth/Seat or Railway Platform with transit context
        if (RailwayCoachSeatRegex.IsMatch(combined) && StationPlatformRegex.IsMatch(combined))
        {
            return true;
        }

        // Generic "Departure" or "Arrival" alone are deliberately rejected without strong railway indicators
        return false;
    }

    /// <summary>
    /// Filters upcoming train journeys from now through the next 2 months, ordered chronologically ascending.
    /// Excludes past journeys.
    /// </summary>
    public static List<CalendarEvent> FilterUpcomingTrainJourneys(IEnumerable<CalendarEvent> events, DateTimeOffset now)
    {
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var twoMonthsLater = todayStart.AddMonths(2).AddDays(1).AddTicks(-1);

        return events
            .Where(e => e.IsTrainJourney && e.End >= now && e.Start <= twoMonthsLater)
            .OrderBy(e => e.Start)
            .ToList();
    }

    private static readonly Regex TaskPatternRegex = new(
        @"^(?:\[task\]|task(?:\s*[:\-#]|\s+\d+|\s*$)|\[todo\]|todo(?:\s*[:\-#]|\s+\d+|\s*$)|\[to-do\]|to-do(?:\s*[:\-#]|\s+\d+|\s*$)|\[action\]|action(?:\s*[:\-#]|\s+\d+|\s*$))|#task\b|#todo\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool IsTask => IsTaskMatch(Title, Description);

    public static bool IsTaskMatch(string? title, string? description)
    {
        var cleanTitle = title?.Trim() ?? string.Empty;
        if (TaskPatternRegex.IsMatch(cleanTitle))
        {
            return true;
        }

        var cleanDesc = description?.Trim() ?? string.Empty;
        if (cleanDesc.Contains("#task", StringComparison.OrdinalIgnoreCase) || cleanDesc.Contains("#todo", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public CalendarTask ToTask()
    {
        var cleanTitle = Regex.Replace(Title, @"^(?:\[task\]|task\s*[:\-]|\[todo\]|todo\s*[:\-]|\[to-do\]|to-do\s*[:\-]|\[action\]|action\s*[:\-])\s*", "", RegexOptions.IgnoreCase).Trim();
        if (string.IsNullOrEmpty(cleanTitle))
        {
            cleanTitle = Title;
        }

        return new CalendarTask
        {
            Id = Id,
            Title = cleanTitle,
            DueDate = Start,
            IsAllDay = IsAllDay,
            IsCompleted = Status == "completed" || Title.Contains("[x]", StringComparison.OrdinalIgnoreCase),
            Description = Description,
            HtmlLink = HtmlLink
        };
    }
}
