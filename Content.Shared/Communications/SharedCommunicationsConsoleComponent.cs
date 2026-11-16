using Content.Shared._Grosse.WorkPhase; //Grosse-edit
using Robust.Shared.Serialization;

namespace Content.Shared.Communications
{
    [Virtual]
    public partial class SharedCommunicationsConsoleComponent : Component
    {
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleInterfaceState : BoundUserInterfaceState
    {
        public readonly bool CanAnnounce;
        public readonly bool CanBroadcast = true;
        public readonly bool CanCall;
        public readonly TimeSpan? ExpectedCountdownEnd;
        public readonly bool CountdownStarted;
        public List<string>? AlertLevels;
        public string CurrentAlert;
        public float CurrentAlertDelay;
        public bool HasWorkPhase;   //Grosse-edit-start
        public WorkPhase CurrentPhase;
        public TimeSpan? PhaseEnd;
        public int WorkDurationMinutes;
        public int FreeDurationMinutes;

        public CommunicationsConsoleInterfaceState(
            bool canAnnounce,
            bool canCall,
            List<string>? alertLevels,
            string currentAlert,
            float currentAlertDelay,
            TimeSpan? expectedCountdownEnd = null,
            bool hasWorkPhase = false,
            WorkPhase currentPhase = default,
            TimeSpan? phaseEnd = null,
            int workDurationMinutes = 0,
            int freeDurationMinutes = 0)   //Grosse-edit-end
        {
            CanAnnounce = canAnnounce;
            CanCall = canCall;
            ExpectedCountdownEnd = expectedCountdownEnd;
            CountdownStarted = expectedCountdownEnd != null;
            AlertLevels = alertLevels;
            CurrentAlert = currentAlert;
            CurrentAlertDelay = currentAlertDelay;
            HasWorkPhase = hasWorkPhase;   //Grosse-edit-start
            CurrentPhase = currentPhase;
            PhaseEnd = phaseEnd;
            WorkDurationMinutes = workDurationMinutes;
            FreeDurationMinutes = freeDurationMinutes;   //Grosse-edit-end
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleSelectAlertLevelMessage : BoundUserInterfaceMessage
    {
        public readonly string Level;

        public CommunicationsConsoleSelectAlertLevelMessage(string level)
        {
            Level = level;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleAnnounceMessage : BoundUserInterfaceMessage
    {
        public readonly string Message;

        public CommunicationsConsoleAnnounceMessage(string message)
        {
            Message = message;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleBroadcastMessage : BoundUserInterfaceMessage
    {
        public readonly string Message;
        public CommunicationsConsoleBroadcastMessage(string message)
        {
            Message = message;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleCallEmergencyShuttleMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleRecallEmergencyShuttleMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public enum CommunicationsConsoleUiKey
    {
        Key
    }
}
