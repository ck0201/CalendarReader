namespace CalendarReader.Models;

public class CalendarTask
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset? DueDate { get; set; }
    public bool IsAllDay { get; set; }
    public bool IsCompleted { get; set; }
    public string? Description { get; set; }
    public string? HtmlLink { get; set; }
    public string AccountEmail { get; set; } = string.Empty;
}
