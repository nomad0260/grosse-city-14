using Robust.Shared.Serialization;
using Content.Shared.NPC;

namespace Content.Shared._Grosse.NPC.Components;

/// <summary>
/// Ключи для appearance компонента закапывания
/// </summary>
[Serializable, NetSerializable]
public enum BurrowVisuals : byte
{
    Burrowed
}

/// <summary>
/// Компонент для клиентской визуализации закапывания NPC
/// </summary>
[RegisterComponent]
public sealed partial class BurrowableVisualsComponent : Component
{
}
