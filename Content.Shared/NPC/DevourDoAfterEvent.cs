using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.NPC;

/// <summary>
/// DoAfter-событие поедания добычи NPC.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DevourDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => new DevourDoAfterEvent();
}
