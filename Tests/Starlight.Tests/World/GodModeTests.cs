using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starlight.Common;
using Starlight.Game.Ability;
using Starlight.Game.Ability.Handlers;
using Starlight.Game.Modules;
using Starlight.Game.Player;
using Starlight.Game.Resources;
using Starlight.Game.Resources.Binary;
using Starlight.Game.Resources.Excel;
using Starlight.Game.World;
using Starlight.Protocol;
using Starlight.Protocol.V70;
using Starlight.Rpc;
using Starlight.Rpc.Tunnel;
using Xunit;
using IMessage = Starlight.Protobuf.Core.IMessage;

namespace Starlight.Tests;

public sealed class GodModeTests
{
    [Fact]
    public async Task HandleAttack_GodModeOn_DoesNotDamageAvatar()
    {
        var (player, avatar, scene) = await PlayerWithAvatarInScene(uid: 1001);

        var result = scene.HandleAttack(Attack(avatar, damage: 1000f));

        Assert.False(result.Handled);
        Assert.True(avatar.IsAlive);
    }

    [Fact]
    public async Task HandleAttack_GodModeOff_DamagesAvatar()
    {
        var (player, avatar, scene) = await PlayerWithAvatarInScene(uid: 1001);
        player.Module<PropsModule>().SetToggle(CheatToggle.GodMode, value: 0);

        var result = scene.HandleAttack(Attack(avatar, damage: 50f));
        var hp = avatar.GetFightProperty(FightProperty.FIGHT_PROP_CUR_HP);
        var maxHp = avatar.GetFightProperty(FightProperty.FIGHT_PROP_MAX_HP);

        Assert.True(result.Handled);
        Assert.True(hp < maxHp);
    }

    [Fact]
    public void PlayerByUid_ResolvesJoinedPlayersOnly()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var registry = new ModuleRegistry();
        var owner = SessionPlayer(services, registry, uid: 1001);
        var guest = SessionPlayer(services, registry, uid: 1002);

        var world = new World(owner);
        world.Join(owner);

        Assert.Same(owner, world.PlayerByUid(1001));
        Assert.Null(world.PlayerByUid(1002));

        world.Join(guest);
        Assert.Same(guest, world.PlayerByUid(1002));
    }

    [Fact]
    public async Task IsAvatarProtected_UnknownOwner_FailsSafe()
    {
        var (player, avatar, scene) = await PlayerWithAvatarInScene(uid: 1001);
        player.Module<PropsModule>().SetToggle(CheatToggle.GodMode, value: 0);
        scene.World.Leave(player);

        Assert.True(scene.World.IsAvatarProtected(avatar));
        Assert.False(scene.HandleAttack(Attack(avatar, damage: 100f)).Handled);
    }

    private static AttackResult Attack(AvatarEntity avatar, float damage)
        => new() { DefenseId = avatar.EntityId, AttackerId = 0, Damage = damage };

    private static async Task<(IPlayer Player, AvatarEntity Avatar, Scene Scene)> PlayerWithAvatarInScene(uint uid)
    {
        var (player, _) = Player(uid, Data());
        var world = player.Module<WorldModule>();
        var scene = player.Module<SceneModule>();

        await player.Module<AvatarModule>().OnLogin();
        world.EnterOwnWorld();
        _ = scene.OnEnterSceneReady(new EnterSceneReadyReq { EnterSceneToken = 1 }).ToArray();
        _ = scene.OnSceneInit(new SceneInitFinishReq { EnterSceneToken = 1 }).ToArray();

        var avatar = world.Scene!.Entities.Values.OfType<AvatarEntity>().Single();
        return (player, avatar, world.Scene!);
    }

    private static GameData Data()
    {
        var data = new GameData(new ConfigurationBuilder().Build());

        data.WeaponData[11501] = new WeaponData {
            Id = 11501,
            GadgetId = 500001,
            SkillAffix = [111]
        };

        data.AvatarData[10000005] = new AvatarData {
            Id = 10000005,
            InitialWeapon = 11501,
            SkillDepotId = 500,
            HpBase = 100,
            AttackBase = 20,
            DefenseBase = 10,
            CritChanceBase = 0.05f,
            CritDamageBase = 0.5f
        };

        data.AvatarSkillDepotData[500] = new AvatarSkillDepotData {
            Id = 500,
            Skills = [501],
            EnergySkill = 502
        };
        data.Avatars[10000005] = new AvatarConfig();

        return data;
    }

    private static StarlightPlayer SessionPlayer(IServiceProvider services, ModuleRegistry registry, uint uid)
    {
        var (_, server) = DirectTunnel.CreatePair();
        return new StarlightPlayer(services, registry, server) { Uid = uid };
    }

    private static (StarlightPlayer Player, List<IMessage> Sent) Player(uint uid, GameData data)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var registry = new ModuleRegistry();
        var guidManager = new GuidManager(serverId: 1);
        var weaponEntities = new WeaponEntityService();
        registry.AddModule<InventoryModule>((_, player) => new InventoryModule(player, guidManager, data));
        registry.AddModule<AvatarModule>((_, player) => new AvatarModule(player, data, guidManager, weaponEntities));
        registry.AddModule<TeamModule>((_, player) => new TeamModule(player));
        registry.AddModule<BornModule>((_, player) => new BornModule(player));
        registry.AddModule<PropsModule>((_, player) => new PropsModule(player));

        var worlds = new WorldManager();
        var protocol = new V70ProtocolRegistry();
        var router = new WorldAbilityRouter();
        var initializer = new AbilityInitializer(data);

        registry.AddModule<WorldModule>((_, player) => new WorldModule(player, worlds));

        registry.AddModule<AbilityModule>((_, player) => new AbilityModule(
            player,
            initializer,
            data,
            config: new AbilityRuntimeConfig(() => false),
            protocol: protocol,
            handlers: new AbilityInvokeHandlerRegistry([]),
            scopes: router,
            forwarder: router));
        registry.AddModule<SceneModule>((_, player) => new SceneModule(player, router, protocol));
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
