/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Server.Actions;
using Content.Shared._Grosse.ZLevels.Flight;
using Content.Shared._Grosse.ZLevels.Flight.Components;
using Robust.Shared.Analyzers;

namespace Content.Server._Grosse.ZLevels.Flight;

public sealed partial class GrosseZFlightSystem : GrosseSharedZFlightSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseControllableFlightComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<GrosseControllableFlightComponent, MapInitEvent>(OnMapInit);
    }
    [Dependency] private ActionsSystem _actions = default!;
    private void OnRemove(Entity<GrosseControllableFlightComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelToggleActionEntity);
    }
    private void OnMapInit(Entity<GrosseControllableFlightComponent> ent, ref MapInitEvent args)
    {
        if (!ZPhyzQuery.TryComp(ent, out var zPhys))
            return;

        if (!TryComp<GrosseZFlyerComponent>(ent.Owner, out var flyerComp))
            return;

        SetTargetHeight(ent.Owner, zPhys.CurrentZLevel);

        _actions.AddAction(ent, ref ent.Comp.ZLevelUpActionEntity, ent.Comp.UpActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelDownActionEntity, ent.Comp.DownActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelToggleActionEntity, ent.Comp.ToggleActionProto);

        _actions.SetEnabled(ent.Comp.ZLevelDownActionEntity, flyerComp.Active);
        _actions.SetEnabled(ent.Comp.ZLevelUpActionEntity, flyerComp.Active);
    }
}
