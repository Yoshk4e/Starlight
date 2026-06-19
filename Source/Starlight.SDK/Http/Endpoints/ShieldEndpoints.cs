using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Starlight.Common;
using Starlight.SDK.Common;
using Starlight.SDK.Database;
using Starlight.SDK.Http.Models;
using Starlight.SDK.Services;

namespace Starlight.SDK.Http.Endpoints;

public static class ShieldEndpoints
{
    public static void MapShieldEndpoints(this IEndpointRouteBuilder routes)
    {
        foreach (var prefix in SdkRoutes.ShieldPathPrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                continue;

            routes.MapPost($"{prefix}/login", HandleLoginAsync);
            routes.MapGet($"{prefix}/loadConfig", HandleLoadConfig);
            routes.MapPost($"{prefix}/loadConfig", HandleLoadConfig);
        }
    }

    private static async Task<IResult> HandleLoginAsync(
        HttpContext httpContext,
        [FromBody] ShieldLoginRequest? body,
        [FromHeader(Name = "x-rpc-device_id")] string? deviceId,
        [FromHeader(Name = "x-rpc-language")] string? language,
        [FromServices] IAuthService auth,
        [FromServices] IGeoIpLookup geoIp,
        [FromServices] IAccountRepository accounts,
        [FromServices] SdkConfig sdkConfig,
        CancellationToken ct
    )
    {
        // missing core fields are treated as network-at-risk,
        // rather than a plain parameter error so the client
        // surfaces the right UI hint.
        if (body is null
            || string.IsNullOrWhiteSpace(body.Account)
            || string.IsNullOrWhiteSpace(body.Password)
            || body.IsCrypto is null
            || string.IsNullOrWhiteSpace(deviceId)
            || !SdkUtils.IsValidDeviceId(deviceId))
        {
            return Results.Ok(ApiResponse.From(Retcode.LoginNetworkAtRisk));
        }

        if (!SdkUtils.IsValidGameBiz(body.GameKey)
            || !SdkUtils.IsValidLanguage(language))
        {
            return Results.Ok(ApiResponse.From(Retcode.LoginNetworkAtRisk));
        }

        var result = await auth.LoginAsync(
            body.Account!,
            body.Password!,
            body.IsCrypto.GetValueOrDefault(),
            deviceId!,
            ct);

        if (!result.IsSuccess || result.Account is null)
            return Results.Ok(ApiResponse.From(result.Code));

        var acc = result.Account;
        var remoteIp = GetClientIp(httpContext);
        var country = await geoIp.GetCountryCodeAsync(remoteIp, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(country))
            country = acc.Country;

        if (string.IsNullOrWhiteSpace(country))
            country = sdkConfig.DefaultCountryCode;

        if (!string.Equals(acc.Country, country, StringComparison.Ordinal))
        {
            acc.Country = country;
            await accounts.UpdateSessionAsync(acc, ct);
        }

        // config default can also be blank, so fall back to "None" explicitly
        // instead of letting a null/empty value sneak into the response.
        var realNameOperation = string.IsNullOrWhiteSpace(acc.RealNameOperation) ?
            string.IsNullOrWhiteSpace(sdkConfig.DefaultRealNameOperation) ? "None" : sdkConfig.DefaultRealNameOperation :
            acc.RealNameOperation;

        var payload = new ShieldLoginResponse {
            Account = new ShieldAccountInfo {
                Id = acc.Id,
                Name = acc.Username,
                Email = acc.Email,
                Token = acc.SessionToken,
                Country = country
            },
            RealPersonRequired = acc.RequireRealPerson,
            SafeMobileRequired = acc.RequireSafeMobile,
            ReactivateRequired = acc.RequireActivation,
            DeviceGrantRequired = acc.RequireDeviceGrant,
            RealNameOperation = realNameOperation
        };

        return Results.Ok(ApiResponse.Ok(payload));
    }

