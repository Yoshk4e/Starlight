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
}
