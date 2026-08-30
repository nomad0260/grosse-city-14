using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._Grosse.Assault;
using Content.Shared._Grosse.Assault.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.Assault;

public sealed partial class AssaultCapturePointSystem : SharedAssaultCapturePointSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AssaultCapturePointComponent, StartCollideEvent>(OnEnter);
        SubscribeLocalEvent<AssaultCapturePointComponent, EndCollideEvent>(OnExit);
    }

    protected override void OnMapInit(Entity<AssaultCapturePointComponent> ent, ref MapInitEvent args)
    {
        base.OnMapInit(ent, ref args);

        if (ent.Comp.Visual is { } existing && Exists(existing))
            return;

        var visual = Spawn(AssaultConstants.CaptureVisualPrototypeId, Transform(ent).Coordinates);
        _xform.SetParent(visual, ent.Owner);
        _xform.SetLocalPosition(visual, Vector2.Zero);
        ent.Comp.Visual = visual;
        Dirty(ent, ent.Comp);
    }

    private void OnEnter(Entity<AssaultCapturePointComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != AssaultConstants.CaptureFixtureId)
            return;

        if (!HasComp<AssaultPlayerComponent>(args.OtherEntity))
            return;

        ent.Comp.Occupants.Add(args.OtherEntity);
    }

    private void OnExit(Entity<AssaultCapturePointComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != AssaultConstants.CaptureFixtureId)
            return;

        ent.Comp.Occupants.Remove(args.OtherEntity);
    }

    public override void Update(float frameTime)
    {
        if (!TryGetActiveRule(out var rule) || rule.Phase != AssaultPhase.Attack)
            return;

        var dt = (float) _timing.TickPeriod.TotalSeconds;
        var query = EntityQueryEnumerator<AssaultCapturePointComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var point, out var meta))
        {
            if (Paused(uid, meta) || point.Captured || point.ZoneIndex != rule.CurrentZone)
            {
                if (!point.Captured)
                    SetVisual(uid, point, AssaultCaptureState.Idle);
                continue;
            }

            var (attackers, defenders) = CountOccupants(point);
            var oldProgress = point.Progress;
            var oldState = point.VisualState;

            if (attackers > 0 && defenders == 0)
            {
                point.Progress = Math.Min(1f, point.Progress + dt / Math.Max(0.1f, point.CaptureTime));
                point.VisualState = AssaultCaptureState.Capturing;
            }
            else if (attackers > 0)
            {
                point.VisualState = AssaultCaptureState.Contested;
            }
            else
            {
                point.Progress = Math.Max(0f, point.Progress - dt / Math.Max(0.1f, point.CaptureTime));
                point.VisualState = point.Progress > 0f ? AssaultCaptureState.Capturing : AssaultCaptureState.Idle;
            }

            if (point.Progress >= 1f)
            {
                point.Progress = 1f;
                point.Captured = true;
                point.VisualState = AssaultCaptureState.Captured;
                Dirty(uid, point);
                var ev = new AssaultZoneCapturedEvent(point.ZoneIndex);
                RaiseLocalEvent(uid, ref ev);
                return;
            }

            if (Math.Abs(point.Progress - oldProgress) > 0.001f || point.VisualState != oldState)
                Dirty(uid, point);
        }
    }

    private (int Attackers, int Defenders) CountOccupants(AssaultCapturePointComponent point)
    {
        var atk = 0;
        var def = 0;
        foreach (var uid in point.Occupants)
        {
            if (!TryComp<AssaultPlayerComponent>(uid, out var player))
                continue;

            if (!TryComp<MobStateComponent>(uid, out var mob) || mob.CurrentState != MobState.Alive)
                continue;

            if (player.Team == AssaultTeam.Attackers)
                atk++;
            else
                def++;
        }

        return (atk, def);
    }

    private void SetVisual(EntityUid uid, AssaultCapturePointComponent point, AssaultCaptureState state)
    {
        if (point.VisualState == state)
            return;

        point.VisualState = state;
        Dirty(uid, point);
    }

    private bool TryGetActiveRule([NotNullWhen(true)] out AssaultRuleComponent? rule)
    {
        var query = EntityQueryEnumerator<AssaultRuleComponent, ActiveGameRuleComponent>();
        return query.MoveNext(out _, out rule, out _);
    }
}
