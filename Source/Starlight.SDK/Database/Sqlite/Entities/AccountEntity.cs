using Starlight.Database.Attributes;
using Starlight.Database.ChangeTracking;

namespace Starlight.SDK.Database.Impl.Entities;

[DbTable("accounts")]
[DbIndex("ix_player_accounts_id", nameof(Id), IsUnique = true)]
[DbIndex("ix_player_accounts_username", nameof(Username), IsUnique = true)]
[DbIndex("ix_player_accounts_session_token", nameof(SessionToken))]
public sealed class AccountEntity : TrackableEntity
{
    [DbPrimaryKey(AutoIncrement = true)]
    [DbColumn("id", IsRequired = true, IsUnique = true)]
    public uint Id
    {
        get;
        private set => Set(ref field, value);
    }

    [DbColumn("username", IsRequired = true, MaxLength = 64)]
    public string Username
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    [DbColumn("email", MaxLength = 320)]
    public string? Email
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>Lowercase SHA-256 hex digest, 64 chars.</summary>
    [DbColumn("password", IsRequired = true, MaxLength = 320)]
    public string Password
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    [DbColumn("session_token", MaxLength = 64)]
    public string? SessionToken
    {
        get;
        set => Set(ref field, value);
    }

    [DbColumn("combo_token", MaxLength = 64)]
    public string? ComboToken
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>
    /// Pipe-delimited list of known device ids (see <see cref="Starlight.SDK.Database.Models.Account.KnownDeviceIds"/>).
    /// </summary>
    [DbColumn("known_device_ids", MaxLength = 768)]
    public string? KnownDeviceIds
    {
        get;
        set => Set(ref field, value);
    }

    [DbColumn("created_at", IsRequired = true)]
    public DateTimeOffset CreatedAt
    {
        get;
        private set => Set(ref field, value);
    } = DateTimeOffset.UtcNow;

    [DbColumn("updated_at", IsRequired = true)]
    public DateTimeOffset UpdatedAt
    {
        get;
        set => Set(ref field, value);
    } = DateTimeOffset.UtcNow;
}
