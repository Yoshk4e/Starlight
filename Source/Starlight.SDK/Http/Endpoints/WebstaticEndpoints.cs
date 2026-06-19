using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Starlight.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Starlight.SDK.Http.Endpoints;

/// <summary>
/// Serves the small static JSON files that the Genshin client fetches
/// during boot, primarily the <c>*-version.json</c> files under
/// <c>/admin/mi18n/**</c>. Optional filesystem serving for arbitrary
/// <c>webstatic/</c>, <c>sdk-public/</c> and <c>launcher-public/</c>
/// resources is enabled via <see cref="WebstaticConfig.ResourceRoot"/>.
/// </summary>
public static class WebstaticEndpoints
{
    public static void MapWebstaticEndpoints(this IEndpointRouteBuilder routes)
    {

        routes.MapGet("/admin/mi18n/plat_oversea/m2020030410/m2020030410-version.json", HandleVersionFile);
        routes.MapGet("/admin/mi18n/plat_os/m09291531181441/m09291531181441-version.json", HandleVersionFile);


        routes.MapGet("/webstatic/{**path}", HandleFile);
        routes.MapGet("/sdk-public/{**path}", HandleFile);
        routes.MapGet("/launcher-public/{**path}", HandleFile);
        routes.MapGet("/admin/mi18n/{**path}", HandleFile);
    }

    private static IResult HandleVersionFile(
        HttpContext httpContext,
        [FromServices] SdkConfig sdkConfig)
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;

        if (sdkConfig.Webstatic.VersionMap.TryGetValue(path, out var version))
        {
            return Results.Ok(new { version });
        }

        return HandleFile(httpContext, sdkConfig);
    }

    private static IResult HandleFile(
        HttpContext httpContext,
        [FromServices] SdkConfig sdkConfig)
    {
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Starlight.SDK.Webstatic");

        var resourceRoot = sdkConfig.Webstatic.ResourceRoot;

        if (string.IsNullOrWhiteSpace(resourceRoot))
        {
            logger.LogDebug("Webstatic miss (no ResourceRoot configured): {Path}", httpContext.Request.Path);
            return Results.NotFound(new { error = "Not Found" });
        }

        if (!Directory.Exists(resourceRoot))
        {
            logger.LogWarning("Configured WebstaticConfig.ResourceRoot does not exist: {Root}", resourceRoot);
            return Results.NotFound(new { error = "Not Found" });
        }

        var relativePath = httpContext.Request.Path.Value?.TrimStart('/') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath))
            return Results.NotFound(new { error = "Not Found" });

        var fullPath = Path.GetFullPath(Path.Combine(resourceRoot, relativePath));
        var rootFull = Path.GetFullPath(resourceRoot);

        // Reject path-traversal escapes outside the configured root.
        if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Rejected path traversal attempt: {Path}", httpContext.Request.Path);
            return Results.NotFound(new { error = "Not Found" });
        }

        if (!File.Exists(fullPath))
        {
            logger.LogDebug("Webstatic miss (file not found): {Path}", fullPath);
            return Results.NotFound(new { error = "Not Found" });
        }

        var contentType = GetContentType(fullPath);
        var fileBytes = File.ReadAllBytes(fullPath);

        return Results.File(fileBytes, contentType, Path.GetFileName(fullPath));
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch {
            ".json" => MediaTypeNames.Application.Json,
            ".html" or ".htm" => MediaTypeNames.Text.Html,
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => MediaTypeNames.Image.Jpeg,
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".otf" => "font/otf",
            ".ico" => "image/x-icon",
            ".txt" => MediaTypeNames.Text.Plain,
            ".xml" => MediaTypeNames.Text.Xml,
            _ => MediaTypeNames.Application.Octet
        };
    }
}

