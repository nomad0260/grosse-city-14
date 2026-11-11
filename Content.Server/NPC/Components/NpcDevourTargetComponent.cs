using Robust.Shared.GameObjects;

namespace Content.Server.NPC.Components;

/// <summary>
/// Отметка на трупе: его уже ест конкретный NPC, остальные не должны
/// налетать на ту же цель.
/// </summary>
[RegisterComponent]
public sealed partial class NpcDevourTargetComponent : Component
{
    /// <summary>
    /// NPC, который ест эту цель.
    /// </summary>
    public EntityUid Eater;
}
