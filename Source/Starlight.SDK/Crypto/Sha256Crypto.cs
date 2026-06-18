using System.Security.Cryptography;
using System.Text;

namespace Starlight.SDK.Crypto;

/// <summary>
/// SHA-256 helpers for password storage. We never store plain-text passwords,
/// the database holds a SHA-256 hex digest produced by <see cref="Hash"/>,
/// and login validates with the constant-time <see cref="Verify"/>.
/// </summary>
public static class Sha256Crypto
{
    /// <summary>SHA-256 of <paramref name="content"/> as lowercase hex.</summary>
    public static string Hash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Constant-time equality check between a candidate plain-text password
    /// and a previously stored SHA-256 hex digest.
    /// </summary>
    public static bool Verify(string content, string expectedHash)
    {
        var computed = Hash(content);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(expectedHash));
    }
}
