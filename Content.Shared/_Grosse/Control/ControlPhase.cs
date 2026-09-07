using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Control;

[Serializable, NetSerializable]
public enum ControlPhase : byte
{
    Lobby = 0,
    Prep = 1,
    Fight = 2,
    LastStand = 3,
    Ended = 4,
}
