namespace Starlight.SDK.Common;

public static class SdkRoutes
{
    public static readonly IReadOnlyList<string> ShieldPathPrefixes =
        ["/hk4e_global/mdk/shield/api", "/hk4e_cn/mdk/shield/api", "/mdk/shield/api"];

    public static readonly IReadOnlyList<string> ComboGranterPathPrefixes =
        ["/hk4e_global/combo/granter/login", "/hk4e_cn/combo/granter/login", "/combo/granter/login"];
}
