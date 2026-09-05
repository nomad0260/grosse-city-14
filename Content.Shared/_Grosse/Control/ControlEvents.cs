using Content.Shared._Grosse.Pvp;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Control;

[Serializable, NetSerializable]
public sealed class ControlHudUpdateEvent : EntityEventArgs
{
    public bool Enabled;
    public ControlPhase Phase;
    public int AttackersScore;
    public int DefendersScore;
    public int ScoreCap;
    public TimeSpan PhaseEndsAt;
    public TimeSpan RoundEndsAt;
    public int AttackersDead;
    public int AttackersTotal;
    public int DefendersDead;
    public int DefendersTotal;
    public float WaveThreshold;
    public string AttackersTeam = ControlConstants.DefaultAttackersTeam;
    public string DefendersTeam = ControlConstants.DefaultDefendersTeam;
    public PvpTeam? Winner;
    public List<ControlPointHudInfo> Points = new();
}

[Serializable, NetSerializable]
public sealed class ControlPointHudInfo
{
    public string Name = string.Empty;
    public PvpTeam? Owner;
    public ControlCaptureState VisualState;
}

[Serializable, NetSerializable]
public enum ControlCaptureState : byte
{
    Neutral = 0,
    Capturing = 1,
    Contested = 2,
    Held = 3,
}

[Serializable, NetSerializable]
public enum ControlCaptureVisualLayers : byte
{
    Screen,
    BarBackground,
    BarFill,
}
