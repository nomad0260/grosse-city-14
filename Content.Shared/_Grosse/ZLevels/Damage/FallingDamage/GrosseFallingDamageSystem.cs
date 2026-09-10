/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared.Damage.Systems;
using Robust.Shared.Analyzers;

namespace Content.Shared._Grosse.ZLevels.Damage.FallingDamage;

public sealed partial class GrosseFallingDamageSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseFallingDamageComponent, GrosseZFellOnMeEvent>(OnFallOnMe);
    }
    [Dependency] private DamageableSystem _damageable = default!;
    private void OnFallOnMe(Entity<GrosseFallingDamageComponent> ent, ref GrosseZFellOnMeEvent args)
    {
        _damageable.TryChangeDamage(args.Fallen, ent.Comp.Damage * args.Speed);
    }
}
