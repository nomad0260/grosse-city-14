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
/// Делает NPC враждебным ко всем мобам вокруг, независимо от фракций.
/// Дружественные (своя фракция) цели по-прежнему игнорируются, чтобы мобы
/// не начинали драку друг с другом.
/// </summary>
[RegisterComponent]
public sealed partial class NpcAggressiveToAllComponent : Component
{
}
