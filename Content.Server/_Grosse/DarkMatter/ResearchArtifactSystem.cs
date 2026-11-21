using System;
using Content.Shared._Grosse.DarkMatter;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.DarkMatter;

/// <summary>
/// Runs a formula embedded in a crystal. This is the only place where a player-created graph
/// affects the world; it is interval-based and energy-limited.
/// </summary>
public sealed partial class ResearchArtifactSystem : EntitySystem
{
    [Dependency] private EffectGraphSystem _graphs = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> RadiationDamageType = "Radiation";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchArtifactComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<ResearchArtifactComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<ResearchArtifactComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnEquipped(EntityUid uid, ResearchArtifactComponent artifact, GotEquippedHandEvent args)
    {
        artifact.CrystalOwner = args.User;
    }

    private void OnUnequipped(EntityUid uid, ResearchArtifactComponent artifact, GotUnequippedHandEvent args)
    {
        if (artifact.CrystalOwner == args.User)
            artifact.CrystalOwner = null;
    }

    /// <summary>
    /// Gives the owner a simple command channel: use the crystal in hand to pause or resume all
    /// its properties. This is the physical implementation of the “owner command” event, without
    /// asking the player to build a trigger graph.
    /// </summary>
    private void OnActivate(EntityUid uid, ResearchArtifactComponent artifact, ActivateInWorldEvent args)
    {
        if (artifact.CrystalOwner != args.User)
            return;

        artifact.Enabled = !artifact.Enabled;
        _popup.PopupEntity(
            artifact.Enabled ? "Кристалл активирован." : "Кристалл переведён в режим ожидания.",
            uid,
            args.User);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ResearchArtifactComponent>();

        while (query.MoveNext(out var uid, out var artifact))
        {
            if (!artifact.Enabled || artifact.Graph.Nodes.Count == 0)
                continue;

            if (artifact.TickInterval <= 0f)
                continue;

            artifact.Accumulator += frameTime;
            if (artifact.Accumulator < artifact.TickInterval)
                continue;

            var interval = artifact.Accumulator;
            artifact.Accumulator = 0f;

            if (!EffectGraphValidator.Validate(artifact.Graph, out var errors))
            {
                artifact.LastError = string.Join(" ", errors);
                artifact.Stability = MathF.Max(0f, artifact.Stability - 1f);
                continue;
            }

            var result = _graphs.Execute(
                uid,
                artifact.Graph,
                artifact.CrystalOwner,
                artifact.Energy,
                artifact.MaxEnergy,
                applyEffects: true);

            artifact.LastError = result.Error;
            artifact.Energy = Math.Clamp(artifact.Energy - result.EnergyCost + result.EnergyGained,
                0f, artifact.MaxEnergy);

            if (result.Markers.Count > 0 &&
                artifact.CrystalOwner is { } owner &&
                _players.TryGetSessionByEntity(owner, out var session))
            {
                RaiseNetworkEvent(
                    new ResearchMarkerEvent(GetNetEntity(uid), result.Markers.ToArray(), 1.5f),
                    session.Channel);
            }

            var complexity = artifact.Graph.Nodes.Count * 0.002f;
            artifact.Degradation += complexity * interval;

            // Every joule spent is partly converted into heat. Thermal resistance raises the
            // threshold, but a dense formula can still overheat a crystal.
            var thermalConductivity = TryComp<ResearchMaterialComponent>(uid, out var crystalMaterial)
                ? Math.Clamp(crystalMaterial.ThermalConductivity, 0.1f, 10f)
                : 1f;
            artifact.Heat = Math.Clamp(
                artifact.Heat + result.EnergyCost * 3f -
                artifact.Heat * (0.02f + thermalConductivity * 0.01f) * interval - interval,
                0f,
                artifact.MaxHeat * 2f);

            var overheated = artifact.Heat > artifact.MaxHeat;
            artifact.Stability = Math.Clamp(
                artifact.Stability - artifact.Degradation * 0.1f - (result.Success ? 0f : 1.5f) -
                (overheated ? 2f + artifact.Heat / MathF.Max(1f, artifact.MaxHeat) : 0f),
                0f,
                100f);

            if (overheated && _prototype.TryIndex(HeatDamageType, out var overheatDamage))
            {
                _damageable.TryChangeDamage(uid,
                    new DamageSpecifier(overheatDamage, FixedPoint2.New(2f + artifact.Heat * 0.02f)),
                    ignoreResistances: true);
                artifact.LastError = "Перегрев: кристалл теряет структуру.";
            }

            if (artifact.Energy <= 0.01f || artifact.Stability <= 0f)
            {
                artifact.Enabled = false;
                artifact.LastError = artifact.Stability <= 0f
                    ? "Структура кристалла разрушена нестабильностью."
                    : "Заряд кристалла исчерпан.";

                if (_prototype.TryIndex(HeatDamageType, out var heat))
                {
                    _damageable.TryChangeDamage(uid,
                        new DamageSpecifier(heat, FixedPoint2.New(artifact.Stability <= 0f ? 20f : 5f)),
                        ignoreResistances: true);
                }
            }
            else
            {
                // A radioactive crystal keeps emitting even when no formula node is active.
                if (artifact.RadiationActivity > 0.01f &&
                    _prototype.TryIndex(RadiationDamageType, out var radiation))
                {
                    var origin = Transform(uid);
                    var originPosition = _transform.GetWorldPosition(origin);
                    var nearby = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
                    while (nearby.MoveNext(out var mob, out var mobState, out var mobTransform))
                    {
                        if (mobTransform.MapID != origin.MapID ||
                            (_transform.GetWorldPosition(mobTransform) - originPosition).LengthSquared() > 6.25f ||
                            !_mobState.IsAlive(mob, mobState))
                            continue;

                        _damageable.TryChangeDamage(mob,
                            new DamageSpecifier(radiation,
                                FixedPoint2.New(artifact.RadiationActivity * 0.05f * interval)),
                            ignoreResistances: true, origin: uid);
                    }
                }

                // Slow passive recharge while the artifact is idle.
                artifact.Energy = Math.Min(artifact.MaxEnergy,
                    artifact.Energy + artifact.RechargeRate * interval);
            }
        }
    }

