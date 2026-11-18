using Content.Shared._Grosse.CommanderTerminal;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Utility;

namespace Content.Client._Grosse.CommanderTerminal;

/// <summary>
/// Adds the squad mark above a member when the local player is wearing a GEO mask.
/// </summary>
public sealed partial class CommanderGroupIconSystem : EntitySystem
{
    private static readonly SpriteSpecifier MemberIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_circle.png"));

    private static readonly SpriteSpecifier LeaderIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_star.png"));

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedCommanderGroupSystem _groups = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommanderGroupMemberComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<CommanderGroupMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        var viewer = _player.LocalSession?.AttachedEntity;
        if (viewer == null || !_groups.WearsGroupMask(viewer.Value))
            return;

        args.StatusIcons.Add(new StatusIconData
        {
            Icon = ent.Comp.IsLeader ? LeaderIcon : MemberIcon,
            Priority = 11,
            LocationPreference = StatusIconLocationPreference.Right,
            Modulate = CommanderGroupColors.ForIndex(ent.Comp.ColorIndex),
        });
    }
}
