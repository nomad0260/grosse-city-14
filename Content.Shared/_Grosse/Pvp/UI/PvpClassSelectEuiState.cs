using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Pvp.UI;

[Serializable, NetSerializable]
public sealed class PvpClassSelectEuiState : EuiStateBase
{
    public PvpTeam Team;
    public int Tickets;
    public bool ShowTickets;
    public bool ShowClassCost = true;
    public string? SelectedClass;
    public List<PvpClassSelectInfo> Classes = new();
}

[Serializable, NetSerializable]
public sealed class PvpClassSelectInfo
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Cost;
    public bool Affordable = true;
    public bool Available = true;
}

[Serializable, NetSerializable]
public sealed class PvpClassSelectMessage : EuiMessageBase
{
    public string ClassId { get; }

    public PvpClassSelectMessage(string classId)
    {
        ClassId = classId;
    }
}
