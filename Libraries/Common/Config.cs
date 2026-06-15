using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Common.Config;

public static class Config
{
    public static string LogLevel { get; private set; } = "Information";
    public static ServerConfig Server { get; private set; } = new();

    public static void Load(string path = "config.json")
    {
        if (!File.Exists(path))
        {
            WriteDefaultConfig(path);
        }

        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(path, optional: false, reloadOnChange: true)
            .AddEnvironmentVariables("SL__")
            .Build()
            .Get<StarlightConfig>() ?? new();

        LogLevel = cfg.LogLevel;
        Server = cfg.Server;
    }

    private static void WriteDefaultConfig(string path)
    {
        var defaults = new StarlightConfig();

        var json = JsonSerializer.Serialize(
            defaults,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);
    }
}

public sealed class StarlightConfig
{
    public string LogLevel { get; set; } = "Information";
    public ServerConfig Server { get; set; } = new();
}

public sealed class ServerConfig
{
    public HttpConfig Http { get; set; } = new();
}

public sealed class HttpConfig
{
    public string BindAddress { get; set; } = "0.0.0.0";
    public int BindPort { get; set; } = 8080;
}
