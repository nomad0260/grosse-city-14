using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Control;

[Serializable, NetSerializable]
public sealed class ControlHudUpdateEvent : EntityEventArgs
{
    public bool Enabled;
    public ControlPhase Phase;
    public int TeamAScore;
    public int TeamBScore;
    public int ScoreCap;
    public TimeSpan PhaseEndsAt;
    public TimeSpan RoundEndsAt;
    public int TeamADead;
    public int TeamATotal;
    public int TeamBDead;
    public int TeamBTotal;
    public float WaveThreshold;
    public string TeamAId = ControlConstants.DefaultTeamA;
    public string TeamBId = ControlConstants.DefaultTeamB;
    public ControlTeam? Winner;
    public List<ControlPointHudInfo> Points = new();
}

[Serializable, NetSerializable]
public sealed class ControlPointHudInfo
{
    public string Name = string.Empty;
    public ControlTeam? Owner;
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
