namespace Starlight.SDK.Common;

public static class SdkValidations
{
    public static bool IsValidGameBiz(string? biz)
        => !string.IsNullOrEmpty(biz) && GameBiz.All.Contains(biz);

    public static bool IsValidLanguage(string? language)
        => !string.IsNullOrEmpty(language) && GameLanguage.All.Contains(language);

    public static bool IsValidAppId(int appId)
        => Enum.IsDefined(typeof(ApplicationId), appId);
}
