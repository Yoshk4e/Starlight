using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Starlight.SDK.Common;
using Starlight.SDK.Http.Models;
using Starlight.SDK.Services;

namespace Starlight.SDK.Http.Endpoints;

public static class ShieldEndpoints
{
    /// <summary>The endpoint is mounted under all three biz-region prefixes the client may use.</summary>
    private static readonly string[] PathPrefixes = [
        "/hk4e_global/mdk/shield/api",
        "/hk4e_cn/mdk/shield/api",
        "/mdk/shield/api",
    ];

    public static void MapShieldEndpoints(this IEndpointRouteBuilder routes)
    {
        foreach (var prefix in PathPrefixes)
        {
            routes.MapPost($"{prefix}/login", HandleLoginAsync);
        }
    }

    private static async Task<IResult> HandleLoginAsync(
        [FromBody] ShieldLoginRequest body,
        [FromHeader(Name = "x-rpc-device_id")] string? deviceId,
        [FromServices] IAuthService auth,
        CancellationToken ct)
    {
        // missing core fields are treated as network-at-risk,
        // rather than a plain parameter error so the client
        // surfaces the right UI hint.
        if (body is null
            || string.IsNullOrWhiteSpace(body.Account)
            || string.IsNullOrWhiteSpace(body.Password)
            || body.IsCrypto is null
            || string.IsNullOrWhiteSpace(deviceId))
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

        var payload = new ShieldLoginResponse {
            Account = new ShieldAccountInfo {
                Id = acc.Id,
                Name = acc.Username,
                Email = acc.Email,
                Token = acc.SessionToken,
            },
            RealPersonRequired = false,
            SafeMobileRequired = false,
            ReactivateRequired = false,
            DeviceGrantRequired = false,
            RealNameOperation = "None",
        };

        return Results.Ok(ApiResponse.Ok(payload));
    }
}
