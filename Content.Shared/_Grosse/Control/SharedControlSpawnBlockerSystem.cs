using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Control.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Grosse.Control;

public sealed partial class SharedControlSpawnBlockerSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public void SetAllActive(bool active)
    {
        var query = AllEntityQuery<ControlSpawnBlockerComponent, PhysicsComponent, FixturesComponent>();
        while (query.MoveNext(out var uid, out var blocker, out var physics, out var fixtures))
        {
            SetBlockerActive((uid, blocker), active, physics, fixtures);
        }
    }

    public void SetBlockerActive(
        Entity<ControlSpawnBlockerComponent> ent,
        bool active,
        PhysicsComponent? physics = null,
        FixturesComponent? fixtures = null)
    {
        if (!Resolve(ent, ref physics, ref fixtures, false))
            return;

        if (!fixtures.Fixtures.TryGetValue(ControlConstants.BlockerFixtureId, out var fixture))
        {
            foreach (var value in fixtures.Fixtures.Values)
            {
                fixture = value;
                break;
            }
        }

        if (fixture == null)
            return;

        var id = ControlConstants.BlockerFixtureId;
        if (!fixtures.Fixtures.ContainsKey(id))
        {
            foreach (var (key, _) in fixtures.Fixtures)
            {
                id = key;
                break;
            }
        }

        var layer = active ? (int) AssaultConstants.GetBlockerLayer(ent.Comp.Team) : 0;
        _physics.SetCollisionLayer(ent, id, fixture, layer, fixtures, physics);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ControlSpawnBlockerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ControlSpawnBlockerComponent> ent, ref MapInitEvent args)
    {
        SetBlockerActive(ent, true);
    }
}
