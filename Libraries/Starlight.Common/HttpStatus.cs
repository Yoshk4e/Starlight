namespace Starlight.Common;

/// <summary>
/// HTTP-style status codes used as numeric markers inside SDK response
/// payloads. These appear as inline literals in
/// <see cref="SdkComboBoxConfig.NetworkStatusCodes"/> defaults and in
/// the device-fingerprint endpoint's response code.
/// </summary>
public static class HttpStatus
{
    /// <summary>OK.</summary>
    public const int Ok = 200;

    /// <summary>Forbidden.</summary>
    public const int Forbidden = 403;

    /// <summary>Not Found.</summary>
    public const int NotFound = 404;

    /// <summary>Too Many Requests.</summary>
    public const int TooManyRequests = 429;
}
