using System.Text.Json.Serialization;

namespace Starlight.SDK.Http.Models;

public sealed class ComboGranterLoginRequest
{
    [JsonPropertyName("app_id")]
    public int? AppId { get; set; }

    [JsonPropertyName("channel_id")]
    public int? ChannelId { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("device")]
    public string? Device { get; set; }

    [JsonPropertyName("sign")]
    public string? Sign { get; set; }
}

/// <summary>
/// Inner payload encoded inside <c>ComboGranterLoginRequest.data</c>.
/// </summary>
public sealed class ComboLoginV2Data
{
    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("guest")]
    public bool? Guest { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
