using System.Globalization;
using Content.Shared.Alert;
using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Grosse.Armor;

/// <summary>
/// Charged armor adds protection and spends cell charge when the wearer is hit.
/// </summary>
public sealed partial class PoweredArmorSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PoweredArmorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PoweredArmorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PoweredArmorComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<PoweredArmorComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<PoweredArmorComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<PoweredArmorComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<PoweredArmorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PoweredArmorComponent, ArmorExamineEvent>(OnArmorExamine);
        SubscribeLocalEvent<PoweredArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<PoweredArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
    }

    private void OnStartup(Entity<PoweredArmorComponent> ent, ref ComponentStartup args)
    {
        UpdateWornState(ent, TryGetWearer(ent, out var wearer) ? wearer : null, playSounds: false);
    }

    private void OnShutdown(Entity<PoweredArmorComponent> ent, ref ComponentShutdown args)
    {
        if (!_net.IsServer || !TryGetWearer(ent, out var wearer))
            return;

        _alerts.ClearAlertCategory(wearer, ent.Comp.AlertCategory);
    }

    private void OnEquipped(Entity<PoweredArmorComponent> ent, ref ClothingGotEquippedEvent args)
    {
        UpdateWornState(ent, args.Wearer, playSounds: true);
    }

    private void OnUnequipped(Entity<PoweredArmorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!_net.IsServer || _timing.ApplyingState)
            return;

        if (ent.Comp.Active)
            _audio.PlayPvs(ent.Comp.PowerOffSound, ent);

        ent.Comp.Active = false;
        _alerts.ClearAlertCategory(args.Wearer, ent.Comp.AlertCategory);
    }

    private void OnPowerCellChanged(Entity<PoweredArmorComponent> ent, ref PowerCellChangedEvent args)
    {
        UpdateWornState(ent, TryGetWearer(ent, out var wearer) ? wearer : null, playSounds: true);
    }

    private void OnChargeChanged(Entity<PoweredArmorComponent> ent, ref ChargeChangedEvent args)
    {
        UpdateWornState(ent, TryGetWearer(ent, out var wearer) ? wearer : null, playSounds: true);
    }

    private void OnExamined(Entity<PoweredArmorComponent> ent, ref ExaminedEvent args)
    {
        var bonus = MathF.Round(ent.Comp.CoefficientBonus * 100f, 1);

        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
        {
            args.PushMarkup(Loc.GetString("powered-armor-examine-no-cell"));
            return;
        }

        var chargePercent = MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 100f);
        if (chargePercent <= 0f)
        {
            args.PushMarkup(Loc.GetString("powered-armor-examine-empty"));
            return;
        }

        args.PushMarkup(Loc.GetString("powered-armor-examine-charged",
            ("charge", chargePercent.ToString("0", CultureInfo.InvariantCulture)),
            ("bonus", bonus.ToString("0.#", CultureInfo.InvariantCulture))));
    }

    private void OnArmorExamine(Entity<PoweredArmorComponent> ent, ref ArmorExamineEvent args)
    {
        if (!IsShieldActive(ent) || !TryComp<ArmorComponent>(ent, out var armor))
            return;

        args.Msg.Clear();
        WriteArmorExamine(args.Msg, EffectiveModifiers(armor, ent.Comp.CoefficientBonus));
    }

    private void OnDamageModify(Entity<PoweredArmorComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!IsShieldActive(ent))
            return;

        ApplyBonus(ent, ref args.Args.Damage);

        if (!_net.IsServer || ent.Comp.EnergyPerDamage <= 0f || !args.Args.OriginalDamage.AnyPositive())
            return;

        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            return;

        var incoming = 0f;
        foreach (var value in args.Args.OriginalDamage.DamageDict.Values)
        {
            if (value > FixedPoint2.Zero)
                incoming += value.Float();
        }

        if (incoming <= 0f)
            return;

        _battery.UseCharge(battery.Value.AsNullable(), incoming * ent.Comp.EnergyPerDamage);
    }

    private void OnCoefficientQuery(Entity<PoweredArmorComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        if (!IsShieldActive(ent) || !TryComp<ArmorComponent>(ent, out var armor))
            return;

        var coefficients = args.Args.DamageModifiers.Coefficients;
        foreach (var (type, coefficient) in armor.Modifiers.Coefficients)
        {
            if (coefficient <= 0f)
                continue;

            var extra = Math.Max(0f, coefficient - ent.Comp.CoefficientBonus) / coefficient;
            coefficients[type] = coefficients.TryGetValue(type, out var existing) ? existing * extra : extra;
        }
    }

    private void UpdateWornState(Entity<PoweredArmorComponent> ent, EntityUid? wearer, bool playSounds)
    {
        if (!_net.IsServer || _timing.ApplyingState)
            return;

        var hasCell = TryGetBatteryCharge(ent, out var charge, out var maxCharge);
        var active = wearer != null && hasCell && charge > 0f;

        if (playSounds && active != ent.Comp.Active)
            _audio.PlayPvs(active ? ent.Comp.PowerOnSound : ent.Comp.PowerOffSound, ent);

        ent.Comp.Active = active;

        if (wearer == null)
            return;

        if (!hasCell)
        {
            _alerts.ShowAlert(wearer.Value, ent.Comp.NoCellAlert);
            return;
        }

        var fraction = maxCharge <= 0f ? 0f : charge / maxCharge;
        var severity = (short)Math.Clamp(MathF.Round(fraction * 10f), 0f, 10f);
        if (severity == 0 && charge > 0f)
            severity = 1;

        _alerts.ShowAlert(wearer.Value, ent.Comp.ChargeAlert, severity);
    }

    private bool IsShieldActive(EntityUid uid)
    {
        return _clothing.IsEquipped(uid)
               && TryGetBatteryCharge(uid, out var charge, out _)
               && charge > 0f;
    }

    private bool TryGetWearer(EntityUid armor, out EntityUid wearer)
    {
        if (!_clothing.IsEquipped(armor)
            || !_container.TryGetContainingContainer((armor, null, null), out var container))
        {
            wearer = default;
            return false;
        }

        wearer = container.Owner;
        return true;
    }

    private bool TryGetBatteryCharge(EntityUid uid, out float charge, out float maxCharge)
    {
        charge = 0f;
        maxCharge = 0f;

        if (!_powerCell.TryGetBatteryFromSlot(uid, out var battery))
            return false;

        charge = _battery.GetCharge(battery.Value.AsNullable());
        maxCharge = battery.Value.Comp.MaxCharge;
        return true;
    }

    private void ApplyBonus(Entity<PoweredArmorComponent> ent, ref DamageSpecifier damage)
    {
        if (!TryComp<ArmorComponent>(ent, out var armor))
            return;

        var extra = new DamageModifierSet();
        foreach (var (type, coefficient) in armor.Modifiers.Coefficients)
        {
            if (coefficient <= 0f)
                continue;

            extra.Coefficients[type] = Math.Max(0f, coefficient - ent.Comp.CoefficientBonus) / coefficient;
        }

        if (extra.Coefficients.Count == 0)
            return;

        damage = DamageSpecifier.ApplyModifierSet(damage, extra);
    }

    private static DamageModifierSet EffectiveModifiers(ArmorComponent armor, float bonus)
    {
        var modifiers = new DamageModifierSet();
        foreach (var (type, coefficient) in armor.Modifiers.Coefficients)
            modifiers.Coefficients[type] = Math.Max(0f, coefficient - bonus);

        foreach (var (type, flat) in armor.Modifiers.FlatReduction)
            modifiers.FlatReduction[type] = flat;

        return modifiers;
    }

    private void WriteArmorExamine(FormattedMessage message, DamageModifierSet modifiers)
    {
        message.AddMarkupOrThrow(Loc.GetString("armor-examine"));

        foreach (var (type, coefficient) in modifiers.Coefficients)
        {
            message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", Loc.GetString("armor-damage-type-" + type.ToLower())),
                ("value", MathF.Round((1f - coefficient) * 100f, 1))));
        }

        foreach (var (type, flat) in modifiers.FlatReduction)
        {
            message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                ("type", Loc.GetString("armor-damage-type-" + type.ToLower())),
                ("value", flat)));
        }
    }
}
