using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.WorkPhase;

[Serializable, NetSerializable]
public sealed class StationMemoState
{
    public string AlertName = string.Empty;
    public string AlertColorHex = "#FFFFFF";
    public string PhaseName = string.Empty;
    public string PhaseColorHex = "#FFFFFF";
    public TimeSpan? PhaseEnd;
    public bool UntilLifted;

    public StationMemoState()
    {
    }

    public StationMemoState(
        string alertName,
        string alertColorHex,
        string phaseName,
        string phaseColorHex,
        TimeSpan? phaseEnd,
        bool untilLifted)
    {
        AlertName = alertName;
        AlertColorHex = alertColorHex;
        PhaseName = phaseName;
        PhaseColorHex = phaseColorHex;
        PhaseEnd = phaseEnd;
        UntilLifted = untilLifted;
    }
}

[Serializable, NetSerializable]
public sealed class StationMemoEvent : EntityEventArgs
{
    public StationMemoState Memo;

    public StationMemoEvent(StationMemoState memo)
    {
        Memo = memo;
    }
}
