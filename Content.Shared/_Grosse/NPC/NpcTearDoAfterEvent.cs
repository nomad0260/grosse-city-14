using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Content.Shared.NPC;

namespace Content.Shared._Grosse.NPC;

/// <summary>
/// Завершение действия разрывания мёртвого тела на части (пожирание NPC).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class NpcTearDoAfterEvent : SimpleDoAfterEvent;
