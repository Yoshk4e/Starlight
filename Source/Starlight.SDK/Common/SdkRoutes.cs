namespace Starlight.SDK.Common;

public static class SdkRoutes
{
    public static readonly IReadOnlyList<string> ShieldPathPrefixes =
        ["/hk4e_global/mdk/shield/api", "/hk4e_cn/mdk/shield/api", "/mdk/shield/api"];

    public static readonly IReadOnlyList<string> ComboGranterPathPrefixes =
        ["/hk4e_global/combo/granter/api", "/hk4e_cn/combo/granter/api", "/combo/granter/api"];

    public static readonly IReadOnlyList<string> ComboBoxPathPrefixes =
        ["/hk4e_global/combo/box/api/config", "/hk4e_cn/combo/box/api/config", "/combo/box/api/config"];

    public static readonly IReadOnlyList<string> MaPassportPathPrefixes =
        ["/hk4e_global/account/ma-passport/api", "/hk4e_cn/account/ma-passport/api", "/account/ma-passport/api"];

    public static readonly IReadOnlyList<string> WebstaticPathPrefixes =
        ["/admin/mi18n", "/webstatic", "/sdk-public", "/launcher-public"];
}
