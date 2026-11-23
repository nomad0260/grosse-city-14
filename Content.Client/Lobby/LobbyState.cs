using Content.Client._Grosse.Pvp;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Shared.CCVar;
using Content.Shared._Grosse.Pvp;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.Lobby
{
    public sealed partial class LobbyState : Robust.Client.State.State
    {
        [Dependency] private IBaseClient _baseClient = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IClientConsoleHost _consoleHost = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IVoteManager _voteManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IClientNetManager _net = default!;

        private ClientGameTicker _gameTicker = default!;
        private PvpClientSystem _pvp = default!;

        // Перестаём авто-раскрывать меню режима после первого ручного клика по кнопке.
        private bool _pvpMenuTouched;

        private float _statusTimer;
        private float _nodeTimer;

        protected override Type? LinkedScreenType { get; } = typeof(LobbyGui);
        public LobbyGui? Lobby;

        protected override void Startup()
        {
            if (_userInterfaceManager.ActiveScreen == null)
            {
                return;
            }

            Lobby = (LobbyGui) _userInterfaceManager.ActiveScreen;

            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            _gameTicker = _entityManager.System<ClientGameTicker>();
            _pvp = _entityManager.System<PvpClientSystem>();

            chatController.SetMainChat(true);

            // Vote popups are parented here; without a container active votes never show up.
            _voteManager.SetPopupContainer(Lobby.VoteContainer);

            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

            var lobbyNameCvar = _cfg.GetCVar(CCVars.ServerLobbyName);
            var serverName = _baseClient.GameInfo?.ServerName ?? string.Empty;

            // The header now carries the game tagline, so the server's lobby name
            // lives in the top-right status line.
            var displayName = string.IsNullOrEmpty(lobbyNameCvar) ? serverName : lobbyNameCvar;
            Lobby.TopServerName.Text = string.IsNullOrEmpty(displayName) ? "—" : displayName;

            UpdateLobbyUi();

            Lobby.CharacterSetupButton.OnPressed += OnSetupPressed;
            Lobby.DeployMenuRow.OnPressed += OnDeployPressed;
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;
            Lobby.PvpMenuButton.OnToggled += OnPvpMenuToggled;
            Lobby.PvpLoadout.LoadoutSelected += OnPvpLoadoutSelected;
            Lobby.PvpLoadout.SummaryChanged += OnPvpSummaryChanged;
            Lobby.PvpLoadout.CollapseRequested += OnPvpCollapseRequested;
            _pvp.LobbyStateChanged += OnPvpLobbyState;
            _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;
        }

        protected override void Shutdown()
        {
            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            chatController.SetMainChat(false);

            // Startup мог выйти раньше (например, ActiveScreen == null) — тогда подписок нет.
            if (Lobby == null)
                return;

            _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;

            _voteManager.ClearPopupContainer();

            Lobby!.CharacterSetupButton.OnPressed -= OnSetupPressed;
            Lobby!.DeployMenuRow.OnPressed -= OnDeployPressed;
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;
            Lobby!.PvpMenuButton.OnToggled -= OnPvpMenuToggled;
            Lobby!.PvpLoadout.LoadoutSelected -= OnPvpLoadoutSelected;
            Lobby!.PvpLoadout.SummaryChanged -= OnPvpSummaryChanged;
            Lobby!.PvpLoadout.CollapseRequested -= OnPvpCollapseRequested;
            _pvp.LobbyStateChanged -= OnPvpLobbyState;

            _pvpMenuTouched = false;

            Lobby = null;
        }

        public void SwitchState(LobbyGui.LobbyGuiState state)
        {
            // Yeah I hate this but LobbyState contains all the badness for now.
            Lobby?.SwitchState(state);
        }

        private void OnSetupPressed(BaseButton.ButtonEventArgs args)
        {
            // Opening the dossier clears readiness on the server. Remember that
            // so the lobby can explain the change instead of silently dropping it.
            var wasReady = Lobby is { ReadyButton.Pressed: true } && !_gameTicker.IsGameStarted;
            SetReady(false);
            if (wasReady)
                Lobby?.NotifyReadinessCleared();

            Lobby?.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted || _gameTicker.DisallowedLateJoin)
            {
                return;
            }

            if (_pvp.LobbyEnabled)
            {
                _pvp.RequestLateJoin();
                return;
            }

            new LateJoinGui().OpenCentered();
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            if (Lobby != null)
                Lobby.SetDeployState(args.Pressed, false, Lobby.ReadyButton.Disabled, null);

            SetReady(args.Pressed);
        }

        /// <summary>
        /// The highlighted DEPLOY row is the prominent action in the reference,
        /// so it performs the same action as the ready plaque: toggle readiness
        /// in the lobby or request a late join once the round has started.
        /// </summary>
        private void OnDeployPressed(BaseButton.ButtonEventArgs args)
        {
            // The row mirrors the ready plaque, including its disabled state
            // (late join closed, PvP loadout not selected).
            if (Lobby == null || Lobby.ReadyButton.Disabled)
                return;

            if (_gameTicker.IsGameStarted)
            {
                OnReadyPressed(args);
                return;
            }

            var ready = !Lobby.ReadyButton.Pressed;
            Lobby.SetDeployState(ready, false, false, null);
            SetReady(ready);
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            _nodeTimer += e.DeltaSeconds;
            if (_nodeTimer >= 1f)
            {
                _nodeTimer = 0f;
                UpdateNodeStatus();
            }

            // Текст статуса меняется раз в секунду; не собираем Loc-строки каждый кадр.
            _statusTimer += e.DeltaSeconds;
            if (_statusTimer < 1f)
                return;

            _statusTimer = 0f;

            if (_gameTicker.IsGameStarted)
            {
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                var roundText = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                Lobby!.StartTime.Text = roundText;
                Lobby!.RoundStatus.Text = roundText;
                Lobby.SetCountdown(true, 0f);
                return;
            }

            Lobby!.RoundStatus.Text = Loc.GetString("lobby-state-player-status-round-not-started");
            string text;

            if (_gameTicker.Paused)
            {
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-paused");
                Lobby?.SetCountdown(false, float.MaxValue);
                return;
            }
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-soon");
                Lobby?.SetCountdown(false, 0f);
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                Lobby?.SetCountdown(false, (float) difference.TotalSeconds);
                if (seconds < 0)
                {
                    text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
                }
                else if (difference.TotalHours >= 1)
                {
                    text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
                }
                else
                {
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
                }
            }

            Lobby!.StartTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
        }

        private void LobbyStatusUpdated()
        {
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            ApplyDeployState();
        }

        /// <summary>
        /// Single source of truth for deploying: the highlighted row and the
        /// ready plaque are rendered from the same lobby/round state.
        /// </summary>
        private void ApplyDeployState()
        {
            if (Lobby == null)
                return;

            if (_gameTicker.IsGameStarted)
            {
                var lateJoinClosed = _gameTicker.DisallowedLateJoin;
                Lobby.ReadyText.Text = Loc.GetString("lobby-state-ready-button-join-state");
                Lobby.ObserveButton.Disabled = false;
                Lobby.SetDeployState(false, true, lateJoinClosed,
                    lateJoinClosed ? Loc.GetString("ui-lobby-deploy-reason-latejoin") : null);
                return;
            }

            var ready = _gameTicker.AreWeReady;
            var disabled = _pvp.LobbyEnabled && !_pvp.CanReady;
            Lobby.ReadyText.Text = Loc.GetString(ready
                ? "lobby-state-player-status-ready"
                : "lobby-state-player-status-not-ready");
            Lobby.ObserveButton.Disabled = true;
            Lobby.SetDeployState(ready, false, disabled,
                disabled ? Loc.GetString("ui-lobby-deploy-reason-loadout") : null);
        }

        private void UpdateLobbyUi()
        {
            ApplyDeployState();

            UpdateOnlinePlayers();
            Lobby!.StationCountLabel.Text = Loc.GetString("ui-lobby-stations", ("count", _gameTicker.StationNames.Count));
            Lobby!.RoundNumberLabel.Text = Loc.GetString("ui-lobby-round-number", ("round", _gameTicker.RoundId));

            var sector = "—";
            foreach (var stationName in _gameTicker.StationNames.Values)
            {
                sector = stationName;
                break;
            }

            Lobby!.MapNameLabel.Text = sector;
            Lobby!.TopServerType.Text = Loc.GetString(_pvp.LobbyEnabled
                ? "ui-lobby-server-type-pvp"
                : "ui-lobby-server-type-roleplay");
        }

        private void UpdateNodeStatus()
        {
            if (Lobby == null)
                return;

            var connected = _net.IsConnected;
            var ping = _net.ServerChannel?.Ping ?? 0;
            var lateJoinOpen = _gameTicker.IsGameStarted && !_gameTicker.DisallowedLateJoin;
            Lobby.UpdateNodeStatus(connected, ping, lateJoinOpen);
        }

        private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
        {
            UpdateOnlinePlayers();
        }

        private void UpdateOnlinePlayers()
        {
            if (Lobby == null)
                return;

            var count = _playerManager.PlayerCount;
            Lobby.PlayersInRoundLabel.Text = Loc.GetString("ui-lobby-players-online", ("count", count));
            Lobby.TopPlayerCount.Text = count.ToString();
        }

        private void OnPvpLoadoutSelected(bool random, Content.Shared._Grosse.Pvp.PvpTeam? team, string? classId)
        {
            _pvp.SelectLoadout(random, team, classId);
        }

        private void OnPvpMenuToggled(BaseButton.ButtonToggledEventArgs args)
        {
            _pvpMenuTouched = true;
            if (Lobby == null)
                return;

            Lobby.PvpLoadout.SetOpen(args.Pressed);
            Lobby.PvpMenuChevron.Rotation = args.Pressed ? MathF.PI / 2f : 0f;
        }

        private void OnPvpSummaryChanged(string summary)
        {
            if (Lobby != null)
                Lobby.PvpMenuSummary.Text = summary;
        }

        private void OnPvpCollapseRequested()
        {
            _pvpMenuTouched = true;
            if (Lobby == null)
                return;

            Lobby.PvpLoadout.SetOpen(false);
            if (Lobby.PvpMenuButton.Pressed)
                Lobby.PvpMenuButton.Pressed = false;
            Lobby.PvpMenuChevron.Rotation = 0f;
        }

        private void OnPvpLobbyState(Content.Shared._Grosse.Pvp.PvpLobbyStateEvent state)
        {
            if (Lobby == null)
                return;

            Lobby.PvpLoadout.UpdateState(state);

            // Карточка режима видна только когда PvP-лобби активно (Штурм, Контроль и т.п.).
            Lobby.PvpCard.Visible = state.Enabled;
            Lobby.PvpMenuButton.Visible = state.Enabled;
            Lobby.PvpMenuButton.Pressed = Lobby.PvpLoadout.Visible;
            if (!string.IsNullOrEmpty(state.Header))
                Lobby.PvpMenuTitle.Text = state.Header;

            Lobby.GameModeLabel.Text = state.Enabled && !string.IsNullOrEmpty(state.Header)
                ? state.Header
                : Loc.GetString("ui-lobby-mode-roleplay");
            Lobby.GameModeDescription.Visible = !state.Enabled;

            // Пока игрок не выбрал набор, само раскрываем меню, чтобы её заметили.
            if (state.Enabled && !state.CanReady && !_pvpMenuTouched)
            {
                Lobby.PvpMenuButton.Pressed = true;
                Lobby.PvpLoadout.SetOpen(true);
                Lobby.PvpMenuChevron.Rotation = MathF.PI / 2f;
            }

            UpdateLobbyUi();
        }

        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
            {
                return;
            }

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }
    }
}
