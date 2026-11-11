using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.HTN.Preconditions;

/// <summary>
/// Проверяет текущее состояние закапывания
/// </summary>
public sealed partial class BurrowStatePrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entityManager = default!;
    private BurrowableSystem? _burrowSystem;

    [DataField("state")]
    public BurrowState RequiredState = BurrowState.Active;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _burrowSystem = sysManager.GetEntitySystem<BurrowableSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entityManager.TryGetComponent<BurrowableComponent>(owner, out var component))
            return false;

        if (_burrowSystem == null)
            return false;

        return _burrowSystem.GetState(owner, component) == RequiredState;
    }
}
