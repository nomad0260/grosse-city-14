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

namespace Content.Server._Grosse.NPC.HTN.PrimitiveTasks.Operators.Patrol;

/// <summary>
/// Занимает точку патруля и ждёт на ней заданное время
/// </summary>
public sealed partial class OccupyPatrolPointOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private NpcPointManagerSystem _pointManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pointManager = sysManager.GetEntitySystem<NpcPointManagerSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Получаем целевую точку
        if (!blackboard.TryGetValue<EntityUid>("PatrolTarget", out var target, _entManager))
            return (false, null);

        // Занимаем слот на точке
        if (!_pointManager.OccupySlot(target, owner))
            return (false, null);

        // Получаем время ожидания
        float waitTime = 2.0f;
        bool lookAround = false;

        if (_entManager.TryGetComponent<PatrolPointComponent>(target, out var point))
        {
            waitTime = point.WaitTime;
            lookAround = point.LookAround;
        }

        // Ждём на точке
        await Task.Delay(TimeSpan.FromSeconds(waitTime), cancelToken);

        // TODO: Если lookAround = true, можно добавить поворот головы
        // TODO: Проверить угрозы вокруг, искать стратегические цели

        return (true, null);
    }
}
