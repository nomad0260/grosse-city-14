using Robust.Shared.Analyzers;
/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

namespace Content.Shared._Grosse.ZLevels.Damage.SafeFalling;

public sealed partial class GrosseSafeFallingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseSafeFallingComponent, GrosseZFallingDamageCalculateEvent>(OnFallingDamageCalculate);
    }
    private void OnFallingDamageCalculate(Entity<GrosseSafeFallingComponent> ent, ref GrosseZFallingDamageCalculateEvent args)
    {
        if (args.Fallen == ent.Owner)
            return;

        args.DamageMultiplier *= ent.Comp.DamageMultiplier;
        args.StunMultiplier *= ent.Comp.StunMultiplier;
    }
}
