using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Strategic;

/// <summary>
/// Освобождает стратегическую точку после удержания.
/// </summary>
public sealed partial class ReleaseStrategicPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private NpcPointManagerSystem _pointManager = default!;

    [DataField("targetKey")]
    public string TargetKey = "StrategicTarget";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        // Освобождение — завершающий шаг плана, поэтому он не должен ломать
        // ветку, даже если точка уже исчезла.
        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, null));
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        EntityUid? target = null;

        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var targetId, _entManager))
            target = targetId;

        if (_entManager.TryGetComponent<NpcPointTrackerComponent>(owner, out var tracker))
        {
            target ??= tracker.CurrentStrategicPoint;
            tracker.CurrentStrategicPoint = null;
        }

        if (target is { } point &&
            _entManager.TryGetComponent<StrategicPointComponent>(point, out _))
        {
            _pointManager.ReleaseStrategicPoint(point, owner);
        }

        blackboard.Remove<EntityUid>(TargetKey);
    }
}
