namespace Starlight.Common;

/// <summary>
/// Token types.
/// </summary>
public enum MaPassportTokenType
{
    /// <summary>
    /// Long-lived server token (stoken). Issued by
    /// <c>appLoginByAuthTicket</c> and <c>verifySToken</c>.
    /// </summary>
    Stoken = 1,

    /// <summary>
    /// Short-lived game / session token. Issued by
    /// <c>appLoginByPassword</c> and <c>reactivateAccount</c>.
    /// </summary>
    GameToken = 3
}
