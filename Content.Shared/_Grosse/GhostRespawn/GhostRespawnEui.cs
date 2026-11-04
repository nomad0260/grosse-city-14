using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.GhostRespawn;

[Serializable, NetSerializable]
public sealed class GhostRespawnAvailabilityEvent : EntityEventArgs
{
    public bool Available;

    public GhostRespawnAvailabilityEvent()
    {
    }

    public GhostRespawnAvailabilityEvent(bool available)
    {
        Available = available;
    }
}

[Serializable, NetSerializable]
public sealed class GhostRespawnRole
{
    public NetEntity Station;
    public string JobId = string.Empty;
    public string Name = string.Empty;
    public bool Selectable;
}

[Serializable, NetSerializable]
public sealed class GhostRespawnCharacter
{
    public int Slot;
    public string Name = string.Empty;
}

[Serializable, NetSerializable]
public sealed class GhostRespawnEuiState : EuiStateBase
{
    public List<GhostRespawnRole> Roles = new();
    public List<GhostRespawnCharacter> Characters = new();
}

[Serializable, NetSerializable]
public sealed class GhostRespawnSelectMessage : EuiMessageBase
{
    public NetEntity Station { get; }
    public string JobId { get; }
    public int CharacterSlot { get; }

    public GhostRespawnSelectMessage(NetEntity station, string jobId, int characterSlot)
    {
        Station = station;
        JobId = jobId;
        CharacterSlot = characterSlot;
    }
}
