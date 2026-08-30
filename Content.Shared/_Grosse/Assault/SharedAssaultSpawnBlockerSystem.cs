using Content.Shared._Grosse.Assault.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Grosse.Assault;

public sealed partial class SharedAssaultSpawnBlockerSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public void UpdateForZone(int currentZone)
    {
        var atkZone = Math.Max(0, currentZone - 1);
        var query = AllEntityQuery<AssaultSpawnBlockerComponent, PhysicsComponent, FixturesComponent>();
        while (query.MoveNext(out var uid, out var blocker, out var physics, out var fixtures))
        {
            var active = blocker.Team == AssaultTeam.Attackers
                ? blocker.ZoneIndex == atkZone
                : blocker.ZoneIndex == currentZone;
            SetBlockerActive((uid, blocker), active, physics, fixtures);
        }
    }

    public void SetBlockerActive(
        Entity<AssaultSpawnBlockerComponent> ent,
        bool active,
        PhysicsComponent? physics = null,
        FixturesComponent? fixtures = null)
    {
        if (!Resolve(ent, ref physics, ref fixtures, false))
            return;

        if (!fixtures.Fixtures.TryGetValue(AssaultConstants.BlockerFixtureId, out var fixture))
        {
            foreach (var value in fixtures.Fixtures.Values)
            {
                fixture = value;
                break;
            }
        }

        if (fixture == null)
            return;

        var id = AssaultConstants.BlockerFixtureId;
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
}
