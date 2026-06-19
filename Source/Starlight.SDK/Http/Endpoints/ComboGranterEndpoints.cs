using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Starlight.Common;
using Starlight.SDK.Common;
using Starlight.Crypto;
using Starlight.SDK.Http.Models;
using Starlight.SDK.Services;

namespace Starlight.SDK.Http.Endpoints;

public static class ComboGranterEndpoints
{
    public static void MapComboGranterEndpoints(this IEndpointRouteBuilder routes)
    {
        foreach (var prefix in SdkRoutes.ComboGranterPathPrefixes)
        {
            routes.MapPost($"{prefix}/v2/login", HandleLoginV2Async);
            routes.MapPost($"{prefix}/login", HandleLoginV2Async);
            routes.MapGet($"{prefix}/getConfig", HandleGetConfig);
            routes.MapPost($"{prefix}/getConfig", HandleGetConfig);
            routes.MapPost($"{prefix}/compareProtocolVersion", HandleCompareProtocolVersion);
            routes.MapPost($"{prefix}/getProtocol", HandleCompareProtocolVersion);
            routes.MapGet($"{prefix}/compareProtocolVersion", HandleCompareProtocolVersionGet);
            routes.MapGet($"{prefix}/getProtocol", HandleCompareProtocolVersionGet);
        }
    }

    private static async Task<IResult> HandleLoginV2Async(
        HttpContext httpContext,
        [FromBody] ComboGranterLoginRequest? body,
        [FromHeader(Name = "x-rpc-device_id")] string? deviceId,
        [FromServices] IAuthService auth,
        [FromServices] IGeoIpLookup geoIp,
        [FromServices] SdkConfig sdkConfig,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct
    )
    {
        var logger = loggerFactory.CreateLogger("Starlight.SDK.ComboGranter");

        if (body is null
            || body.AppId is null
            || body.ChannelId is null
            || string.IsNullOrEmpty(body.Data)
            || string.IsNullOrEmpty(body.Device)
            || string.IsNullOrEmpty(deviceId)
            || !SdkUtils.IsValidDeviceId(deviceId))
        {
            return Results.Ok(ApiResponse.From(Retcode.ParameterError));
        }

        if (!SdkUtils.IsValidAppId(body.AppId.GetValueOrDefault()))
        {
            logger.LogInformation("Rejected combo granter login: app_id {AppId} is not an ApplicationId", body.AppId);
            return Results.Ok(ApiResponse.From(Retcode.ParameterError));
        }

        if (!string.Equals(body.Device, deviceId, StringComparison.Ordinal))
            return Results.Ok(ApiResponse.From(Retcode.LoginNetworkAtRisk));

        // HMAC signature verification, skipped only when explicitly disabled
        // (debug builds, integration tests).
        if (!sdkConfig.SkipSignatureCheck)
        {
            if (string.IsNullOrEmpty(body.Sign))
                return Results.Ok(ApiResponse.From(Retcode.ParameterError));

            if (string.IsNullOrEmpty(sdkConfig.HmacKey))
            {
                logger.LogError("ComboGranter HMAC key is not configured but SkipSignatureCheck=false");
                return Results.Ok(ApiResponse.From(Retcode.SystemError));
            }

            var canonical = CreateMessage(body);

            if (!HmacCrypto.Verify(canonical, sdkConfig.HmacKey, body.Sign!))
                return Results.Ok(ApiResponse.From(Retcode.MissingConfiguration));
        }

        ComboLoginV2Data? inner;

        try
        {
            inner = JsonSerializer.Deserialize<ComboLoginV2Data>(body.Data!);
        }
        catch (JsonException)
        {
            return Results.Ok(ApiResponse.From(Retcode.InvalidJsonBody));
        }

        if (inner is null || string.IsNullOrEmpty(inner.Token))
            return Results.Ok(ApiResponse.From(Retcode.ParameterError));

        var result = await auth.ExchangeComboTokenAsync(inner.Token!, deviceId!, ct);

        if (!result.IsSuccess || result.Account is null)
            return Results.Ok(ApiResponse.From(result.Code));

        var acc = result.Account;

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var countryCode = await geoIp.GetCountryCodeAsync(remoteIp, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(countryCode))
            countryCode = acc.Country;

        if (string.IsNullOrWhiteSpace(countryCode))
            countryCode = sdkConfig.DefaultCountryCode;

        var isGuest = acc.AccountType == 0;

        var innerJson = JsonSerializer.Serialize(new ComboInnerData {
            Guest = isGuest,
            CountryCode = countryCode,
            IsNewRegister = false
        });

        var payload = new ComboGranterLoginResponse {
            ComboId = sdkConfig.DefaultComboId,
            OpenId = acc.Id.ToString(),
            ComboToken = acc.ComboToken,
            Data = innerJson,
            Heartbeat = false,
            AccountType = acc.AccountType,
            FatigueRemind = null
        };

        return Results.Ok(ApiResponse.Ok(payload));
    }