    // behind a reverse proxy, Connection.RemoteIpAddress is just the proxy's
    // own IP, so GeoIP lookups need the real client IP from forwarded headers.
    private static string? GetClientIp(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();

        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        var realIp = httpContext.Request.Headers["X-Real-IP"].ToString();

        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Trim();

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Handles <c>GET/POST /hk4e_global/mdk/shield/api/loadConfig</c>.
    /// Returns the client-side SDK configuration for the requested
    /// platform and game biz. values come from <see cref="SdkShieldConfig"/>.
    /// </summary>
    private static IResult HandleLoadConfig(
        [FromQuery] int? client,
        [FromQuery] string? game_key,
        [FromServices] SdkConfig sdkConfig)
    {
        if (string.IsNullOrEmpty(game_key))
            return Results.Ok(ApiResponse.From(Retcode.ParameterError));

        if (client is null || client < 1 || client > 13 || !SdkUtils.IsValidGameBiz(game_key))
            return Results.Ok(ApiResponse.From(Retcode.MissingConfiguration));

        var s = sdkConfig.Shield;
        var isPhone = client is 1 or 2 or 8;

        var data = new ShieldLoadConfigData {
            Id = GetConfigId(client.Value),
            GameKey = game_key!,
            AppId = (int)Common.ApplicationId.Release,
            Client = GetPlatformNameById(client.Value),
            Identity = "I_IDENTITY",
            Guest = s.EnableGuestLogin,
            IgnoreVersions = "2.6.0",
            Scene = client == 3 ? "S_ACCOUNT" : "S_NORMAL",
            Name = s.AppName,
            DisableRegist = s.DisableRegistrations,
            EnableEmailCaptcha = s.UseEmailCaptcha,
            Thirdparty = s.ThirdPartyApps,
            DisableMmt = s.DisableMmt,
            ServerGuest = s.EnableServerGuest,
            ThirdpartyIgnore = s.ThirdPartyIgnored,
            EnablePsBindAccount = s.EnablePSBindAccount,
            ThirdpartyLoginConfigs = s.ThirdPartyConfigs,
            InitializeFirebase = isPhone && s.EnableFireBase,
            BbsAuthLogin = s.EnableBBSAuthLogin,
            BbsAuthLoginIgnore = s.BBSAuthLoginIgnored,
            FetchInstanceId = s.FetchCurrentInstanceId,
            EnableFlashLogin = s.EnableFlashLogin,
            EnableLogo18 = s.EnableAdultLogoAndroid,
            LogoHeight = s.AdultLogoAndroidHeight.ToString(),
            LogoWidth = s.AdultLogoAndroidWidth.ToString(),
            EnableCxBindAccount = s.EnableCXBindAccount,
            FirebaseBlacklistDevicesSwitch = isPhone && s.EnableFireBaseBlacklistDevicesSwitch,
            FirebaseBlacklistDevicesVersion = isPhone ? s.FireBaseBlacklistVersion : 0,
            HoyolabAuthLogin = s.EnableHoyoLabAuthLogin,
            HoyolabAuthLoginIgnore = s.HoyoLabAuthIgnore,
            HoyoplayAuthLogin = s.EnableHoyoPlayAuthLogin
        };

        return Results.Ok(ApiResponse.Ok(data));
    }

    /// <summary>
    /// Maps a platform id to the configuration id.
    /// </summary>
    private static int GetConfigId(int platformId) => platformId switch {
        1 => 4,
        2 => 5,
        3 => 6,
        6 => 30,
        8 => 27,
        9 => 53,
        10 => 26,
        11 => 28,
        13 => 44,
        _ => -1
    };

    /// <summary>
    /// Maps a platform id to its human-readable name as expected by the
    /// client SDK. Unknown ids return an empty string.
    /// </summary>
    private static string GetPlatformNameById(int id) => id switch {
        1 => "IOS",
        2 => "Android",
        3 => "PC",
        4 or 5 => "Browser",
        6 => "PS",
        8 => "CloudAndroid",
        9 => "CloudPC",
        10 => "CloudIOS",
        11 => "PS5",
        12 => "MacOS",
        13 => "CloudMacOS",
        _ => string.Empty
    };
}
