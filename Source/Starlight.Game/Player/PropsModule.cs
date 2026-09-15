using Starlight.Game.Modules;
using Starlight.Protocol;

namespace Starlight.Game.Player;

public sealed class PropsModule(IPlayer player) : IModule
{
    private PlayerProps? _props;

    public PlayerProps Props
    {
        get
        {
            if (_props is not null)
                return _props;

            lock (player.StateLock)
            {
                if (_props is not null)
                    return _props;

                Cheats.Load(player.State);
                _props = PlayerProps.FromState(player.State);
            }

            return _props;
        }
    }

    public CheatToggles Cheats { get; } = new();

    public async Task<string?> SetPropAsync(PlayerProperty prop, long value)
    {
        long previous;

        lock (player.StateLock)
        {
            previous = Props.Get(prop);

            var error = Props.TrySet(prop, value);

            if (error is not null)
                return error;

            Props.WriteTo(player.State);
        }

        if (ReasonFor(prop) is {} reason)
        {
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

        lock (player.StateLock)
        {
            enabled = value switch {
                -1 => !Cheats.Get(toggle),
                0 => false,
                _ => true
            };

            Cheats.Set(toggle, enabled);
            Cheats.WriteTo(player.State);
        }

        return enabled;
    }
}
