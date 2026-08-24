using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Assault.UI;

[Serializable, NetSerializable]
public sealed class AssaultClassSelectEuiState : EuiStateBase
{
    public AssaultTeam Team;
    public int Tickets;
    public string? SelectedClass;
    public List<AssaultClassSelectInfo> Classes = new();
}

[Serializable, NetSerializable]
public sealed class AssaultClassSelectInfo
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Cost;
    public bool Affordable;
}

[Serializable, NetSerializable]
public sealed class AssaultClassSelectMessage : EuiMessageBase
{
    public string ClassId { get; }

    public AssaultClassSelectMessage(string classId)
    {
        ClassId = classId;
    }
}
