using Content.Server.PatchHTN.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.PatchHTN.Systems;

/// <summary>
/// Links mapper-placed <see cref="PatrolPointComponent"/> markers into patrol networks and answers
/// queries about them for the patrol HTN task.
/// </summary>
public sealed partial class PatrolNetworkSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PatrolPointComponent, MapInitEvent>(OnPointMapInit);
        SubscribeLocalEvent<PatrolPointComponent, ComponentShutdown>(OnPointShutdown);
    }

    private void OnPointMapInit(EntityUid uid, PatrolPointComponent component, MapInitEvent args)
    {
        component.Neighbors.Clear();

        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var mapId = _transform.GetMapId(xform.Coordinates);
        var query = EntityQueryEnumerator<PatrolPointComponent, TransformComponent>();

        while (query.MoveNext(out var other, out var otherComponent, out var otherXform))
        {
            if (other == uid ||
                otherComponent.Network != component.Network ||
                _transform.GetMapId(otherXform.Coordinates) != mapId)
            {
                continue;
            }

            if (!xform.Coordinates.TryDistance(EntityManager, otherXform.Coordinates, out var distance) ||
                distance > MathF.Min(component.LinkRadius, otherComponent.LinkRadius))
            {
                continue;
            }

            // Map init order isn't guaranteed, so the other point may already link to us.
            if (!component.Neighbors.Contains(other))
                component.Neighbors.Add(other);

            if (!otherComponent.Neighbors.Contains(uid))
                otherComponent.Neighbors.Add(uid);
        }
    }

    private void OnPointShutdown(EntityUid uid, PatrolPointComponent component, ComponentShutdown args)
    {
        foreach (var neighbor in component.Neighbors)
        {
            if (TryComp<PatrolPointComponent>(neighbor, out var neighborComponent))
                neighborComponent.Neighbors.Remove(uid);
        }
    }

    /// <summary>
    /// Fills <paramref name="neighbors"/> with the enabled neighbors of the specified point.
    /// </summary>
    public void GetNeighbors(EntityUid uid, List<EntityUid> neighbors)
    {
        neighbors.Clear();

        if (!TryComp<PatrolPointComponent>(uid, out var component))
            return;

        foreach (var neighbor in component.Neighbors)
        {
            if (TryComp<PatrolPointComponent>(neighbor, out var neighborComponent) && neighborComponent.Enabled)
                neighbors.Add(neighbor);
        }
    }

    /// <summary>
    /// Picks a random enabled point of the specified network on the same map.
    /// </summary>
    /// <remarks>
    /// Distance is deliberately ignored: NPCs spawned in a crowd all stand on the same marker, so a
    /// nearest-point pick sent every one of them down the same route. The sample is reservoir based,
    /// so it costs a single pass and no allocation per call.
    /// </remarks>
    /// <param name="exclude">Point to skip, usually the one the NPC is walking to; pass
    /// <see cref="EntityUid.Invalid"/> to allow every point.</param>
    public bool TryGetRandomPoint(
        EntityCoordinates coordinates,
        string network,
        IRobustRandom random,
        EntityUid exclude,
        out EntityUid point)
    {
        point = EntityUid.Invalid;
        var mapId = _transform.GetMapId(coordinates);
        var query = EntityQueryEnumerator<PatrolPointComponent, TransformComponent>();
        var count = 0;

        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (!component.Enabled ||
                component.Network != network ||
                _transform.GetMapId(xform.Coordinates) != mapId ||
                uid == exclude)
            {
                continue;
            }

            count++;

            if (random.Next(count) == 0)
                point = uid;
        }

        return point.IsValid();
    }

    /// <summary>
    /// Gets the nearest enabled point of the specified network if it is within range.
    /// </summary>
    public bool TryGetNearestPoint(
        EntityCoordinates coordinates,
        string network,
        float maxDistance,
        out EntityUid point)
    {
        point = EntityUid.Invalid;
        var mapId = _transform.GetMapId(coordinates);
        var nearest = maxDistance;
        var query = EntityQueryEnumerator<PatrolPointComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (!component.Enabled ||
                component.Network != network ||
                _transform.GetMapId(xform.Coordinates) != mapId)
            {
                continue;
            }

            if (!coordinates.TryDistance(EntityManager, xform.Coordinates, out var distance) ||
                distance > nearest)
            {
                continue;
            }

            nearest = distance;
            point = uid;
        }

        return point.IsValid();
    }
}
