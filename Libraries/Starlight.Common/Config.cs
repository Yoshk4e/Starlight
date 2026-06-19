using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Starlight.Common;

public static class Config
{
    /// Please avoid as much as possible. Use this as a last-resort where
    /// dependency injection falls through.
    public static StarlightConfig Instance { get; private set; } = new();

    public static LogEventLevel LogLevel => Instance.LogLevel;
    public static ExternalResources Resources => Instance.Resources;
    [Obsolete]
    public static ServerConfig Server => Instance.Server;
    public static DatabaseConfig Database => Instance.Database;

    private static void WriteDefaultConfig(string path)
    {
        var json = JsonSerializer.Serialize(Instance, Constants.JsonOptions);
        File.WriteAllText(path, json);
    }

    public static IHostApplicationBuilder AddConfig(this IHostApplicationBuilder builder, string path = "config.json")
    {
        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(path, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("SL__");

        // Docker containers generally want a read-only system.
        //
        // Usually we use environment variables in that context anyway,
        // so in those cases we forgo writing a JSON file.
        if (!Env.IsContainerized && !File.Exists(path))
        {
            WriteDefaultConfig(path);
        }

        var cfg = builder.Configuration
            .Build()
            .Get<StarlightConfig>();

        if (cfg is null)
        {
            Log.Warning("Failed to parse configuration, please check for invalid values!");
            cfg = new StarlightConfig();
        }

        Instance = cfg;

        return builder;
    }
}

public sealed class StarlightConfig
{
    [JsonConverter(typeof(JsonStringEnumConverter<LogEventLevel>))]
    public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;
    public ExternalResources Resources { get; set; } = new();
    [Obsolete]
    public ServerConfig Server { get; set; } = new();
    public DatabaseConfig Database { get; set; } = new();
    public SdkConfig Sdk { get; set; } = new();
}

public sealed class ExternalResources
{
    public string ResourcesPath { get; set; } = "./resources.zip";
}

[Obsolete]
public sealed class ServerConfig
{
    [Obsolete]
    public GameConfig Game { get; set; } = new();
}

public sealed class SdkConfig
{
    /// HTTP server bind address.
    /// <br/>
    /// Use <code>0.0.0.0</code> to bind on all addresses.
    public string BindAddress { get; set; } = "0.0.0.0";
    /// HTTP server bind port.
    public int BindPort { get; set; } = 8080;

    /// <summary>
    /// Shared HMAC-SHA256 key used to verify <c>sign</c> on the combo
    /// granter login endpoint. Must match the value the client was built
    /// with.
    /// </summary>
    public string HmacKey { get; set; } = string.Empty;

    /// <summary>
    /// When true, the combo granter endpoint accepts requests without
    /// validating their HMAC signature. Intended for local development
    /// only — never enable in production.
    /// </summary>
    public bool SkipSignatureCheck { get; set; }

    /// <summary>
    /// Filesystem path to the PKCS#8 RSA private key the shield login
    /// endpoint uses to decrypt passwords sent with <c>is_crypto=true</c>.
    /// Leave empty to disable RSA password decryption.
    /// </summary>
    public string? PasswordRsaKeyPath { get; set; }

    /// <summary>
    /// When true, a login attempt for a username that doesn't exist yet
    /// will create a brand-new account using the supplied credentials
    /// instead of failing with <see cref="Starlight.SDK.Common.Retcode.LoginInvalidAccount"/>.
    /// When false (the default), unknown accounts are rejected and must be
    /// created.
    /// </summary>
    public bool AllowAccountAutoCreate { get; set; }

    /// <summary>
    /// ISO-3166 country code returned to the client when GeoIP lookup is
    /// unavailable or disabled. Corresponds to the literal <c>"US"</c>
    /// value that was previously baked into <see cref="Starlight.SDK.Http.Models.ShieldAccountInfo"/>
    /// and <see cref="Starlight.SDK.Http.Models.ComboInnerData"/> as a
    /// default.
    /// </summary>
    public string DefaultCountryCode { get; set; } = "US";

