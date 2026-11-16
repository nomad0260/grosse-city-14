using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Grosse.WorkPhase;
using Content.Shared.Database;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using WorkPhaseKind = Content.Shared._Grosse.WorkPhase.WorkPhase;

namespace Content.Server._Grosse.WorkPhase;

public sealed partial class WorkPhaseSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private StationSystem _station = default!;

    private static readonly SoundSpecifier NoticeSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");
    private static readonly SoundSpecifier CurfewSound = new SoundPathSpecifier("/Audio/Misc/redalert.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialize);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WorkPhaseComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Started || comp.PhaseEnd is not { } end || now < end)
                continue;

            if (comp.CurrentPhase == WorkPhaseKind.Curfew)
                continue;

            var next = comp.CurrentPhase == WorkPhaseKind.Work ? WorkPhaseKind.Free : WorkPhaseKind.Work;
            SetPhase(uid, next, true, comp);
        }
    }

    private void OnStationInitialize(StationInitializedEvent args)
    {
        if (!TryComp<WorkPhaseComponent>(args.Station, out var comp) || comp.Started)
            return;

        SetPhase(args.Station, comp.CurrentPhase, false, comp);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        if (!HasComp<WorkPhaseComponent>(args.Station))
            return;

        BroadcastMemo(args.Station);
    }

    /// <summary>
    /// Changes the city phase. The same phase does nothing and does not reset the timer.
    /// Curfew stays until another phase is chosen. The round-start call does not announce.
    /// </summary>
    public bool SetPhase(EntityUid station, WorkPhaseKind phase, bool announce, WorkPhaseComponent? component = null, MetaDataComponent? meta = null)
    {
        if (!Resolve(station, ref component, ref meta))
            return false;

        if (component.Started && component.CurrentPhase == phase)
            return false;

        component.Started = true;
        component.CurrentPhase = phase;
        component.PhaseEnd = phase == WorkPhaseKind.Curfew
            ? null
            : _timing.CurTime + DurationFor(component, phase);

        if (announce)
        {
            var phaseName = Loc.GetString(WorkPhases.Name(phase));
            var detail = Loc.GetString(WorkPhases.Announcement(phase));
            var message = Loc.GetString("work-phase-announcement", ("name", phaseName), ("announcement", detail));
            var sound = phase == WorkPhaseKind.Curfew ? CurfewSound : NoticeSound;
            _chat.DispatchStationAnnouncement(station, message, meta.EntityName, true, sound, WorkPhases.ColorOf(phase));
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"Work phase on {ToPrettyString(station)} is now {phase}.");
            BroadcastMemo(station, component);
        }

        RaiseLocalEvent(new WorkPhaseChangedEvent(station, phase));
        return true;
    }

    /// <summary>
    /// Saves new phase lengths. The phase that is running now restarts its countdown when its own length changes.
    /// </summary>
    public bool SetDurations(EntityUid station, int workMinutes, int freeMinutes, WorkPhaseComponent? component = null)
    {
        if (!Resolve(station, ref component))
            return false;

        workMinutes = Math.Clamp(workMinutes, WorkPhases.MinMinutes, WorkPhases.MaxMinutes);
        freeMinutes = Math.Clamp(freeMinutes, WorkPhases.MinMinutes, WorkPhases.MaxMinutes);

        var newWork = TimeSpan.FromMinutes(workMinutes);
        var newFree = TimeSpan.FromMinutes(freeMinutes);
        var workChanged = component.WorkDuration != newWork;
        var freeChanged = component.FreeDuration != newFree;
        if (!workChanged && !freeChanged)
            return false;

        component.WorkDuration = newWork;
        component.FreeDuration = newFree;

        if (component.CurrentPhase == WorkPhaseKind.Work && workChanged)
            component.PhaseEnd = _timing.CurTime + newWork;
        else if (component.CurrentPhase == WorkPhaseKind.Free && freeChanged)
            component.PhaseEnd = _timing.CurTime + newFree;

        RaiseLocalEvent(new WorkPhaseChangedEvent(station, component.CurrentPhase));
        BroadcastMemo(station, component);
        return true;
    }

    public StationMemoState? BuildMemo(EntityUid station, WorkPhaseComponent? component = null)
    {
        if (!Resolve(station, ref component, false))
            return null;

        var alertName = Loc.GetString("alert-level-unknown");
        var alertColor = Color.White;
        if (TryComp<AlertLevelComponent>(station, out var alert)
            && alert.AlertLevels != null
            && alert.AlertLevels.Levels.TryGetValue(alert.CurrentLevel, out var detail))
        {
            if (Loc.TryGetString($"alert-level-{alert.CurrentLevel}", out var localized))
                alertName = localized;
            else
                alertName = alert.CurrentLevel;
            alertColor = detail.Color;
        }

        var phase = component.CurrentPhase;
        return new StationMemoState(
            alertName,
            alertColor.ToHexNoAlpha(),
            Loc.GetString(WorkPhases.Name(phase)),
            WorkPhases.ColorOf(phase).ToHexNoAlpha(),
            component.PhaseEnd,
            phase == WorkPhaseKind.Curfew);
    }

    private void BroadcastMemo(EntityUid station, WorkPhaseComponent? component = null)
    {
        if (BuildMemo(station, component) is not { } memo)
            return;

        if (!TryComp<StationDataComponent>(station, out var data))
            return;

        RaiseNetworkEvent(new StationMemoEvent(memo), _station.GetInStation(data));
    }

    private static TimeSpan DurationFor(WorkPhaseComponent component, WorkPhaseKind phase)
    {
        var duration = phase == WorkPhaseKind.Work ? component.WorkDuration : component.FreeDuration;
        var minimum = TimeSpan.FromMinutes(WorkPhases.MinMinutes);
        return duration < minimum ? minimum : duration;
    }
}

public sealed class WorkPhaseChangedEvent : EntityEventArgs
{
    public EntityUid Station { get; }
    public WorkPhaseKind Phase { get; }

    public WorkPhaseChangedEvent(EntityUid station, WorkPhaseKind phase)
    {
        Station = station;
        Phase = phase;
    }
}
