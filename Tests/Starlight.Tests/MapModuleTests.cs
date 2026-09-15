using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starlight.Game.Modules;
using Starlight.Game.Player;
using Starlight.Game.Resources;
using Starlight.Game.Resources.Binary;
using Starlight.Game.World;
using Starlight.Protocol;
using Starlight.Rpc;
using Starlight.Rpc.Proto;
using Starlight.Rpc.Tunnel;
using Xunit;
using IMessage = Starlight.Protobuf.Core.IMessage;

namespace Starlight.Tests;

public sealed class MapModuleTests
{
    [Fact]
    public async Task UnlockAll_UnfilteredValue_UnlocksEveryPointAndAllAreas()
    {
        var data = Data();
        var (player, sent) = Player(uid: 1001, data);

        var added = await player.Module<MapModule>().UnlockAllAsync(value: -2);

        Assert.Equal(3, added);
        var unlocks = player.State.SceneUnlocks[3];
        Assert.Equal([1u, 2u, 3u], unlocks.UnlockedPoints.Order());
        Assert.Equal(999, unlocks.UnlockedAreas.Count);
        Assert.Equal(1u, unlocks.UnlockedAreas[0]);
        Assert.Equal(999u, unlocks.UnlockedAreas[^1]);

        // The player is not in a scene, so no unlock notify should go out yet.
        Assert.Empty(sent.OfType<ScenePointUnlockNotify>());
    }

    [Fact]
    public async Task UnlockAll_FilteredValue_SkipsForbiddenPoints()
    {
        var data = Data();
        var (player, _) = Player(uid: 1001, data);

        var added = await player.Module<MapModule>().UnlockAllAsync(value: 1);

        Assert.Equal(2, added);
        Assert.Equal([1u, 3u], player.State.SceneUnlocks[3].UnlockedPoints.Order());
    }

    [Fact]
    public async Task UnlockAll_SecondRun_AddsNothingNew()
    {
        var data = Data();
        var (player, _) = Player(uid: 1001, data);
        var map = player.Module<MapModule>();

        await map.UnlockAllAsync(value: -2);
        var added = await map.UnlockAllAsync(value: -2);

        Assert.Equal(0, added);
    }

    [Fact]
    public async Task OnGetScenePoint_ServesUnlockedListsAndGcAreaQuirk()
    {
        var data = Data();
        var (player, _) = Player(uid: 1001, data);
        await player.Module<MapModule>().UnlockAllAsync(value: -2);

        var response = player.Module<MapModule>().OnGetScenePoint(
            new GetScenePointReq { SceneId = 3, BelongUid = 1001, IsRelogin = false });

        Assert.Equal(3u, response.SceneId);
        Assert.Equal(1001u, response.BelongUid);
        Assert.Equal([1u, 2u, 3u], response.UnlockedPointList.Order());
        Assert.Equal(response.UnlockedPointList.Order(), response.UnhidePointList.Order());

        Assert.Equal([1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u], response.UnlockAreaList.Order());
    }

    [Fact]
    public async Task OnGetScenePoint_UnknownScene_StillServesGcAreaQuirk()
    {
        var (player, _) = Player(uid: 1001, Data());

        var response = player.Module<MapModule>().OnGetScenePoint(
            new GetScenePointReq { SceneId = 999 });

        Assert.Empty(response.UnlockedPointList);
        Assert.Empty(response.UnhidePointList);
        Assert.Equal([1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u], response.UnlockAreaList.Order());
    }

    [Fact]
    public async Task OnGetSceneArea_ServesUnlockedAreas()
    {
        var data = Data();
        var (player, _) = Player(uid: 1001, data);
        await player.Module<MapModule>().UnlockAllAsync(value: -2);

        var response = player.Module<MapModule>().OnGetSceneArea(new GetSceneAreaReq { SceneId = 3 });

        Assert.Equal(3u, response.SceneId);
        Assert.Equal(999, response.AreaIdList.Count);
    }

    [Fact]
    public async Task Unlocks_SurviveStateClone()
    {
        var data = Data();
        var (player, _) = Player(uid: 1001, data);
        await player.Module<MapModule>().UnlockAllAsync(value: 1);

        var cloned = NetPlayerState.Parser.ParseFrom(player.State.ToByteArray());

        Assert.Equal([1u, 3u], cloned.SceneUnlocks[3].UnlockedPoints.Order());
        Assert.Contains(500u, cloned.SceneUnlocks[3].UnlockedAreas);
    }

    private static GameData Data()
    {
        var data = new GameData(new ConfigurationBuilder().Build());

        data.ScenePoints[3] = new Dictionary<uint, PointData> {
            [1] = new PointData { PointId = 1, SceneId = 3, AreaId = 1 },
            [2] = new PointData { PointId = 2, SceneId = 3, AreaId = 1, ForbidSimpleUnlock = true },
            [3] = new PointData { PointId = 3, SceneId = 3, AreaId = 2 }
        };

        return data;
    }

    private static (StarlightPlayer Player, List<IMessage> Sent) Player(uint uid, GameData data)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var registry = new ModuleRegistry();
        registry.AddModule<WorldModule>((_, player) => new WorldModule(player, new WorldManager()));
        registry.AddModule<MapModule>((_, player) => new MapModule(player, data));
        registry.Build();

        var (client, server) = DirectTunnel.CreatePair();
        var sent = new List<IMessage>();

        _ = client.Subscribe(GameSubjects.OutboundPacket, message => {
            sent.Add(message.Decode<IMessage>());
            return Task.CompletedTask;
        });

        return (new StarlightPlayer(services, registry, server) { Uid = uid }, sent);
    }
}
