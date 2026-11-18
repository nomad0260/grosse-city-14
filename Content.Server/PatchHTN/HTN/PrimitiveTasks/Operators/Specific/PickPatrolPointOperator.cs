using System.Threading;
using System.Threading.Tasks;
using Content.Server.PatchHTN.Components;
using Content.Server.PatchHTN.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.PatchHTN.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Picks a random point of a mapper-defined patrol network (see <see cref="PatrolPointComponent"/>) and stores
/// it as a movement target. Only the destination tile is checked during planning; the actual path is built by
/// the steering system when the mob starts moving.
/// </summary>
/// <remarks>
/// The point is random across the whole network, including the far side of the map, and is not limited by
/// distance. Picking the nearest point instead made a crowd spawned on one marker walk the same route in
/// single file, and left anything further than <c>pointRange</c> from a marker without a patrol at all.
/// </remarks>
public sealed partial class PickPatrolPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IRobustRandom _random = default!;

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

    /// <summary>
    /// How many random points we try before giving up.
    /// </summary>
    [DataField("maxAttempts")]
    public int MaxAttempts = 3;

    /// <summary>
    /// Blackboard key used to remember the last visited point so we don't backtrack immediately.
    /// </summary>
    [DataField("lastPointKey")]
    public string LastPointKey = "PatrolPoint";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _patrol = sysManager.GetEntitySystem<PatrolNetworkSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform))
            return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));

        // Remember the point we are walking to so the next plan does not turn around immediately.
        var excluded = EntityUid.Invalid;

        if (blackboard.TryGetValue<EntityUid>(LastPointKey, out var last, _entManager))
            excluded = last;

        var hasFallback = false;
        var fallbackPoint = EntityUid.Invalid;
        var fallback = EntityCoordinates.Invalid;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (!_patrol.TryGetRandomPoint(xform.Coordinates, Network, _random, excluded, out var point))
            {
                // A network with a single marker, or one where the only other point is the excluded one, must
                // still give us a destination. Bailing out here would drop the NPC into the burrow branch and
                // leave it standing in one place.
                excluded = EntityUid.Invalid;

                if (!_patrol.TryGetRandomPoint(xform.Coordinates, Network, _random, excluded, out point))
                    return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));
            }

            if (!_entManager.TryGetComponent<TransformComponent>(point, out var pointXform))
                continue;

            excluded = point;
            var destination = pointXform.Coordinates;

            // Only a cheap "is there a walkable poly here" check. A full A* per plan made planning so
            // expensive that a crowd of spawned NPCs took seconds to get through the planner queue, and
            // the steering system pathfinds for real once the mob actually starts moving anyway.
            if (_pathfinding.GetPoly(destination) == null)
            {
                // Pathfinding data is rebuilt on a cooldown, so right after a map loads or terrain changes
                // every destination can come back empty. Failing here would leave freshly spawned NPCs
                // without a plan and make them wait out the replan backoff instead of walking off.
                if (!hasFallback)
                {
                    hasFallback = true;
                    fallback = destination;
                    fallbackPoint = point;
                }

                continue;
            }

            return Task.FromResult<(bool, Dictionary<string, object>?)>((true, new Dictionary<string, object>
            {
                { TargetKey, destination },
                { LastPointKey, point },
            }));
        }

        if (hasFallback)
        {
            return Task.FromResult<(bool, Dictionary<string, object>?)>((true, new Dictionary<string, object>
            {
                { TargetKey, fallback },
                { LastPointKey, fallbackPoint },
            }));
        }

        return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));
    }
}
