namespace CalendarReader.Services;

using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Provides rate-limiting configuration and session partitioning for the AI Calendar Briefing endpoint.
/// Partitions strictly by application session (cr_session cookie) using a SHA-256 pseudonymized partition key.
/// Unauthenticated requests are not throttled here; they pass through to be rejected with HTTP 401 Unauthorized by the endpoint.
/// </summary>
public static class BriefingRateLimiter
{
    public const string PolicyName = "briefing_rate_limit";
    public const int PermitLimit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static RateLimitPartition<string> GetPartition(HttpContext httpContext, string sessionCookieName = "cr_session")
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var sessionId = httpContext.Request.Cookies[sessionCookieName];
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            // Unauthenticated requests pass through without consuming rate limit permits,
            // ensuring the endpoint returns its standard HTTP 401 Unauthorized response.
            return RateLimitPartition.GetNoLimiter("unauthenticated");
        }

        // Use SHA-256 hash prefix as partition key to prevent logging or retaining raw session tokens
        var sessionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))[..16];

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: sessionHash,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitLimit,
                Window = Window,
                QueueLimit = 0
            });
    }
}
