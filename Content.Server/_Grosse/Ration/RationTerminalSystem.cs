using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Shared._Grosse.Ration;
using Content.Shared.Database;
using Content.Shared.Forensics.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Station;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Ration;

public sealed class RationRecipient
{
    public EntityUid Body { get; }
    public string Name { get; }
    public string JobId { get; }
    public string JobTitle { get; }
    public int Count { get; }
    public string? Fingerprint { get; }

    public RationRecipient(EntityUid body, string name, string jobId, string jobTitle, int count, string? fingerprint)
    {
        Body = body;
        Name = name;
        JobId = jobId;
        JobTitle = jobTitle;
        Count = count;
        Fingerprint = fingerprint;
    }
}

public sealed partial class RationTerminalSystem : EntitySystem
{
    private const string City14Department = "City14";
    private const int EnvelopesPerBox = 9;

    private static readonly ProtoId<JobPrototype> WorkersHead = "UnionWorkersHead";
    private static readonly ProtoId<JobPrototype> FactoryHead = "UnionFactoryHead";

    [Dependency] private IAdminLogManager _admin = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private RationEnvelopeSystem _envelopes = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RationTerminalComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<RationTerminalComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<RationTerminalComponent, RationTerminalSetQuotaMessage>(OnSetQuota);
        SubscribeLocalEvent<RationTerminalComponent, RationTerminalIssueMessage>(OnIssue);
        SubscribeLocalEvent<RationTerminalComponent, RationTerminalIssueAllMessage>(OnIssueAll);
    }

    public bool IsCity14Job(string jobId)
    {
        if (!_jobs.TryGetAllDepartments(jobId, out var departments))
            return false;

        foreach (var department in departments)
        {
            if (department.ID == City14Department)
                return true;
        }

        return false;
    }

    public bool IsOperator(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;

        if (!_jobs.MindTryGetJobId(mindId, out var job) || job is not { } id)
            return false;

        return id == WorkersHead || id == FactoryHead;
    }

    public List<RationRecipient> CollectRecipients(RationQuotaComponent quota)
    {
        var recipients = new List<RationRecipient>();
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mind.OwnedEntity is not { } body || !Exists(body) || HasComp<GhostComponent>(body))
                continue;

            if (!TryBuildRecipient(body, mindId, quota, out var recipient))
                continue;

            recipients.Add(recipient);
        }

        recipients.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        return recipients;
    }

    public List<EntityUid> IssueEnvelopes(EntityUid? user, EntityCoordinates coords, IReadOnlyList<RationRecipient> recipients)
    {
        var envelopes = new List<EntityUid>();
        foreach (var recipient in recipients)
        {
            if (recipient.Count <= 0)
                continue;

            var envelope = _envelopes.SpawnFilledEnvelope(coords, recipient.Name, recipient.JobTitle, recipient.Count, recipient.Fingerprint);
            if (envelope != null)
                envelopes.Add(envelope.Value);
        }

        if (envelopes.Count == 0)
            return envelopes;

        if (envelopes.Count == 1)
        {
            GiveOrDrop(user, envelopes[0]);
            return envelopes;
        }

        var boxes = PackBoxes(coords, envelopes);
        foreach (var box in boxes)
            GiveOrDrop(user, box);

        return boxes;
    }

    private void OnOpenAttempt(Entity<RationTerminalComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (IsOperator(args.User))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("ration-terminal-denied"), ent, args.User);
    }

    private void OnOpened(Entity<RationTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!TryGetQuota(ent, args.Actor, out var station))
            return;

        UpdateUi(ent, station.Comp);
    }

    private void OnSetQuota(Entity<RationTerminalComponent> ent, ref RationTerminalSetQuotaMessage args)
    {
        if (!TryGetQuota(ent, args.Actor, out var station) || !IsCity14Job(args.JobId))
            return;

        var value = Math.Clamp(args.Quota, 0, RationQuotaComponent.MaxCoupons);
        ProtoId<JobPrototype> job = args.JobId;
        var hadValue = station.Comp.Quotas.TryGetValue(job, out var old);
        if (hadValue && old == value)
            return;

        station.Comp.Quotas[job] = value;
        _admin.Add(LogType.Ration, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set ration quota for {args.JobId} from {(hadValue ? old : 0)} to {value}");
        UpdateUi(ent, station.Comp);
    }

    private void OnIssue(Entity<RationTerminalComponent> ent, ref RationTerminalIssueMessage args)
    {
        if (!TryGetQuota(ent, args.Actor, out var station))
            return;

        if (!TryGetEntity(args.Body, out var body)
            || !_mind.TryGetMind(body.Value, out var mindId, out _)
            || !TryBuildRecipient(body.Value, mindId, station.Comp, out var recipient))
        {
            _popup.PopupEntity(Loc.GetString("ration-terminal-nobody"), ent, args.Actor);
            return;
        }

        if (recipient.Count <= 0)
        {
            _popup.PopupEntity(Loc.GetString("ration-terminal-zero"), ent, args.Actor);
            return;
        }

        var issued = IssueEnvelopes(args.Actor, Transform(ent).Coordinates, new List<RationRecipient> { recipient });
        LogIssue(args.Actor, issued, new List<RationRecipient> { recipient });
    }

    private void OnIssueAll(Entity<RationTerminalComponent> ent, ref RationTerminalIssueAllMessage args)
    {
        if (!TryGetQuota(ent, args.Actor, out var station))
            return;

        var recipients = CollectRecipients(station.Comp).Where(recipient => recipient.Count > 0).ToList();
        if (recipients.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("ration-terminal-nobody"), ent, args.Actor);
            return;
        }

        var issued = IssueEnvelopes(args.Actor, Transform(ent).Coordinates, recipients);
        LogIssue(args.Actor, issued, recipients);
    }

    private bool TryGetQuota(EntityUid console, EntityUid user, out Entity<RationQuotaComponent> station)
    {
        station = default;
        if (!IsOperator(user))
            return false;

        if (_station.GetOwningStation(console) is not { } stationUid
            || !TryComp<RationQuotaComponent>(stationUid, out var quota))
        {
            _popup.PopupEntity(Loc.GetString("ration-terminal-no-station"), console, user);
            return false;
        }

        station = (stationUid, quota);
        return true;
    }

    private bool TryBuildRecipient(EntityUid body, EntityUid mindId, RationQuotaComponent quota, out RationRecipient recipient)
    {
        recipient = null!;
        if (!_jobs.MindTryGetJobId(mindId, out var job) || job is not { } jobId || !IsCity14Job(jobId))
            return false;

        var count = quota.Quotas.TryGetValue(jobId, out var quotaCount) ? quotaCount : 0;
        var title = _jobs.MindTryGetJobName(mindId, out var ranked) ? ranked : jobId.Id;
        string? fingerprint = null;
        if (TryComp<FingerprintComponent>(body, out var print))
            fingerprint = print.Fingerprint;

        recipient = new RationRecipient(body, Name(body), jobId.Id, title, count, fingerprint);
        return true;
    }

    private void UpdateUi(EntityUid console, RationQuotaComponent quota)
    {
        var entries = CollectRecipients(quota)
            .Select(recipient => new RationTerminalEntry(
                GetNetEntity(recipient.Body),
                recipient.Name,
                recipient.JobTitle,
                recipient.JobId,
                recipient.Count))
            .ToList();

        _ui.SetUiState(console, RationTerminalUiKey.Key, new RationTerminalBoundUserInterfaceState(entries));
    }

    private List<EntityUid> PackBoxes(EntityCoordinates coords, List<EntityUid> envelopes)
    {
        var boxes = new List<EntityUid>();
        EntityUid? current = null;
        var filled = 0;

        foreach (var envelope in envelopes)
        {
            if (current == null || filled >= EnvelopesPerBox)
            {
                current = SpawnBox(coords);
                boxes.Add(current.Value);
                filled = 0;
            }

            if (!_storage.Insert(current.Value, envelope, out _, playSound: false))
            {
                current = SpawnBox(coords);
                boxes.Add(current.Value);
                filled = 0;
                if (!_storage.Insert(current.Value, envelope, out _, playSound: false))
                    continue;
            }

            filled++;
        }

        return boxes;
    }

    private EntityUid SpawnBox(EntityCoordinates coords)
    {
        var box = Spawn("BoxCardboard", coords);
        _meta.SetEntityName(box, Loc.GetString("ration-box-name"));
        return box;
    }

    private void GiveOrDrop(EntityUid? user, EntityUid item)
    {
        if (user == null)
            return;

        _hands.TryPickupAnyHand(user.Value, item);
    }

    private void LogIssue(EntityUid actor, List<EntityUid> issued, List<RationRecipient> recipients)
    {
        var kind = issued.Count == 1 && HasComp<RationEnvelopeComponent>(issued[0])
            ? "an envelope"
            : $"{issued.Count} cardboard box(es)";
        var summary = string.Join(", ", recipients.Select(recipient => $"{recipient.Name}/{recipient.JobId}:{recipient.Count}"));
        _admin.Add(LogType.Ration, LogImpact.Medium,
            $"{ToPrettyString(actor):player} issued ration coupons as {kind}: {summary}");
    }
}
