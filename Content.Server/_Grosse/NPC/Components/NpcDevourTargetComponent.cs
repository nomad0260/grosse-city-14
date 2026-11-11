using Robust.Shared.GameObjects;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Components;

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
