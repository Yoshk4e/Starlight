using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starlight.Common;
using Starlight.SDK.Common;
using Starlight.Crypto;
using Starlight.SDK.Database;
using Starlight.SDK.Database.Models;
using Starlight.SDK.Http;
using Starlight.SDK.Http.Models;
using Starlight.SDK.Services;

namespace Starlight.SDK.Http.Endpoints;

/// <summary>
/// Implements the <c>/hk4e_global/account/ma-passport/api/**</c> endpoints.
/// Currently covers:
/// <list type="bullet">
///   <item><c>POST getConfig</c></item>
///   <item><c>POST appLoginByPassword</c></item>
///   <item><c>POST appLoginByAuthTicket</c></item>
///   <item><c>POST reactivateAccount</c></item>
///   <item><c>GET  getSwitchStatus</c></item>
/// </list>
/// </summary>
public static class PassportEndpoints
{
    public static void MapPassportEndpoints(this IEndpointRouteBuilder routes)
    {
        foreach (var prefix in SdkRoutes.MaPassportPathPrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                continue;

            routes.MapPost($"{prefix}/getConfig", HandleGetConfig);

            // Login flows. The three POST endpoints share a common response
            // shape (MaPassportLoginData) and a common helper for building
            // the user_info block.
            routes.MapPost($"{prefix}/appLoginByPassword", HandleAppLoginByPasswordAsync);
            routes.MapPost($"{prefix}/appLoginByAuthTicket", HandleAppLoginByAuthTicketAsync);
            routes.MapPost($"{prefix}/reactivateAccount", HandleReactivateAccountAsync);

            // Per-platform UI feature flags for the SDK login screen.
            routes.MapGet($"{prefix}/getSwitchStatus", HandleGetSwitchStatus);
        }
    }


    private static async Task<IResult> HandleGetConfig(
        HttpContext httpContext,
        [FromServices] SdkConfig sdkConfig,
        [FromServices] IGeoIpLookup geoIp,
        CancellationToken ct)
    {
        var remoteIp = GetClientIp(httpContext);
        var countryCode = await geoIp.GetCountryCodeAsync(remoteIp, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(countryCode))
            countryCode = sdkConfig.DefaultCountryCode;

        var areaCode = CountryToMobileCode(countryCode);

        return Results.Ok(ApiResponse.Ok(new MaPassportConfigData {
            Ip = new MaPassportIpInfo {
                CountryCode = countryCode,
                Language = sdkConfig.MaPassport.Language,
                AreaCode = areaCode
            },
            AreaWhitelist = sdkConfig.MaPassport.AreaWhitelist,
            RealnameWhitelist = sdkConfig.MaPassport.RealnameWhitelist,
            GuardianAgeLimit = sdkConfig.MaPassport.GuardianAgeLimit,
            DisableMmt = sdkConfig.MaPassport.DisableMmt,
            ShowBirthday = sdkConfig.MaPassport.ShowBirthday.ToString().ToLowerInvariant()
        }));
    }

