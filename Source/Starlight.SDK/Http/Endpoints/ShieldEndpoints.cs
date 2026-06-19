using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Starlight.Common;
using Starlight.SDK.Common;
using Starlight.SDK.Http.Models;
using Starlight.SDK.Services;

namespace Starlight.SDK.Http.Endpoints;

public static class ShieldEndpoints
{
    /// <summary>Maximum accepted length of the <c>x-rpc-device_id</c> header.</summary>
    private const int MaxDeviceIdLength = 128;

    public static void MapShieldEndpoints(this IEndpointRouteBuilder routes)
    {
        foreach (var prefix in SdkRoutes.ShieldPathPrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                continue;

            routes.MapPost($"{prefix}/login", HandleLoginAsync);
        }
    }

    private static async Task<IResult> HandleLoginAsync(
        HttpContext httpContext,
        [FromBody] ShieldLoginRequest? body,
        [FromHeader(Name = "x-rpc-device_id")] string? deviceId,
        [FromHeader(Name = "x-rpc-language")] string? language,
        [FromServices] IAuthService auth,
        [FromServices] IGeoIpLookup geoIp,
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
            || !IsValidDeviceId(deviceId))
        {
            return Results.Ok(ApiResponse.From(Retcode.LoginNetworkAtRisk));
        }

        if (!SdkValidations.IsValidGameBiz(body.GameKey)
            || !SdkValidations.IsValidLanguage(language))
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

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var country = await geoIp.GetCountryCodeAsync(remoteIp, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(country))
            country = acc.Country;

        if (string.IsNullOrWhiteSpace(country))
            country = sdkConfig.DefaultCountryCode;

        if (!string.Equals(acc.Country, country, StringComparison.Ordinal))
            acc.Country = country;

        var realNameOperation = string.IsNullOrWhiteSpace(acc.RealNameOperation) ? sdkConfig.DefaultRealNameOperation : acc.RealNameOperation;

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

    /// <summary>
    /// Validates the device id before it is forwarded downstream. Rejects
    /// oversized values (would exceed DB column limits when serialized into
    /// <c>known_device_ids</c>) and values that contain the pipe delimiter
    /// or control characters, both of which would corrupt the pipe-delimited
    /// storage format.
    /// </summary>
    private static bool IsValidDeviceId(string value)
    {
        if (value.Length > MaxDeviceIdLength || value.Contains('|'))
            return false;

        foreach (var ch in value)
        {
            if (char.IsControl(ch))
                return false;
        }

        return true;
    }
}
