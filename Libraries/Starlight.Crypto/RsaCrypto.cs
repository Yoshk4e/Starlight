using System.Security.Cryptography;
using System.Text;

namespace Starlight.Crypto;

/// <summary>
/// RSA helpers.
/// </summary>
public sealed class RsaCrypto
{
    private readonly RSA _privateKey;

    public RsaCrypto(byte[] pkcs8PrivateKey)
    {
        _privateKey = RSA.Create();
        _privateKey.ImportPkcs8PrivateKey(pkcs8PrivateKey, out _);
    }

    /// <summary>
    /// Build an <see cref="RsaCrypto"/> from a base64-encoded PKCS#8 string
    /// (with or without PEM markers).
    /// </summary>
    public static RsaCrypto FromBase64Pkcs8(string base64)
    {
        var cleaned = base64
            .Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
            .Replace("-----END PRIVATE KEY-----", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();
        return new RsaCrypto(Convert.FromBase64String(cleaned));
    }

    /// <summary>
    /// Build an <see cref="RsaCrypto"/> from a PKCS#8 binary file on disk.
    /// </summary>
    public static RsaCrypto FromPkcs8File(string path) =>
        new(File.ReadAllBytes(path));

    /// <summary>
    /// Decrypts a base64-encoded password that the client encrypted with
    /// PKCS#1 v1.5 padding under the matching public key.
    /// </summary>
    public string DecryptPassword(string base64Cipher)
    {
        var cipher = Convert.FromBase64String(base64Cipher);
        var plain = _privateKey.Decrypt(cipher, RSAEncryptionPadding.Pkcs1);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>
    /// Tries to decrypt the supplied cipher. Returns <c>false</c> if the
    /// padding is invalid or the input is not valid base64.
    /// </summary>
    public bool TryDecryptPassword(string base64Cipher, out string plain)
    {
        try
        {
            plain = DecryptPassword(base64Cipher);
            return true;
        }
        catch
        {
            plain = string.Empty;
            return false;
        }
    }
}
