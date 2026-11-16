namespace Content.Server.PatchHTN.Components;

/// <summary>
/// Tracks how long an NPC has had no target in vision so idle-only HTN branches (e.g. burrowing
/// antlions) can be gated on it. Only NPCs with this component track idle time.
/// </summary>
[RegisterComponent]
public sealed partial class NPCIdleTrackerComponent : Component
{
    /// <summary>
    /// Accumulated time in seconds without a target in vision.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float IdleTime;
}
