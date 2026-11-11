using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._Grosse.Ration;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FingerprintReader;
using Content.Shared.Forensics.Components;
using Content.Shared.Paper;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Grosse.Ration;

public sealed partial class RationEnvelopeSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _admin = default!;
    [Dependency] private FingerprintReaderSystem _fingerprints = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private StackSystem _stacks = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RationEnvelopeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs, after: [typeof(EnvelopeSystem)]);
        SubscribeLocalEvent<RationEnvelopeComponent, RationEnvelopeTearDoAfterEvent>(OnTearDoAfter);
        SubscribeLocalEvent<RationEnvelopeComponent, AttemptSimpleToolUseEvent>(OnToolAttempt);
        SubscribeLocalEvent<RationEnvelopeComponent, SimpleToolDoAfterEvent>(OnToolDoAfter);
    }

    /// <summary>
    /// Fills and seals an envelope. Returns null when the coupon stack cannot be inserted.
    /// </summary>
    public EntityUid? SpawnFilledEnvelope(EntityCoordinates coords, string recipientName, string jobTitle, int count, string? fingerprint)
    {
        if (count <= 0)
            return null;

        var envelope = Spawn("RationEnvelope", coords);
        var ration = Comp<RationEnvelopeComponent>(envelope);
        ration.RecipientName = recipientName;
        ration.CouponCount = count;

        _meta.SetEntityName(envelope, Loc.GetString("ration-envelope-name", ("name", recipientName)));
        _paper.SetContent(envelope, Loc.GetString("ration-envelope-content",
            ("name", recipientName),
            ("job", jobTitle),
            ("count", count)));

        var stack = _stacks.SpawnAtPosition(count, "CouponC17", coords);
        if (!TryComp<EnvelopeComponent>(envelope, out var envelopeComp)
            || !_slots.TryInsert(envelope, envelopeComp.SlotId, stack, user: null))
        {
            QueueDel(stack);
            QueueDel(envelope);
            return null;
        }

        if (TryComp<FingerprintReaderComponent>(envelope, out var reader))
        {
            var allowed = string.IsNullOrWhiteSpace(fingerprint) ? "ration-envelope-no-fingerprint" : fingerprint;
            _fingerprints.AddAllowedFingerprint((envelope, reader), allowed);
        }

        envelopeComp.State = EnvelopeComponent.EnvelopeState.Sealed;
        Dirty(envelope, envelopeComp);
        return envelope;
    }

    /// <summary>
    /// Recipient tear: the full coupon stack is ejected.
    /// </summary>
    public void CompleteRecipientOpen(EntityUid envelope, EntityUid user)
    {
        if (!TryComp<RationEnvelopeComponent>(envelope, out var ration)
            || !TryComp<EnvelopeComponent>(envelope, out var envelopeComp)
            || envelopeComp.State != EnvelopeComponent.EnvelopeState.Sealed)
            return;

        if (!_fingerprints.IsAllowed(envelope, user, out _, showPopup: false))
            return;

        var count = ReadCouponCount(envelope, envelopeComp);
        Tear(envelope, envelopeComp, user, eject: true);
        RemComp<SimpleToolUsageComponent>(envelope);

        _admin.Add(LogType.Ration, LogImpact.Low,
            $"{ToPrettyString(user):player} opened their ration envelope ({ration.RecipientName}) and received {count} coupons");
    }

    /// <summary>
    /// Tool tear used by anyone who is not accepted by the fingerprint reader, including a gloved recipient.
    /// Keeps half the coupons, rounded down.
    /// </summary>
    public void OpenForced(EntityUid envelope, EntityUid user)
    {
        if (!TryComp<RationEnvelopeComponent>(envelope, out var ration)
            || !TryComp<EnvelopeComponent>(envelope, out var envelopeComp)
            || envelopeComp.State != EnvelopeComponent.EnvelopeState.Sealed)
            return;

        var original = ReadCouponCount(envelope, envelopeComp);
        var kept = original / 2;
        if (kept <= 0)
            DestroyCoupons(envelope, envelopeComp);
        else if (_slots.TryGetSlot(envelope, envelopeComp.SlotId, out var slot) && slot.Item is { } stack)
            _stacks.SetCount(stack, kept);

        Tear(envelope, envelopeComp, user, eject: kept > 0);
        RemComp<SimpleToolUsageComponent>(envelope);

        var message = kept > 0
            ? Loc.GetString("ration-envelope-wrong-open", ("name", ration.RecipientName), ("kept", kept), ("total", original))
            : Loc.GetString("ration-envelope-wrong-open-empty", ("name", ration.RecipientName));
        _popup.PopupEntity(message, envelope, user);

        _admin.Add(LogType.Ration, LogImpact.Medium,
            $"{ToPrettyString(user):player} forced open ration envelope addressed to {ration.RecipientName}: {original} coupons reduced to {kept}");
    }

    private void OnGetAltVerbs(Entity<RationEnvelopeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<EnvelopeComponent>(ent, out var envelope) || envelope.State != EnvelopeComponent.EnvelopeState.Sealed)
            return;

        var tearText = Loc.GetString("envelope-verb-tear");
        args.Verbs.RemoveWhere(verb => verb.Text == tearText);

        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (!_fingerprints.IsAllowed(ent.Owner, args.User, out _, showPopup: false))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("ration-envelope-open-verb"),
            Act = () => TryStartTear(ent, user),
        });
    }

    private void TryStartTear(Entity<RationEnvelopeComponent> ent, EntityUid user)
    {
        if (ent.Comp.TearDoAfter != null)
            return;

        if (!TryComp<EnvelopeComponent>(ent, out var envelope) || envelope.State != EnvelopeComponent.EnvelopeState.Sealed)
            return;

        var doAfter = new DoAfterArgs(EntityManager, user, envelope.TearDelay, new RationEnvelopeTearDoAfterEvent(), ent, ent)
        {
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            MovementThreshold = 0.01f,
            DistanceThreshold = 1f,
        };

        if (_doAfter.TryStartDoAfter(doAfter, out var id))
            ent.Comp.TearDoAfter = id;
    }

    private void OnTearDoAfter(Entity<RationEnvelopeComponent> ent, ref RationEnvelopeTearDoAfterEvent args)
    {
        ent.Comp.TearDoAfter = null;
        if (args.Cancelled)
            return;

        CompleteRecipientOpen(ent, args.User);
    }

    private void OnToolAttempt(Entity<RationEnvelopeComponent> ent, ref AttemptSimpleToolUseEvent args)
    {
        if (!TryComp<EnvelopeComponent>(ent, out var envelope) || envelope.State != EnvelopeComponent.EnvelopeState.Sealed)
            args.Cancelled = true;
    }

    private void OnToolDoAfter(Entity<RationEnvelopeComponent> ent, ref SimpleToolDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        OpenForced(ent, args.User);
    }

    private void Tear(EntityUid envelope, EnvelopeComponent envelopeComp, EntityUid user, bool eject)
    {
        envelopeComp.State = EnvelopeComponent.EnvelopeState.Torn;
        Dirty(envelope, envelopeComp);

        if (envelopeComp.TearSound != null)
            _audio.PlayPvs(envelopeComp.TearSound, envelope);

        if (!eject || !_slots.TryGetSlot(envelope, envelopeComp.SlotId, out var slot))
            return;

        _slots.TryEjectToHands(envelope, slot, user);
    }

    private int ReadCouponCount(EntityUid envelope, EnvelopeComponent envelopeComp)
    {
        if (!_slots.TryGetSlot(envelope, envelopeComp.SlotId, out var slot) || slot.Item is not { } stack)
            return 0;

        return TryComp<Content.Shared.Stacks.StackComponent>(stack, out var stackComp) ? stackComp.Count : 0;
    }

    private void DestroyCoupons(EntityUid envelope, EnvelopeComponent envelopeComp)
    {
        if (!_slots.TryGetSlot(envelope, envelopeComp.SlotId, out var slot) || slot.Item is not { } stack)
            return;

        _stacks.SetCount(stack, 0);
    }
}
