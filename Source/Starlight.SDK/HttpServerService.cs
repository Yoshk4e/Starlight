using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Starlight.SDK;

public sealed class HttpServerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSerilog();
        builder.WebHost.UseUrls("http://0.0.0.0:8080");

        var app = builder.Build();

        app.MapGet("/", () => Results.Ok("Starlight"));

        await app.RunAsync(stoppingToken);
    }
}
