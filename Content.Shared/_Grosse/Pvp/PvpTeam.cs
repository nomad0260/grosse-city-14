using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Pvp;

[Serializable, NetSerializable]
public enum PvpTeam : byte
{
    Attackers = 0,
    Defenders = 1,
}
