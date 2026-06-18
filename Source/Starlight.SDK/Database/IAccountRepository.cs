using Starlight.SDK.Database.Models;

namespace Starlight.SDK.Database;

public interface IAccountRepository
{
    Task<Account?> GetAccountById(uint id);

    /// <summary>Lookup by login name (i.e. the <c>account</c> body field).</summary>
    Task<Account?> GetAccountByUsernameAsync(string username, CancellationToken ct);

    /// <summary>Lookup by the session token previously issued from shield login.</summary>
    Task<Account?> GetAccountBySessionTokenAsync(string token, CancellationToken ct);

    /// <summary>
    /// Persists the rotating fields (session token, combo token, device id)
    /// after an auth operation succeeds.
    /// </summary>
    Task UpdateSessionAsync(Account account, CancellationToken ct);
}
