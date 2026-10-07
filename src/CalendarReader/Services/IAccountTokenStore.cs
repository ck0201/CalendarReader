namespace CalendarReader.Services;

using CalendarReader.Models;

public interface IAccountTokenStore
{
    Task<IReadOnlyList<ConnectedAccount>> GetAccountsAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<AccountTokenData?> GetAccountTokenAsync(string sessionId, string accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountTokenData>> GetAllAccountTokensAsync(string sessionId, CancellationToken cancellationToken = default);
    Task SaveAccountTokenAsync(string sessionId, AccountTokenData accountToken, CancellationToken cancellationToken = default);
    Task MarkRequiresReauthAsync(string sessionId, string accountId, CancellationToken cancellationToken = default);
    Task RemoveAccountAsync(string sessionId, string accountId, CancellationToken cancellationToken = default);
    Task RemoveAllAccountsAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<bool> HasAnyAccountAsync(string sessionId, CancellationToken cancellationToken = default);
}
