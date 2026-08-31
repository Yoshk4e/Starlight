using Starlight.Game.Player;
using Starlight.Protocol;

namespace Starlight.Game.World;

public sealed record AvatarEntity(SceneEntityInfo Info, uint WeaponEntityId)
{
    private const uint Alive = 1;

    public uint EntityId => Info.EntityId;

    /// <summary>Spawns <paramref name="avatar"/> into <paramref name="world"/> standing at <paramref name="position"/>.</summary>
    public static AvatarEntity Create(
        World world,
        uint uid,
        uint peerId,
        Avatar avatar,
        Vector position,
        Vector? rotation = null,
        Vector? refPos = null
    )
    {
        var weaponEntityId = world.NextEntityId(ProtEntityType.PROT_ENTITY_TYPE_WEAPON);

        var sceneAvatar = new SceneAvatarInfo {
            Uid = uid,
            AvatarId = avatar.AvatarId,
            Guid = avatar.Guid,
            PeerId = peerId,
            SkillDepotId = avatar.SkillDepotId,
            BornTime = avatar.BornTime,
            WearingFlycloakId = Avatar.DefaultFlycloak,
            EquipIdList = [avatar.WeaponItemId],
            Weapon = new SceneWeaponInfo {
                EntityId = weaponEntityId,
                GadgetId = avatar.WeaponGadgetId,
                ItemId = avatar.WeaponItemId,
                Guid = avatar.WeaponGuid,
                Level = 1
            }
        };

        foreach (var skill in avatar.Skills)
        {
            sceneAvatar.SkillLevelMap[skill] = 1;
        }

        var info = new SceneEntityInfo {
            EntityType = ProtEntityType.PROT_ENTITY_TYPE_AVATAR,
            EntityId = world.NextEntityId(ProtEntityType.PROT_ENTITY_TYPE_AVATAR),
            LifeState = Alive,
            MotionInfo = new MotionInfo {
                Pos = position,
                Rot = rotation ?? new Vector(),
                Speed = new Vector(),
                RefPos = refPos ?? new Vector(),
                State = MotionState.MOTION_STATE_STANDBY
            },
            EntityClientData = new EntityClientData(),
            EntityAuthorityInfo = new EntityAuthorityInfo {
                AbilityInfo = new AbilitySyncStateInfo(),
                BornPos = new Vector(),
                ClientExtraInfo = new EntityClientExtraInfo { SkillAnchorPosition = new Vector() }
            },
            PropList = [
                new PropPair { Type = (uint)PlayerProperty.Level, PropValue = PlayerProperty.Level.Value(1) }
            ],
            Avatar = sceneAvatar
        };

        foreach (var (prop, value) in avatar.FightProps)
        {
            info.FightPropList.Add(new FightPropPair { PropType = prop, PropValue = value });
        }

        return new AvatarEntity(info, weaponEntityId);
    }
}
