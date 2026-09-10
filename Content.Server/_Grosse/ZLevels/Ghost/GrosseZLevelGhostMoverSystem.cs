/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared._Grosse.ZLevels.Ghost;
using Content.Shared.Actions;
using Robust.Shared.Analyzers;

namespace Content.Server._Grosse.ZLevels.Ghost;

public sealed partial class GrosseZLevelGhostMoverSystem : GrosseSharedZLevelGhostMoverSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseZLevelGhostMoverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GrosseZLevelGhostMoverComponent, ComponentRemove>(OnRemove);
    }
    [Dependency] private SharedActionsSystem _actions = default!;
    private void OnMapInit(Entity<GrosseZLevelGhostMoverComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ZLevelUpActionEntity, ent.Comp.UpActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelDownActionEntity, ent.Comp.DownActionProto);
    }
    private void OnRemove(Entity<GrosseZLevelGhostMoverComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
    }
}
