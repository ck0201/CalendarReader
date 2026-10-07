namespace CalendarReader.Tests;

using CalendarReader.Models;
using CalendarReader.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class SqliteAccountTokenStoreTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly SqliteAccountTokenStore _store;

    public SqliteAccountTokenStoreTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_tokens_{Guid.NewGuid():N}.db");
        _dataProtection = new EphemeralDataProtectionProvider();
        _store = new SqliteAccountTokenStore(
            $"Data Source={_tempDbPath}",
            _dataProtection,
            NullLogger<SqliteAccountTokenStore>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try
            {
                File.Delete(_tempDbPath);
            }
            catch
            {
                // Ignore cleanup lock in temp folder
            }
        }
    }

    [Fact]
    public async Task SaveAccountTokenAsync_StoresAndRetrievesMultipleAccountsForOneSession()
    {
        var sessionId = "session-123";

        var account1 = new AccountTokenData
        {
            AccountId = "google-sub-1",
            Email = "personal@gmail.com",
            DisplayName = "Personal User",
            SessionId = sessionId,
            AccessToken = "access-token-1",
            RefreshToken = "refresh-token-1",
            ConnectedAt = DateTimeOffset.UtcNow
        };

        var account2 = new AccountTokenData
        {
            AccountId = "google-sub-2",
            Email = "work@gmail.com",
            DisplayName = "Work User",
            SessionId = sessionId,
            AccessToken = "access-token-2",
            RefreshToken = "refresh-token-2",
            ConnectedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };

        await _store.SaveAccountTokenAsync(sessionId, account1);
        await _store.SaveAccountTokenAsync(sessionId, account2);

        var accounts = await _store.GetAccountsAsync(sessionId);
        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a.Email == "personal@gmail.com" && a.AccountId == "google-sub-1");
        Assert.Contains(accounts, a => a.Email == "work@gmail.com" && a.AccountId == "google-sub-2");

        var token1 = await _store.GetAccountTokenAsync(sessionId, "google-sub-1");
        Assert.NotNull(token1);
        Assert.Equal("access-token-1", token1.AccessToken);
        Assert.Equal("refresh-token-1", token1.RefreshToken);

        var token2 = await _store.GetAccountTokenAsync(sessionId, "google-sub-2");
        Assert.NotNull(token2);
        Assert.Equal("access-token-2", token2.AccessToken);
        Assert.Equal("refresh-token-2", token2.RefreshToken);
    }

    [Fact]
    public async Task ReconnectingSameAccount_UpdatesExistingRecordWithoutDuplicating()
    {
        var sessionId = "session-456";

        var initial = new AccountTokenData
        {
            AccountId = "google-sub-same",
            Email = "user@gmail.com",
            DisplayName = "User Initial",
            SessionId = sessionId,
            AccessToken = "initial-access",
            RefreshToken = "initial-refresh",
            ConnectedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveAccountTokenAsync(sessionId, initial);

        var updated = new AccountTokenData
        {
            AccountId = "google-sub-same",
            Email = "user@gmail.com",
            DisplayName = "User Updated",
            SessionId = sessionId,
            AccessToken = "new-access",
            RefreshToken = null, // Google didn't return new refresh token
            ConnectedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveAccountTokenAsync(sessionId, updated);

        var accounts = await _store.GetAccountsAsync(sessionId);
        Assert.Single(accounts);
        Assert.Equal("User Updated", accounts[0].DisplayName);

        var token = await _store.GetAccountTokenAsync(sessionId, "google-sub-same");
        Assert.NotNull(token);
        Assert.Equal("new-access", token.AccessToken);
        // Preserves existing refresh token via COALESCE logic
        Assert.Equal("initial-refresh", token.RefreshToken);
    }

    [Fact]
    public async Task RemoveAccountAsync_RemovesOnlySelectedAccount()
    {
        var sessionId = "session-789";

        await _store.SaveAccountTokenAsync(sessionId, new AccountTokenData
        {
            AccountId = "acc-1",
            Email = "first@gmail.com",
            SessionId = sessionId,
            AccessToken = "tok-1",
            ConnectedAt = DateTimeOffset.UtcNow
        });

        await _store.SaveAccountTokenAsync(sessionId, new AccountTokenData
        {
            AccountId = "acc-2",
            Email = "second@gmail.com",
            SessionId = sessionId,
            AccessToken = "tok-2",
            ConnectedAt = DateTimeOffset.UtcNow
        });

        await _store.RemoveAccountAsync(sessionId, "acc-1");

        var accounts = await _store.GetAccountsAsync(sessionId);
        Assert.Single(accounts);
        Assert.Equal("second@gmail.com", accounts[0].Email);

        var remainingToken = await _store.GetAccountTokenAsync(sessionId, "acc-2");
        Assert.NotNull(remainingToken);
        Assert.Equal("tok-2", remainingToken.AccessToken);
    }

    [Fact]
    public async Task MarkRequiresReauthAsync_UpdatesAccountStatusWithoutBreakingOthers()
    {
        var sessionId = "session-reauth";

        await _store.SaveAccountTokenAsync(sessionId, new AccountTokenData
        {
            AccountId = "acc-healthy",
            Email = "healthy@gmail.com",
            SessionId = sessionId,
            AccessToken = "tok-healthy",
            ConnectedAt = DateTimeOffset.UtcNow
        });

        await _store.SaveAccountTokenAsync(sessionId, new AccountTokenData
        {
            AccountId = "acc-expired",
            Email = "expired@gmail.com",
            SessionId = sessionId,
            AccessToken = "tok-expired",
            ConnectedAt = DateTimeOffset.UtcNow
        });

        await _store.MarkRequiresReauthAsync(sessionId, "acc-expired");

        var accounts = await _store.GetAccountsAsync(sessionId);
        Assert.Equal(2, accounts.Count);

        var healthy = accounts.First(a => a.AccountId == "acc-healthy");
        var expired = accounts.First(a => a.AccountId == "acc-expired");

        Assert.False(healthy.RequiresReauth);
        Assert.True(expired.RequiresReauth);

        Assert.True(await _store.HasAnyAccountAsync(sessionId));
    }

    [Fact]
    public async Task RemoveAllAccountsAsync_ClearsSessionCompletely()
    {
        var sessionId = "session-logout";

        await _store.SaveAccountTokenAsync(sessionId, new AccountTokenData
        {
            AccountId = "acc-1",
            Email = "a@gmail.com",
            SessionId = sessionId,
            AccessToken = "tok-1",
            ConnectedAt = DateTimeOffset.UtcNow
        });

        await _store.RemoveAllAccountsAsync(sessionId);

        var accounts = await _store.GetAccountsAsync(sessionId);
        Assert.Empty(accounts);
        Assert.False(await _store.HasAnyAccountAsync(sessionId));
    }
}
