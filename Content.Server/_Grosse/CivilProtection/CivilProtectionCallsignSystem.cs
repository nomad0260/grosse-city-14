using Content.Server.Mind;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.VoiceMask;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Grosse.CivilProtection;

/// <summary>
/// Issues a civil-protection callsign (<c>0000.05.14</c>) and keeps the gas-mask vocoder on that name.
/// </summary>
public sealed partial class CivilProtectionCallsignSystem : EntitySystem
{
    private const string DepartmentId = "City14CivilProtection";
    private const string DefaultCallsign = "05";
    private const string CitySuffix = "14";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<IdBoundVoiceMaskComponent, ClothingGotEquippedEvent>(OnMaskEquipped);
        SubscribeLocalEvent<IdBoundVoiceMaskComponent, ItemMaskToggledEvent>(OnMaskToggled);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId is not { } jobId || !IsCivilProtectionJob(jobId))
            return;

        if (!_mind.TryGetMind(args.Mob, out var mindId, out _))
            return;

        var serial = GetOrCreateSerial(mindId);
        var callsign = DefaultCallsign;
        if (TryComp<JobPlayTimeRankComponent>(args.Mob, out var rank) && !string.IsNullOrEmpty(rank.Callsign))
            callsign = rank.Callsign;

        var designation = $"{serial:D4}.{callsign}.{CitySuffix}";
        if (!TryGetWornId(args.Mob, out var card))
            return;

        _idCard.TryChangeFullName(card, designation);
        EnsureComp<FixedIdNameComponent>(card);
        SyncMask(args.Mob);
    }

    private void OnMaskEquipped(Entity<IdBoundVoiceMaskComponent> ent, ref ClothingGotEquippedEvent args)
    {
        SyncMask(args.Wearer);
    }

    private void OnMaskToggled(Entity<IdBoundVoiceMaskComponent> ent, ref ItemMaskToggledEvent args)
    {
        if (args.Wearer is { } wearer)
            SyncMask(wearer);
    }

    private bool IsCivilProtectionJob(string jobId)
    {
        return _prototypes.TryIndex<DepartmentPrototype>(DepartmentId, out var department)
               && department.Roles.Contains(jobId);
    }

    private int GetOrCreateSerial(EntityUid mindId)
    {
        if (TryComp<CivilProtectionSerialComponent>(mindId, out var serial))
            return serial.Serial;

        var created = EnsureComp<CivilProtectionSerialComponent>(mindId);
        created.Serial = _random.Next(10000);
        return created.Serial;
    }

    private void SyncMask(EntityUid wearer)
    {
        if (!_inventory.TryGetSlotEntity(wearer, "mask", out var maskUid)
            || !HasComp<IdBoundVoiceMaskComponent>(maskUid)
            || !TryComp<VoiceMaskComponent>(maskUid, out var voice))
            return;

        var coveringFace = !TryComp<MaskComponent>(maskUid, out var mask) || !mask.IsToggled;
        if (!coveringFace)
        {
            voice.Active = false;
            return;
        }

        if (TryGetWornId(wearer, out var card) && !string.IsNullOrWhiteSpace(card.Comp.FullName))
            voice.VoiceMaskName = card.Comp.FullName;

        voice.Active = true;
    }

    private bool TryGetWornId(EntityUid wearer, out Entity<IdCardComponent> card)
    {
        card = default;
        return _inventory.TryGetSlotEntity(wearer, "id", out var idUid)
               && _idCard.TryGetIdCard(idUid.Value, out card);
    }
}
