using System.Diagnostics.CodeAnalysis;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._Grosse.Weapons;

/// <summary>
/// Applies <see cref="GrosseHeldGunBonusComponent"/> to guns in the owner's hands.
/// </summary>
public sealed partial class SharedGrosseHeldGunBonusSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrosseHeldGunBonusComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GrosseHeldGunBonusComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnGunEquipped);
        SubscribeLocalEvent<GunComponent, GotUnequippedHandEvent>(OnGunUnequipped);
    }

    private void OnStartup(Entity<GrosseHeldGunBonusComponent> ent, ref ComponentStartup args)
    {
        RefreshHeldGuns(ent.Owner);
    }

    private void OnShutdown(Entity<GrosseHeldGunBonusComponent> ent, ref ComponentShutdown args)
    {
        RefreshHeldGuns(ent.Owner);
    }

    private void OnGunEquipped(Entity<GunComponent> gun, ref GotEquippedHandEvent args)
    {
        _gun.RefreshModifiers(gun.Owner);
    }

    private void OnGunUnequipped(Entity<GunComponent> gun, ref GotUnequippedHandEvent args)
    {
        // The gun has already left the hand, so the refresh drops the bonus.
        _gun.RefreshModifiers(gun.Owner);
    }

    private void OnGunRefreshModifiers(Entity<GunComponent> gun, ref GunRefreshModifiersEvent args)
    {
        if (!TryGetHeldBonus(gun, out var bonus))
            return;

        args.MinAngle += bonus.MinAngle;
        args.MaxAngle += bonus.MaxAngle;
        args.AngleDecay += bonus.AngleDecay;
        args.AngleIncrease += bonus.AngleIncrease;
    }

    /// <summary>
    /// Startup still has <see cref="ComponentLifeStage.Starting"/>, and shutdown is already
    /// <see cref="ComponentLifeStage.Stopping"/>. Anything before stopping still grants the bonus,
    /// so a shutdown refresh clears it.
    /// </summary>
    private bool TryGetHeldBonus(EntityUid gun, [NotNullWhen(true)] out GrosseHeldGunBonusComponent? bonus)
    {
        bonus = null;

        if (!_container.TryGetContainingContainer((gun, null, null), out var container) ||
            !TryComp(container.Owner, out bonus) ||
            bonus.LifeStage >= ComponentLifeStage.Stopping ||
            !_hands.IsHolding(container.Owner, gun))
        {
            bonus = null;
            return false;
        }

        return true;
    }

    private void RefreshHeldGuns(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<GunComponent>(held))
                _gun.RefreshModifiers(held);
        }
    }
}
