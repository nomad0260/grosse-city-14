/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Analyzers;

namespace Content.Shared._Grosse.ZLevels.Damage.SoftPaws;

public sealed partial class GrosseSoftPawsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseSoftPawsComponent, GrosseZFallingDamageCalculateEvent>(OnFallingDamageCalculate);
    }
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standingState = default!;
    private void OnFallingDamageCalculate(Entity<GrosseSoftPawsComponent> ent, ref GrosseZFallingDamageCalculateEvent args)
    {
        if (_standingState.IsDown(ent.Owner))
            return;

        if (args.Speed <= ent.Comp.MaxSpeedLimit)
        {
            args.DamageMultiplier *= ent.Comp.DamageMultiplier;
            args.StunMultiplier *= ent.Comp.StunMultiplier;

            _popup.PopupPredicted(Loc.GetString("grosse-soft-paws"), ent, ent);
        }
        else
        {
            args.DamageMultiplier *= ent.Comp.DamageHardFallMultiplier;
            args.StunMultiplier *= ent.Comp.StunHardFallMultiplier;

            _popup.PopupPredicted(Loc.GetString("grosse-soft-paws-too-high"), ent, ent, PopupType.SmallCaution);
        }
    }
}
