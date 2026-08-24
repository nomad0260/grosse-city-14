using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Assault;

[Serializable, NetSerializable]
public enum AssaultTeam : byte
{
    Attackers = 0,
    Defenders = 1,
}