    /// <summary>
    /// Handles <c>POST /hk4e_global/account/ma-passport/api/appLoginByPassword</c>.
    /// Authenticates an account using email + RSA-encrypted password and
    /// returns a fresh game token plus the masked user-info block.
    /// </summary>
    private static async Task<IResult> HandleAppLoginByPasswordAsync(
        HttpContext httpContext,
        [FromBody] MaPassportAppLoginByPasswordRequest? body,
        [FromHeader(Name = "x-rpc-device_id")] string? deviceId,
        [FromServices] SdkConfig sdkConfig,
        [FromServices] IGeoIpLookup geoIp,
        [FromServices] IAccountRepository accounts,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Starlight.SDK.Passport");

        if (body is null || string.IsNullOrEmpty(body.Account) || string.IsNullOrEmpty(body.Password))
            return Results.Ok(ApiResponse.From(Retcode.MaPassportParameterError));

        if (string.IsNullOrEmpty(deviceId) || !SdkUtils.IsValidDeviceId(deviceId))
            return Results.Ok(ApiResponse.From(Retcode.MaPassportParameterError));

        string account;
        string password;

        if (sdkConfig.MaPassport.Login.SkipRsaDecryption)
        {
            account = body.Account!;
            password = body.Password!;
        }
        else
        {
            var passwordCrypto = httpContext.RequestServices.GetService<RsaCrypto>();
            if (passwordCrypto is null)
            {
                logger.LogError("appLoginByPassword: no RSA key configured but SkipRsaDecryption=false");
                return Results.Ok(ApiResponse.From(Retcode.MaPassportSystemError));
            }

            if (!passwordCrypto.TryDecryptPassword(body.Account!, out account)
                || !passwordCrypto.TryDecryptPassword(body.Password!, out password))
            {
                return Results.Ok(ApiResponse.From(Retcode.MaPassportSystemError));
            }
        }

        if (!account.Contains('@'))
            return Results.Ok(ApiResponse.From(Retcode.MaPassportAccountFormatError));

        var login = sdkConfig.MaPassport.Login;
        if (password.Length < login.MinPasswordLength || password.Length > login.MaxPasswordLength)
            return Results.Ok(ApiResponse.From(Retcode.MaPassportPasswordFormatError));

        var acc = await accounts.GetAccountByEmailAsync(account, ct);
        if (acc is null || !Argon2Crypto.Verify(password, acc.PasswordHash))
            return Results.Ok(ApiResponse.From(Retcode.MaPassportAccountMismatch));


        if (!login.SkipDeviceIdCheck
            && acc.KnownDeviceIds.Count > 0
            && !acc.KnownDeviceIds.Contains(deviceId))
        {
            return Results.Ok(ApiResponse.From(Retcode.MaPassportAccountNewDeviceDetected));
        }

        // Issue a fresh session token and persist.
        acc.SessionToken = GenerateToken(login.TokenLength);
        acc.RegisterDevice(deviceId);

        if (string.IsNullOrEmpty(acc.Country))
            acc.Country = await geoIp.GetCountryCodeAsync(GetClientIp(httpContext), ct).ConfigureAwait(false);

        await accounts.UpdateSessionAsync(acc, ct);

        return Results.Ok(ApiResponse.Ok(BuildLoginData(
            acc,
            tokenType: login.AppLoginTokenType,
            token: acc.SessionToken,
            reactivateActionTicket: string.Empty,
            bindEmailActionTicket: string.Empty,
            country: acc.Country)));
    }

    // -----------------------------------------------------------------------
    // appLoginByAuthTicket
    // -----------------------------------------------------------------------

    /// <summary>
    /// Handles <c>POST /hk4e_global/account/ma-passport/api/appLoginByAuthTicket</c>.
    /// Exchanges a one-time <c>AuthLoginTicket</c> for an stoken. The
    /// ticket is consumed (single-use).
    /// </summary>
    /// <remarks>
    /// Starlight does not yet persist tickets in its database, so this
    /// endpoint returns <see cref="Retcode.MaPassportIllegalParameter"/>
    /// for any non-empty ticket. The plumbing is in place; the only thing
    /// missing is a <c>ITicketRepository</c> wired through
    /// <see cref="IAccountRepository"/>. TODO: implement once ticket
    /// storage is added.
    /// </remarks>
    private static Task<IResult> HandleAppLoginByAuthTicketAsync(
        [FromBody] MaPassportAppLoginByAuthTicketRequest? body)
    {
        if (body is null || string.IsNullOrEmpty(body.Ticket))
            return Task.FromResult(Results.Ok(ApiResponse.From(Retcode.MaPassportParameterError)));

        // TODO: look up the ticket in the ticket repository, resolve the
        // account, and delete the ticket (single-use). For now we return
        // an explicit "illegal parameter" so the client surfaces the
        // right error rather than hanging.
        //
        // Once a ticket repository exists, the implementation should:
        //   1. Resolve the ticket to an account id.
        //   2. Load the account via accounts.GetAccountById(id, ct).
        //   3. Resolve the caller's country via geoIp.
        //   4. Return BuildLoginData(acc, AuthTicketTokenType, acc.SessionToken, ...).
        return Task.FromResult(Results.Ok(ApiResponse.From(Retcode.MaPassportIllegalParameter)));
    }


