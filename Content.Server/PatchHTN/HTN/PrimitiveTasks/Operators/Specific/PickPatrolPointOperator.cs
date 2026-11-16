using System.Threading;
using System.Threading.Tasks;
using Content.Server.PatchHTN.Components;
using Content.Server.PatchHTN.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Robust.Shared.Map;

namespace Content.Server.PatchHTN.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Picks a reachable neighbor of the nearest point in a mapper-defined patrol network
/// (see <see cref="PatrolPointComponent"/>) and stores it as a movement target. The path is validated
/// during planning so NPCs don't pick points they can't reach.
/// </summary>
public sealed partial class PickPatrolPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private PathfindingSystem _pathfinding = default!;
    private PatrolNetworkSystem _patrol = default!;

    /// <summary>
    /// Network to patrol, see <see cref="PatrolPointComponent.Network"/>.
    /// </summary>
    [DataField("network")]
    public string Network = "Default";

    /// <summary>
    /// Where the movement target is stored.
    /// </summary>
    [DataField("targetKey")]
    public string TargetKey = "TargetCoordinates";

    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    /// <summary>
    /// How far away a point may be for us to treat it as our current position in the network.
    /// </summary>
    [DataField("pointRange")]
    public float PointRange = 20f;

    /// <summary>
    /// How many candidate points we try to pathfind to before giving up.
    /// </summary>
    [DataField("maxAttempts")]
    public int MaxAttempts = 2;

    /// <summary>
    /// Blackboard key used to remember the last visited point so we don't backtrack immediately.
    /// </summary>
    [DataField("lastPointKey")]
    public string LastPointKey = "PatrolPoint";

    /// <summary>
    /// How close we need to get to the point for the movement to be considered done.
    /// </summary>
    [DataField("arriveRange")]
    public float ArriveRange = 0.5f;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _patrol = sysManager.GetEntitySystem<PatrolNetworkSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            !_patrol.TryGetNearestPoint(xform.Coordinates, Network, PointRange, out var current))
        {
            return (false, null);
        }

        var neighbors = new List<EntityUid>();
        _patrol.GetNeighbors(current, neighbors);

        // Don't walk straight back where we came from unless that is the only option.
        if (neighbors.Count > 1 &&
            blackboard.TryGetValue<EntityUid>(LastPointKey, out var last, _entManager))
        {
            neighbors.Remove(last);
        }

        // Try the closest points first so the NPC always takes the shortest hop.
        var candidates = new List<(float Distance, EntityCoordinates Coordinates)>(neighbors.Count);

        foreach (var neighbor in neighbors)
        {
            if (!_entManager.TryGetComponent<TransformComponent>(neighbor, out var neighborXform) ||
                !xform.Coordinates.TryDistance(_entManager, neighborXform.Coordinates, out var distance))
            {
                continue;
            }

            candidates.Add((distance, neighborXform.Coordinates));
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        var flags = _pathfinding.GetFlags(blackboard);
        var attempts = Math.Min(MaxAttempts, candidates.Count);

        for (var i = 0; i < attempts; i++)
        {
            var destination = candidates[i].Coordinates;

            var path = await _pathfinding.GetPath(
                owner,
                xform.Coordinates,
                destination,
                ArriveRange,
                cancelToken,
                flags);

            if (path.Result != PathResult.Path)
                continue;

            return (true, new Dictionary<string, object>
            {
                { TargetKey, destination },
                { PathfindKey, path },
                // Remember where we came from so the next plan can avoid immediately backtracking.
                { LastPointKey, current },
            });
        }

        return (false, null);
    }
}
