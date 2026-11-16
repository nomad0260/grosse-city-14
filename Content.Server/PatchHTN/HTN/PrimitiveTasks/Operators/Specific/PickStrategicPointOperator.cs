using System.Threading;
using System.Threading.Tasks;
using Content.Server.PatchHTN.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Robust.Shared.Map;

namespace Content.Server.PatchHTN.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Picks the best reachable <see cref="NPCStrategicPointComponent"/> and stores it as a movement target.
/// Candidates are ordered by priority and distance and then validated with the pathfinder, so NPCs
/// don't try to walk to objectives they can't reach (e.g. another grid without a dock).
/// </summary>
public sealed partial class PickStrategicPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private PathfindingSystem _pathfinding = default!;
    private SharedTransformSystem _transform = default!;

    /// <summary>
    /// Only points of this network are considered, see <see cref="NPCStrategicPointComponent.Network"/>.
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
    /// How far away we look for strategic points.
    /// </summary>
    [DataField("range")]
    public float Range = 250f;

    /// <summary>
    /// How many candidates are validated with the pathfinder. Every check is a pathfind, so keep this low.
    /// </summary>
    [DataField("maxPathChecks")]
    public int MaxPathChecks = 2;

    /// <summary>
    /// How close we need to get to the point for the movement to be considered done.
    /// </summary>
    [DataField("arriveRange")]
    public float ArriveRange = 0.5f;

    /// <summary>
    /// Whether path validation may route through destructible obstacles.
    /// </summary>
    [DataField("allowSmashing")]
    public bool AllowSmashing = true;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform))
            return (false, null);

        var mapId = _transform.GetMapId(xform.Coordinates);
        var candidates = new List<(float Priority, float Distance, EntityCoordinates Coordinates)>();

        foreach (var (point, pointXform) in _entManager.EntityQuery<NPCStrategicPointComponent, TransformComponent>())
        {
            if (!point.Enabled ||
                point.Network != Network ||
                _transform.GetMapId(pointXform.Coordinates) != mapId)
            {
                continue;
            }

            if (!xform.Coordinates.TryDistance(_entManager, pointXform.Coordinates, out var distance) ||
                distance > Range)
            {
                continue;
            }

            // Priority is the primary ordering; distance only breaks equal-priority ties.
            candidates.Add((point.Priority, distance, pointXform.Coordinates));
        }

        candidates.Sort((a, b) =>
        {
            var priorityComparison = b.Priority.CompareTo(a.Priority);
            return priorityComparison != 0 ? priorityComparison : a.Distance.CompareTo(b.Distance);
        });

        var flags = _pathfinding.GetFlags(blackboard);
        if (!AllowSmashing)
            flags &= ~PathFlags.Smashing;
        var checks = Math.Min(MaxPathChecks, candidates.Count);

        for (var i = 0; i < checks; i++)
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
            });
        }

        return (false, null);
    }
}
