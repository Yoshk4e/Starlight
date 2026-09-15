using Microsoft.Extensions.DependencyInjection;
using Starlight.Commands;
using Starlight.Game.Modules;
using Starlight.Game.Player;
using Starlight.Game.Resources;
using Starlight.Game.World;
using Starlight.Protocol;
using Starlight.Rpc;
using Starlight.Rpc.Proto;
using Starlight.Rpc.Tunnel;
using Xunit;
using IMessage = Starlight.Protobuf.Core.IMessage;

namespace Starlight.Tests;

public sealed class PropCommandTests
{
    [Fact]
    public async Task Execute_PlayerContext_SetsWorldLevelAndNotifies()
    {
        var players = new PlayerManager();
        var (player, sent) = Player(uid: 1001);
        Assert.True(players.Add(player));

        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player), ["wl", "5"]);

        var notify = Assert.IsType<PlayerPropNotify>(Assert.Single(sent.OfType<PlayerPropNotify>()));
        Assert.Equal(5, notify.PropMap[(uint)PlayerProperty.PlayerWorldLevel].Val);
        Assert.Equal(5, player.Module<PropsModule>().Props.Get(PlayerProperty.PlayerWorldLevel));

        var change = Assert.IsType<PlayerPropChangeNotify>(Assert.Single(sent.OfType<PlayerPropChangeNotify>()));
        Assert.Equal((uint)PlayerProperty.PlayerWorldLevel, change.PropType);
        Assert.Equal(4u, change.PropDelta);
    }

    [Fact]
    public async Task Execute_ConsoleWithUid_TargetsOnlinePlayer()
    {
        var players = new PlayerManager();
        var (player, sent) = Player(uid: 1001);
        Assert.True(players.Add(player));

        var output = new RecordingOutput();
        await new PropCommand(players).ExecuteAsync(
            new CommandContext(CommandSource.Console, output, CancellationToken.None), ["1001", "wl", "3"]);

        Assert.Contains(output.Messages, message => message.Message.Contains("set to 3"));
        Assert.Equal(3, player.Module<PropsModule>().Props.Get(PlayerProperty.PlayerWorldLevel));
        Assert.Single(sent.OfType<PlayerPropNotify>());
    }

    [Fact]
    public async Task Execute_UnknownUid_WarnsPlayerNotOnline()
    {
        var output = new RecordingOutput();
        await new PropCommand(new PlayerManager()).ExecuteAsync(
            new CommandContext(CommandSource.Console, output, CancellationToken.None), ["1001", "wl", "3"]);

        var message = Assert.Single(output.Messages);
        Assert.Equal(CommandOutputLevel.Warning, message.Level);
        Assert.Contains("not online", message.Message);
    }

    [Fact]
    public async Task Execute_ValueOutOfRange_WarnsWithRange()
    {
        var players = new PlayerManager();
        var (player, _) = Player(uid: 1001);
        Assert.True(players.Add(player));

        var output = new RecordingOutput();
        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player, output), ["wl", "99"]);

        var message = Assert.Single(output.Messages);
        Assert.Equal(CommandOutputLevel.Warning, message.Level);
        Assert.Contains("between 0 and 8", message.Message);
        Assert.Equal(1, player.Module<PropsModule>().Props.Get(PlayerProperty.PlayerWorldLevel));
    }

    [Theory]
    [InlineData("god", "off", false)]
    [InlineData("god", "toggle", false)]
    [InlineData("godmode", "on", true)]
    public async Task Execute_ToggleAliases_PersistToState(string alias, string value, bool expected)
    {
        var players = new PlayerManager();
        var (player, _) = Player(uid: 1001);
        Assert.True(players.Add(player));

        player.Module<PropsModule>().SetToggle(CheatToggle.GodMode, 1);

        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player), [alias, value]);

        Assert.NotNull(player.State.Cheats);
        Assert.Equal(expected, player.State.Cheats.GodMode);
    }

    [Theory]
    [InlineData("ns", "StaminaManager")]
    [InlineData("ue", "EnergyManager")]
    public async Task Execute_ManagerlessToggles_WarnNotImplemented(string alias, string manager)
    {
        var players = new PlayerManager();
        var (player, _) = Player(uid: 1001);
        Assert.True(players.Add(player));

        var output = new RecordingOutput();
        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player, output), [alias, "on"]);

        var message = Assert.Single(output.Messages);
        Assert.Equal(CommandOutputLevel.Warning, message.Level);
        Assert.Contains("not implemented", message.Message);
        Assert.Contains(manager, message.Message);
        Assert.Null(player.State.Cheats); // the toggle was never flipped
    }

    [Fact]
    public async Task Execute_RawPropertyName_SetsProp()
    {
        var players = new PlayerManager();
        var (player, _) = Player(uid: 1001);
        Assert.True(players.Add(player));

        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player), ["playerresin", "160"]);

        Assert.Equal(160, player.Module<PropsModule>().Props.Get(PlayerProperty.PlayerResin));
    }

    [Fact]
    public async Task Execute_FlyAlias_SetsIsFlyable()
    {
        var players = new PlayerManager();
        var (player, sent) = Player(uid: 1001);
        Assert.True(players.Add(player));

        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player), ["fly", "off"]);

        Assert.Equal(0, player.Module<PropsModule>().Props.Get(PlayerProperty.IsFlyable));
        Assert.Single(sent.OfType<PlayerPropNotify>());
    }

    [Fact]
    public async Task Execute_DiveAlias_SetsDiveProps()
    {
        var players = new PlayerManager();
        var (player, _) = Player(uid: 1001);
        Assert.True(players.Add(player));

        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player), ["dive", "on"]);

        var props = player.Module<PropsModule>().Props;
        Assert.Equal(1, props.Get(PlayerProperty.IsDiveable));
        Assert.Equal(10_000, props.Get(PlayerProperty.MaxDiveStamina));
        Assert.Equal(10_000, props.Get(PlayerProperty.CurPersistDiveStamina));
    }

    [Fact]
    public async Task Execute_GarbageValue_Warns()
    {
        var players = new PlayerManager();
        var (player, _) = Player(uid: 1001);
        Assert.True(players.Add(player));

        var output = new RecordingOutput();
        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player, output), ["wl", "high"]);

        var message = Assert.Single(output.Messages);
        Assert.Equal(CommandOutputLevel.Warning, message.Level);
        Assert.Contains("not a valid value", message.Message);
    }

    [Fact]
    public async Task Execute_UnknownProp_WarnsWithAdvertisedNames()
    {
        var players = new PlayerManager();
        var (player, _) = Player(uid: 1001);
        Assert.True(players.Add(player));

        var output = new RecordingOutput();
        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player, output), ["banana", "1"]);

        var message = Assert.Single(output.Messages);
        Assert.Equal(CommandOutputLevel.Warning, message.Level);
        Assert.Contains("god", message.Message);
    }

    [Fact]
    public async Task Execute_NoArgs_ReportsStatus()
    {
        var players = new PlayerManager();
        var (player, _) = Player(uid: 1001);
        Assert.True(players.Add(player));

        var output = new RecordingOutput();
        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player, output), []);

        var message = Assert.Single(output.Messages);
        Assert.Contains("godmode on", message.Message);
        Assert.Contains("world level 1", message.Message);
    }

    [Fact]
    public async Task Execute_UnlockMapAlias_UnlocksAllPoints()
    {
        var players = new PlayerManager();
        var data = new GameData(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        data.ScenePoints[3] = new Dictionary<uint, Starlight.Game.Resources.Binary.PointData> {
            [1] = new() { PointId = 1, SceneId = 3, AreaId = 1 },
            [2] = new() { PointId = 2, SceneId = 3, AreaId = 1, ForbidSimpleUnlock = true }
        };

        var (player, _) = Player(uid: 1001, data);
        Assert.True(players.Add(player));

        var output = new RecordingOutput();
        await new PropCommand(players).ExecuteAsync(Context(CommandSource.Player, player, output), ["um", "all"]);

        var message = Assert.Single(output.Messages);
        Assert.Contains("Unlocked 2 map points", message.Message);
        Assert.Equal([1u, 2u], player.State.SceneUnlocks[3].UnlockedPoints.Order());
    }

    private static CommandContext Context(
        CommandSource source,
        IPlayer player,
        ICommandOutput? output = null)
        => new(source, output ?? NullCommandOutput.Instance, CancellationToken.None, player, player);

    private static (StarlightPlayer Player, List<IMessage> Sent) Player(uint uid, GameData? data = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var registry = new ModuleRegistry();
        registry.AddModule<WorldModule>((_, player) => new WorldModule(player, new WorldManager()));
        registry.AddModule<MapModule>((_, player) => new MapModule(player, data));
        registry.AddModule<PropsModule>((_, player) => new PropsModule(player));
        registry.Build();

        var (client, server) = DirectTunnel.CreatePair();
        var sent = new List<IMessage>();

        _ = client.Subscribe(GameSubjects.OutboundPacket, message => {
            sent.Add(message.Decode<IMessage>());
            return Task.CompletedTask;
        });

        return (new StarlightPlayer(services, registry, server) { Uid = uid }, sent);
    }

    private sealed class RecordingOutput : ICommandOutput
    {
        public List<(CommandOutputLevel Level, string Message)> Messages { get; } = [];

        public ValueTask WriteAsync(CommandOutputLevel level, string message, CancellationToken cancellationToken)
        {
            Messages.Add((level, message));
            return ValueTask.CompletedTask;
        }
    }
}
