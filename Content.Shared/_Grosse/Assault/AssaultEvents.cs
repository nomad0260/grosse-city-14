using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Assault;

[Serializable, NetSerializable]
public sealed class AssaultLobbyStateEvent : EntityEventArgs
{
    public bool Enabled;
    public int AttackersCount;
    public int DefendersCount;
    public bool RandomSelected;
    public AssaultTeam? SelectedTeam;
    public string? SelectedClass;
    public bool CanReady;
    public bool InWaveQueue;

    public AssaultLobbyStateEvent(
        bool enabled,
        int attackersCount,
        int defendersCount,
        bool randomSelected,
        AssaultTeam? selectedTeam,
        string? selectedClass,
        bool canReady,
        bool inWaveQueue)
    {
        Enabled = enabled;
        AttackersCount = attackersCount;
        DefendersCount = defendersCount;
        RandomSelected = randomSelected;
        SelectedTeam = selectedTeam;
        SelectedClass = selectedClass;
        CanReady = canReady;
        InWaveQueue = inWaveQueue;
    }
}

[Serializable, NetSerializable]
public sealed class AssaultSelectLoadoutEvent : EntityEventArgs
{
    public bool Random;
    public AssaultTeam? Team;
    public string? ClassId;

    public AssaultSelectLoadoutEvent(bool random, AssaultTeam? team, string? classId)
    {
        Random = random;
        Team = team;
        ClassId = classId;
    }
}

[Serializable, NetSerializable]
public sealed class AssaultLateJoinRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class AssaultHudUpdateEvent : EntityEventArgs
{
    public bool Enabled;
    public AssaultPhase Phase;
    public int AttackersTickets;
    public int DefendersTickets;
    public int CurrentZone;
    public int TotalZones;
    public TimeSpan PhaseEndsAt;
    public TimeSpan RoundEndsAt;
    public int AttackersDead;
    public int AttackersTotal;
    public int DefendersDead;
    public int DefendersTotal;
    public float WaveThreshold;
    public float CaptureProgress;
}
