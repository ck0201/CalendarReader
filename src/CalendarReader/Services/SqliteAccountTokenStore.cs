namespace CalendarReader.Services;

using System.Security.Cryptography;
using CalendarReader.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class SqliteAccountTokenStore : IAccountTokenStore
{
    private readonly string _connectionString;
    private readonly IDataProtector _protector;
    private readonly ILogger<SqliteAccountTokenStore> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SqliteAccountTokenStore(
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SqliteAccountTokenStore> logger)
    {
        _logger = logger;
        _protector = dataProtectionProvider.CreateProtector("CalendarReader.OAuthTokens");

        var configuredPath = configuration["Database:Path"];
        string dbPath;
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            dbPath = configuredPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(appData, "CalendarReader");
            dbPath = Path.Combine(appDir, "calendarreader.db");
        }

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    // Constructor for testing with custom connection string
    public SqliteAccountTokenStore(
        string connectionString,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SqliteAccountTokenStore> logger)
    {
        _logger = logger;
        _protector = dataProtectionProvider.CreateProtector("CalendarReader.OAuthTokens");
        _connectionString = connectionString;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS ConnectedAccounts (
                    SessionId TEXT NOT NULL,
                    AccountId TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    AvatarUrl TEXT,
                    ProtectedAccessToken TEXT NOT NULL,
                    ProtectedRefreshToken TEXT,
                    IssuedUtc TEXT,
                    ExpiresInSeconds INTEGER,
                    ConnectedAt TEXT NOT NULL,
                    RequiresReauth INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (SessionId, AccountId)
                );
                CREATE INDEX IF NOT EXISTS IX_ConnectedAccounts_SessionId ON ConnectedAccounts(SessionId);";

            await using var command = new SqliteCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<IReadOnlyList<ConnectedAccount>> GetAccountsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return [];
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT AccountId, Email, DisplayName, AvatarUrl, ConnectedAt, RequiresReauth
            FROM ConnectedAccounts
            WHERE SessionId = @SessionId
            ORDER BY ConnectedAt ASC;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var accounts = new List<ConnectedAccount>();

        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new ConnectedAccount
            {
                AccountId = reader.GetString(0),
                Email = reader.GetString(1),
                DisplayName = reader.GetString(2),
                AvatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                ConnectedAt = DateTimeOffset.Parse(reader.GetString(4)),
                RequiresReauth = reader.GetInt32(5) == 1
            });
        }

        return accounts;
    }

    public async Task<AccountTokenData?> GetAccountTokenAsync(string sessionId, string accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(accountId))
        {
            return null;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT AccountId, Email, DisplayName, AvatarUrl, ProtectedAccessToken,
                   ProtectedRefreshToken, IssuedUtc, ExpiresInSeconds, ConnectedAt, RequiresReauth
            FROM ConnectedAccounts
            WHERE SessionId = @SessionId AND AccountId = @AccountId
            LIMIT 1;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapAccountTokenData(sessionId, reader);
    }

    public async Task<IReadOnlyList<AccountTokenData>> GetAllAccountTokensAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return [];
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT AccountId, Email, DisplayName, AvatarUrl, ProtectedAccessToken,
                   ProtectedRefreshToken, IssuedUtc, ExpiresInSeconds, ConnectedAt, RequiresReauth
            FROM ConnectedAccounts
            WHERE SessionId = @SessionId
            ORDER BY ConnectedAt ASC;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<AccountTokenData>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var data = MapAccountTokenData(sessionId, reader);
            if (data != null)
            {
                list.Add(data);
            }
        }

        return list;
    }

    public async Task SaveAccountTokenAsync(string sessionId, AccountTokenData accountToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(accountToken.AccountId))
        {
            throw new ArgumentException("Account ID cannot be empty.", nameof(accountToken.AccountId));
        }

        await EnsureInitializedAsync(cancellationToken);

        var protectedAccess = _protector.Protect(accountToken.AccessToken);
        string? protectedRefresh = !string.IsNullOrWhiteSpace(accountToken.RefreshToken)
            ? _protector.Protect(accountToken.RefreshToken)
            : null;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO ConnectedAccounts (
                SessionId, AccountId, Email, DisplayName, AvatarUrl,
                ProtectedAccessToken, ProtectedRefreshToken, IssuedUtc,
                ExpiresInSeconds, ConnectedAt, RequiresReauth
            ) VALUES (
                @SessionId, @AccountId, @Email, @DisplayName, @AvatarUrl,
                @ProtectedAccessToken, @ProtectedRefreshToken, @IssuedUtc,
                @ExpiresInSeconds, @ConnectedAt, @RequiresReauth
            )
            ON CONFLICT(SessionId, AccountId) DO UPDATE SET
                Email = excluded.Email,
                DisplayName = excluded.DisplayName,
                AvatarUrl = excluded.AvatarUrl,
                ProtectedAccessToken = excluded.ProtectedAccessToken,
                ProtectedRefreshToken = CASE
                    WHEN excluded.ProtectedRefreshToken IS NOT NULL AND excluded.ProtectedRefreshToken != ''
                        THEN excluded.ProtectedRefreshToken
                    ELSE ConnectedAccounts.ProtectedRefreshToken
                END,
                IssuedUtc = excluded.IssuedUtc,
                ExpiresInSeconds = excluded.ExpiresInSeconds,
                RequiresReauth = excluded.RequiresReauth;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        command.Parameters.AddWithValue("@AccountId", accountToken.AccountId);
        command.Parameters.AddWithValue("@Email", accountToken.Email);
        command.Parameters.AddWithValue("@DisplayName", accountToken.DisplayName);
        command.Parameters.AddWithValue("@AvatarUrl", (object?)accountToken.AvatarUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("@ProtectedAccessToken", protectedAccess);
        command.Parameters.AddWithValue("@ProtectedRefreshToken", (object?)protectedRefresh ?? DBNull.Value);
        command.Parameters.AddWithValue("@IssuedUtc", (object?)accountToken.IssuedUtc?.ToString("o") ?? DBNull.Value);
        command.Parameters.AddWithValue("@ExpiresInSeconds", (object?)accountToken.ExpiresInSeconds ?? DBNull.Value);
        command.Parameters.AddWithValue("@ConnectedAt", accountToken.ConnectedAt.ToString("o"));
        command.Parameters.AddWithValue("@RequiresReauth", accountToken.RequiresReauth ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkRequiresReauthAsync(string sessionId, string accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(accountId))
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE ConnectedAccounts
            SET RequiresReauth = 1
            WHERE SessionId = @SessionId AND AccountId = @AccountId;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAccountAsync(string sessionId, string accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(accountId))
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM ConnectedAccounts WHERE SessionId = @SessionId AND AccountId = @AccountId;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAllAccountsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM ConnectedAccounts WHERE SessionId = @SessionId;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> HasAnyAccountAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT 1 FROM ConnectedAccounts WHERE SessionId = @SessionId AND RequiresReauth = 0 LIMIT 1;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    private AccountTokenData? MapAccountTokenData(string sessionId, SqliteDataReader reader)
    {
        try
        {
            var protectedAccess = reader.GetString(4);
            var accessToken = _protector.Unprotect(protectedAccess);

            string? refreshToken = null;
            if (!reader.IsDBNull(5))
            {
                var protectedRefresh = reader.GetString(5);
                if (!string.IsNullOrWhiteSpace(protectedRefresh))
                {
                    try
                    {
                        refreshToken = _protector.Unprotect(protectedRefresh);
                    }
                    catch (CryptographicException)
                    {
                        _logger.LogWarning("Failed to decrypt refresh token for account {AccountId}", reader.GetString(0));
                    }
                }
            }

            DateTimeOffset? issuedUtc = null;
            if (!reader.IsDBNull(6) && DateTimeOffset.TryParse(reader.GetString(6), out var parsedIssued))
            {
                issuedUtc = parsedIssued;
            }

            long? expiresIn = reader.IsDBNull(7) ? null : reader.GetInt64(7);

            return new AccountTokenData
            {
                SessionId = sessionId,
                AccountId = reader.GetString(0),
                Email = reader.GetString(1),
                DisplayName = reader.GetString(2),
                AvatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                IssuedUtc = issuedUtc,
                ExpiresInSeconds = expiresIn,
                ConnectedAt = DateTimeOffset.Parse(reader.GetString(8)),
                RequiresReauth = reader.GetInt32(9) == 1
            };
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt tokens for account {AccountId} in session {SessionId}", reader.GetString(0), sessionId);
            return null;
        }
    }
}
