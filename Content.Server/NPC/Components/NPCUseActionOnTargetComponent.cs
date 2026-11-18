using Content.Server.NPC.Systems;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Components;

/// <summary>
/// This is used for an NPC that constantly tries to use an action on a given target.
/// </summary>
[RegisterComponent, Access(typeof(NPCUseActionOnTargetSystem))]
public sealed partial class NPCUseActionOnTargetComponent : Component
{
    /// <summary>
    /// HTN blackboard key for the target entity
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    /// <summary>
    /// Action that's going to attempt to be used.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<TargetActionComponent> ActionId;

    [DataField]
    public EntityUid? ActionEnt;

    /// <summary>
    /// Only use the action when the target is at least this far away. 0 disables the check.
    /// </summary>
    [DataField]
    public float MinRange;

    /// <summary>
    /// Only use the action when the target is at most this far away. 0 disables the check.
    /// </summary>
    [DataField]
    public float MaxRange;

    /// <summary>
    /// Only use the action when there is an unobstructed line to the target, so e.g. a leaping
    /// mob doesn't throw itself into a wall.
    /// </summary>
    [DataField]
    public bool RequireLineOfSight;
}
