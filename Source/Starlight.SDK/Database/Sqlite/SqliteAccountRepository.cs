using Starlight.Database;
using Starlight.SDK.Database.Impl.Entities;
using Starlight.SDK.Database.Models;

namespace Starlight.SDK.Database.Impl;

public sealed class SqliteAccountRepository(StarlightDatabase db) : IAccountRepository
{
    public async Task<Account?> GetAccountById(uint id)
    {
        var entity = await db.FindAsync<AccountEntity>(id);
        return entity is null ? null : Map(entity);
    }

    public async Task<Account?> GetAccountByUsernameAsync(string username, CancellationToken ct)
    {
        var entities = await db.QueryAsync<AccountEntity>(a => a.Username == username, ct);
        var entity = entities.FirstOrDefault();
        return entity is null ? null : Map(entity);
    }

    public async Task<Account?> GetAccountBySessionTokenAsync(string token, CancellationToken ct)
    {
        var entities = await db.QueryAsync<AccountEntity>(a => a.SessionToken == token, ct);
        var entity = entities.FirstOrDefault();
        return entity is null ? null : Map(entity);
    }

    public async Task UpdateSessionAsync(Account account, CancellationToken ct)
    {
        var entity = await db.FindAsync<AccountEntity>(account.Id, ct);
        if (entity is null) return;

        entity.SessionToken = account.SessionToken;
        entity.ComboToken = account.ComboToken;
        entity.KnownDeviceIds = string.Join(DeviceIdDelimiter, account.KnownDeviceIds);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private const string DeviceIdDelimiter = "|";

    private static Account Map(AccountEntity entity) => new() {
        Id = entity.Id,
        Username = entity.Username,
        Email = entity.Email ?? string.Empty,
        PasswordHash = entity.Password,
        PasswordTime = 0,
        SessionToken = entity.SessionToken ?? string.Empty,
        ComboToken = entity.ComboToken ?? string.Empty,
        KnownDeviceIds = string.IsNullOrEmpty(entity.KnownDeviceIds) ?
            [] :
            entity.KnownDeviceIds.Split(DeviceIdDelimiter, StringSplitOptions.RemoveEmptyEntries).ToList()
    };
}
