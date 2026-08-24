using Content.Shared._Grosse.Assault.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server._Grosse.Assault;

public sealed class AssaultSpawnBlockerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AssaultSpawnBlockerComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<AssaultSpawnBlockerComponent> ent, ref PreventCollideEvent args)
    {
        if (!TryComp<AssaultPlayerComponent>(args.OtherEntity, out var player))
            return;

        if (player.Team == ent.Comp.Team)
            args.Cancelled = true;
    }
}
