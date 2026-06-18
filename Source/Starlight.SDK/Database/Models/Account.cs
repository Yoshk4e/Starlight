namespace Starlight.SDK.Database.Models;

/// <summary>
/// Domain-model view of an SDK account. The session/combo tokens are
/// mutated by the auth service on every successful login and are persisted
/// via <see cref="Starlight.SDK.Database.IAccountRepository.UpdateSessionAsync"/>.
/// </summary>
public sealed class Account
{
    public uint Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Lowercase SHA-256 hex digest of the password. Validated with
    /// <see cref="Starlight.SDK.Crypto.Sha256Crypto.Verify"/>.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public int PasswordTime { get; set; }

    /// <summary>
    /// Token returned by the shield <c>login</c> endpoint and consumed by
    /// the combo granter endpoint. Rotated on every fresh login.
    /// </summary>
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>
    /// Token consumed by the gate server. Rotated on every combo exchange.
    /// </summary>
    public string ComboToken { get; set; } = string.Empty;

    /// <summary>
    /// Last device id seen on this account, set by both endpoints from the
    /// <c>x-rpc-device_id</c> request header.
    /// </summary>
    public string CurrentDeviceId { get; set; } = string.Empty;
}
