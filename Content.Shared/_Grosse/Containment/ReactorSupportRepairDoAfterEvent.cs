using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Containment;

/// <summary>
/// Ремонт вспомогательной системы реактора (контур, экран, охлаждение, распределитель).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ReactorSupportRepairDoAfterEvent : SimpleDoAfterEvent;
