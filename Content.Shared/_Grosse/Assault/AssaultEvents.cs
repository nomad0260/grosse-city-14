using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Assault;

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
    public string AttackersTeam = AssaultConstants.DefaultAttackersTeam;
    public string DefendersTeam = AssaultConstants.DefaultDefendersTeam;
}

[ByRefEvent]
public readonly record struct AssaultZoneCapturedEvent(int ZoneIndex);
