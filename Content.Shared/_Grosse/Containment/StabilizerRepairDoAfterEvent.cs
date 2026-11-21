using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Containment;

/// <summary>
/// Ремонт проектора стабилизации сварочным инструментом.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class StabilizerRepairDoAfterEvent : SimpleDoAfterEvent;
