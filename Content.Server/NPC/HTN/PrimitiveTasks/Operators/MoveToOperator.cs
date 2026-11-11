using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Server._Grosse.ZLevels.LaddersCache;
using Content.Shared._Grosse.ZLevels.Core.Components;
using Content.Shared._Grosse.ZLevels.Core.EntitySystems;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
// Grosse: файл изменён нашим форком (NPC: патруль, стратегические точки, поиск пути, двери). При мердже апстрима учитывай правки ниже.

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Moves an NPC to the specified target key. Hands the actual steering off to NPCSystem.Steering
/// </summary>
public sealed partial class MoveToOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;
    private NPCSteeringSystem _steering = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedTransformSystem _transform = default!;
    private GrosseZLevelsLaddersCacheSystem _ladders = default!;
    private GrosseSharedZLevelsSystem _zLevels = default!;
    private SharedMapSystem _mapSystem = default!;

    /// <summary>
    /// When to shut the task down.
    /// </summary>
    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.PlanFinished;

    /// <summary>
    /// Should we assume the MovementTarget is reachable during planning or should we pathfind to it?
    /// </summary>
    [DataField("pathfindInPlanning")]
    public bool PathfindInPlanning = true;

    /// <summary>
    /// When we're finished moving to the target should we remove its key?
    /// </summary>
    [DataField("removeKeyOnFinish")]
    public bool RemoveKeyOnFinish = true;

    /// <summary>
    /// Target Coordinates to move to. This gets removed after execution.
    /// </summary>
    [DataField("targetKey")]
    public string TargetKey = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField("rangeKey")]
    public string RangeKey = "MovementRange";

    /// <summary>
    /// Do we only need to move into line of sight.
    /// </summary>
    [DataField("stopOnLineOfSight")]
    public bool StopOnLineOfSight;

    /// <summary>
    /// Maximum range to search for ladders when cross-Z-level movement is needed.
    /// </summary>
    [DataField("ladderSearchRange")]
    public float LadderSearchRange = 50f;

    private const string MovementCancelToken = "MovementCancelToken";
    private const string ZLevelLadderKey = "ZLevelLadder"; // Stores target ladder coordinates for Z-level transitions

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _steering = sysManager.GetEntitySystem<NPCSteeringSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _ladders = sysManager.GetEntitySystem<GrosseZLevelsLaddersCacheSystem>();
        _zLevels = sysManager.GetEntitySystem<GrosseSharedZLevelsSystem>();
        _mapSystem = sysManager.GetEntitySystem<SharedMapSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var targetCoordinates, _entManager))
        {
            return (false, null);
        }


        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            !_entManager.TryGetComponent<PhysicsComponent>(owner, out var body))
            return (false, null);

        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);

        // Check if in range using map coordinates (works across grids/Z-levels)
        var ownerMapCoords = _transform.ToMapCoordinates(xform.Coordinates);
        var targetMapCoords = _transform.ToMapCoordinates(targetCoordinates);

        if (ownerMapCoords.MapId == targetMapCoords.MapId)
        {
            var distance = (ownerMapCoords.Position - targetMapCoords.Position).Length();
            if (distance <= range)
            {
                // In range
                return (true, new Dictionary<string, object>()
                {
                    {NPCBlackboard.OwnerCoordinates, blackboard.GetValueOrDefault<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, _entManager)}
                });
            }
        }

        var ownerGridUid = _transform.GetGrid(xform.Coordinates);
        var targetGridUid = _transform.GetGrid(targetCoordinates);

        // Check if owner and target are on different grids or different maps (Z-levels)
        var differentGrids = ownerGridUid != targetGridUid;
        var differentMaps = ownerMapCoords.MapId != targetMapCoords.MapId;

        // Try to detect if this is a Z-level difference (same map network, different vertical levels)
        var isZLevelTransition = false;
        EntityCoordinates? ladderTarget = null;

        if (differentMaps)
        {
            // Check if both maps are in the same Z-level network
            // Get EntityUid of the maps from MapId
            var ownerMapUid = _mapSystem.GetMap(ownerMapCoords.MapId);
            var targetMapUid = _mapSystem.GetMap(targetMapCoords.MapId);

            if (_entManager.TryGetComponent<GrosseZMapComponent>(ownerMapUid, out var ownerZMap) &&
                _entManager.TryGetComponent<GrosseZMapComponent>(targetMapUid, out var targetZMap))
            {
                if (ownerZMap.NetworkUid == targetZMap.NetworkUid)
                {
                    // Same network! This is a Z-level transition
                    isZLevelTransition = true;
                    var zLevelOffset = targetZMap.Depth - ownerZMap.Depth;


                    // Try to find a ladder on our current grid
                    if (ownerGridUid != null && _entManager.HasComponent<MapGridComponent>(ownerGridUid.Value))
                    {
                        var ownerWorldPos = _transform.GetWorldPosition(xform);

                        if (_ladders.GetNearestLadder(ownerGridUid.Value, ownerWorldPos, LadderSearchRange, out var ladderTilePos, out var ladderSlope))
                        {
                            // Found a ladder! Set it as intermediate target
                            var ladderEntity = ladderSlope.Entity;
                            if (_entManager.TryGetComponent<TransformComponent>(ladderEntity, out var ladderXform))
                            {
                                ladderTarget = ladderXform.Coordinates;
                            }
                        }
                        else
                        {
                        }
                    }
                }
            }
        }

        // If we need a ladder but didn't find one, fail the plan
        if (isZLevelTransition && ladderTarget == null)
        {
            return (false, null);
        }

        // Determine actual pathfinding target: ladder (if Z-transition) or original target
        var pathfindTarget = ladderTarget ?? targetCoordinates;
        var isIntermediateTarget = ladderTarget != null;

        if (!PathfindInPlanning)
        {
            var effects = new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, pathfindTarget}
            };

            if (isIntermediateTarget)
            {
                effects[ZLevelLadderKey] = pathfindTarget;
            }

            return (true, effects);
        }

        // Cross-grid movement on same map (e.g., shuttles) - allow direct movement
        if (differentGrids && !differentMaps && !isZLevelTransition)
        {
            return (true, new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, targetCoordinates}
            });
        }

        // Pathfind to the target (or to ladder if Z-transition)
        if (!_entManager.TryGetComponent<MapGridComponent>(_transform.GetGrid(xform.Coordinates), out var ownerGrid))
        {
            return (false, null);
        }

        if (!_entManager.TryGetComponent<MapGridComponent>(_transform.GetGrid(pathfindTarget), out var targetGrid))
        {
            return (false, null);
        }

        var path = await _pathfind.GetPath(
            blackboard.GetValue<EntityUid>(NPCBlackboard.Owner),
            xform.Coordinates,
            pathfindTarget,
            range,
            cancelToken,
            _pathfind.GetFlags(blackboard));

        if (path.Result != PathResult.Path)
        {
            return (false, null);
        }

        var planEffects = new Dictionary<string, object>()
        {
            {NPCBlackboard.OwnerCoordinates, pathfindTarget},
            {PathfindKey, path}
        };

        // Store ladder target if this is Z-transition - we'll need to replan after reaching it
        if (isIntermediateTarget)
        {
            planEffects[ZLevelLadderKey] = pathfindTarget;
        }

        return (true, planEffects);
    }

    // Given steering is complicated we'll hand it off to a dedicated system rather than this singleton operator.

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        // Need to remove the planning value for execution.
        blackboard.Remove<EntityCoordinates>(NPCBlackboard.OwnerCoordinates);
        var targetCoordinates = blackboard.GetValue<EntityCoordinates>(TargetKey);
        var uid = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);


        // Re-use the path we may have if applicable.
        var comp = _steering.Register(uid, targetCoordinates);
        comp.ArriveOnLineOfSight = StopOnLineOfSight;


        if (blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
        {
            comp.Range = range;
        }

        if (blackboard.TryGetValue<PathResultEvent>(PathfindKey, out var result, _entManager))
        {
            if (blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coordinates, _entManager))
            {
                var mapCoords = _transform.ToMapCoordinates(coordinates);
                _steering.PrunePath(uid, mapCoords, _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position, result.Path);
            }

            comp.CurrentPath = new Queue<PathPoly>(result.Path);
        }
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
        {
            return HTNOperatorStatus.Failed;
        }


        // Check if we reached a ladder (intermediate target for Z-level transition)
        if (blackboard.TryGetValue<EntityCoordinates>(ZLevelLadderKey, out var ladderCoords, _entManager))
        {
            if (steering.Status == SteeringStatus.InRange)
            {
                // We reached the ladder! Remove the ladder key to trigger replanning
                blackboard.Remove<EntityCoordinates>(ZLevelLadderKey);
                // Mark as failed to force HTN to replan with new position
                return HTNOperatorStatus.Failed;
            }
        }

        var result = steering.Status switch
        {
            SteeringStatus.InRange => HTNOperatorStatus.Finished,
            SteeringStatus.NoPath => HTNOperatorStatus.Failed,
            SteeringStatus.Moving => HTNOperatorStatus.Continuing,
            _ => throw new ArgumentOutOfRangeException()
        };

        return result;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        // Cleanup the blackboard and remove steering.
        if (blackboard.TryGetValue<CancellationTokenSource>(MovementCancelToken, out var cancelToken, _entManager))
        {
            cancelToken.Cancel();
            blackboard.Remove<CancellationTokenSource>(MovementCancelToken);
        }

        // OwnerCoordinates is only used in planning so dump it.
        blackboard.Remove<PathResultEvent>(PathfindKey);
        blackboard.Remove<EntityCoordinates>(ZLevelLadderKey); // Clean up ladder target if present

        if (RemoveKeyOnFinish)
        {
            blackboard.Remove<EntityCoordinates>(TargetKey);
        }

        _steering.Unregister(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
