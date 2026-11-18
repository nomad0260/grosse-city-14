using System.Threading;
using Content.Server.NPC.Components;

namespace Content.Server.NPC.HTN;

[RegisterComponent]
public sealed partial class HTNComponent : NPCComponent
{
    /// <summary>
    /// The base task to use for planning
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite),
    DataField("rootTask", required: true)]
    public HTNCompoundTask RootTask = default!;

    /// <summary>
    /// Check any active services for our current plan. This is used to find new targets for example without changing our plan.
    /// </summary>
    [DataField("checkServices")]
    public bool CheckServices = true;

    /// <summary>
    /// The NPC's current plan.
    /// </summary>
    [ViewVariables]
    public HTNPlan? Plan;
    // TODO FULL GAME SAVE serialize this?

    /// <summary>
    /// How long to wait after having planned to try planning again.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("planCooldown")]
    public float PlanCooldown = 0.45f;

    /// <summary>
    /// How much longer until we can try re-planning. This will happen even during update in case something changed.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float PlanAccumulator = 0f;

    /// <summary>
    /// Re-plan every <see cref="PlanCooldown"/> while a plan is running, instead of only when the plan finishes.
    /// Grosse: the default is unchanged (on). Crowded maps switch it off for specific mobs in YAML with
    /// <c>constantlyReplan: false</c>, so the shared default keeps behaving as before for every other NPC;
    /// <see cref="Content.Server.NPC.Systems.NPCThreatWakeSystem"/> then asks for a new plan when a threat appears.
    /// </summary>
    [DataField]
    public bool ConstantlyReplan = true;

    [ViewVariables]
    public HTNPlanJob? PlanningJob = null;

    /// <summary>
    /// Grosse: a replan was requested while a plan job was already in flight, so it has to run once
    /// that job finishes instead of being dropped.
    /// </summary>
    [ViewVariables]
    public bool ReplanPending;

    [ViewVariables]
    public CancellationTokenSource? PlanningToken = null;

    /// <summary>
    /// Is this NPC currently planning?
    /// </summary>
    [ViewVariables] public bool Planning => PlanningJob != null;

    /// <summary>
    /// Determines whether plans should be made / updated for this entity
    /// </summary>
    [DataField]
    public bool Enabled = true;
}
