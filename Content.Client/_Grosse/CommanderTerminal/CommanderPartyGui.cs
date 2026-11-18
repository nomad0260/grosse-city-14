using System.Numerics;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Grosse.CommanderTerminal;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Grosse.CommanderTerminal;

public sealed class CommanderPartyUIController : UIController
{
    private CommanderPartyGui? _gui;

    public override void Initialize()
    {
        base.Initialize();

        var gameplay = UIManager.GetUIController<GameplayStateLoadController>();
        gameplay.OnScreenLoad += OnScreenLoad;
        gameplay.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        OnScreenUnload();
        if (UIManager.ActiveScreen is not { } screen)
            return;

        _gui = new CommanderPartyGui();
        screen.AddChild(_gui);
        LayoutContainer.SetAnchorPreset(_gui, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetMarginLeft(_gui, 12);
        LayoutContainer.SetMarginTop(_gui, 96);
    }

    private void OnScreenUnload()
    {
        _gui?.Orphan();
        _gui = null;
    }
}

public sealed partial class CommanderPartyGui : PanelContainer
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly DamageableSystem _damage;
    private readonly MobStateSystem _mobState;
    private readonly MobThresholdSystem _thresholds;

    private readonly BoxContainer _root;
    private readonly Label _title;
    private readonly Label _order;
    private readonly BoxContainer _rows;
    private readonly List<MemberRow> _rowPool = new();
    private readonly List<Entity<CommanderGroupMemberComponent>> _members = new();

    private string _signature = string.Empty;

    public CommanderPartyGui()
    {
        IoCManager.InjectDependencies(this);
        _damage = _entities.System<DamageableSystem>();
        _mobState = _entities.System<MobStateSystem>();
        _thresholds = _entities.System<MobThresholdSystem>();

        MouseFilter = MouseFilterMode.Ignore;
        Visible = false;
        MinWidth = 220;
        PanelOverride = new StyleBoxFlat(Color.Black.WithAlpha(0.72f))
        {
            ContentMarginLeftOverride = 8,
            ContentMarginTopOverride = 6,
            ContentMarginRightOverride = 8,
            ContentMarginBottomOverride = 6,
        };

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };
        _title = new Label { ClipText = true };
        _order = new Label { ClipText = true, FontColorOverride = Color.White.WithAlpha(0.85f) };
        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };
        _root.AddChild(_title);
        _root.AddChild(_order);
        _root.AddChild(_rows);
        AddChild(_root);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var local = _player.LocalEntity;
        if (local == null || !_entities.TryGetComponent<CommanderGroupMemberComponent>(local.Value, out var own))
        {
            Visible = false;
            _signature = string.Empty;
            return;
        }

        Visible = true;
        _members.Clear();
        var query = _entities.EntityQueryEnumerator<CommanderGroupMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.GroupId == own.GroupId)
                _members.Add((uid, member));
        }

        _members.Sort((a, b) =>
        {
            var leader = b.Comp.IsLeader.CompareTo(a.Comp.IsLeader);
            if (leader != 0)
                return leader;

            return string.Compare(IdentityName(a.Owner), IdentityName(b.Owner), StringComparison.Ordinal);
        });

        var orderLocation = string.Empty;
        if (_entities.TryGetComponent<CommanderGroupOrderComponent>(local.Value, out var ownOrder))
            orderLocation = ownOrder.Location;

        var signature = own.GroupId + "|" + own.GroupName + "|" + own.ColorIndex + "|" + orderLocation;
        foreach (var member in _members)
        {
            signature += $"|{member.Owner}:{member.Comp.IsLeader}:{IdentityName(member.Owner)}";
        }

        if (signature != _signature)
        {
            _signature = signature;
            _title.Text = own.GroupName;
            _title.FontColorOverride = CommanderGroupColors.ForIndex(own.ColorIndex);
            _order.Visible = orderLocation.Length > 0;
            _order.Text = orderLocation.Length == 0
                ? string.Empty
                : Loc.GetString("commander-terminal-party-order", ("location", orderLocation));

            var index = 0;
            foreach (var member in _members)
            {
                if (index >= _rowPool.Count)
                {
                    var row = new MemberRow();
                    _rowPool.Add(row);
                    _rows.AddChild(row);
                }

                var line = _rowPool[index];
                line.Visible = true;
                line.NameLabel.Text = member.Comp.IsLeader
                    ? $"★ {IdentityName(member.Owner)}"
                    : IdentityName(member.Owner);
                index++;
            }

            for (var i = index; i < _rowPool.Count; i++)
            {
                _rowPool[i].Visible = false;
            }
        }

        for (var i = 0; i < _members.Count && i < _rowPool.Count; i++)
        {
            var fraction = HealthFraction(_members[i].Owner);
            var row = _rowPool[i];
            row.Bar.Value = fraction;
            row.Fill.BackgroundColor = fraction <= 0f
                ? Color.DarkRed
                : Color.InterpolateBetween(new Color(220, 40, 40), new Color(40, 200, 70), fraction);
        }
    }

    private string IdentityName(EntityUid uid)
    {
        return Content.Shared.IdentityManagement.Identity.Name(uid, _entities);
    }

    private float HealthFraction(EntityUid uid)
    {
        if (!_entities.TryGetComponent<DamageableComponent>(uid, out var damage)
            || !_entities.TryGetComponent<MobStateComponent>(uid, out var state))
            return 1f;

        var total = _damage.GetTotalDamage((uid, damage));
        if (_mobState.IsDead(uid, state))
            return 0f;

        if (_mobState.IsAlive(uid, state))
        {
            if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var threshold)
                && !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out threshold))
                return 1f;

            return Math.Clamp(1f - (total / threshold.Value).Float(), 0f, 1f);
        }

        if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold)
            || !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold))
            return 0.5f;

        var crit = critThreshold.Value;
        var dead = deadThreshold.Value;
        if (dead <= crit)
            return 0.5f;

        var critRatio = 1f - ((total - crit) / (dead - crit)).Float();
        return Math.Clamp(critRatio, 0f, 1f);
    }

    private sealed class MemberRow : BoxContainer
    {
        public readonly Label NameLabel;
        public readonly ProgressBar Bar;
        public readonly StyleBoxFlat Fill;

        public MemberRow()
        {
            Orientation = LayoutOrientation.Horizontal;
            SeparationOverride = 6;
            MouseFilter = MouseFilterMode.Ignore;
            NameLabel = new Label
            {
                MinWidth = 108,
                ClipText = true,
                MouseFilter = MouseFilterMode.Ignore,
            };
            Fill = new StyleBoxFlat(new Color(40, 200, 70));
            Bar = new ProgressBar
            {
                MinSize = new Vector2(84, 12),
                MaxValue = 1f,
                HorizontalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
                ForegroundStyleBoxOverride = Fill,
                BackgroundStyleBoxOverride = new StyleBoxFlat(Color.Black),
            };
            AddChild(NameLabel);
            AddChild(Bar);
        }
    }
}
