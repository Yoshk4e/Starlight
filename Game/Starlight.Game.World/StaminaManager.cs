using Starlight.Game.Modules;
using Starlight.Game.Player;
using Starlight.Protocol;

namespace Starlight.Game.World;


public sealed class StaminaManager(IPlayer player) : IModule
{
    // TODO: Track the current avatar's motion state from SceneModule's entity-move
    //       handling
    // TODO: Consume CurPersistStamina (player prop 10011) on consumption ticks and
    //       restore it on idle ticks, broadcasting PlayerPropNotify with the new value.
    // TODO: When consumption would hit 0, notify the client so it stops the movement
    // TODO: Add an UnlimitedStamina CheatToggle (PropsModule.SetToggle) that skips
    //       consumption, and the "ns" alias in PropCommand to it once this
    //       consumes for real.
}
