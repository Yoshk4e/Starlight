using Starlight.SDK.Common;

namespace Starlight.SDK.Http;

public sealed class ApiResponse
{
    public int Retcode { get; init; }
    public string Message { get; init; } = string.Empty;
    public object? Data { get; init; }

    /// <summary>Build a response from a <see cref="Common.Retcode"/> using the message table.</summary>
    public static ApiResponse From(Retcode code, object? data = null) => new() {
        Retcode = (int)code,
        Message = RetcodeMessages.Get(code),
        Data = data
    };

    /// <summary>Build a response with an explicit override message.</summary>
    public static ApiResponse From(Retcode code, string message, object? data = null) => new() {
        Retcode = (int)code,
        Message = message,
        Data = data
    };

    public static ApiResponse Ok(object? data = null) => From(Common.Retcode.Success, data);
}
