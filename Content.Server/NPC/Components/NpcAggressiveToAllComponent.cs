using Robust.Shared.GameObjects;

namespace Content.Server.NPC.Components;

/// <summary>
/// Делает NPC враждебным ко всем мобам вокруг, независимо от фракций.
/// Дружественные (своя фракция) цели по-прежнему игнорируются, чтобы мобы
/// не начинали драку друг с другом.
/// </summary>
[RegisterComponent]
public sealed partial class NpcAggressiveToAllComponent : Component
{
}
