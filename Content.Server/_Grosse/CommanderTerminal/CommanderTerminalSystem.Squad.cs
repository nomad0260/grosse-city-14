using System.Linq;
using Content.Shared._Grosse.CommanderTerminal;
using Robust.Server.GameStates;

namespace Content.Server._Grosse.CommanderTerminal;

public sealed partial class CommanderTerminalSystem
{
    private void OnExpandPvs(Entity<CommanderGroupMemberComponent> ent, ref ExpandPvsEvent args)
    {
        var groupId = ent.Comp.GroupId;
        if (groupId.Length == 0)
            return;

        var members = EntityQueryEnumerator<CommanderGroupMemberComponent>();
        while (members.MoveNext(out var uid, out var member))
        {
            if (uid == ent.Owner || member.GroupId != groupId)
                continue;

            args.Entities ??= new List<EntityUid>();
            args.Entities.Add(uid);
        }
    }

    /// <summary>
    /// Copies the in-memory groups onto the bodies those groups currently contain.
    /// </summary>
    private void SyncMembers()
    {
        var seen = new HashSet<EntityUid>();
        var syncedScopes = new HashSet<EntityUid>();

        foreach (var (scope, data) in _scopes)
        {
            if (!TryGetScopeTerminal(scope, out var terminal))
                continue;

            syncedScopes.Add(scope);
            foreach (var entry in CollectRoster(terminal, scope))
            {
                if (entry.Member.GroupId == null)
                    continue;

                var group = data.Groups.FirstOrDefault(candidate => candidate.GroupId == entry.Member.GroupId);
                if (group == null)
                    continue;

                var body = GetEntity(entry.Member.Body);
                if (!Exists(body))
                    continue;

                seen.Add(body);
                ApplyMembership(body, group, entry.Member.IsLeader);
            }
        }

        var unsyncedGroups = new HashSet<string>();
        foreach (var (scope, data) in _scopes)
        {
            if (syncedScopes.Contains(scope))
                continue;

            foreach (var group in data.Groups)
            {
                unsyncedGroups.Add(group.GroupId);
            }
        }

        var remove = new List<EntityUid>();
        var members = EntityQueryEnumerator<CommanderGroupMemberComponent>();
        while (members.MoveNext(out var uid, out var member))
        {
            if (seen.Contains(uid) || unsyncedGroups.Contains(member.GroupId))
                continue;

            remove.Add(uid);
        }

        foreach (var uid in remove)
        {
            RemComp<CommanderGroupMemberComponent>(uid);
            RemComp<CommanderGroupOrderComponent>(uid);
        }
    }

    private void ApplyMembership(EntityUid body, CommanderGroupData group, bool leader)
    {
        var member = EnsureComp<CommanderGroupMemberComponent>(body);
        if (member.GroupId != group.GroupId
            || member.GroupName != group.Name
            || member.ColorIndex != group.ColorIndex
            || member.IsLeader != leader)
        {
            member.GroupId = group.GroupId;
            member.GroupName = group.Name;
            member.ColorIndex = group.ColorIndex;
            member.IsLeader = leader;
            Dirty(body, member);
        }

        if (!group.HasOrder)
        {
            if (HasComp<CommanderGroupOrderComponent>(body))
                RemComp<CommanderGroupOrderComponent>(body);
            return;
        }

        var order = EnsureComp<CommanderGroupOrderComponent>(body);
        if (order.Coordinates.Equals(group.OrderCoordinates)
            && order.Depth == group.OrderDepth
            && order.Location == group.OrderLocation)
            return;

        order.Coordinates = group.OrderCoordinates;
        order.Depth = group.OrderDepth;
        order.Location = group.OrderLocation;
        Dirty(body, order);
    }

    private bool TryGetScopeTerminal(EntityUid scope, out Entity<CommanderTerminalComponent> terminal)
    {
        var query = EntityQueryEnumerator<CommanderTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryGetScope(uid, out var found) || found != scope)
                continue;

            terminal = (uid, comp);
            return true;
        }

        terminal = default;
        return false;
    }
}
