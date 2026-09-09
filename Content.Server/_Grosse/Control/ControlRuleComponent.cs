using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Control;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Control;

[RegisterComponent]
public sealed partial class ControlRuleComponent : Component
{
    [DataField]
    public TimeSpan PrepTime = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan RoundTime = TimeSpan.FromSeconds(1200);

    [DataField]
    public int ScoreCap = 2000;

    [DataField]
    public TimeSpan ScoreInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public int ScorePerHeldPoint = 10;

    [ViewVariables]
    public int TeamAScorePerPoint = 10;

    [ViewVariables]
    public int TeamBScorePerPoint = 10;

    [DataField]
    public float WaveThreshold = 0.5f;

    [DataField]
    public TimeSpan WaveTimeout = TimeSpan.FromSeconds(45);

    [DataField]
    public int ComebackDeficit = 200;

    [DataField]
    public TimeSpan LastStandTime = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan RestartDelay = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public ControlPhase Phase = ControlPhase.Lobby;

    [ViewVariables]
    public int TeamAScore;

    [ViewVariables]
    public int TeamBScore;

    [ViewVariables]
    public TimeSpan PrepEndsAt;

    [ViewVariables]
    public TimeSpan RoundEndsAt;

    [ViewVariables]
    public TimeSpan NextScoreTick;

    [ViewVariables]
    public TimeSpan LastStandEndsAt;

    [ViewVariables]
    public TimeSpan HudNextUpdate;

    [ViewVariables]
    public ControlTeam? Winner;

    [ViewVariables]
    public bool TeamAComebackGiven;

    [ViewVariables]
    public bool TeamBComebackGiven;

    [ViewVariables]
    public ProtoId<ControlTeamPrototype> TeamAId = ControlConstants.DefaultTeamA;

    [ViewVariables]
    public ProtoId<ControlTeamPrototype> TeamBId = ControlConstants.DefaultTeamB;

    [ViewVariables]
    public Dictionary<NetUserId, ControlPlayerSlot> Players = new();
}

public sealed class ControlPlayerSlot
{
    public ControlTeam Team;
    public ProtoId<AssaultClassPrototype>? Class;
    public bool InWaveQueue;
    public TimeSpan QueuedAt;
    public EntityUid? Mob;
    public bool LastStandSpent;
}
