using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Starlight.Common;
using Starlight.SDK.Common;
using Starlight.Crypto;
using Starlight.SDK.Database;

namespace Starlight.SDK.Services;

/// <summary>
/// Default <see cref="IAuthService"/>. Holds the RSA password-decryption key
/// in memory and delegates all storage operations to
/// <see cref="IAccountRepository"/>.
/// </summary>
public sealed class AuthService(
    IAccountRepository accounts,
    RsaCrypto? passwordCrypto,
    SdkConfig sdkConfig,
    ILogger<AuthService> logger
)
    : IAuthService
{
    /// <summary>
    /// Token length used for both session and combo tokens.
    /// </summary>
    private const int TokenLength = 30;

    public async Task<AuthResult> LoginAsync(
        string account,
        string password,
        bool isCryptoEncrypted,
        string deviceId,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Fail(Retcode.ParameterError);

        // Decrypt the password if the client wrapped it with the public key.
        if (isCryptoEncrypted)
        {
            if (passwordCrypto is null)
            {
                logger.LogError("Client sent is_crypto=true but no RSA key is configured");
                return AuthResult.Fail(Retcode.SystemError);
            }

            if (!passwordCrypto.TryDecryptPassword(password, out var decrypted))
            {
                logger.LogWarning("Failed to RSA-decrypt password for account {Account}", account);
                return AuthResult.Fail(Retcode.LoginFailed);
            }

            password = decrypted;
        }

        var record = await accounts.GetAccountByUsernameAsync(account, ct);

        if (record is null)
        {
            // Unknown account. Only create one on the fly if the server
            // has opted into that behavior, otherwise this is a normal
            // login failure.
            // TODO: Implement the account creation endpoint later on and leave this as option for users that really wants it
            if (!sdkConfig.AllowAccountAutoCreate)
                return AuthResult.Fail(Retcode.LoginInvalidAccount);

            record = await accounts.CreateAccountAsync(account, Argon2Crypto.Hash(password), ct);
            logger.LogInformation("Auto-created account {Account} (id {Id}) on first login", account, record.Id);
        } else if (!Argon2Crypto.Verify(password, record.PasswordHash))
        {
            return AuthResult.Fail(Retcode.LoginInvalidAccount);
        }

        record.SessionToken = GenerateToken();
        record.RegisterDevice(deviceId);

        await accounts.UpdateSessionAsync(record, ct);
        return AuthResult.Ok(record);
    }

    public async Task<AuthResult> ExchangeComboTokenAsync(
        string sessionToken,
        string deviceId,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
            return AuthResult.Fail(Retcode.ParameterError);

        var record = await accounts.GetAccountBySessionTokenAsync(sessionToken, ct);

        if (record is null)
            return AuthResult.Fail(Retcode.LoginInvalidAccount);

        record.ComboToken = GenerateToken();
        record.RegisterDevice(deviceId);

        await accounts.UpdateSessionAsync(record, ct);
        return AuthResult.Ok(record);
    }

    private static string GenerateToken()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Span<char> buffer = stackalloc char[TokenLength];

        for (var i = 0; i < TokenLength; i++)
        {
            buffer[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }
        return new string(buffer);
    }
}
