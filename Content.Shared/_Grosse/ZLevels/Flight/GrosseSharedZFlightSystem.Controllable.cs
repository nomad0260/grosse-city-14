/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared._Grosse.ZLevels.Flight.Components;
using Content.Shared.DoAfter;
using Content.Shared.Toggleable;
using Robust.Shared.Analyzers;

namespace Content.Shared._Grosse.ZLevels.Flight;

public abstract partial class GrosseSharedZFlightSystem
{
    private void InitializeControllable()
    {
        SubscribeLocalEvent<GrosseControllableFlightComponent, GrosseFlightStoppedEvent>(OnControllableFlightStopped);
        SubscribeLocalEvent<GrosseControllableFlightComponent, GrosseFlightStartedEvent>(OnControllableFlightStarted);
        SubscribeLocalEvent<GrosseControllableFlightComponent, GrosseZFlightActionUp>(OnZLevelUp);
        SubscribeLocalEvent<GrosseControllableFlightComponent, GrosseZFlightActionDown>(OnZLevelDown);
        SubscribeLocalEvent<GrosseControllableFlightComponent, ToggleActionEvent>(OnZLevelToggle);
        SubscribeLocalEvent<GrosseControllableFlightComponent, GrosseStartFlightDoAfterEvent>(OnStartFlightDoAfter);
    }
    private void OnControllableFlightStopped(Entity<GrosseControllableFlightComponent> ent, ref GrosseFlightStoppedEvent args)
    {
        _actions.SetEnabled(ent.Comp.ZLevelDownActionEntity, false);
        _actions.SetEnabled(ent.Comp.ZLevelUpActionEntity, false);

        // Update toggle action icon state
        if (ent.Comp.ZLevelToggleActionEntity != null)
            _actions.SetToggled(ent.Comp.ZLevelToggleActionEntity, false);
    }
    private void OnControllableFlightStarted(Entity<GrosseControllableFlightComponent> ent, ref GrosseFlightStartedEvent args)
    {
        _actions.SetEnabled(ent.Comp.ZLevelDownActionEntity, true);
        _actions.SetEnabled(ent.Comp.ZLevelUpActionEntity, true);

        // Update toggle action icon state
        if (ent.Comp.ZLevelToggleActionEntity != null)
            _actions.SetToggled(ent.Comp.ZLevelToggleActionEntity, true);
    }
    private void OnZLevelUp(Entity<GrosseControllableFlightComponent> ent, ref GrosseZFlightActionUp args)
    {
        if (args.Handled)
            return;

        var map = Transform(ent).MapUid;
        if (map is null)
            return;

        if (!TryComp<GrosseZFlyerComponent>(ent, out var flyerComp))
            return;

        if (!_zLevel.TryMapUp(map.Value, out var mapAbove))
            return;

        flyerComp.TargetMapHeight = mapAbove.Comp.Depth;
        DirtyField(ent, flyerComp, nameof(GrosseZFlyerComponent.TargetMapHeight));

        args.Handled = true;
    }
    private void OnZLevelDown(Entity<GrosseControllableFlightComponent> ent, ref GrosseZFlightActionDown args)
    {
        if (args.Handled)
            return;

        var map = Transform(ent).MapUid;
        if (map is null)
            return;

        if (!TryComp<GrosseZFlyerComponent>(ent, out var flyerComp))
            return;

        if (!_zLevel.TryMapDown(map.Value, out var mapBelow))
            return;

        flyerComp.TargetMapHeight = mapBelow.Comp.Depth;
        DirtyField(ent, flyerComp, nameof(GrosseZFlyerComponent.TargetMapHeight));

        args.Handled = true;
    }
    private void OnZLevelToggle(Entity<GrosseControllableFlightComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Action.Owner != ent.Comp.ZLevelToggleActionEntity)
            return;

        if (!TryComp<GrosseZFlyerComponent>(ent, out var flyerComp))
            return;

        if (flyerComp.Active)
        {
            DeactivateFlight((ent, flyerComp));
        }
        else
        {
            // If StartFlightDoAfter is set, start a doAfter before activating flight
            if (ent.Comp.StartFlightDoAfter != null)
            {
                //Preventive start flying visuals
                StartFlightVisuals((ent, flyerComp));

                var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.StartFlightDoAfter.Value, new GrosseStartFlightDoAfterEvent(), ent)
                {
                    BreakOnMove = false,
                    BlockDuplicate = true,
                    BreakOnDamage = true,
                    CancelDuplicate = true,
                };

                _doAfter.TryStartDoAfter(doAfter);
            }
            else
            {
                // No delay, activate flight immediately
                TryActivateFlight((ent, flyerComp));
            }
        }

        args.Handled = true;
    }
    private void OnStartFlightDoAfter(Entity<GrosseControllableFlightComponent> ent, ref GrosseStartFlightDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            StopFlightVisuals(ent.Owner);
            return;
        }

        TryActivateFlight(ent.Owner);
        args.Handled = true;
    }
}
