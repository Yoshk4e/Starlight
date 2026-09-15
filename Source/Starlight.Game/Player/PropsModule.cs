using Starlight.Game.Modules;
using Starlight.Protocol;
using Starlight.Rpc.Proto;

namespace Starlight.Game.Player;

public sealed class PropsModule(IPlayer player) : IModule
{
    private PlayerProps? _props;
    private readonly CheatToggles _cheats = new();

    public PlayerProps Props
    {
        get {
            if (_props is not null)
                return _props;

            lock (player.StateLock) {
                if (_props is not null)
                    return _props;

                _cheats.Load(player.State);
                _props = PlayerProps.FromState(player.State);
            }

            return _props;
        }
    }

    public CheatToggles Cheats => _cheats;

    public async Task<string?> SetPropAsync(PlayerProperty prop, long value)
    {
        long previous;

        lock (player.StateLock) {
            previous = Props.Get(prop);

            var error = Props.TrySet(prop, value);

            if (error is not null)
                return error;

            Props.WriteTo(player.State);
        }

        if (ReasonFor(prop) is { } reason) {
            await player.Send(new PlayerPropChangeReasonNotify {
                PropType = (uint)prop,
                Reason = reason,
                OldValue = previous,
                CurValue = value
            });
        }

        await player.Send(new PlayerPropNotify {
            PropMap = { [(uint)prop] = prop.Value(value) }
        });

        await player.Send(new PlayerPropChangeNotify {
            PropType = (uint)prop,
            PropDelta = (uint)(value - previous)
        });

        return null;
    }

    private static PropChangeReason? ReasonFor(PlayerProperty prop)
        => prop switch {
            PlayerProperty.PlayerExp => PropChangeReason.PROP_CHANGE_REASON_PLAYER_ADD_EXP,
            PlayerProperty.PlayerLevel => PropChangeReason.PROP_CHANGE_REASON_LEVELUP,
            PlayerProperty.MaxStamina => PropChangeReason.PROP_CHANGE_REASON_CITY_LEVELUP,
            _ => null
        };

    public bool SetToggle(CheatToggle toggle, int value)
    {
        bool enabled;

        lock (player.StateLock) {
            enabled = value switch {
                -1 => !_cheats.Get(toggle),
                0 => false,
                _ => true
            };

            _cheats.Set(toggle, enabled);
            _cheats.WriteTo(player.State);
        }

        return enabled;
    }
}
