using Starlight.Rpc.Proto;

namespace Starlight.Game.Player;
public enum CheatToggle
{
    GodMode
}

public sealed class CheatToggles
{
    public bool GodMode { get; private set; } = true;

    public event Action? Changed;

    // TODO: UnlimitedStamina, shelved until Starlight.Game.World.StaminaManager exists.
    // TODO: UnlimitedEnergy, shelved until an EnergyManager exists.

    public bool Get(CheatToggle toggle)
        => toggle switch {
            CheatToggle.GodMode => GodMode,
            _ => false
        };

    public void Set(CheatToggle toggle, bool value)
    {
        if (Get(toggle) == value)
            return;

        if (toggle == CheatToggle.GodMode)
            GodMode = value;

        Changed?.Invoke();
    }

    public void Load(NetPlayerState state)
    {
        if (state.Cheats is not { } cheats)
            return;

        if (cheats.HasGodMode)
            GodMode = cheats.GodMode;
    }

    public void WriteTo(NetPlayerState state)
    {
        state.Cheats = new NetCheatToggles {
            GodMode = GodMode
        };
    }
}