    private static string CreateMessage(ComboGranterLoginRequest body)
    {
        var pairs = new SortedDictionary<string, string> {
            ["app_id"] = body.AppId!.Value.ToString(),
            ["channel_id"] = body.ChannelId!.Value.ToString(),
            ["data"] = body.Data!,
            ["device"] = body.Device!
        };

        return string.Join("&", pairs.Select(p => $"{p.Key}={p.Value}"));
    }

    /// <summary>
    /// Handles <c>GET/POST /hk4e_global/combo/granter/api/getConfig</c>.
    /// Returns the SDK-level configuration: announce URL, log level, QR
    /// login settings, etc. Client-type-specific switch configs (jpush,
    /// appsflyer, notifications) are populated per platform.
    /// </summary>
    private static IResult HandleGetConfig(
        [FromQuery] int? app_id,
        [FromQuery] int? channel_id,
        [FromQuery] int? client_type,
        [FromServices] SdkConfig sdkConfig)
    {
        if (app_id is null || channel_id is null || client_type is null
            || client_type < 1 || client_type > 13)
        {
            return Results.Ok(ApiResponse.From(Retcode.SystemError));
        }

        if (!SdkUtils.IsValidAppId(app_id.GetValueOrDefault()))
            return Results.Ok(ApiResponse.From(Retcode.SystemError));

        var s = sdkConfig.Shield;
        var data = new ComboGranterConfigData {
            Protocol = channel_id != 1,
            QrEnabled = s.UseQRLogin,
            LogLevel = s.ApiLogLevel,
            AnnounceUrl = s.AnnouncementUrl,
            PushAliasType = s.AliasPushType,
            DisableYsdkGuard = s.DisableYSDKGuard,
            EnableAnnouncePicPopup = s.EnableAnnouncementPopUp,
            AppName = s.AppName,
            QrCloudDisplayName = s.QrCloudDisplayName,
            EnableUserCenter = s.UseAccountCenter,
            FunctionalSwitchConfigs = new()
        };

        // QR settings only apply to PC/Cloud PC (client_type 3 and 9).
        if (client_type == 3 || client_type == 9)
        {
            data.QrEnabledApps = s.QrApps;
            data.QrAppIcons = s.QrAppIcons;
        }

        // Per-platform functional_switch_configs.
        switch (client_type)
        {
            case 1: // iOS
                data.FunctionalSwitchConfigs["jpush"] = s.JPushConfig;
                data.FunctionalSwitchConfigs["initialize_appsflyer"] = s.InitializeAppsFlyerConfig;
                break;
            case 2: // Android
                data.FunctionalSwitchConfigs["jpush"] = s.JPushConfig;
                data.FunctionalSwitchConfigs["allow_notification"] = s.AllowNotificationConfig;
                data.FunctionalSwitchConfigs["initialize_appsflyer"] = s.InitializeAppsFlyerConfig;
                break;
            case 8: // Cloud Android
                data.FunctionalSwitchConfigs["initialize_appsflyer"] = s.InitializeAppsFlyerConfig;
                break;
        }

        return Results.Ok(ApiResponse.Ok(data));
    }

    private static IResult HandleCompareProtocolVersion(
        [FromBody] CompareProtocolVersionRequest? body)
    {
        if (body is null || string.IsNullOrEmpty(body.Language)
            || !SdkUtils.IsValidLanguage(body.Language))
        {
            return Results.Ok(ApiResponse.From(Retcode.ProtocolFailed));
        }

        if (!SdkUtils.IsValidAppId(body.AppId))
            return Results.Ok(ApiResponse.From(Retcode.ProtocolFailed));

        var isModified = body.ChannelId != 1;

        var data = new CompareProtocolVersionData {
            Modified = isModified,
            Protocol = isModified
                ? new ProtocolInfo {
                    Id = 0,
                    AppId = body.AppId,
                    Language = body.Language!,
                    Major = body.Major,
                    Minimum = body.Minimum
                }
                : null
        };

        return Results.Ok(ApiResponse.Ok(data));
    }


    private static IResult HandleCompareProtocolVersionGet(
        [FromQuery] int? app_id,
        [FromQuery] string? language,
        [FromQuery] int? major,
        [FromQuery] int? minimum,
        [FromQuery] int? channel_id)
    {
        if (app_id is null || string.IsNullOrEmpty(language)
            || !SdkUtils.IsValidLanguage(language) || major is null || minimum is null)
        {
            return Results.Ok(ApiResponse.From(Retcode.ProtocolFailed));
        }

        if (!SdkUtils.IsValidAppId(app_id.Value))
            return Results.Ok(ApiResponse.From(Retcode.ProtocolFailed));

        var data = new CompareProtocolVersionData {
            Modified = true,
            Protocol = new ProtocolInfo {
                Id = 5,
                AppId = app_id.Value,
                Language = language!,
                Major = major.Value,
                Minimum = minimum.Value
            }
        };

        return Results.Ok(ApiResponse.Ok(data));
    }
}
