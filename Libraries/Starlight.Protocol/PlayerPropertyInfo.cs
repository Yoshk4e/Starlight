namespace Starlight.Protocol;


public static class PlayerPropertyInfo
{
    private const long UnboundedMin = long.MinValue;
    private const long UnboundedMax = long.MaxValue;

    public static bool IsSettable(PlayerProperty prop) => prop != PlayerProperty.None;

    public static bool IsDynamicRange(PlayerProperty prop)
        => prop is PlayerProperty.CurSpringVolume or PlayerProperty.CurPersistStamina;

    public static long Min(PlayerProperty prop) => (prop, IsDynamicRange(prop)) switch {
        (_, true) => 0,
        (PlayerProperty.Exp, _) => 0,
        (PlayerProperty.Level, _) => 0,
        (PlayerProperty.MaxSpringVolume, _) => 0,
        (PlayerProperty.IsSpringAutoUse, _) => 0,
        (PlayerProperty.SpringAutoUsePercent, _) => 0,
        (PlayerProperty.IsFlyable, _) => 0,
        (PlayerProperty.IsWeatherLocked, _) => 0,
        (PlayerProperty.IsGameTimeLocked, _) => 0,
        (PlayerProperty.IsTransferable, _) => 0,
        (PlayerProperty.MaxStamina, _) => 0,
        (PlayerProperty.PlayerLevel, _) => 1,
        (PlayerProperty.PlayerExp, _) => 0,
        (PlayerProperty.PlayerScoin, _) => 0,
        (PlayerProperty.PlayerMpSettingType, _) => 0,
        (PlayerProperty.IsMpModeAvailable, _) => 0,
        (PlayerProperty.PlayerWorldLevel, _) => 0,
        (PlayerProperty.PlayerResin, _) => 0,
        (PlayerProperty.IsOnlyMpWithPsPlayer, _) => 0,
        (PlayerProperty.PlayerLegendaryKey, _) => 0,
        (PlayerProperty.PlayerForgePoint, _) => 0,
        (PlayerProperty.PlayerWorldLevelLimit, _) => 0,
        (PlayerProperty.PlayerHomeCoin, _) => 0,
        (PlayerProperty.IsDiveable, _) => 0,
        (PlayerProperty.MaxDiveStamina, _) => 0,
        (PlayerProperty.CurPersistDiveStamina, _) => 0,
        _ => UnboundedMin
    };

    public static long Max(PlayerProperty prop) => prop switch {
        PlayerProperty.Level => 90,
        PlayerProperty.MaxSpringVolume => 8_500_000,
        PlayerProperty.IsSpringAutoUse => 1,
        PlayerProperty.SpringAutoUsePercent => 100,
        PlayerProperty.IsFlyable => 1,
        PlayerProperty.IsWeatherLocked => 1,
        PlayerProperty.IsGameTimeLocked => 1,
        PlayerProperty.IsTransferable => 1,
        PlayerProperty.MaxStamina => 24_000,
        PlayerProperty.PlayerLevel => 60,
        PlayerProperty.PlayerMpSettingType => 2,
        PlayerProperty.IsMpModeAvailable => 1,
        PlayerProperty.PlayerWorldLevel => 8,
        PlayerProperty.PlayerResin => 2000,
        PlayerProperty.IsOnlyMpWithPsPlayer => 1,
        PlayerProperty.PlayerForgePoint => 300_000,
        PlayerProperty.PlayerWorldLevelLimit => 8,
        PlayerProperty.IsDiveable => 1,
        PlayerProperty.MaxDiveStamina => 10_000,
        PlayerProperty.CurPersistDiveStamina => 10_000,
        _ => UnboundedMax
    };
}
