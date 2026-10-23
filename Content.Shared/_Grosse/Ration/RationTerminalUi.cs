using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Ration;

[Serializable, NetSerializable]
public enum RationTerminalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RationTerminalBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<RationTerminalEntry> Entries { get; }

    public RationTerminalBoundUserInterfaceState(List<RationTerminalEntry> entries)
    {
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class RationTerminalEntry
{
    public NetEntity Body { get; }
    public string Name { get; }
    public string JobTitle { get; }
    public string JobId { get; }
    public int Quota { get; }

    public RationTerminalEntry(NetEntity body, string name, string jobTitle, string jobId, int quota)
    {
        Body = body;
        Name = name;
        JobTitle = jobTitle;
        JobId = jobId;
        Quota = quota;
    }
}

[Serializable, NetSerializable]
public sealed class RationTerminalSetQuotaMessage : BoundUserInterfaceMessage
{
    public string JobId { get; }
    public int Quota { get; }

    public RationTerminalSetQuotaMessage(string jobId, int quota)
    {
        JobId = jobId;
        Quota = quota;
    }
}

[Serializable, NetSerializable]
public sealed class RationTerminalIssueMessage : BoundUserInterfaceMessage
{
    public NetEntity Body { get; }

    public RationTerminalIssueMessage(NetEntity body)
    {
        Body = body;
    }
}

[Serializable, NetSerializable]
public sealed class RationTerminalIssueAllMessage : BoundUserInterfaceMessage
{
}
