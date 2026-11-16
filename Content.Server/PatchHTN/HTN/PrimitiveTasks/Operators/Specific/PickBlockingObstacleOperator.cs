using System.Threading;
using System.Threading.Tasks;
using Content.Server.Destructible;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.FixedPoint;
using Content.Shared.NPC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.PatchHTN.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Picks the weakest destructible obstacle on the shortest route to the current target and replaces the
/// movement target with it. Only plans successfully when the target can't be reached without smashing,
/// so it is used as a fallback for movement plans; steering then breaks the obstacle while moving into
/// it (see <see cref="PathFlags.Smashing"/>).
/// </summary>
public sealed partial class PickBlockingObstacleOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private DestructibleSystem _destructible = default!;
    private PathfindingSystem _pathfinding = default!;
    private SharedMapSystem _map = default!;

    /// <summary>
    /// Blackboard key with the coordinates we're trying to reach. Replaced with the obstacle's coordinates.
    /// </summary>
    [DataField("targetKey")]
    public string TargetKey = "TargetCoordinates";

    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    /// <summary>
    /// How close we need to get to the target for it to count as reachable.
    /// </summary>
    [DataField("arriveRange")]
    public float ArriveRange = 0.5f;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _destructible = sysManager.GetEntitySystem<DestructibleSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _map = sysManager.GetEntitySystem<SharedMapSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var target, _entManager) ||
            !_entManager.TryGetComponent<TransformComponent>(owner, out var xform))
        {
            return (false, null);
        }

        var flags = _pathfinding.GetFlags(blackboard);

        // We can already get there, nothing to break.
        var direct = await _pathfinding.GetPath(
            owner,
            xform.Coordinates,
            target,
            ArriveRange,
            cancelToken,
            flags & ~PathFlags.Smashing);

        if (direct.Result == PathResult.Path)
            return (false, null);

        // Shortest route that goes through destructible obstacles. The pathfinder already prices
        // obstacles by their durability, so this is the cheapest wall to break through.
        var path = await _pathfinding.GetPath(owner, xform.Coordinates, target, ArriveRange, cancelToken, flags);

        if (path.Result != PathResult.Path)
            return (false, null);

        var best = EntityUid.Invalid;
        var bestCoords = EntityCoordinates.Invalid;
        var bestDamage = FixedPoint2.MaxValue;
        var bestIndex = -1;

        for (var i = 0; i < path.Path.Count; i++)
        {
            var poly = path.Path[i];

            if (poly.Data.IsFreeSpace)
                continue;

            // Doors and climbables are handled by steering itself.
            if ((poly.Data.Flags & (PathfindingBreadcrumbFlag.Door | PathfindingBreadcrumbFlag.Climb)) != 0x0)
                continue;

            if (!_entManager.TryGetComponent<MapGridComponent>(poly.GraphUid, out var grid))
                return (false, null);

            // Among everything blocking this tile, break whatever needs the least damage.
            foreach (var obstacle in _map.GetLocalAnchoredEntities(poly.GraphUid, grid, poly.Box))
            {
                if (!_entManager.TryGetComponent<DestructibleComponent>(obstacle, out var destructible) ||
                    !_entManager.TryGetComponent<TransformComponent>(obstacle, out var obstacleXform))
                {
                    continue;
                }

                var destroyedAt = _destructible.DestroyedAt(obstacle, destructible);

                if (destroyedAt == FixedPoint2.MaxValue || destroyedAt >= bestDamage)
                    continue;

                best = obstacle;
                bestCoords = obstacleXform.Coordinates;
                bestDamage = destroyedAt;
                bestIndex = i;
            }

            // This is the first node that really blocks the way, so there is nothing to pick beyond it.
            if (!best.IsValid())
                return (false, null);

            break;
        }

        if (!best.IsValid())
            return (false, null);

        // Only walk as far as the obstacle: the rest of the route is planned again after it breaks.
        // Reusing the route beyond it made NPCs run around or past the wall they were meant to break.
        var route = path.Path.GetRange(0, bestIndex + 1);

        return (true, new Dictionary<string, object>
        {
            { TargetKey, bestCoords },
            { PathfindKey, new PathResultEvent(PathResult.Path, route) },
        });
    }
}
