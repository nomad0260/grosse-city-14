using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._Grosse.Assault;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Assault;

public sealed class AssaultLobbySystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly Dictionary<NetUserId, AssaultLobbyChoice> _choices = new();

    public bool LobbyEnabled { get; private set; }

    public IReadOnlyDictionary<NetUserId, AssaultLobbyChoice> Choices => _choices;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GamePresetChangedEvent>(OnPresetChanged);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnJoinedLobby);
        SubscribeLocalEvent<ToggleReadyAttemptEvent>(OnReadyAttempt);
        SubscribeNetworkEvent<AssaultSelectLoadoutEvent>(OnSelectLoadout);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;

        RefreshEnabled();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public bool TryGetChoice(NetUserId user, out AssaultLobbyChoice choice)
    {
        return _choices.TryGetValue(user, out choice!);
    }

    public bool HasValidChoice(NetUserId user)
    {
        return _choices.TryGetValue(user, out var choice) && IsValid(choice);
    }

    public static bool IsValid(AssaultLobbyChoice choice)
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

            if (choice.Team == AssaultTeam.Attackers)
                atk++;
            else
                def++;
        }

        return (atk, def);
    }

    public bool CanJoinTeam(NetUserId user, AssaultTeam team)
    {
        var (atk, def) = GetTeamCounts();
        if (_choices.TryGetValue(user, out var existing) && !existing.Random && existing.Team == team)
            return true;

        if (_choices.TryGetValue(user, out existing) && !existing.Random && existing.Team != null)
        {
            if (existing.Team == AssaultTeam.Attackers)
                atk--;
            else
                def--;
        }

        if (team == AssaultTeam.Attackers)
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
        var inQueue = false;
        var query = EntityQueryEnumerator<AssaultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out var gameRule))
        {
            if (!_ticker.IsGameRuleActive(uid, gameRule))
                continue;

            if (rule.Players.TryGetValue(session.UserId, out var slot))
                inQueue = slot.InWaveQueue;
        }

        RaiseNetworkEvent(new AssaultLobbyStateEvent(
            LobbyEnabled,
            atk,
            def,
            choice?.Random ?? false,
            choice?.Team,
            choice?.Class,
            choice != null && IsValid(choice),
            inQueue), session.Channel);
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
        if (!LobbyEnabled)
            return;

        if (HasValidChoice(ev.Player.UserId))
            return;

        ev.Cancel();
        _chat.DispatchServerMessage(ev.Player, Loc.GetString("assault-lobby-need-loadout"));
    }

    private void OnSelectLoadout(AssaultSelectLoadoutEvent ev, EntitySessionEventArgs args)
    {
        if (!LobbyEnabled)
            return;

        var user = args.SenderSession.UserId;
        if (ev.Random)
        {
            _choices[user] = new AssaultLobbyChoice { Random = true };
            BroadcastAll();
            return;
        }

        if (ev.Team is not { } team)
            return;

        if (!CanJoinTeam(user, team))
        {
            _chat.DispatchServerMessage(args.SenderSession, Loc.GetString("assault-lobby-team-full"));
            SendTo(args.SenderSession);
            return;
        }

        ProtoId<AssaultClassPrototype>? classId = null;
        if (!string.IsNullOrEmpty(ev.ClassId))
        {
            if (!_proto.TryIndex<AssaultClassPrototype>(ev.ClassId, out var proto) || proto.Team != team)
                return;

            classId = proto.ID;
        }

        _choices[user] = new AssaultLobbyChoice
        {
            Team = team,
            Class = classId,
        };
        BroadcastAll();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        if (_choices.Remove(args.Session.UserId))
            BroadcastAll();
    }

    private void RefreshEnabled()
    {
        LobbyEnabled = false;
        var preset = _ticker.CurrentPreset ?? _ticker.Preset;
        if (preset == null)
            return;

        foreach (var rule in preset.Rules)
        {
            if (rule == AssaultConstants.RulePrototypeId)
            {
                LobbyEnabled = true;
                return;
            }
        }
    }
}
