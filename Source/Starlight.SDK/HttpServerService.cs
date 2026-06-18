using Starlight.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Starlight.Database.DependencyInjection;
using Starlight.SDK.Crypto;
using Starlight.SDK.Database;
using Starlight.SDK.Database.Impl;
using Starlight.SDK.Http.Endpoints;
using Starlight.SDK.Services;

namespace Starlight.SDK;

public sealed class HttpServerService(
    StarlightConfig config,
    IServiceProvider rootProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services
            .AddSerilog()
            .AddSingleton(_ => config)
            .AddSingleton(rootProvider.GetRequiredService<IAccountRepository>())
            .AddSingleton(rootProvider.GetRequiredService<IAuthService>())
            .AddSingleton(rootProvider.GetRequiredService<SdkConfig>());

        // NOTE: If you wish to use SSL, do so behind a reverse proxy.
        builder.WebHost.UseUrls($"http://{config.Server.Http.BindAddress}:{config.Server.Http.BindPort}");

        var app = builder.Build();

        app.MapGet("/", () => Results.Ok("Starlight"));


        app.MapShieldEndpoints();
        app.MapComboGranterEndpoints();

        await app.RunAsync(stoppingToken);
    }
}

public static class ServiceExtensions
{
    public static IServiceCollection AddSdkServer(this IServiceCollection collection, StarlightConfig config)
    {
        var provider = DatabaseHelper.ParseProvider(config.Database.ConnectionString, out var connString);
        switch (provider)
        {
            case ProviderType.Sqlite:
                {
                    collection.AddStarlightDatabase(connString, config.Database.Sqlite, typeof(HttpServerService).Assembly);
                    collection.AddSingleton<IAccountRepository, SqliteAccountRepository>();
                    break;
                }
            default:
                throw new NotSupportedException($"Unsupported or missing database provider '{provider?.ToString() ?? "<null>"}'.");
        }

        var sdkCfg = config.Server.Sdk;
        collection.AddSingleton(sdkCfg);

        // Load the password decryption key lazily, it's only needed when a
        // client sends is_crypto=true, and absence shouldn't crash the host.
        collection.AddSingleton<RsaCrypto?>(_ => {
            if (string.IsNullOrWhiteSpace(sdkCfg.PasswordRsaKeyPath))
                return null;

            if (!File.Exists(sdkCfg.PasswordRsaKeyPath))
            {
                Log.Warning("Configured SDK password RSA key not found at {Path}", sdkCfg.PasswordRsaKeyPath);
                return null;
            }

            try
            {
                return RsaCrypto.FromPkcs8File(sdkCfg.PasswordRsaKeyPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load SDK password RSA key");
                return null;
            }
        });

        collection.AddSingleton<IAuthService, AuthService>();
        collection.AddHostedService<HttpServerService>();

        return collection;
    }
}
