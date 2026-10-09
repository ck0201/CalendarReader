namespace CalendarReader.Models;

/// <summary>
/// Configuration settings for AI calendar briefing generation.
/// API key should be provided via environment variables (e.g. GEMINI_API_KEY)
/// or .NET User Secrets (e.g. "Ai:ApiKey").
/// </summary>
public class AiBriefingConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.8-flash";
    public int TimeoutSeconds { get; set; } = 15;
    public int CacheTtlMinutes { get; set; } = 5;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
