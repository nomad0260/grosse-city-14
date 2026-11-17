using System.Linq;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.CommanderTerminal;

[Serializable, NetSerializable]
public enum CommanderTerminalUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CommanderTerminalState : BoundUserInterfaceState, IEquatable<CommanderTerminalState>
{
    public string StationName { get; }
    public NetEntity? ConsoleGrid { get; }
    public List<CommanderMember> Members { get; }
    public List<CommanderGroup> Groups { get; }
    public List<CommanderFloor> Floors { get; }

    public CommanderTerminalState(
        string stationName,
        NetEntity? consoleGrid,
        List<CommanderMember> members,
        List<CommanderGroup> groups,
        List<CommanderFloor> floors)
    {
        StationName = stationName;
        ConsoleGrid = consoleGrid;
        Members = members;
        Groups = groups;
        Floors = floors;
    }

    public bool Equals(CommanderTerminalState? other)
    {
        return other != null
               && StationName == other.StationName
               && ConsoleGrid == other.ConsoleGrid
               && Members.SequenceEqual(other.Members)
               && Groups.SequenceEqual(other.Groups)
               && Floors.SequenceEqual(other.Floors);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CommanderTerminalState);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StationName);
        hash.Add(ConsoleGrid);
        foreach (var member in Members)
            hash.Add(member);
        foreach (var group in Groups)
            hash.Add(group);
        foreach (var floor in Floors)
            hash.Add(floor);
        return hash.ToHashCode();
    }
}

[Serializable, NetSerializable]
public sealed class CommanderMember : IEquatable<CommanderMember>
{
    public string MemberId { get; }
    public NetEntity Body { get; }
    public string Name { get; }
    public string IdName { get; }
    public string? GroupId { get; }
    public bool IsLeader { get; }

    public CommanderMember(
        string memberId,
        NetEntity body,
        string name,
        string idName,
        string? groupId,
        bool isLeader)
    {
        MemberId = memberId;
        Body = body;
        Name = name;
        IdName = idName;
        GroupId = groupId;
        IsLeader = isLeader;
    }

    public bool Equals(CommanderMember? other)
    {
        return other != null
               && MemberId == other.MemberId
               && Body.Equals(other.Body)
               && Name == other.Name
               && IdName == other.IdName
               && GroupId == other.GroupId
               && IsLeader == other.IsLeader;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CommanderMember);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MemberId, Body, Name, IdName, GroupId, IsLeader);
    }
}

/// <summary>
/// Positions for the map and the beacon column. Sent on a timer, not when the roster changes.
/// </summary>
[Serializable, NetSerializable]
public sealed class CommanderMapUpdateMessage : BoundUserInterfaceMessage
{
    public List<CommanderMapEntry> Entries { get; }

    public CommanderMapUpdateMessage(List<CommanderMapEntry> entries)
    {
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class CommanderMapEntry
{
    public string MemberId { get; }
    public string Location { get; }
    public NetCoordinates Coordinates { get; }

    public CommanderMapEntry(string memberId, string location, NetCoordinates coordinates)
    {
        MemberId = memberId;
        Location = location;
        Coordinates = coordinates;
    }
}

[Serializable, NetSerializable]
public sealed class CommanderGroup : IEquatable<CommanderGroup>
{
    public string GroupId { get; }
    public string Name { get; }
    public string? LeaderId { get; }
    public int ColorIndex { get; }
    public List<string> Members { get; }

    public CommanderGroup(string groupId, string name, string? leaderId, int colorIndex, List<string> members)
    {
        GroupId = groupId;
        Name = name;
        LeaderId = leaderId;
        ColorIndex = colorIndex;
        Members = members;
    }

    public bool Equals(CommanderGroup? other)
    {
        return other != null
               && GroupId == other.GroupId
               && Name == other.Name
               && LeaderId == other.LeaderId
               && ColorIndex == other.ColorIndex
               && Members.SequenceEqual(other.Members);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CommanderGroup);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GroupId);
        hash.Add(Name);
        hash.Add(LeaderId);
        hash.Add(ColorIndex);
        foreach (var member in Members)
            hash.Add(member);
        return hash.ToHashCode();
    }
}

[Serializable, NetSerializable]
public sealed class CommanderFloor : IEquatable<CommanderFloor>
{
    public int Depth { get; }
    public NetEntity Grid { get; }

    public CommanderFloor(int depth, NetEntity grid)
    {
        Depth = depth;
        Grid = grid;
    }

    public bool Equals(CommanderFloor? other)
    {
        return other != null && Depth == other.Depth && Grid.Equals(other.Grid);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CommanderFloor);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Depth, Grid);
    }
}

[Serializable, NetSerializable]
public sealed class CreateCommanderGroupMessage : BoundUserInterfaceMessage
{
    public string Name { get; }

    public CreateCommanderGroupMessage(string name)
    {
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class RenameCommanderGroupMessage : BoundUserInterfaceMessage
{
    public string GroupId { get; }
    public string Name { get; }

    public RenameCommanderGroupMessage(string groupId, string name)
    {
        GroupId = groupId;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class DeleteCommanderGroupMessage : BoundUserInterfaceMessage
{
    public string GroupId { get; }

    public DeleteCommanderGroupMessage(string groupId)
    {
        GroupId = groupId;
    }
}

[Serializable, NetSerializable]
public sealed class AssignCommanderMemberMessage : BoundUserInterfaceMessage
{
    public string GroupId { get; }
    public string MemberId { get; }

    public AssignCommanderMemberMessage(string groupId, string memberId)
    {
        GroupId = groupId;
        MemberId = memberId;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveCommanderMemberMessage : BoundUserInterfaceMessage
{
    public string MemberId { get; }

    public RemoveCommanderMemberMessage(string memberId)
    {
        MemberId = memberId;
    }
}

[Serializable, NetSerializable]
public sealed class SetCommanderLeaderMessage : BoundUserInterfaceMessage
{
    public string GroupId { get; }
    public string MemberId { get; }

    public SetCommanderLeaderMessage(string groupId, string memberId)
    {
        GroupId = groupId;
        MemberId = memberId;
    }
}

[Serializable, NetSerializable]
public sealed class IssueCommanderOrderMessage : BoundUserInterfaceMessage
{
    public string GroupId { get; }
    public NetEntity Grid { get; }
    public Vector2 Position { get; }

    public IssueCommanderOrderMessage(string groupId, NetEntity grid, Vector2 position)
    {
        GroupId = groupId;
        Grid = grid;
        Position = position;
    }
}
