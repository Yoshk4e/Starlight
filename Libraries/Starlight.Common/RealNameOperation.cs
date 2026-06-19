namespace Starlight.Common;

public static class RealNameOperations
{
    /// <summary>
    /// No real-name flow is pending for the account. Default value
    /// reported when the account has already completed verification or
    /// has never been flagged.
    /// </summary>
    public const string None = "None";

    /// <summary>
    /// Account must still complete real-name verification. Reported in
    /// the response of <c>shield/login</c> and
    /// <c>ma-passport/appLoginByPassword</c> when
    /// <see cref="Starlight.SDK.Database.Models.Account.RequireRealPerson"/>
    /// is set.
    /// </summary>
    public const string BindRealname = "bindRealname";
}