    public void Configure(
        EntityUid uid,
        EffectGraph graph,
        EntityUid? owner,
        ResearchMaterialComponent? material,
        ResearchArtifactComponent? artifact = null)
    {
        if (!Resolve(uid, ref artifact))
            return;

        if (graph == null)
        {
            artifact.Enabled = false;
            artifact.LastError = "Формула отсутствует.";
            return;
        }

        _graphs.ResetState(uid);
        artifact.Graph = graph.Clone();
        artifact.CrystalOwner = owner;
        artifact.MaxEnergy = Math.Clamp(material?.EnergyCapacity ?? 100f, 1f, 100000f);
        artifact.EffectEfficiency = Math.Clamp(material?.EffectEfficiency ?? 1f, 0.1f, 5f);
        artifact.RadiationActivity = Math.Clamp(material?.RadiationActivity ?? 0f, 0f, 20f);
        artifact.MaxHeat = 80f + Math.Clamp(material?.ThermalResistance ?? 1f, 0f, 10f) * 30f;
        artifact.Heat = 0f;

        if (material != null)
        {
            var crystalMaterial = EnsureComp<ResearchMaterialComponent>(uid);
            crystalMaterial.MaterialName = material.MaterialName;
            crystalMaterial.Density = material.Density;
            crystalMaterial.Strength = material.Strength;
            crystalMaterial.Hardness = material.Hardness;
            crystalMaterial.ThermalConductivity = material.ThermalConductivity;
            crystalMaterial.ElectricalConductivity = material.ElectricalConductivity;
            crystalMaterial.ThermalResistance = material.ThermalResistance;
            crystalMaterial.RadiationResistance = material.RadiationResistance;
            crystalMaterial.EnergyCapacity = material.EnergyCapacity;
            crystalMaterial.Stability = material.Stability;
            crystalMaterial.Reactivity = material.Reactivity;
            crystalMaterial.Degradation = material.Degradation;
            crystalMaterial.Purity = material.Purity;
            crystalMaterial.DarkMatter = material.DarkMatter;
            crystalMaterial.RadiationActivity = material.RadiationActivity;
            crystalMaterial.EffectEfficiency = material.EffectEfficiency;
        }

        artifact.Energy = artifact.MaxEnergy;
        artifact.Stability = Math.Clamp(
            (material?.Stability ?? 100f) * 0.75f +
            (material?.Strength ?? 1f) * 4f +
            (material?.Hardness ?? 1f) * 2f,
            1f,
            100f);
        artifact.Degradation = Math.Clamp(material?.Degradation ?? 0f, 0f, 5f);
        artifact.Enabled = true;
        artifact.LastError = string.Empty;
        artifact.Accumulator = 0f;
    }
}
