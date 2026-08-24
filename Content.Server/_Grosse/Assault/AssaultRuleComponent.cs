using Content.Shared._Grosse.Assault;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Assault;

[RegisterComponent]
public sealed partial class AssaultRuleComponent : Component
{
    [DataField]
    public TimeSpan PrepTime = TimeSpan.FromSeconds(180);

    [DataField]
    public TimeSpan RoundTime = TimeSpan.FromSeconds(1200);

    [DataField]
    public float WaveThreshold = 0.5f;

    [DataField]
    public TimeSpan WaveTimeout = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan GateOpenDelay = TimeSpan.FromSeconds(20);

    [DataField]
    public int StartingTickets = 200;

    [DataField]
    public int AttackersCaptureReward = 50;

    [DataField]
    public int DefendersCaptureReward = 40;

    [DataField]
    public TimeSpan RestartDelay = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public AssaultPhase Phase = AssaultPhase.Lobby;

    [ViewVariables]
    public int CurrentZone;

    [ViewVariables]
    public int TotalZones;

    [ViewVariables]
    public int AttackersTickets;

    [ViewVariables]
    public int DefendersTickets;

    [ViewVariables]
    public TimeSpan PrepEndsAt;

    [ViewVariables]
    public TimeSpan RoundEndsAt;

    [ViewVariables]
    public TimeSpan IntermissionEndsAt;

    [ViewVariables]
    public TimeSpan HudNextUpdate;

    [ViewVariables]
    public AssaultTeam? Winner;

    [ViewVariables]
    public Dictionary<NetUserId, AssaultPlayerSlot> Players = new();
}

public sealed class AssaultPlayerSlot
{
    public AssaultTeam Team;
    public ProtoId<AssaultClassPrototype>? Class;
    public bool InWaveQueue;
    public TimeSpan QueuedAt;
    public EntityUid? Mob;
}
