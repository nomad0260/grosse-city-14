using Content.Server.PatchHTN.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.PatchHTN.HTN.Preconditions;

/// <summary>
/// Requires the NPC to have had no target in vision for at least <see cref="Minimum"/> seconds. Only NPCs
/// with <see cref="NPCIdleTrackerComponent"/> can satisfy this; its tracker resets the timer whenever the
/// NPC has a target it can act on.
/// </summary>
public sealed partial class IdleTimePrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    /// <summary>
    /// Minimum time without a target in vision in seconds.
    /// </summary>
    [DataField("minimum")]
    public float Minimum = 30f;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager))
            return false;

        return _entManager.TryGetComponent<NPCIdleTrackerComponent>(owner, out var tracker) &&
               tracker.IdleTime >= Minimum;
    }
}
