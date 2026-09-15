using Google.Protobuf;
using Starlight.Game.Player;
using Starlight.Protocol;
using Starlight.Rpc.Proto;
using Xunit;

namespace Starlight.Tests;

public sealed class PlayerPropsTests
{
    [Fact]
    public void Snapshot_ContainsLoginDefaults()
    {
        var snapshot = new PlayerProps().Snapshot().ToDictionary(entry => entry.Key, entry => entry.Value);

        Assert.Equal(1, snapshot[(uint)PlayerProperty.IsFlyable].Val);
        Assert.Equal(1, snapshot[(uint)PlayerProperty.IsTransferable].Val);
        Assert.Equal(1, snapshot[(uint)PlayerProperty.IsDiveable].Val);
        Assert.Equal(60, snapshot[(uint)PlayerProperty.Level].Val);
        Assert.Equal(60, snapshot[(uint)PlayerProperty.PlayerLevel].Val);
        Assert.Equal(0, snapshot[(uint)PlayerProperty.PlayerExp].Val);
        Assert.Equal(24_000, snapshot[(uint)PlayerProperty.MaxStamina].Val);
        Assert.Equal(24_000, snapshot[(uint)PlayerProperty.CurPersistStamina].Val);
        Assert.Equal(1, snapshot[(uint)PlayerProperty.PlayerWorldLevel].Val);
    }

    [Fact]
    public void TrySet_AcceptsValueInsideStaticRange()
    {
        var props = new PlayerProps();

        Assert.Null(props.TrySet(PlayerProperty.PlayerWorldLevel, 5));
        Assert.Equal(5, props.Get(PlayerProperty.PlayerWorldLevel));
    }

    [Theory]
    [InlineData(PlayerProperty.PlayerWorldLevel, 9)]
    [InlineData(PlayerProperty.PlayerWorldLevel, -1)]
    [InlineData(PlayerProperty.PlayerLevel, 61)]
    [InlineData(PlayerProperty.PlayerLevel, 0)]
    [InlineData(PlayerProperty.MaxStamina, 24_001)]
    [InlineData(PlayerProperty.PlayerResin, 2001)]
    public void TrySet_RejectsValueOutsideStaticRange(PlayerProperty prop, long value)
    {
        var error = new PlayerProps().TrySet(prop, value);

        Assert.NotNull(error);
        Assert.Contains(prop.ToString(), error);
    }

    [Fact]
    public void TrySet_StaminaIsBoundedByCurrentMaxStamina()
    {
        var props = new PlayerProps();

        Assert.Null(props.TrySet(PlayerProperty.MaxStamina, 10_000));
        Assert.Null(props.TrySet(PlayerProperty.CurPersistStamina, 10_000));
        Assert.NotNull(props.TrySet(PlayerProperty.CurPersistStamina, 10_001));
    }

    [Fact]
    public void TrySet_NonePropertyIsRejected()
        => Assert.NotNull(new PlayerProps().TrySet(PlayerProperty.None, 1));

    [Fact]
    public void RoundTrip_SurvivesStateClone()
    {
        var state = new NetPlayerState();
        var props = PlayerProps.FromState(state);

        Assert.Null(props.TrySet(PlayerProperty.PlayerWorldLevel, 5));
        Assert.Null(props.TrySet(PlayerProperty.PlayerScoin, 12_345));
        props.WriteTo(state);

        var cloned = NetPlayerState.Parser.ParseFrom(state.ToByteArray());
        var restored = PlayerProps.FromState(cloned);

        Assert.Equal(5, restored.Get(PlayerProperty.PlayerWorldLevel));
        Assert.Equal(12_345, restored.Get(PlayerProperty.PlayerScoin));
        Assert.Equal(60, restored.Get(PlayerProperty.PlayerLevel));
    }

    [Fact]
    public void RoundTrip_OverwritesStaleExplicitValues()
    {
        var state = new NetPlayerState();
        var props = PlayerProps.FromState(state);

        Assert.Null(props.TrySet(PlayerProperty.PlayerWorldLevel, 5));
        props.WriteTo(state);
        Assert.Null(props.TrySet(PlayerProperty.PlayerWorldLevel, 7));
        props.WriteTo(state);

        var restored = PlayerProps.FromState(NetPlayerState.Parser.ParseFrom(state.ToByteArray()));

        Assert.Equal(7, restored.Get(PlayerProperty.PlayerWorldLevel));
        Assert.Single(restored.Explicit);
    }

    [Fact]
    public void Cheats_GodModeDefaultsOnAndSurvivesRoundTrip()
    {
        var cheats = new CheatToggles();

        Assert.True(cheats.GodMode);

        var state = new NetPlayerState();
        cheats.WriteTo(state);

        var legacy = new CheatToggles();
        legacy.Load(new NetPlayerState());
        Assert.True(legacy.GodMode);

        var restored = new CheatToggles();
        restored.Load(NetPlayerState.Parser.ParseFrom(state.ToByteArray()));
        Assert.True(restored.GodMode);
    }

    [Fact]
    public void Cheats_DisablingGodModeSurvivesRoundTrip()
    {
        var state = new NetPlayerState();
        var cheats = new CheatToggles();

        cheats.Set(CheatToggle.GodMode, false);
        cheats.WriteTo(state);

        var restored = new CheatToggles();
        restored.Load(NetPlayerState.Parser.ParseFrom(state.ToByteArray()));

        Assert.False(restored.GodMode);
    }

    [Fact]
    public void Cheats_ChangedFiresOnlyOnActualChange()
    {
        var cheats = new CheatToggles();
        var changes = 0;
        cheats.Changed += () => changes++;

        cheats.Set(CheatToggle.GodMode, true);
        Assert.Equal(0, changes);

        cheats.Set(CheatToggle.GodMode, false);
        Assert.Equal(1, changes);
    }
}
