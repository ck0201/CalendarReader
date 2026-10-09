namespace CalendarReader.Tests;

using System.Threading.RateLimiting;
using CalendarReader.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

public class BriefingRateLimiterTests
{
    private static DefaultHttpContext CreateContext(string? sessionId = null)
    {
        var context = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(sessionId))
        {
            context.Request.Headers["Cookie"] = $"cr_session={sessionId}";
        }
        return context;
    }

    [Fact]
    public void GetPartition_UnauthenticatedRequest_ReturnsNoLimiter()
    {
        var context = CreateContext(sessionId: null);

        var partition = BriefingRateLimiter.GetPartition(context);

        // An unauthenticated request gets a NoLimiter so it passes straight through
        // to the endpoint to be rejected by authentication with HTTP 401 Unauthorized
        var limiter = partition.Factory("unauthenticated");
        for (var i = 0; i < 15; i++)
        {
            using var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired, "Unauthenticated requests must not be throttled by session rate limiter");
        }
    }

    [Fact]
    public void GetPartition_AuthenticatedRequest_EnforcesLimitOf5RequestsPerMinute()
    {
        var context = CreateContext(sessionId: "user-session-token-1");

        var partition = BriefingRateLimiter.GetPartition(context);
        var limiter = partition.Factory(partition.PartitionKey);

        // First 5 requests must succeed
        for (var i = 0; i < 5; i++)
        {
            using var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired, $"Request {i + 1} of 5 must be permitted");
        }

        // 6th request must be rejected (Rate limit exceeded)
        using var rejectedLease = limiter.AttemptAcquire();
        Assert.False(rejectedLease.IsAcquired, "6th request within window must be rejected with 429");
    }

    [Fact]
    public void GetPartition_SeparateSessions_AreIsolated()
    {
        var contextA = CreateContext(sessionId: "session-user-alpha");
        var contextB = CreateContext(sessionId: "session-user-beta");

        var partitionA = BriefingRateLimiter.GetPartition(contextA);
        var partitionB = BriefingRateLimiter.GetPartition(contextB);

        Assert.NotEqual(partitionA.PartitionKey, partitionB.PartitionKey);

        var limiterA = partitionA.Factory(partitionA.PartitionKey);
        var limiterB = partitionB.Factory(partitionB.PartitionKey);

        // Exhaust session A's quota (5 requests)
        for (var i = 0; i < 5; i++)
        {
            using var lease = limiterA.AttemptAcquire();
            Assert.True(lease.IsAcquired);
        }

        // Session A is blocked
        using (var leaseA = limiterA.AttemptAcquire())
        {
            Assert.False(leaseA.IsAcquired);
        }

        // Session B still has full quota available
        using (var leaseB = limiterB.AttemptAcquire())
        {
            Assert.True(leaseB.IsAcquired, "Session B must have its own independent rate limit quota");
        }
    }

    [Fact]
    public void GetPartition_PartitionKey_DoesNotContainRawSessionToken()
    {
        const string rawSession = "super-secret-user-cookie-998877";
        var context = CreateContext(sessionId: rawSession);

        var partition = BriefingRateLimiter.GetPartition(context);

        Assert.DoesNotContain(rawSession, partition.PartitionKey);
        Assert.Equal(16, partition.PartitionKey.Length);
    }
}
