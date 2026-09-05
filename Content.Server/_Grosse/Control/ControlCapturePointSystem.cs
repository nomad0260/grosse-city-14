using System.Diagnostics.CodeAnalysis;
using Content.Shared._Grosse.Control;
using Content.Shared._Grosse.Control.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.Control;

public sealed partial class ControlCapturePointSystem : SharedControlCapturePointSystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ControlCapturePointComponent, StartCollideEvent>(OnEnter);
        SubscribeLocalEvent<ControlCapturePointComponent, EndCollideEvent>(OnExit);
    }

    private void OnEnter(Entity<ControlCapturePointComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ControlConstants.CaptureFixtureId)
            return;

        if (!HasComp<ControlPlayerComponent>(args.OtherEntity))
            return;

        ent.Comp.Occupants.Add(args.OtherEntity);
    }

    private void OnExit(Entity<ControlCapturePointComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != ControlConstants.CaptureFixtureId)
            return;

        ent.Comp.Occupants.Remove(args.OtherEntity);
    }

    public override void Update(float frameTime)
    {
        if (!TryGetActiveRule(out var rule) || rule.Phase != ControlPhase.Fight)
            return;

        var dt = (float) _timing.TickPeriod.TotalSeconds;
        var query = EntityQueryEnumerator<ControlCapturePointComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var point, out var meta))
        {
            if (Paused(uid, meta))
                continue;

            var (teamA, teamB) = CountOccupants(point);
            var oldProgress = point.Progress;
            var oldState = point.VisualState;
            var oldOwner = point.Owner;
            var oldCapturing = point.CapturingTeam;

            if (teamA > 0 && teamB > 0)
            {
                point.VisualState = ControlCaptureState.Contested;
            }
            else if (teamA > 0 || teamB > 0)
            {
                var team = teamA > 0 ? ControlTeam.TeamA : ControlTeam.TeamB;
                if (point.Owner == team)
                {
                    point.Progress = Math.Max(0f, point.Progress - dt / Math.Max(0.1f, point.CaptureTime));
                    point.CapturingTeam = point.Progress > 0f ? point.CapturingTeam : null;
                    point.VisualState = point.Progress > 0f ? ControlCaptureState.Capturing : ControlCaptureState.Held;
                }
                else
                {
                    if (point.CapturingTeam != team)
                    {
                        point.Progress = 0f;
                        point.CapturingTeam = team;
                    }

                    point.Progress = Math.Min(1f, point.Progress + dt / Math.Max(0.1f, point.CaptureTime));
                    point.VisualState = ControlCaptureState.Capturing;
                    if (point.Progress >= 1f)
                    {
                        point.Owner = team;
                        point.Progress = 0f;
                        point.CapturingTeam = null;
                        point.VisualState = ControlCaptureState.Held;
                    }
                }
            }
            else
            {
                point.Progress = Math.Max(0f, point.Progress - dt / Math.Max(0.1f, point.CaptureTime));
                if (point.Progress <= 0f)
                    point.CapturingTeam = null;

                point.VisualState = point.Owner != null
                    ? ControlCaptureState.Held
                    : point.Progress > 0f
                        ? ControlCaptureState.Capturing
                        : ControlCaptureState.Neutral;
            }

            if (Math.Abs(point.Progress - oldProgress) > 0.001f
                || point.VisualState != oldState
                || point.Owner != oldOwner
                || point.CapturingTeam != oldCapturing)
                Dirty(uid, point);
        }
    }

    private (int TeamA, int TeamB) CountOccupants(ControlCapturePointComponent point)
    {
        var a = 0;
        var b = 0;
        foreach (var uid in point.Occupants)
        {
            if (!TryComp<ControlPlayerComponent>(uid, out var player))
                continue;

            if (!TryComp<MobStateComponent>(uid, out var mob) || mob.CurrentState != MobState.Alive)
                continue;

            if (player.Team == ControlTeam.TeamA)
                a++;
            else
                b++;
        }

        return (a, b);
    }

    private bool TryGetActiveRule([NotNullWhen(true)] out ControlRuleComponent? rule)
    {
        var query = EntityQueryEnumerator<ControlRuleComponent, ActiveGameRuleComponent>();
        return query.MoveNext(out _, out rule, out _);
    }
}
