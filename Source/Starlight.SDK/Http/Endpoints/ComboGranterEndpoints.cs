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
    private static readonly string[] PathPrefixes = [
        "/hk4e_global/combo/granter/login",
        "/hk4e_cn/combo/granter/login",
        "/combo/granter/login",
    ];

    public static void MapComboGranterEndpoints(this IEndpointRouteBuilder routes)
    {
        foreach (var prefix in PathPrefixes)
        {
            routes.MapPost($"{prefix}/v2/login", HandleLoginV2Async);
            routes.MapPost($"{prefix}/login", HandleLoginV2Async);
        }
    }

    private static async Task<IResult> HandleLoginV2Async(
        [FromBody] ComboGranterLoginRequest body,
        [FromHeader(Name = "x-rpc-device_id")] string? deviceId,
        [FromServices] IAuthService auth,
        [FromServices] SdkConfig sdkConfig,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Starlight.SDK.ComboGranter");

        if (body is null
            || body.AppId is null
            || body.ChannelId is null
            || string.IsNullOrEmpty(body.Data)
            || string.IsNullOrEmpty(body.Device)
            || string.IsNullOrEmpty(body.Sign)
            || string.IsNullOrEmpty(deviceId))
        {
            return Results.Ok(ApiResponse.From(Retcode.ParameterError));
        }

        if (!string.Equals(body.Device, deviceId, StringComparison.Ordinal))
            return Results.Ok(ApiResponse.From(Retcode.LoginNetworkAtRisk));

        // HMAC signature verification, skipped only when explicitly disabled
        // (debug builds, integration tests).
        if (!sdkConfig.SkipSignatureCheck)
        {
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
        var innerJson = JsonSerializer.Serialize(new ComboInnerData {
            Guest = inner.Guest.GetValueOrDefault(),
            CountryCode = "US",
            IsNewRegister = false,
        });

        var payload = new ComboGranterLoginResponse {
            ComboId = "0",
            OpenId = acc.Id.ToString(),
            ComboToken = acc.ComboToken,
            Data = innerJson,
            Heartbeat = false,
            AccountType = inner.Guest.GetValueOrDefault() ? 0 : 1,
            FatigueRemind = null,
        };

        return Results.Ok(ApiResponse.Ok(payload));
    }


    private static string CreateMessage(ComboGranterLoginRequest body)
    {
        var pairs = new SortedDictionary<string, string> {
            ["app_id"] = body.AppId!.Value.ToString(),
            ["channel_id"] = body.ChannelId!.Value.ToString(),
            ["data"] = body.Data!,
            ["device"] = body.Device!,
        };

        return string.Join("&", pairs.Select(p => $"{p.Key}={p.Value}"));
    }
}