    /// <summary>
    /// Value reported in <c>realname_operation</c> when no real-name flow
    /// is pending for the account.
    /// </summary>
    public string DefaultRealNameOperation { get; set; } = "None";

    /// <summary>
    /// Value reported in <c>combo_id</c> on the combo granter login
    /// response.
    /// Still go no idea what "ComboID" is really used for maybe hiro could know
    /// </summary>
    public string DefaultComboId { get; set; } = "0";

    /// <summary>
    /// Minimum accepted length of a (decrypted) password.
    /// </summary>
    public int MinPasswordLength { get; set; } = 15;

    /// <summary>
    /// Configuration for the real ip-api.com GeoIP lookup. When
    /// <see cref="IpApiGeoIpConfig.Enabled"/> is <c>true</c> the SDK
    /// registers <see cref="Starlight.SDK.Services.IpApiGeoIpLookup"/>
    /// as the <see cref="Starlight.SDK.Services.IGeoIpLookup"/> implementation;
    /// otherwise the no-op <see cref="Starlight.SDK.Services.DefaultGeoIpLookup"/>
    /// is used and every request resolves to
    /// <see cref="DefaultCountryCode"/>.
    /// </summary>
    public IpApiGeoIpConfig IpApi { get; set; } = new();
}

/// <summary>
/// Configuration for the ip-api.com JSON endpoint
/// (<a href="https://ip-api.com/docs/api:json">docs</a>). The free tier
/// is HTTP-only, requires no API key, and is rate-limited to 45 requests
/// per minute per server IP. The lookup implementation caches results
/// client-side and respects the <c>X-Rl</c> / <c>X-Ttl</c> rate-limit
/// headers returned by the service.
/// </summary>
public sealed class IpApiGeoIpConfig
{
    /// <summary>
    /// When <c>true</c>, the SDK calls ip-api.com to resolve client IPs
    /// to country codes. When <c>false</c> (default), the no-op
    /// <see cref="Starlight.SDK.Services.DefaultGeoIpLookup"/> is used
    /// and every request resolves to
    /// <see cref="SdkConfig.DefaultCountryCode"/>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Base URL of the ip-api.com JSON endpoint. The free tier only
    /// serves HTTP; if you have a pro account with HTTPS access, change
    /// this to <c>https://pro.ip-api.com/json</c> (and set
    /// <see cref="ApiKey"/>).
    /// </summary>
    public string Endpoint { get; set; } = "http://ip-api.com/json";

    /// <summary>
    /// Optional API key for the ip-api.com pro tier.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Response language for the (unused) country name field. ip-api.com
    /// supports <c>en</c>, <c>de</c>, <c>es</c>, <c>pt-BR</c>, <c>fr</c>,
    /// <c>ja</c>, <c>zh-CN</c>, <c>ru</c>. Default is <c>en</c>. The
    /// SDK only consumes the <c>countryCode</c> field, which is always
    /// ISO-3166-1 alpha-2 regardless of language.
    /// </summary>
    public string Lang { get; set; } = "en";

    /// <summary>
    /// Per-IP cache TTL in seconds. Successful lookups are cached for
    /// this duration to respect the 45 req/min rate limit across
    /// repeated logins from the same client. Defaults to 5 minutes.
    /// Set to <c>0</c> to disable caching entirely.
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 300;

    /// <summary>
    /// HTTP request timeout in milliseconds. The lookup is on the
    /// critical path of every login, so keep this short and fall back
    /// to the configured default country code on timeout. Defaults to
    /// 2 seconds.
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 2000;
}

public sealed class SqliteConfig
{
    public bool CreateIfMissing { get; set; } = true;
    public bool UseWal { get; set; } = true;
    public string Synchronous { get; set; } = "NORMAL";
    public int BusyTimeoutMilliseconds { get; set; } = 5000;
    public bool AllowClientEvaluation { get; set; } = true;
}

public sealed class DatabaseConfig
{
    public string ConnectionString { get; set; } = "sqlite:./data/starlight.db";
    public SqliteConfig Sqlite { get; set; } = new();
}

public sealed class GameConfig
{
    public string BindAddress { get; set; } = "0.0.0.0";
    public int BindPort { get; set; } = 22102;
}