    /// <summary>
    /// Handles <c>POST /hk4e_global/account/ma-passport/api/reactivateAccount</c>.
    /// Consumes a one-time <c>reactivation</c> action ticket and clears
    /// <see cref="Account.RequireActivation"/> on the associated account.
    /// </summary>
    /// <remarks>
    /// Same caveat as <see cref="HandleAppLoginByAuthTicketAsync"/>:
    /// ticket persistence isn't wired up yet. The plumbing is in place
    /// so this can be flipped on once a ticket repository exists.
    /// </remarks>
    private static Task<IResult> HandleReactivateAccountAsync(
        [FromBody] MaPassportReactivateAccountRequest? body)
    {
        if (body is null || string.IsNullOrEmpty(body.ActionTicket))
            return Task.FromResult(Results.Ok(ApiResponse.From(Retcode.MaPassportParameterError)));

        // TODO: look up the ticket in the ticket repository and resolve
        // the account id, then clear RequireActivation.
        //
        // Once a ticket repository exists, the implementation should:
        //   1. Resolve the ticket to an account id.
        //   2. Load the account via accounts.GetAccountById(id, ct).
        //   3. Set acc.RequireActivation = false; await accounts.UpdateSessionAsync(acc, ct).
        //   4. Resolve the caller's country via geoIp.
        //   5. Return BuildLoginData(acc, AppLoginTokenType, acc.SessionToken, ...).
        return Task.FromResult(Results.Ok(ApiResponse.From(Retcode.MaPassportTicketNotExist)));
    }

    /// <summary>
    /// Handles <c>GET /hk4e_global/account/ma-passport/api/getSwitchStatus</c>.
    /// Returns the per-platform UI feature flags for the SDK login screen.
    /// </summary>
    private static IResult HandleGetSwitchStatus(
        [FromQuery] string? app_id,
        [FromQuery] int? platform,
        [FromServices] SdkConfig sdkConfig)
    {
        if (string.IsNullOrEmpty(app_id) || platform is null)
            return Results.Ok(ApiResponse.From(Retcode.MaPassportParameterError));

        var s = sdkConfig.MaPassport.Login;
        var switchMap = new Dictionary<string, MaPassportSwitchEntry>();

        // ui_v2 is only enabled on Android.
        if (platform == 2 && s.EnableAndroidUiV2)
        {
            switchMap["ui_v2"] = new MaPassportSwitchEntry { Enabled = true };
        }

        // Third-party login options.
        if (s.EnableThirdPartyLogins)
        {
            switchMap["apple_login"] = new MaPassportSwitchEntry { Enabled = true };
            switchMap["google_login"] = new MaPassportSwitchEntry { Enabled = true };
            switchMap["twitter_login"] = new MaPassportSwitchEntry { Enabled = true };
            switchMap["facebook_login"] = new MaPassportSwitchEntry { Enabled = true };
        }

        // Login/register tabs.
        if (s.EnableLoginRegisterTabs)
        {
            switchMap["pwd_login_tab"] = new MaPassportSwitchEntry { Enabled = true };
            switchMap["account_register_tab"] = new MaPassportSwitchEntry { Enabled = true };
        }

        // Always-on UI affordances.
        switchMap["password_reset_entry"] = new MaPassportSwitchEntry { Enabled = true };
        switchMap["common_question_entry"] = new MaPassportSwitchEntry { Enabled = true };
        switchMap["bind_user_thirdparty_email"] = new MaPassportSwitchEntry { Enabled = true };
        switchMap["third_party_bind_email"] = new MaPassportSwitchEntry { Enabled = true };
        switchMap["user_name_bind_email"] = new MaPassportSwitchEntry { Enabled = true };
        switchMap["marketing_authorization"] = new MaPassportSwitchEntry { Enabled = true };

        // Always-off / Vietnam-only switches.
        switchMap["vn_real_name"] = new MaPassportSwitchEntry { Enabled = s.EnableVietnamRealName };
        switchMap["vn_real_name_v2"] = new MaPassportSwitchEntry { Enabled = s.EnableVietnamRealName };
        switchMap["firebase_return_unmasked_email"] = new MaPassportSwitchEntry { Enabled = false };
        switchMap["bind_thirdparty"] = new MaPassportSwitchEntry { Enabled = false };

        return Results.Ok(ApiResponse.Ok(new MaPassportSwitchStatusData {
            SwitchStatusMap = switchMap
        }));
    }


