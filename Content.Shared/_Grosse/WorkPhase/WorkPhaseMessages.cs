using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.WorkPhase;

[Serializable, NetSerializable]
public sealed class CommunicationsConsoleSelectWorkPhaseMessage : BoundUserInterfaceMessage
{
    public readonly WorkPhase Phase;

    public CommunicationsConsoleSelectWorkPhaseMessage(WorkPhase phase)
    {
        Phase = phase;
    }
}

[Serializable, NetSerializable]
public sealed class CommunicationsConsoleSetWorkPhaseDurationsMessage : BoundUserInterfaceMessage
{
    public readonly int WorkMinutes;
    public readonly int FreeMinutes;

    public CommunicationsConsoleSetWorkPhaseDurationsMessage(int workMinutes, int freeMinutes)
    {
        WorkMinutes = workMinutes;
        FreeMinutes = freeMinutes;
    }
}
