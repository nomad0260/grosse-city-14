using System.Linq;
using System.Numerics;
using Content.Client.CrewManifest;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Client.UIpatch;
using Content.Client.UserInterface.Controls;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.LateJoin;

    public sealed partial class LateJoinGui : DefaultWindow
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IClientConsoleHost _consoleHost = default!;
        [Dependency] private IConfigurationManager _configManager = default!;
        [Dependency] private IEntitySystemManager _entitySystem = default!;
        [Dependency] private JobRequirementsManager _jobRequirements = default!;
        [Dependency] private IClientPreferencesManager _preferencesManager = default!;
        [Dependency] private ILogManager _logManager = default!;

        public event Action<(NetEntity, string)> SelectedId;

        private readonly ClientGameTicker _gameTicker;
        private readonly SpriteSystem _sprites;
        private readonly CrewManifestSystem _crewManifest;
        private readonly ISawmill _sawmill;

        private readonly Dictionary<NetEntity, Dictionary<string, List<JobButton>>> _jobButtons = new();
        private readonly Dictionary<NetEntity, Dictionary<string, BoxContainer>> _jobCategories = new();
        private readonly List<ScrollContainer> _jobLists = new();

        private readonly Control _base;

        public LateJoinGui()
        {
            MinSize = SetSize = new Vector2(540, 620);
            IoCManager.InjectDependencies(this);
            _sprites = _entitySystem.GetEntitySystem<SpriteSystem>();
            _crewManifest = _entitySystem.GetEntitySystem<CrewManifestSystem>();
            _gameTicker = _entitySystem.GetEntitySystem<ClientGameTicker>();
            _sawmill = _logManager.GetSawmill("latejoin.panel");

            Title = Loc.GetString("late-join-gui-title");

            _base = new BoxContainer()
            {
                Orientation = LayoutOrientation.Vertical,
                VerticalExpand = true,
            };

            var glass = new LobbyGlassPanel
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                Tint = LobbyFx.Graphite.WithAlpha(0.32f),
                EdgeColor = Color.Transparent,
            };

            ContentsContainer.AddChild(glass);
            ContentsContainer.AddChild(_base);
            ApplyTranslucentWindowTheme(this);

            _jobRequirements.Updated += RebuildUI;
            RebuildUI();

            SelectedId += x =>
            {
                var (station, jobId) = x;
                _sawmill.Info($"Late joining as ID: {jobId}");
                _consoleHost.ExecuteCommand($"joingame {CommandParsing.Escape(jobId)} {station}");
                Close();
            };

            _gameTicker.LobbyJobsAvailableUpdated += JobsAvailableUpdated;
        }

        private static void ApplyTranslucentWindowTheme(Control root)
        {
            foreach (var child in root.Children)
            {
                if (child is PanelContainer panel)
                {
                    if (panel.HasStyleClass(StyleClassWindowPanel))
                    {
                        panel.PanelOverride = new StyleBoxFlat
                        {
                            BackgroundColor = LobbyFx.Graphite.WithAlpha(0.46f),
                            BorderColor = LobbyFx.Steel.WithAlpha(0.45f),
                            BorderThickness = new Thickness(1f),
                        };
                    }
                    else if (panel.HasStyleClass(StyleClassWindowHeader))
                    {
                        panel.PanelOverride = new StyleBoxFlat
                        {
                            BackgroundColor = LobbyFx.GraphiteDark.WithAlpha(0.68f),
                            BorderColor = LobbyFx.RedAccent.WithAlpha(0.85f),
                            BorderThickness = new Thickness(0f, 0f, 0f, 2f),
                        };
                    }
                }

                ApplyTranslucentWindowTheme(child);
            }
        }

        private void RebuildUI()
        {
            _base.RemoveAllChildren();
            _jobLists.Clear();
            _jobButtons.Clear();
            _jobCategories.Clear();

            if (!_gameTicker.DisallowedLateJoin && _gameTicker.StationNames.Count == 0)
                _sawmill.Warning("No stations exist, nothing to display in late-join GUI");

            foreach (var (id, name) in _gameTicker.StationNames)
            {
                var jobList = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Margin = new Thickness(0, 0, 5f, 0),
                };

                var collapseButton = new ContainerButton()
                {
                    HorizontalAlignment = HAlignment.Right,
                    ToggleMode = true,
                    Children =
                    {
                        new TextureRect
                        {
                            StyleClasses = { OptionButton.StyleClassOptionTriangle },
                            Margin = new Thickness(8, 0),
                            HorizontalAlignment = HAlignment.Center,
                            VerticalAlignment = VAlignment.Center,
                        }
                    }
                };

                _base.AddChild(new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = LobbyFx.Graphite.WithAlpha(0.50f),
                        BorderColor = LobbyFx.RedAccent.WithAlpha(0.85f),
                        BorderThickness = new Thickness(3f, 0f, 0f, 1f),
                        ContentMarginLeftOverride = 12,
                        ContentMarginRightOverride = 10,
                        ContentMarginTopOverride = 8,
                        ContentMarginBottomOverride = 8,
                    },
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            HorizontalExpand = true,
                            Children =
                            {
                                new Label
                                {
                                    StyleClasses = { "LobbyHeroSub" },
                                    Text = name,
                                    FontColorOverride = Color.FromHex("#ECEEEB"),
                                    HorizontalExpand = true,
                                    VerticalAlignment = VAlignment.Center,
                                },
                                collapseButton
                            }
                        }
                    }
                });

                if (_configManager.GetCVar(CCVars.CrewManifestWithoutEntity))
                {
                    var crewManifestButton = new Button
                    {
                        Text = Loc.GetString("crew-manifest-button-label"),
                        Margin = new Thickness(0, 6, 0, 4),
                        StyleBoxOverride = new StyleBoxFlat
                        {
                            BackgroundColor = LobbyFx.GraphiteDark.WithAlpha(0.48f),
                            BorderColor = LobbyFx.Steel.WithAlpha(0.42f),
                            BorderThickness = new Thickness(1f),
                            ContentMarginLeftOverride = 10,
                            ContentMarginRightOverride = 10,
                            ContentMarginTopOverride = 5,
                            ContentMarginBottomOverride = 5,
                        }
                    };
                    crewManifestButton.OnMouseEntered += _ => LobbyFx.PlayHover();
                    crewManifestButton.OnPressed += _ =>
                    {
                        LobbyFx.PlayClick();
                        _crewManifest.RequestCrewManifest(id);
                    };

                    _base.AddChild(crewManifestButton);
                }

                var jobListScroll = new ScrollContainer()
                {
                    VerticalExpand = true,
                    Children = { jobList },
                    Visible = false,
                };

                if (_jobLists.Count == 0)
                    jobListScroll.Visible = true;

                _jobLists.Add(jobListScroll);

                _base.AddChild(jobListScroll);

                collapseButton.OnToggled += _ =>
                {
                    foreach (var section in _jobLists)
                    {
                        section.Visible = false;
                    }
                    jobListScroll.Visible = true;
                };

                var firstCategory = true;
                var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>().ToArray();
                Array.Sort(departments, DepartmentUIComparer.Instance);

                _jobButtons[id] = new Dictionary<string, List<JobButton>>();

                foreach (var department in departments)
                {
                    var departmentName = Loc.GetString(department.Name);
                    _jobCategories[id] = new Dictionary<string, BoxContainer>();
                    var stationAvailable = _gameTicker.JobsAvailable[id];
                    var jobsAvailable = new List<JobPrototype>();

                    foreach (var jobId in department.Roles)
                    {
                        if (!stationAvailable.ContainsKey(jobId))
                            continue;

                        jobsAvailable.Add(_prototypeManager.Index<JobPrototype>(jobId));
                    }

                    jobsAvailable.Sort(JobUIComparer.Instance);

                    // Do not display departments with no jobs available.
                    if (jobsAvailable.Count == 0)
                        continue;

                    var category = new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        Name = department.ID,
                        ToolTip = Loc.GetString("late-join-gui-jobs-amount-in-department-tooltip",
                            ("departmentName", departmentName))
                    };

                    if (firstCategory)
                    {
                        firstCategory = false;
                        category.AddChild(new Control { MinSize = new Vector2(0, 6) });
                    }
                    else
                    {
                        category.AddChild(new Control
                        {
                            MinSize = new Vector2(0, 14),
                        });
                    }

                    category.AddChild(new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        SeparationOverride = 4,
                        Margin = new Thickness(0, 0, 0, 6),
                        Children =
                        {
                            new Label
                            {
                                StyleClasses = { "LobbyDeployHeading" },
                                Text = Loc.GetString("late-join-gui-department-jobs-label", ("departmentName", departmentName))
                            },
                            new BoxContainer
                            {
                                Orientation = LayoutOrientation.Horizontal,
                                HorizontalExpand = true,
                                Children =
                                {
                                    new PanelContainer
                                    {
                                        PanelOverride = new StyleBoxFlat(Color.FromHex("#D13239")),
                                        MinSize = new Vector2(28, 2),
                                        MaxSize = new Vector2(28, 2),
                                    },
                                    new PanelContainer
                                    {
                                        PanelOverride = new StyleBoxFlat(Color.FromHex("#777970").WithAlpha(0.35f)),
                                        MinSize = new Vector2(0, 1),
                                        MaxSize = new Vector2(float.PositiveInfinity, 1),
                                        HorizontalExpand = true,
                                        VerticalAlignment = VAlignment.Center,
                                    }
                                }
                            }
                        }
                    });

                    _jobCategories[id][department.ID] = category;
                    jobList.AddChild(category);

                    foreach (var prototype in jobsAvailable)
                    {
                        var value = stationAvailable[prototype.ID];

                        var jobLabel = new Label
                        {
                            Margin = new Thickness(5f, 0, 0, 0)
                        };

                        var jobButton = new JobButton(jobLabel, prototype.ID, prototype.LocalizedName, value);

                        var jobSelector = new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            HorizontalExpand = true
                        };

                        var icon = new TextureRect
                        {
                            TextureScale = new Vector2(2, 2),
                            VerticalAlignment = VAlignment.Center
                        };

                        var jobIcon = _prototypeManager.Index(prototype.Icon);
                        icon.Texture = _sprites.Frame0(jobIcon.Icon);
                        jobSelector.AddChild(icon);

                        jobSelector.AddChild(jobLabel);
                        jobButton.AddChild(jobSelector);
                        category.AddChild(jobButton);

                        jobButton.OnPressed += _ => SelectedId.Invoke((id, jobButton.JobId));

                        if (!_jobRequirements.IsAllowed(prototype, (HumanoidCharacterProfile?)_preferencesManager.Preferences?.SelectedCharacter, out var reason))
                        {
                            jobButton.Disabled = true;

                            if (!reason.IsEmpty)
                            {
                                var tooltip = new Tooltip();
                                tooltip.SetMessage(reason);
                                jobButton.TooltipSupplier = _ => tooltip;
                            }

                            jobSelector.AddChild(new TextureRect
                            {
                                TextureScale = new Vector2(0.4f, 0.4f),
                                Stretch = TextureRect.StretchMode.KeepCentered,
                                Texture = _sprites.Frame0(new SpriteSpecifier.Texture(new ("/Textures/Interface/Nano/lock.svg.192dpi.png"))),
                                HorizontalExpand = true,
                                HorizontalAlignment = HAlignment.Right,
                            });
                        }
                        else if (value == 0)
                        {
                            jobButton.Disabled = true;
                        }

                        if (!_jobButtons[id].ContainsKey(prototype.ID))
                        {
                            _jobButtons[id][prototype.ID] = new List<JobButton>();
                        }

                        _jobButtons[id][prototype.ID].Add(jobButton);
                    }
                }
            }
        }

        private void JobsAvailableUpdated(IReadOnlyDictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>> updatedJobs)
        {
            foreach (var stationEntries in updatedJobs)
            {
                if (_jobButtons.ContainsKey(stationEntries.Key))
                {
                    var jobsAvailable = stationEntries.Value;

                    var existingJobEntries = _jobButtons[stationEntries.Key];
                    foreach (var existingJobEntry in existingJobEntries)
                    {
                        if (jobsAvailable.ContainsKey(existingJobEntry.Key))
                        {
                            var updatedJobValue = jobsAvailable[existingJobEntry.Key];
                            foreach (var matchingJobButton in existingJobEntry.Value)
                            {
                                if (matchingJobButton.Amount != updatedJobValue)
                                {
                                    matchingJobButton.RefreshLabel(updatedJobValue);
                                    matchingJobButton.Disabled |= matchingJobButton.Amount == 0;
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _jobRequirements.Updated -= RebuildUI;
                _gameTicker.LobbyJobsAvailableUpdated -= JobsAvailableUpdated;
                _jobButtons.Clear();
                _jobCategories.Clear();
            }
        }
    }

    sealed class JobButton : ContainerButton
    {
        private static readonly Color BaseBg = Color.FromHex("#151817");
        private static readonly Color HoverBg = Color.FromHex("#1E2221");
        private static readonly Color Steel = Color.FromHex("#777970");
        private static readonly Color RedAccent = Color.FromHex("#D13239");

        public Label JobLabel { get; }
        public string JobId { get; }
        public string JobLocalisedName { get; }
        public int? Amount { get; private set; }
        private bool _initialised = false;
        private float _hover;
        private bool _wasHovered;

        public JobButton(Label jobLabel, ProtoId<JobPrototype> jobId, string jobLocalisedName, int? amount)
        {
            JobLabel = jobLabel;
            JobId = jobId;
            JobLocalisedName = jobLocalisedName;
            Margin = new Thickness(0, 2);
            MinHeight = 36f;
            RefreshLabel(amount);
            StyleBoxOverride = new StyleBoxFlat(Color.Transparent)
            {
                ContentMarginLeftOverride = 12,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            };
            OnPressed += _ => LobbyFx.PlayDeploy();
            _initialised = true;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            var hovered = !Disabled && IsHovered;
            if (hovered && !_wasHovered)
                LobbyFx.PlayHover();

            _wasHovered = hovered;
            _hover = LobbyFx.Approach(_hover, hovered ? 1f : 0f, 14f, args.DeltaSeconds);
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var size = new Vector2(PixelSize.X, PixelSize.Y);
            if (size.X > 0f && size.Y > 0f)
            {
                var bg = Disabled
                    ? BaseBg.WithAlpha(0.24f)
                    : Color.InterpolateBetween(BaseBg.WithAlpha(0.40f), HoverBg.WithAlpha(0.72f), _hover);
                handle.DrawRect(new UIBox2(0f, 0f, size.X, size.Y), bg);

                var border = Disabled
                    ? Steel.WithAlpha(0.20f)
                    : Color.InterpolateBetween(Steel.WithAlpha(0.35f), RedAccent.WithAlpha(0.80f), _hover);
                handle.DrawRect(new UIBox2(0f, 0f, size.X, 1f), border);
                handle.DrawRect(new UIBox2(0f, size.Y - 1f, size.X, size.Y), border);
                handle.DrawRect(new UIBox2(size.X - 1f, 0f, size.X, size.Y), border);

                var leftBar = Disabled ? Steel.WithAlpha(0.25f) : Color.InterpolateBetween(Steel.WithAlpha(0.55f), RedAccent, _hover);
                handle.DrawRect(new UIBox2(0f, 0f, 2f + 2f * _hover, size.Y), leftBar);
            }

            base.Draw(handle);
        }

        public void RefreshLabel(int? amount)
        {
            if (Amount == amount && _initialised)
            {
                return;
            }
            Amount = amount;

            JobLabel.Text = Amount != null ?
                Loc.GetString("late-join-gui-job-slot-capped", ("jobName", JobLocalisedName), ("amount", Amount)) :
                Loc.GetString("late-join-gui-job-slot-uncapped", ("jobName", JobLocalisedName));
        }
    }