    /// <summary>
    /// Builds the common login response payload shared by
    /// <c>appLoginByPassword</c>, <c>appLoginByAuthTicket</c>,
    /// <c>reactivateAccount</c> and <c>verifySToken</c>.
    /// </summary>
    private static MaPassportLoginData BuildLoginData(
        Account acc,
        int tokenType,
        string token,
        string reactivateActionTicket,
        string bindEmailActionTicket,
        string country)
    {
        return new MaPassportLoginData {
            ReactivateActionTicket = reactivateActionTicket,
            BindEmailActionTicket = bindEmailActionTicket,
            ExtUserInfo = new MaPassportExtUserInfo {
                GuardianEmail = string.Empty,
                Birth = "0"
            },
            Token = new MaPassportTokenInfo {
                Token = token,
                TokenType = tokenType
            },
            UserInfo = new MaPassportUserInfo {
                Aid = acc.Id,
                Mid = "nigs",
                AccountName = acc.Username,
                Email = MaskString(acc.Email),
                IsEmailVerify = 0,
                AreaCode = string.Empty,
                Mobile = string.Empty,
                SafeAreaCode = string.Empty,
                SafeMobile = string.Empty,
                Realname = string.Empty,
                IdentityCode = string.Empty,
                RebindAreaCode = string.Empty,
                RebindMobile = string.Empty,
                RebindMobileTime = "0",
                Links = new(),
                Country = country,
                PasswordTime = acc.PasswordTime > 0 ? acc.PasswordTime.ToString() : "0",
                UnmaskedEmail = string.Empty,
                UnmaskedEmailType = 0
            }
        };
    }

    /// <summary>
    /// Generates a random alphanumeric token of the given length. Mirrors
    /// the AuthService helper so we don't take a dependency on it from
    /// here.
    /// </summary>
    private static string GenerateToken(int length)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Span<char> buffer = stackalloc char[length];

        for (var i = 0; i < length; i++)
            buffer[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        return new string(buffer);
    }

    private static string MaskString(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length < 4)
            return new string('*', text.Length);

        var start = text.Length >= 10 ? 2 : 1;
        var end = text.Length > 5 ? 2 : 1;
        var masked = new string('*', text.Length - start - end);
        return string.Concat(text.AsSpan(0, start), masked, text.AsSpan(text.Length - end));
    }

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
    /// Maps an ISO-3166-1 alpha-2 country code to its ITU mobile dialling
    /// code (without the leading +). Used by the ma-passport config
    /// endpoint to populate <c>area_code</c> for the SDK's phone-input
    /// flow. Returns an empty string for unknown countries.
    /// </summary>
    private static string CountryToMobileCode(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return string.Empty;

        return countryCode.ToUpperInvariant() switch {
            "US" => "1",
            "CA" => "1",
            "GB" => "44",
            "FR" => "33",
            "DE" => "49",
            "JP" => "81",
            "KR" => "82",
            "CN" => "86",
            "TW" => "886",
            "HK" => "852",
            "SG" => "65",
            "IN" => "91",
            "BR" => "55",
            "RU" => "7",
            "AU" => "61",
            "NZ" => "64",
            "IT" => "39",
            "ES" => "34",
            "PT" => "351",
            "MX" => "52",
            "ID" => "62",
            "TH" => "66",
            "VN" => "84",
            "PH" => "63",
            "MY" => "60",
            "SA" => "966",
            "AE" => "971",
            "TR" => "90",
            "NL" => "31",
            "BE" => "32",
            "CH" => "41",
            "AT" => "43",
            "SE" => "46",
            "NO" => "47",
            "DK" => "45",
            "FI" => "358",
            "PL" => "48",
            "UA" => "380",
            "EG" => "20",
            "ZA" => "27",
            _ => string.Empty
        };
    }
}
