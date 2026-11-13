using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.NPC.Components;

/// <summary>
/// Ключи для appearance компонента закапывания.
/// </summary>
[Serializable, NetSerializable]
public enum BurrowVisuals : byte
{
    Burrowed
}

/// <summary>
/// Маркер для клиентской визуализации закапывания.
/// <para>
/// Компонент должен быть networked: клиентский <c>VisualizerSystem</c> работает
/// только на сущностях, у которых этот компонент реально есть. Раньше его никто
/// не добавлял, поэтому закопанный NPC оставался видимым.
/// </para>
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BurrowableVisualsComponent : Component
{
}
