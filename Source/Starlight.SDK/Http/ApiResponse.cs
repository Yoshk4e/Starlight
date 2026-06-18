using Starlight.SDK.Common;

namespace Starlight.SDK.Http;

public sealed class ApiResponse
{
    public int retcode { get; init; }
    public string message { get; init; } = string.Empty;
    public object? data { get; init; }

    /// <summary>Build a response from a <see cref="Retcode"/> using the message table.</summary>
    public static ApiResponse From(Retcode code, object? data = null) => new() {
        retcode = (int)code,
        message = RetcodeMessages.Get(code),
        data = data
    };

    /// <summary>Build a response with an explicit override message.</summary>
    public static ApiResponse From(Retcode code, string message, object? data = null) => new() {
        retcode = (int)code,
        message = message,
        data = data
    };

    public static ApiResponse Ok(object? data = null) => From(Retcode.Success, data);
}
