namespace CalendarReader.Services;

using CalendarReader.Models;

public interface ICalendarAnalysisService
{
    CalendarInsightResult Analyze(IReadOnlyList<CalendarEvent> events, DateTimeOffset start, DateTimeOffset end, IReadOnlyList<CalendarTask>? tasks = null);
    ScheduleSnapshot CreateScheduleSnapshot(CalendarInsightResult insights, bool includeTitles = false);
}
