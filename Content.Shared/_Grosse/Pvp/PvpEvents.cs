using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Pvp;

[Serializable, NetSerializable]
public sealed class PvpLobbyStateEvent : EntityEventArgs
{
    public bool Enabled;
    public int AttackersCount;
    public int DefendersCount;
    public bool RandomSelected;
    public PvpTeam? SelectedTeam;
    public string? SelectedClass;
    public bool CanReady;
    public bool InWaveQueue;
    public bool ShowClassCost;
    public string Header = string.Empty;
    public string AttackersName = string.Empty;
    public string DefendersName = string.Empty;
    public Dictionary<string, int> ClassCounts = new();
    public List<PvpClassInfo> AttackersClasses = new();
    public List<PvpClassInfo> DefendersClasses = new();

    public PvpLobbyStateEvent()
    {
    }

    public PvpLobbyStateEvent(
        bool enabled,
        int attackersCount,
        int defendersCount,
        bool randomSelected,
        PvpTeam? selectedTeam,
        string? selectedClass,
        bool canReady,
        bool inWaveQueue,
        bool showClassCost,
        string header,
        string attackersName,
        string defendersName,
        Dictionary<string, int>? classCounts = null,
        List<PvpClassInfo>? attackersClasses = null,
        List<PvpClassInfo>? defendersClasses = null)
    {
        Enabled = enabled;
        AttackersCount = attackersCount;
        DefendersCount = defendersCount;
        RandomSelected = randomSelected;
        SelectedTeam = selectedTeam;
        SelectedClass = selectedClass;
        CanReady = canReady;
        InWaveQueue = inWaveQueue;
        ShowClassCost = showClassCost;
        Header = header;
        AttackersName = attackersName;
        DefendersName = defendersName;
        ClassCounts = classCounts ?? new();
        AttackersClasses = attackersClasses ?? new();
        DefendersClasses = defendersClasses ?? new();
    }
}

[Serializable, NetSerializable]
public sealed class PvpSelectLoadoutEvent : EntityEventArgs
{
    public bool Random;
    public PvpTeam? Team;
    public string? ClassId;

    public PvpSelectLoadoutEvent(bool random, PvpTeam? team, string? classId)
    {
        Random = random;
        Team = team;
        ClassId = classId;
    }
}

[Serializable, NetSerializable]
public sealed class PvpLateJoinRequestEvent : EntityEventArgs
{
}
