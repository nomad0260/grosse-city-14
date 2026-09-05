using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._Grosse.Pvp;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Grosse.Pvp;

public sealed class PvpLobbySystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _players = default!;

    private readonly List<IPvpLobbySource> _sources = new();
    private readonly Dictionary<NetUserId, PvpLobbyChoice> _choices = new();

    public bool LobbyEnabled { get; private set; }
    public IPvpLobbySource? ActiveSource { get; private set; }
    public IReadOnlyDictionary<NetUserId, PvpLobbyChoice> Choices => _choices;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GamePresetChangedEvent>(OnPresetChanged);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnJoinedLobby);
        SubscribeLocalEvent<ToggleReadyAttemptEvent>(OnReadyAttempt);
        SubscribeNetworkEvent<PvpSelectLoadoutEvent>(OnSelectLoadout);
        SubscribeNetworkEvent<PvpLateJoinRequestEvent>(OnLateJoin);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public void Register(IPvpLobbySource source)
    {
        if (_sources.Contains(source))
            return;

        _sources.Add(source);
        RefreshEnabled();
        BroadcastAll();
    }

    public bool TryGetChoice(NetUserId user, out PvpLobbyChoice choice)
    {
        return _choices.TryGetValue(user, out choice!);
    }

    public bool HasValidChoice(NetUserId user)
    {
        return _choices.TryGetValue(user, out var choice) && IsValid(choice);
    }

    public static bool IsValid(PvpLobbyChoice choice)
    {
        return choice.Random || choice.Team != null && choice.Class != null;
    }

    public (int Attackers, int Defenders) GetTeamCounts()
    {
        var atk = 0;
        var def = 0;
        foreach (var choice in _choices.Values)
        {
            if (choice.Random || choice.Team == null)
                continue;

            if (choice.Team == PvpTeam.Attackers)
                atk++;
            else
                def++;
        }

        return (atk, def);
    }

    public bool CanSelectClass(NetUserId user, string classId, bool includeUnassignedLobby = true)
    {
        var source = ActiveSource;
        if (source == null)
            return false;

        var maxCount = 0;
        foreach (var team in new[] { PvpTeam.Attackers, PvpTeam.Defenders })
        {
            foreach (var info in source.GetClasses(team))
            {
                if (info.Id != classId)
                    continue;

                maxCount = info.MaxCount;
                break;
            }
        }

        if (maxCount <= 0)
            return true;

        return CountClassOccupants(classId, user, includeUnassignedLobby) < maxCount;
    }

    public int CountClassOccupants(string classId, NetUserId? exclude = null, bool includeUnassignedLobby = true)
    {
        var count = 0;
        var counted = new HashSet<NetUserId>();

        if (ActiveSource != null)
        {
            foreach (var (user, assigned) in ActiveSource.GetAssignedClasses())
            {
                if (exclude != null && user == exclude.Value)
                    continue;

                if (assigned != classId)
                    continue;

                counted.Add(user);
                count++;
            }
        }

        if (!includeUnassignedLobby)
            return count;

        foreach (var (user, choice) in _choices)
        {
            if (counted.Contains(user))
                continue;

            if (exclude != null && user == exclude.Value)
                continue;

            if (choice.Class != classId)
                continue;

            count++;
        }

        return count;
    }

    public Dictionary<string, int> GetClassCounts()
    {
        var counts = new Dictionary<string, int>();
        var counted = new HashSet<NetUserId>();

        if (ActiveSource != null)
        {
            foreach (var (user, assigned) in ActiveSource.GetAssignedClasses())
            {
                counted.Add(user);
                counts[assigned] = counts.GetValueOrDefault(assigned) + 1;
            }
        }

        foreach (var (user, choice) in _choices)
        {
            if (counted.Contains(user) || choice.Class is not { } chosen)
                continue;

            counts[chosen] = counts.GetValueOrDefault(chosen) + 1;
        }

        return counts;
    }

    public bool CanJoinTeam(NetUserId user, PvpTeam team)
    {
        var (atk, def) = GetTeamCounts();
        if (_choices.TryGetValue(user, out var existing) && !existing.Random && existing.Team == team)
            return true;

        if (_choices.TryGetValue(user, out existing) && !existing.Random && existing.Team != null)
        {
            if (existing.Team == PvpTeam.Attackers)
                atk--;
            else
                def--;
        }

        if (team == PvpTeam.Attackers)
            atk++;
        else
            def++;

        return Math.Abs(atk - def) <= 1;
    }

    public void BroadcastAll()
    {
        foreach (var session in _players.Sessions)
        {
            SendTo(session);
        }
    }

    public void SendTo(ICommonSession session)
    {
        _choices.TryGetValue(session.UserId, out var choice);
        var (atk, def) = GetTeamCounts();
        var source = ActiveSource;
        var inQueue = source?.IsInWaveQueue(session.UserId) ?? false;
        var attackersName = source?.GetTeamName(PvpTeam.Attackers) ?? string.Empty;
        var defendersName = source?.GetTeamName(PvpTeam.Defenders) ?? string.Empty;
        var header = source != null ? Loc.GetString(source.HeaderLoc) : string.Empty;

        RaiseNetworkEvent(new PvpLobbyStateEvent(
            LobbyEnabled,
            atk,
            def,
            choice?.Random ?? false,
            choice?.Team,
            choice?.Class,
            choice != null && IsValid(choice),
            inQueue,
            source?.ShowClassCost ?? false,
            header,
            attackersName,
            defendersName,
            GetClassCounts(),
            source?.GetClasses(PvpTeam.Attackers).ToList(),
            source?.GetClasses(PvpTeam.Defenders).ToList()), session.Channel);
    }

    private void OnPresetChanged(GamePresetChangedEvent ev)
    {
        RefreshEnabled();
        BroadcastAll();
    }

    private void OnJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        RefreshEnabled();
        SendTo(ev.PlayerSession);
    }

    private void OnReadyAttempt(ToggleReadyAttemptEvent ev)
    {
        if (!LobbyEnabled || ActiveSource == null)
            return;

        if (HasValidChoice(ev.Player.UserId))
            return;

        ev.Cancel();
        _chat.DispatchServerMessage(ev.Player, Loc.GetString(ActiveSource.NeedLoadoutLoc));
    }

    private void OnSelectLoadout(PvpSelectLoadoutEvent ev, EntitySessionEventArgs args)
    {
        if (!LobbyEnabled || ActiveSource == null)
            return;

        var user = args.SenderSession.UserId;
        if (ev.Random)
        {
            _choices[user] = new PvpLobbyChoice { Random = true };
            BroadcastAll();
            return;
        }

        if (ev.Team is not { } team)
            return;

        if (!CanJoinTeam(user, team))
        {
            _chat.DispatchServerMessage(args.SenderSession, Loc.GetString(ActiveSource.TeamFullLoc));
            SendTo(args.SenderSession);
            return;
        }

        string? classId = null;
        if (!string.IsNullOrEmpty(ev.ClassId))
        {
            if (!ActiveSource.ContainsClass(team, ev.ClassId))
                return;

            if (!CanSelectClass(user, ev.ClassId))
            {
                _chat.DispatchServerMessage(args.SenderSession, Loc.GetString(ActiveSource.ClassFullLoc));
                SendTo(args.SenderSession);
                return;
            }

            classId = ev.ClassId;
        }

        _choices[user] = new PvpLobbyChoice
        {
            Team = team,
            Class = classId,
        };
        BroadcastAll();
    }

    private void OnLateJoin(PvpLateJoinRequestEvent ev, EntitySessionEventArgs args)
    {
        ActiveSource?.HandleLateJoin(args.SenderSession);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        if (_choices.Remove(args.Session.UserId))
            BroadcastAll();
    }

    public void RefreshEnabled()
    {
        LobbyEnabled = false;
        ActiveSource = null;
        var preset = _ticker.CurrentPreset ?? _ticker.Preset;
        if (preset == null)
            return;

        foreach (var source in _sources)
        {
            foreach (var rule in preset.Rules)
            {
                if (rule != source.RuleId)
                    continue;

                LobbyEnabled = true;
                ActiveSource = source;
                return;
            }
        }
    }
}
