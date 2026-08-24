using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Assault;

[Serializable, NetSerializable]
public enum AssaultPhase : byte
{
    Lobby = 0,
    Prep = 1,
    Attack = 2,
    Intermission = 3,
    Ended = 4,
}
