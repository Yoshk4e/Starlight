namespace Starlight.Common;

/// <summary>
/// Short codes used as <see cref="ThirdPartyTokenConfig"/> dictionary
/// keys in <see cref="SdkShieldConfig.ThirdPartyConfigs"/> and as
/// entries in <see cref="SdkShieldConfig.ThirdPartyApps"/>.
/// </summary>
public static class ThirdPartyApp
{
    /// <summary>Apple Sign-in.</summary>
    public const string Apple = "ap";

    /// <summary>Google Sign-in.</summary>
    public const string Google = "gl";

    /// <summary>Facebook Login.</summary>
    public const string Facebook = "fb";

    /// <summary>Twitter / X Login.</summary>
    public const string Twitter = "tw";

    /// <summary>Game Center (iOS) Sign-in.</summary>
    public const string GameCenter = "gc";

    /// <summary>TapTap Sign-in.</summary>
    public const string TapTap = "tp";
}
