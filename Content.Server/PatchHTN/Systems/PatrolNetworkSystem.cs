using Content.Server.PatchHTN.Components;
using Robust.Shared.Map;

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
