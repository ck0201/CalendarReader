namespace CalendarReader.Models;

public class ConnectedAccount
{
    public string AccountId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset ConnectedAt { get; set; }
    public bool RequiresReauth { get; set; }
}

public class AccountTokenData
{
    public string AccountId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset? IssuedUtc { get; set; }
    public long? ExpiresInSeconds { get; set; }
    public DateTimeOffset ConnectedAt { get; set; }
    public bool RequiresReauth { get; set; }
}
