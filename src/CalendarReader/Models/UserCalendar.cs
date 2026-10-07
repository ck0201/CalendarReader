namespace CalendarReader.Models;

public class UserCalendar
{
    public string Id { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPrimary { get; set; }
    public string? BackgroundColor { get; set; }
    public string? ForegroundColor { get; set; }
    public string AccessRole { get; set; } = string.Empty;
    public bool IsSelectedByDefault { get; set; }
    public bool IsShared => !IsPrimary && !string.Equals(AccessRole, "owner", StringComparison.OrdinalIgnoreCase);
}
