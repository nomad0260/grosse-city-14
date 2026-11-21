using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._Grosse.DarkMatter;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Robust.Shared.Player;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.DarkMatter;

public sealed class EffectExecutionResult
{
    public bool Success;
    public string Error = string.Empty;
    public float EnergyCost;
    public float EnergyGained;
    public readonly List<Vector2> Markers = new();
    public readonly List<string> Applied = new();
}

/// <summary>
/// Server-authoritative interpreter for the unrestricted (but typed) operation graph. Player input
/// is never compiled or evaluated as code; only registered operations can run.
/// </summary>
public sealed partial class EffectGraphSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedTemperatureSystem _temperature = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> RadiationDamageType = "Radiation";

    private readonly Dictionary<(EntityUid Artifact, int Node), TimeSpan> _timerState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), float> _accumulatorState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), float> _memoryState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), bool> _latchState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), bool> _toggleState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), bool> _edgeState = new();
    private readonly HashSet<(EntityUid Artifact, int Node)> _onceState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), int> _sequenceState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), int> _executionCount = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), TimeSpan> _firstSeen = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), TimeSpan> _delayState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), TimeSpan> _pulseState = new();
    private readonly Dictionary<(EntityUid Artifact, int Node), TimeSpan> _actionCooldown = new();

    private const float MaxEffectMagnitude = 250f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchArtifactComponent, ComponentShutdown>(OnArtifactShutdown);
    }

    private void OnArtifactShutdown(EntityUid uid, ResearchArtifactComponent artifact, ComponentShutdown args)
    {
        ResetState(uid);
    }

    /// <summary>
    /// Drops all per-node runtime state for an artifact. Entity ids can be reused after deletion,
    /// so stale timers or latches must not leak into a newly spawned crystal.
    /// </summary>
    public void ResetState(EntityUid artifact)
    {
        RemoveKeys(_timerState, artifact);
        RemoveKeys(_accumulatorState, artifact);
        RemoveKeys(_memoryState, artifact);
        RemoveKeys(_latchState, artifact);
        RemoveKeys(_toggleState, artifact);
        RemoveKeys(_edgeState, artifact);
        RemoveKeys(_sequenceState, artifact);
        RemoveKeys(_executionCount, artifact);
        RemoveKeys(_firstSeen, artifact);
        RemoveKeys(_delayState, artifact);
        RemoveKeys(_pulseState, artifact);
        RemoveKeys(_actionCooldown, artifact);
        _onceState.RemoveWhere(key => key.Artifact == artifact);
    }

    private static void RemoveKeys<T>(Dictionary<(EntityUid Artifact, int Node), T> state, EntityUid artifact)
    {
        foreach (var key in state.Keys.Where(key => key.Artifact == artifact).ToList())
            state.Remove(key);
    }

    private static readonly Dictionary<string, Func<float, float>> UnaryMath = new(StringComparer.Ordinal)
    {
        ["tan"] = MathF.Tan,
        ["asin"] = MathF.Asin,
        ["acos"] = MathF.Acos,
        ["atan"] = MathF.Atan,
        ["sinh"] = MathF.Sinh,
        ["cosh"] = MathF.Cosh,
        ["tanh"] = MathF.Tanh,
        ["floor"] = MathF.Floor,
        ["ceil"] = MathF.Ceiling,
        ["round"] = MathF.Round,
        ["trunc"] = MathF.Truncate,
        ["sign"] = value => (float) MathF.Sign(value),
        ["sqrt"] = value => value < 0f ? 0f : MathF.Sqrt(value),
        ["cbrt"] = MathF.Cbrt,
        ["ln"] = value => value <= 0f ? 0f : MathF.Log(value),
        ["log10"] = value => value <= 0f ? 0f : MathF.Log10(value),
        ["exp"] = MathF.Exp,
        ["fract"] = value => value - MathF.Floor(value),
        ["negate"] = value => -value,
        ["reciprocal"] = value => MathF.Abs(value) < 0.0001f ? 0f : 1f / value,
        ["saturate"] = value => Math.Clamp(value, 0f, 1f),
        ["clamp01"] = value => Math.Clamp(value, 0f, 1f),
    };

    private static readonly Dictionary<string, Func<float, float, float>> BinaryMath = new(StringComparer.Ordinal)
    {
        ["atan2"] = MathF.Atan2,
        ["hypot"] = (a, b) => MathF.Sqrt(a * a + b * b),
        ["mod"] = (a, b) => MathF.Abs(b) < 0.0001f ? 0f : a - MathF.Floor(a / b) * b,
        ["step"] = (a, b) => a >= b ? 1f : 0f,
        ["distance1d"] = (a, b) => MathF.Abs(a - b),
        ["angle_diff"] = (a, b) =>
        {
            var difference = (a - b) % MathF.Tau;
            if (difference > MathF.PI)
                difference -= MathF.Tau;
            else if (difference < -MathF.PI)
                difference += MathF.Tau;
            return difference;
        },
        ["average"] = (a, b) => (a + b) * 0.5f,
        ["percentage"] = (a, b) => MathF.Abs(b) < 0.0001f ? 0f : Math.Clamp(a / b, 0f, 1f),
        ["ratio"] = (a, b) => MathF.Abs(b) < 0.0001f ? 0f : a / b,
        ["weighted"] = (a, b) => a * b,
    };

    private static readonly Dictionary<string, Func<float, float, float, float>> TernaryMath = new(StringComparer.Ordinal)
    {
        ["lerp"] = (a, b, t) => a + (b - a) * t,
        ["smoothstep"] = (a, b, t) =>
        {
            t = Math.Clamp(t, 0f, 1f);
            t = t * t * (3f - 2f * t);
            return a + (b - a) * t;
        },
        ["wrap"] = (a, b, c) =>
        {
            var span = c - b;
            if (MathF.Abs(span) < 0.0001f)
                return b;

            var offset = (a - b) % span;
            if (offset < 0f)
                offset += span;
            return b + offset;
        },
        ["remap"] = (a, b, c) => b + (c - b) * a,
    };

    private static readonly Dictionary<string, Func<bool, bool, bool>> BoolBinary = new(StringComparer.Ordinal)
    {
        ["xor"] = (a, b) => a ^ b,
        ["nand"] = (a, b) => !(a && b),
        ["nor"] = (a, b) => !(a || b),
        ["xnor"] = (a, b) => a == b,
    };

    private static readonly Dictionary<string, Func<float, bool>> NumberPredicates = new(StringComparer.Ordinal)
    {
        ["is_zero"] = value => MathF.Abs(value) < 0.0001f,
        ["is_positive"] = value => value > 0f,
        ["is_negative"] = value => value < 0f,
        ["is_finite"] = float.IsFinite,
    };

    private static readonly Dictionary<string, (string Type, float Sign)> DamageHandlers = new(StringComparer.Ordinal)
    {
        ["burn"] = ("Heat", 1f),
        ["cold_damage"] = ("Cold", 1f),
        ["shock"] = ("Shock", 1f),
        ["cut"] = ("Slash", 1f),
        ["pierce"] = ("Piercing", 1f),
        ["blunt"] = ("Blunt", 1f),
        ["corrosive"] = ("Caustic", 1f),
        ["poison"] = ("Poison", 1f),
        ["poison_damage"] = ("Poison", 1f),
        ["cure_poison"] = ("Poison", -1f),
        ["cellular_damage"] = ("Cellular", 1f),
        ["asphyxiation_damage"] = ("Asphyxiation", 1f),
        ["asphyxiate"] = ("Asphyxiation", 1f),
        ["oxygenate"] = ("Asphyxiation", -1f),
        ["bloodloss_damage"] = ("Bloodloss", 1f),
        ["bleed"] = ("Bloodloss", 1f),
        ["stop_bleeding"] = ("Bloodloss", -1f),
        ["genetic_damage"] = ("Cellular", 1f),
        ["radiation_damage"] = ("Radiation", 1f),
        ["absorb_radiation"] = ("Radiation", -1f),
    };

    private sealed class RuntimeContext
    {
        public required EntityUid Artifact;
        public required EntityUid? Owner;
        public required EffectGraph Graph;
        public required bool ApplyEffects;
        public required float EnergyAvailable;
        public required float MaxEnergy;
        public required float Efficiency;
        public required TimeSpan Now;
        public readonly EffectExecutionResult Result = new();
        public readonly Dictionary<int, Dictionary<string, object?>> Outputs = new();
    }

    public EffectExecutionResult Execute(
        EntityUid artifact,
        EffectGraph graph,
        EntityUid? owner,
        float energyAvailable,
        float maxEnergy = float.PositiveInfinity,
        bool applyEffects = true)
    {
        var result = new EffectExecutionResult();

        if (!EffectGraphValidator.Validate(graph, out var errors))
        {
            result.Error = string.Join(" ", errors);
            return result;
        }

        if (!TryGetTopologicalOrder(graph, out var order))
        {
            result.Error = "Формула содержит цикл.";
            return result;
        }

        var context = new RuntimeContext
        {
            Artifact = artifact,
            Owner = owner,
            Graph = graph,
            ApplyEffects = applyEffects,
            EnergyAvailable = MathF.Max(0f, energyAvailable),
            MaxEnergy = float.IsFinite(maxEnergy) && maxEnergy > 0f
                ? maxEnergy
                : MathF.Max(1f, energyAvailable),
            Efficiency = GetEffectEfficiency(artifact),
            Now = _timing.CurTime,
        };

        foreach (var node in order)
        {
            context.Result.EnergyCost += 0.02f;

            if (!ExecuteNode(context, node))
            {
                // A failed optional node must not abort unrelated branches. A malformed graph is
                // already rejected by validation, so this is a runtime condition such as an invalid
                // target; expose it for the journal and continue.
                if (context.Result.Error.Length == 0)
                    context.Result.Error = $"Операция {node.Operation} не выполнена.";
            }
        }

        result.Error = context.Result.Error;
        result.Success = string.IsNullOrEmpty(result.Error);
        result.EnergyCost = context.Result.EnergyCost;
        result.EnergyGained = context.Result.EnergyGained;
        result.Markers.AddRange(context.Result.Markers);
        result.Applied.AddRange(context.Result.Applied);
        return result;
    }

    private static bool TryGetTopologicalOrder(EffectGraph graph, out List<EffectGraphNode> order)
    {
        order = new List<EffectGraphNode>();
        var byId = graph.Nodes.ToDictionary(node => node.Id);
        var indegree = graph.Nodes.ToDictionary(node => node.Id, _ => 0);
        var outgoing = graph.Nodes.ToDictionary(node => node.Id, _ => new List<int>());

        foreach (var connection in graph.Connections)
        {
            if (!outgoing.ContainsKey(connection.FromNode) || !indegree.ContainsKey(connection.ToNode))
                return false;

            outgoing[connection.FromNode].Add(connection.ToNode);
            indegree[connection.ToNode]++;
        }

        var queue = new Queue<int>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(byId[id]);

            foreach (var target in outgoing[id])
            {
                if (--indegree[target] == 0)
                    queue.Enqueue(target);
            }
        }

        return order.Count == graph.Nodes.Count;
    }

    private bool ExecuteNode(RuntimeContext context, EffectGraphNode node)
    {
        var definition = EffectOperationCatalog.Get(node.Operation);
        if (definition == null)
            return false;

        object? value;
        if (TryExecuteGeneric(context, node, definition.Handler, out var genericValue))
            value = genericValue;
        else
        switch (definition.Handler)
        {
            case "constant":
                value = Param(node, "value", 1f);
                break;
            case "self":
                value = context.Artifact;
                break;
            case "owner":
                value = context.Owner ?? EntityUid.Invalid;
                break;
            case "energy":
                value = MathF.Max(0f, context.EnergyAvailable - context.Result.EnergyCost);
                break;
            case "get_position":
                value = GetPosition(InputEntity(context, node, "entity"));
                break;
            case "find_entities":
                value = FindEntities(context, node);
                break;
            case "timer":
                value = TimerTick(context, node);
                break;
            case "accumulate":
            {
                var key = (context.Artifact, node.Id);
                var previous = _accumulatorState.GetValueOrDefault(key);
                var limit = MathF.Max(1f, Param(node, "limit", 1000f));
                var total = Math.Clamp(previous + InputNumber(context, node, "a"), -limit, limit);
                _accumulatorState[key] = total;
                value = total;
                break;
            }
            case "filter_living":
                value = InputList(context, node, "entities")
                    .Where(uid => Exists(uid) && _mobState.IsAlive(uid))
                    .ToList();
                break;
            case "filter_dead":
                value = InputList(context, node, "entities")
                    .Where(uid => Exists(uid) && TryComp<MobStateComponent>(uid, out _) && !_mobState.IsAlive(uid))
                    .ToList();
                break;
            case "filter_items":
                value = InputList(context, node, "entities")
                    .Where(uid => Exists(uid) && HasComp<ItemComponent>(uid))
                    .ToList();
                break;
            case "filter_structures":
                value = InputList(context, node, "entities")
                    .Where(uid => Exists(uid) && !HasComp<ItemComponent>(uid) && !HasComp<MobStateComponent>(uid))
                    .ToList();
                break;
            case "nearest":
                value = FindNearest(context, node);
                break;
            case "random_target":
            {
                var candidates = InputList(context, node, "entities");
                value = candidates.Count == 0
                    ? EntityUid.Invalid
                    : candidates[Random.Shared.Next(candidates.Count)];
                break;
            }
            case "count":
                value = (float) InputList(context, node, "entities").Count;
                break;
            case "add":
                value = InputNumber(context, node, "a") + InputNumber(context, node, "b");
                break;
            case "subtract":
                value = InputNumber(context, node, "a") - InputNumber(context, node, "b");
                break;
            case "multiply":
                value = InputNumber(context, node, "a") * InputNumber(context, node, "b");
                break;
            case "divide":
                value = SafeDivide(InputNumber(context, node, "a"), InputNumber(context, node, "b"));
                break;
            case "power":
                value = SafePow(InputNumber(context, node, "a"), InputNumber(context, node, "b"));
                break;
            case "root":
                value = SafeRoot(InputNumber(context, node, "a"), InputNumber(context, node, "b"));
                break;
            case "min":
                value = MathF.Min(InputNumber(context, node, "a"), InputNumber(context, node, "b"));
                break;
            case "max":
                value = MathF.Max(InputNumber(context, node, "a"), InputNumber(context, node, "b"));
                break;
            case "abs":
                value = MathF.Abs(InputNumber(context, node, "a"));
                break;
            case "sin":
                value = MathF.Sin(InputNumber(context, node, "a"));
                break;
            case "cos":
                value = MathF.Cos(InputNumber(context, node, "a"));
                break;
            case "clamp":
                value = Math.Clamp(InputNumber(context, node, "a"), InputNumber(context, node, "min"),
                    InputNumber(context, node, "max"));
                break;
            case "normalize":
            {
                var min = InputNumber(context, node, "min");
                var max = InputNumber(context, node, "max");
                value = MathF.Abs(max - min) < 0.0001f
                    ? 0f
                    : Math.Clamp((InputNumber(context, node, "a") - min) / (max - min), 0f, 1f);
                break;
            }
            case "random":
                value = Random.Shared.NextSingle() * (Param(node, "max", 1f) - Param(node, "min", 0f)) +
                    Param(node, "min", 0f);
                break;
            case "greater":
                value = InputNumber(context, node, "a") > InputNumber(context, node, "b");
                break;
            case "less":
                value = InputNumber(context, node, "a") < InputNumber(context, node, "b");
                break;
            case "equal":
                value = MathF.Abs(InputNumber(context, node, "a") - InputNumber(context, node, "b")) < 0.001f;
                break;
            case "not_equal":
                value = MathF.Abs(InputNumber(context, node, "a") - InputNumber(context, node, "b")) >= 0.001f;
                break;
            case "and":
                value = InputBool(context, node, "a") && InputBool(context, node, "b");
                break;
            case "or":
                value = InputBool(context, node, "a") || InputBool(context, node, "b");
                break;
            case "not":
                value = !InputBool(context, node, "a");
                break;
            case "in_range":
            {
                var number = InputNumber(context, node, "a");
                value = number >= InputNumber(context, node, "b") && number <= InputNumber(context, node, "c");
                break;
            }
            case "distance":
                value = (GetPosition(InputEntity(context, node, "entity")) -
                    GetPosition(InputEntity(context, node, "other"))).Length();
                break;
            case "direction":
            {
                var direction = GetPosition(InputEntity(context, node, "other")) -
                    GetPosition(InputEntity(context, node, "entity"));
                value = direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : Vector2.Zero;
                break;
            }
            case "in_radius":
                value = (GetPosition(InputEntity(context, node, "entity")) -
                    GetPosition(InputEntity(context, node, "other"))).Length() <= InputNumber(context, node, "amount");
                break;
            case "filter_range":
            {
                var reference = InputEntity(context, node, "entity");
                if (!Exists(reference))
                    reference = context.Artifact;

                var range = Math.Clamp(InputNumber(context, node, "amount"), 0f, 30f);
                value = InputList(context, node, "entities")
                    .Where(uid => Exists(uid) &&
                                  (_transform.GetWorldPosition(uid) - GetPosition(reference)).Length() <= range)
                    .ToList();
                break;
            }
            case "line_of_sight":
            {
                var from = InputEntity(context, node, "entity");
                var to = InputEntity(context, node, "other");
                var range = Math.Clamp(InputNumber(context, node, "amount"), 0f, 30f);
                value = Exists(from) && Exists(to) &&
                        _interaction.InRangeUnobstructed(from, to, range);
                break;
            }
            case "ignore_obstacles":
                value = InputList(context, node, "entities");
                break;
            case "heal":
                value = ApplyHeal(context, node);
                break;
            case "repair":
                value = ApplyHeal(context, node, "ремонт");
                break;
            case "damage":
                value = ApplyDamage(context, node);
                break;
            case "set_temperature":
                value = ApplyTemperature(context, node);
                break;
            case "radiation":
                value = ApplyRadiation(context, node);
                break;
            case "absorb_radiation_area":
                value = ApplyRadiationAbsorptionArea(context, node);
                break;
            case "impulse":
                value = ApplyImpulse(context, node);
                break;
            case "set_velocity":
                value = ApplyVelocity(context, node);
                break;
            case "move_entity":
                value = ApplyMove(context, node);
                break;
            case "transfer_energy":
                value = ApplyEnergyTransfer(context, node);
                break;
            case "light":
                value = ApplyLight(context, node);
                break;
            case "markers":
                value = ApplyMarkers(context, node);
                break;
            case "consume_energy":
            {
                var amount = MathF.Max(0f, InputNumber(context, node, "amount"));
                if (amount > context.EnergyAvailable - context.Result.EnergyCost)
                {
                    value = false;
                    break;
                }

                context.Result.EnergyCost += amount;
                value = true;
                break;
            }
            case "store_energy":
            {
                // Free energy was the main exploit: a two-node graph could recharge a crystal
                // forever. Storing is now rate-limited, capped per tick and disabled when full.
                if (context.EnergyAvailable >= context.MaxEnergy * 0.9f ||
                    !TryActionCooldown(context, node, 5f))
                {
                    value = 0f;
                    break;
                }

                var amount = Math.Clamp(InputNumber(context, node, "amount"), 0f, context.MaxEnergy * 0.02f);
                context.Result.EnergyGained = MathF.Min(
                    context.Result.EnergyGained + amount,
                    context.MaxEnergy * 0.05f);
                value = amount;
                break;
            }
            case "health":
                value = GetDamage(InputEntity(context, node, "entity"));
                break;
            case "temperature":
                value = GetTemperature(InputEntity(context, node, "entity"));
                break;
            case "velocity":
            case "get_velocity":
                value = GetVelocity(InputEntity(context, node, "entity"));
                break;
            case "self_stability":
                value = SelfStat(context, material => material.Stability, 100f);
                break;
            case "self_density":
                value = SelfStat(context, material => material.Density, 1f);
                break;
            case "self_reactivity":
                value = SelfStat(context, material => material.Reactivity, 1f);
                break;
            case "self_purity":
                value = SelfStat(context, material => material.Purity, 1f);
                break;
            case "self_dark_matter":
                value = SelfStat(context, material => material.DarkMatter, 0f);
                break;
            case "self_efficiency":
                value = SelfStat(context, material => material.EffectEfficiency, 1f);
                break;
            case "tick":
            case "time":
            case "tick_count":
            case "lifetime":
            {
                var key = (context.Artifact, node.Id);
                if (!_firstSeen.TryGetValue(key, out var firstSeen))
                {
                    firstSeen = context.Now;
                    _firstSeen[key] = firstSeen;
                }

                var count = _executionCount.GetValueOrDefault(key) + 1;
                _executionCount[key] = count;
                value = node.Operation == "tick" || node.Operation == "tick_count"
                    ? count
                    : (float) (context.Now - firstSeen).TotalSeconds;
                break;
            }
            case "is_alive":
                value = Exists(InputEntity(context, node, "entity")) &&
                        _mobState.IsAlive(InputEntity(context, node, "entity"));
                break;
            case "is_dead":
                value = Exists(InputEntity(context, node, "entity")) &&
                        _mobState.IsDead(InputEntity(context, node, "entity"));
                break;
            case "is_item":
                value = Exists(InputEntity(context, node, "entity")) &&
                        HasComp<ItemComponent>(InputEntity(context, node, "entity"));
                break;
            case "is_structure":
            {
                var target = InputEntity(context, node, "entity");
                value = Exists(target) && !HasComp<ItemComponent>(target) && !HasComp<MobStateComponent>(target);
                break;
            }
            case "has_health":
                value = HasComp<DamageableComponent>(InputEntity(context, node, "entity"));
                break;
            case "entity_exists":
                value = Exists(InputEntity(context, node, "entity"));
                break;
            case "same_entity":
                value = InputEntity(context, node, "entity") == InputEntity(context, node, "other");
                break;
            case "is_owner":
                value = context.Owner is { } owner && InputEntity(context, node, "entity") == owner;
                break;
            case "always":
                value = true;
                break;
            case "never":
                value = false;
                break;
            case "orbit_position":
            {
                var entity = InputEntity(context, node, "entity");
                if (!Exists(entity))
                {
                    value = Vector2.Zero;
                    break;
                }

                var center = GetPosition(entity);
                var radius = Math.Clamp(InputNumber(context, node, "amount"), 0f, 30f);
                var angle = InputNumber(context, node, "b");
                value = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                break;
            }
            case "position_offset":
            {
                var entity = InputEntity(context, node, "entity");
                value = Exists(entity)
                    ? GetPosition(entity) + InputVector(context, node, "vector")
                    : Vector2.Zero;
                break;
            }
            case "pull":
                value = ApplyPull(context, node, toward: true);
                break;
            case "push":
                value = ApplyPull(context, node, toward: false);
                break;
            case "teleport_entity":
                value = ApplyTeleport(context, node);
                break;
            case "swap_positions":
                value = ApplySwap(context, node);
                break;
            case "cooldown":
                value = TimerTick(context, node, "interval");
                break;
            case "every_n":
                value = TimerTick(context, node, "period");
                break;
            case "delay":
                value = ApplyDelay(context, node);
                break;
            case "pulse":
                value = ApplyPulse(context, node);
                break;
            case "once":
            {
                var key = (context.Artifact, node.Id);
                value = _onceState.Add(key);
                break;
            }
            case "sequence":
            {
                var key = (context.Artifact, node.Id);
                var next = _sequenceState.GetValueOrDefault(key) + 1;
                var limit = Math.Max(1, (int) Param(node, "limit", 100f));
                if (next > limit)
                    next = 1;
                _sequenceState[key] = next;
                value = (float) next;
                break;
            }
            case "phase":
            {
                var period = MathF.Max(0.2f, Param(node, "period", 4f));
                value = MathF.Sin((float) context.Now.TotalSeconds / period * MathF.Tau) * 0.5f + 0.5f;
                break;
            }
            case "clock":
                value = (float) context.Now.TotalSeconds;
                break;
            case "farthest":
                value = FindFarthest(context, node);
                break;
            case "first":
            {
                var list = InputList(context, node, "entities");
                value = list.Count > 0 ? list[0] : EntityUid.Invalid;
                break;
            }
            case "last":
            {
                var list = InputList(context, node, "entities");
                value = list.Count > 0 ? list[^1] : EntityUid.Invalid;
                break;
            }
            case "limit":
            {
                var count = Math.Clamp((int) InputNumber(context, node, "amount"), 0, 128);
                value = InputList(context, node, "entities").Take(count).ToList();
                break;
            }
            case "shuffle":
            {
                var list = InputList(context, node, "entities").ToList();
                for (var i = list.Count - 1; i > 0; i--)
                {
                    var j = Random.Shared.Next(i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }

                value = list;
                break;
            }
            case "sort_distance":
            {
                var reference = InputEntity(context, node, "entity");
                if (!Exists(reference))
                    reference = context.Artifact;
                var origin = GetPosition(reference);
                value = InputList(context, node, "entities")
                    .OrderBy(uid => (GetPosition(uid) - origin).LengthSquared())
                    .ToList();
                break;
            }
            case "deduplicate":
                value = InputList(context, node, "entities").Distinct().ToList();
                break;
            case "merge_lists":
            {
                var merged = InputList(context, node, "entities").ToList();
                merged.AddRange(InputList(context, node, "list_b"));
                value = merged.Distinct().ToList();
                break;
            }
            case "exclude_self":
                value = InputList(context, node, "entities")
                    .Where(uid => uid != context.Artifact).ToList();
                break;
            case "exclude_owner":
                value = InputList(context, node, "entities")
                    .Where(uid => context.Owner is not { } owner || uid != owner).ToList();
                break;
            case "filter_damaged":
                value = InputList(context, node, "entities")
                    .Where(uid => Exists(uid) && GetDamage(uid) > 0.01f).ToList();
                break;
            case "filter_healthy":
                value = InputList(context, node, "entities")
                    .Where(uid => Exists(uid) && GetDamage(uid) <= 0.01f).ToList();
                break;
            case "filter_players":
                value = InputList(context, node, "entities")
                    .Where(uid => Exists(uid) && HasComp<ActorComponent>(uid)).ToList();
                break;
            case "filter_friendly":
                value = FilterByFaction(context, node, friendly: true);
                break;
            case "filter_hostile":
                value = FilterByFaction(context, node, friendly: false);
                break;
            case "list_contains":
                value = InputList(context, node, "entities").Contains(InputEntity(context, node, "entity"));
                break;
            case "discharge_battery":
                value = ApplyBattery(context, node, -1f);
                break;
            case "battery_level":
            {
                var target = InputEntity(context, node, "entity");
                value = TryComp<BatteryComponent>(target, out var battery)
                    ? _battery.GetCharge((target, battery))
                    : 0f;
                break;
            }
            case "battery_max":
            {
                var target = InputEntity(context, node, "entity");
                value = TryComp<BatteryComponent>(target, out var battery)
                    ? battery.MaxCharge
                    : 0f;
                break;
            }
            case "emp":
                value = ApplyEmp(context, node);
                break;
            case "overcharge":
                value = ApplyOvercharge(context, node);
                break;
            case "revive":
                value = ApplyMobState(context, node, MobState.Alive);
                break;
            case "kill":
                value = ApplyMobState(context, node, MobState.Dead);
                break;
            case "notify":
                value = ApplyNotify(context, node);
                break;
            case "alarm":
                value = ApplyAlarm(context, node);
                break;
            case "memory_set":
            {
                var key = (context.Artifact, node.Id);
                var stored = InputNumber(context, node, "a");
                _memoryState[key] = stored;
                value = stored;
                break;
            }
            case "memory_get":
                value = _memoryState.GetValueOrDefault((context.Artifact, node.Id));
                break;
            case "memory_add":
            {
                var key = (context.Artifact, node.Id);
                var stored = _memoryState.GetValueOrDefault(key) + InputNumber(context, node, "a");
                _memoryState[key] = stored;
                value = stored;
                break;
            }
            case "memory_subtract":
            {
                var key = (context.Artifact, node.Id);
                var stored = _memoryState.GetValueOrDefault(key) - InputNumber(context, node, "a");
                _memoryState[key] = stored;
                value = stored;
                break;
            }
            case "counter":
            {
                var key = (context.Artifact, node.Id);
                var stored = _memoryState.GetValueOrDefault(key) + 1f;
                _memoryState[key] = stored;
                value = stored;
                break;
            }
            case "latch":
            {
                var key = (context.Artifact, node.Id);
                if (InputBool(context, node, "trigger"))
                    _latchState[key] = true;
                value = _latchState.GetValueOrDefault(key);
                break;
            }
            case "toggle":
            {
                var key = (context.Artifact, node.Id);
                var trigger = InputBool(context, node, "trigger");
                var previousTrigger = _edgeState.GetValueOrDefault(key);
                var output = _toggleState.GetValueOrDefault(key);
                if (trigger && !previousTrigger)
                    output = !output;

                _edgeState[key] = trigger;
                _toggleState[key] = output;
                value = output;
                break;
            }
            case "edge":
            {
                var key = (context.Artifact, node.Id);
                var trigger = InputBool(context, node, "trigger");
                var previousTrigger = _edgeState.GetValueOrDefault(key);
                _edgeState[key] = trigger;
                value = trigger && !previousTrigger;
                break;
            }
            default:
                return false;
        }

        var outputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var port in definition.Outputs)
        {
            if (port.Id == "value")
                outputs[port.Id] = value;
        }

        context.Outputs[node.Id] = outputs;
        return true;
    }

    private bool TryExecuteGeneric(RuntimeContext context, EffectGraphNode node, string handler, out object? value)
    {
        value = null;

        if (UnaryMath.TryGetValue(handler, out var unary))
        {
            var result = unary(InputNumber(context, node, "a"));
            value = float.IsFinite(result) ? result : 0f;
            return true;
        }

        if (BinaryMath.TryGetValue(handler, out var binary))
        {
            var result = binary(InputNumber(context, node, "a"), InputNumber(context, node, "b"));
            value = float.IsFinite(result) ? result : 0f;
            return true;
        }

        if (TernaryMath.TryGetValue(handler, out var ternary))
        {
            var result = ternary(InputNumber(context, node, "a"), InputNumber(context, node, "b"),
                InputNumber(context, node, "c"));
            value = float.IsFinite(result) ? result : 0f;
            return true;
        }

        if (BoolBinary.TryGetValue(handler, out var boolBinary))
        {
            value = boolBinary(InputBool(context, node, "a"), InputBool(context, node, "b"));
            return true;
        }

        if (NumberPredicates.TryGetValue(handler, out var predicate))
        {
            value = predicate(InputNumber(context, node, "a"));
            return true;
        }

        switch (handler)
        {
            case "between":
                value = InputNumber(context, node, "a") >= InputNumber(context, node, "b") &&
                        InputNumber(context, node, "a") <= InputNumber(context, node, "c");
                return true;
            case "vector_add":
                value = InputVector(context, node, "a") + InputVector(context, node, "b");
                return true;
            case "vector_subtract":
                value = InputVector(context, node, "a") - InputVector(context, node, "b");
                return true;
            case "vector_scale":
                value = InputVector(context, node, "a") * InputNumber(context, node, "b");
                return true;
            case "vector_length":
                value = InputVector(context, node, "a").Length();
                return true;
            case "vector_normalize":
                value = Normalize(InputVector(context, node, "a"));
                return true;
            case "vector_dot":
                value = Vector2.Dot(InputVector(context, node, "a"), InputVector(context, node, "b"));
                return true;
            case "vector_cross":
            {
                var a = InputVector(context, node, "a");
                var b = InputVector(context, node, "b");
                value = a.X * b.Y - a.Y * b.X;
                return true;
            }
            case "vector_distance":
                value = (InputVector(context, node, "a") - InputVector(context, node, "b")).Length();
                return true;
            case "vector_angle":
            {
                var a = Normalize(InputVector(context, node, "a"));
                var b = Normalize(InputVector(context, node, "b"));
                value = MathF.Acos(Math.Clamp(Vector2.Dot(a, b), -1f, 1f));
                return true;
            }
            case "vector_rotate":
            {
                var vector = InputVector(context, node, "a");
                var angle = InputNumber(context, node, "b");
                var cos = MathF.Cos(angle);
                var sin = MathF.Sin(angle);
                value = new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
                return true;
            }
            case "vector_from_angle":
            {
                var angle = InputNumber(context, node, "a");
                value = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                return true;
            }
        }

        if (DamageHandlers.TryGetValue(handler, out var damage))
        {
            value = ApplyDamageType(context, node, damage.Type, damage.Sign);
            return true;
        }

        return false;
    }

    private List<EntityUid> FilterByFaction(RuntimeContext context, EffectGraphNode node, bool friendly)
    {
        var list = InputList(context, node, "entities");
        if (context.Owner is not { } owner ||
            !TryComp<NpcFactionMemberComponent>(owner, out var ownerFaction) ||
            ownerFaction.Factions.Count == 0)
        {
            // Factionless owners keep the old behavior instead of silently doing nothing.
            return list;
        }

        var result = new List<EntityUid>(list.Count);
        foreach (var uid in list)
        {
            if (!Exists(uid) || !TryComp<NpcFactionMemberComponent>(uid, out var targetFaction))
                continue;

            var matches = friendly
                ? _factions.IsEntityFriendly((owner, ownerFaction), (uid, targetFaction))
                : IsHostileToOwner(ownerFaction, uid, targetFaction);

            if (matches)
                result.Add(uid);
        }

        return result;
    }

    private bool IsHostileToOwner(
        NpcFactionMemberComponent ownerFaction,
        EntityUid targetUid,
        NpcFactionMemberComponent targetFaction)
    {
        foreach (var faction in ownerFaction.Factions)
        {
            if (_factions.IsFactionHostile(faction.Id, (targetUid, targetFaction)))
                return true;
        }

        return false;
    }

    private float GetDamage(EntityUid uid)
    {
        return Exists(uid) ? _damageable.GetTotalDamage(uid).Float() : 0f;
    }

    private float GetTemperature(EntityUid uid)
    {
        return Exists(uid) && TryComp<TemperatureComponent>(uid, out var temperature)
            ? temperature.CurrentTemperature
            : 293f;
    }

    private Vector2 GetVelocity(EntityUid uid)
    {
        return Exists(uid) && TryComp<PhysicsComponent>(uid, out var physics)
            ? physics.LinearVelocity
            : Vector2.Zero;
    }

    private float SelfStat(RuntimeContext context, Func<ResearchMaterialComponent, float> selector, float fallback)
    {
        return TryComp<ResearchMaterialComponent>(context.Artifact, out var material)
            ? selector(material)
            : fallback;
    }

    private EntityUid FindFarthest(RuntimeContext context, EffectGraphNode node)
    {
        var reference = InputEntity(context, node, "entity");
        if (!Exists(reference))
            reference = context.Artifact;

        var referenceMap = Transform(reference).MapID;
        var origin = GetPosition(reference);
        var farthest = EntityUid.Invalid;
        var farthestDistance = -1f;

        foreach (var candidate in InputList(context, node, "entities"))
        {
            if (!Exists(candidate) || Transform(candidate).MapID != referenceMap)
                continue;

            var distance = (GetPosition(candidate) - origin).LengthSquared();
            if (distance <= farthestDistance)
                continue;

            farthestDistance = distance;
            farthest = candidate;
        }

        return farthest;
    }

    private bool ApplyPull(RuntimeContext context, EffectGraphNode node, bool toward)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<PhysicsComponent>(target, out var physics) ||
            physics.BodyType == BodyType.Static || !OnSameMap(context.Artifact, target))
            return false;

        var amount = MathF.Abs(InputNumber(context, node, "amount")) * context.Efficiency;
        if (amount <= 0.01f)
            return false;

        var direction = GetPosition(context.Artifact) - GetPosition(target);
        if (direction.LengthSquared() < 0.0001f)
            return false;

        if (!SpendEnergy(context, MathF.Max(0.05f, amount * 0.02f)))
            return false;

        direction = Vector2.Normalize(direction);
        if (!toward)
            direction = -direction;

        _physics.ApplyLinearImpulse(target, direction * amount, body: physics);
        context.Result.Applied.Add(toward ? $"притяжение {amount:0.##}" : $"отталкивание {amount:0.##}");
        return true;
    }

    private bool ApplyTeleport(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !OnSameMap(context.Artifact, target) ||
            !TryActionCooldown(context, node, 3f) || !SpendEnergy(context, 0.2f))
            return false;

        _transform.SetWorldPosition(target, InputVector(context, node, "vector"));
        context.Result.Applied.Add("телепортация");
        return true;
    }

    private bool ApplySwap(RuntimeContext context, EffectGraphNode node)
    {
        var first = InputEntity(context, node, "entity");
        var second = InputEntity(context, node, "other");
        if (!Exists(first) || !Exists(second))
            return false;

        // SetWorldPosition keeps each entity on its current map, so swapping across maps would
        // silently collapse both entities onto the first map.
        if (!OnSameMap(first, second))
            return false;

        if (!TryActionCooldown(context, node, 3f) || !SpendEnergy(context, 0.2f))
            return false;

        var firstPosition = GetPosition(first);
        var secondPosition = GetPosition(second);
        _transform.SetWorldPosition(first, secondPosition);
        _transform.SetWorldPosition(second, firstPosition);
        context.Result.Applied.Add("обмен позициями");
        return true;
    }

    private bool ApplyDelay(RuntimeContext context, EffectGraphNode node)
    {
        var key = (context.Artifact, node.Id);
        if (!InputBool(context, node, "trigger"))
        {
            _delayState.Remove(key);
            return false;
        }

        if (!_delayState.TryGetValue(key, out var started))
        {
            _delayState[key] = context.Now;
            return false;
        }

        var delay = TimeSpan.FromSeconds(Math.Clamp(Param(node, "delay", 1f), 0.1f, 30f));
        return context.Now - started >= delay;
    }

    private bool ApplyPulse(RuntimeContext context, EffectGraphNode node)
    {
        var key = (context.Artifact, node.Id);
        if (InputBool(context, node, "trigger"))
            _pulseState[key] = context.Now;

        if (!_pulseState.TryGetValue(key, out var last))
            return false;

        var duration = TimeSpan.FromSeconds(Math.Clamp(Param(node, "duration", 0.5f), 0.1f, 10f));
        return context.Now - last < duration;
    }

    private bool TryActionCooldown(RuntimeContext context, EffectGraphNode node, float seconds)
    {
        var key = (context.Artifact, node.Id);
        var duration = TimeSpan.FromSeconds(Math.Clamp(seconds, 0.1f, 60f));

        if (_actionCooldown.TryGetValue(key, out var last) && context.Now - last < duration)
            return false;

        _actionCooldown[key] = context.Now;
        return true;
    }

    private bool ApplyBattery(RuntimeContext context, EffectGraphNode node, float sign)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<BatteryComponent>(target, out var battery))
            return false;

        var amount = Math.Clamp(MathF.Abs(InputNumber(context, node, "amount")) * context.Efficiency,
            0f, MaxEffectMagnitude);
        if (amount <= 0.01f)
            return false;

        // Draining a battery is not free: the crystal still has to power the effect. The old code
        // passed spend=false, which allowed unlimited free discharges.
        if (!SpendEnergy(context, MathF.Max(0.05f, amount * 0.02f)))
            return false;

        _battery.ChangeCharge((target, battery), sign * amount);
        context.Result.Applied.Add(sign > 0f ? $"заряд {amount:0.##}" : $"разряд {amount:0.##}");
        return true;
    }

    private bool ApplyEmp(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target))
            return false;

        var amount = Math.Clamp(MathF.Abs(InputNumber(context, node, "amount")) * context.Efficiency,
            0f, MaxEffectMagnitude);
        if (amount <= 0.01f || !TryActionCooldown(context, node, 2f) || !SpendEnergy(context, 0.3f))
            return false;
        if (TryComp<BatteryComponent>(target, out var battery))
            _battery.ChangeCharge((target, battery), -MathF.Max(amount * 5f, battery.MaxCharge));

        if (TryComp<DamageableComponent>(target, out _))
        {
            var type = GetDamageType("Shock");
            _damageable.TryChangeDamage(target, new DamageSpecifier(type, FixedPoint2.New(amount * 0.5f)),
                ignoreResistances: true, origin: context.Artifact);
        }

        context.Result.Applied.Add($"ЭМИ {amount:0.##}");
        return true;
    }

    private bool ApplyOvercharge(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<BatteryComponent>(target, out var battery))
            return false;

        var amount = Math.Clamp(MathF.Abs(InputNumber(context, node, "amount")) * context.Efficiency,
            0f, MaxEffectMagnitude);
        if (amount <= 0.01f || !SpendEnergy(context, 0.4f))
            return false;
        _battery.ChangeCharge((target, battery), amount * 2f);

        if (TryComp<DamageableComponent>(target, out _))
        {
            var type = GetDamageType("Heat");
            _damageable.TryChangeDamage(target, new DamageSpecifier(type, FixedPoint2.New(amount * 0.2f)),
                ignoreResistances: true, origin: context.Artifact);
        }

        context.Result.Applied.Add($"перезарядка {amount:0.##}");
        return true;
    }

    private bool ApplyMobState(RuntimeContext context, EffectGraphNode node, MobState state)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<MobStateComponent>(target, out var mobState) ||
            !_interaction.InRangeUnobstructed(context.Artifact, target, 10f) ||
            !TryActionCooldown(context, node, 3f) ||
            !SpendEnergy(context, 0.5f))
            return false;

        _mobState.ChangeMobState(target, state, mobState, context.Artifact);
        context.Result.Applied.Add(state == MobState.Alive ? "оживление" : "смерть");
        return true;
    }

    private bool ApplyNotify(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        if (context.Owner is not { } owner || !SpendEnergy(context, 0.05f))
            return false;

        _popup.PopupEntity("Кристалл передаёт сигнал.", context.Artifact, owner);
        context.Result.Applied.Add("уведомление");
        return true;
    }

    private bool ApplyAlarm(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !SpendEnergy(context, 0.1f))
            return false;

        var energy = MathF.Max(0.1f, InputNumber(context, node, "amount"));
        var light = EnsureComp<PointLightComponent>(target);
        _lights.SetEnabled(target, true, light);
        _lights.SetEnergy(target, energy, light);
        _lights.SetRadius(target, 6f, light);
        context.Result.Applied.Add("тревога");
        return true;
    }

    private bool ApplyDamageType(RuntimeContext context, EffectGraphNode node, string typeId, float sign)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<DamageableComponent>(target, out _))
            return false;

        var amount = Math.Clamp(MathF.Abs(InputNumber(context, node, "amount")) * context.Efficiency,
            0f, MaxEffectMagnitude) * sign;
        if (MathF.Abs(amount) < 0.001f)
            return false;

        if (!SpendEnergy(context, MathF.Max(0.05f, MathF.Abs(amount) * 0.02f)))
            return false;

        var type = GetDamageType(typeId);
        _damageable.TryChangeDamage(target, new DamageSpecifier(type, FixedPoint2.New(amount)),
            ignoreResistances: true, origin: context.Artifact);
        context.Result.Applied.Add($"{type.ID} {amount:0.##}");
        return true;
    }

    private DamageTypePrototype GetDamageType(string id)
    {
        if (_prototype.TryIndex<DamageTypePrototype>(id, out var type))
            return type;

        return _prototype.Index(HeatDamageType);
    }

    private float GetEffectEfficiency(EntityUid artifact)
    {
        if (!TryComp<ResearchMaterialComponent>(artifact, out var material))
            return 1f;

        return Math.Clamp(0.35f + material.EffectEfficiency * 0.45f + material.Reactivity * 0.15f +
                          material.Purity * 0.2f + material.DarkMatter * 0.01f, 0.2f, 3f);
    }

    private static Vector2 Normalize(Vector2 value)
    {
        return value.LengthSquared() > 0.0001f ? Vector2.Normalize(value) : Vector2.Zero;
    }

    private List<EntityUid> FindEntities(RuntimeContext context, EffectGraphNode node)
    {
        var radius = Math.Clamp(Param(node, "radius", 5f), 0f, 30f);
        var result = new List<EntityUid>();
        const int maxTargets = 64;

        foreach (var uid in _lookup.GetEntitiesInRange(context.Artifact, radius,
                     LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries))
        {
            if (uid == context.Artifact || !Exists(uid))
                continue;

            if (Param(node, "through_walls", 1f) < 0.5f &&
                !_interaction.InRangeUnobstructed(context.Artifact, uid, radius))
                continue;

            result.Add(uid);
            if (result.Count >= maxTargets)
                break;
        }

        return result;
    }

    private bool TimerTick(RuntimeContext context, EffectGraphNode node, string parameter = "interval")
    {
        var key = (context.Artifact, node.Id);
        var interval = TimeSpan.FromSeconds(Math.Clamp(Param(node, parameter, 1f), 0.2f, 60f));

        if (_timerState.TryGetValue(key, out var last) && context.Now - last < interval)
            return false;

        _timerState[key] = context.Now;
        return true;
    }

    private EntityUid FindNearest(RuntimeContext context, EffectGraphNode node)
    {
        var reference = InputEntity(context, node, "entity");
        if (!Exists(reference))
            reference = context.Artifact;

        var referencePosition = GetPosition(reference);
        var referenceMap = Transform(reference).MapID;
        var nearest = EntityUid.Invalid;
        var nearestDistance = float.MaxValue;

        foreach (var candidate in InputList(context, node, "entities"))
        {
            if (!Exists(candidate) || Transform(candidate).MapID != referenceMap)
                continue;

            var distance = (GetPosition(candidate) - referencePosition).LengthSquared();
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    private bool ApplyHeal(RuntimeContext context, EffectGraphNode node, string label = "лечение")
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<DamageableComponent>(target, out _))
            return false;

        var amount = Math.Clamp(MathF.Max(0f, InputNumber(context, node, "amount")) *
                                Param(node, "efficiency", 1f) * context.Efficiency,
            0f, MaxEffectMagnitude);
        if (amount <= 0f)
            return false;

        if (!SpendEnergy(context, MathF.Max(0.05f, amount * 0.02f)))
            return false;

        // Negative Blunt damage only reduced the Blunt pool; HealEvenly removes existing damage
        // across all supported types and never creates a negative damage entry.
        _damageable.HealEvenly(target, FixedPoint2.New(-amount), origin: context.Artifact);
        context.Result.Applied.Add($"{label} {amount:0.##}");
        return true;
    }

    private bool ApplyDamage(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<DamageableComponent>(target, out _))
            return false;

        var amount = Math.Clamp(MathF.Max(0f, InputNumber(context, node, "amount")) *
                                Param(node, "efficiency", 1f) * context.Efficiency,
            0f, MaxEffectMagnitude);
        if (amount <= 0f)
            return false;

        if (!SpendEnergy(context, MathF.Max(0.05f, amount * 0.02f)))
            return false;

        var type = _prototype.Index(HeatDamageType);

        _damageable.TryChangeDamage(target, new DamageSpecifier(type, FixedPoint2.New(amount)),
            ignoreResistances: true, origin: context.Artifact);
        context.Result.Applied.Add($"урон {amount:0.##}");
        return true;
    }

    private bool ApplyTemperature(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target))
            return false;

        var amount = Math.Clamp(InputNumber(context, node, "amount") * context.Efficiency, -500f, 500f);
        if (MathF.Abs(amount) < 0.001f)
            return false;

        var temperature = EnsureComp<TemperatureComponent>(target);
        var heatCapacity = _temperature.GetHeatCapacity(target, temperature);
        if (heatCapacity <= 0f)
            return false;

        if (!SpendEnergy(context, MathF.Max(0.05f, MathF.Abs(amount) * 0.005f)))
            return false;

        // ChangeHeat raises OnTemperatureChangeEvent (damage, movement, UI). Assigning
        // CurrentTemperature directly skipped those subscribers.
        _temperature.ChangeHeat(target, amount * heatCapacity, temperature: temperature);
        temperature.CurrentTemperature = Math.Clamp(temperature.CurrentTemperature, 0f, 5000f);
        context.Result.Applied.Add($"температура {amount:+0.##;-0.##} K");
        return true;
    }

    private bool ApplyRadiation(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<DamageableComponent>(target, out _))
            return false;

        var amount = Math.Clamp(MathF.Max(0f, InputNumber(context, node, "amount")) * context.Efficiency,
            0f, MaxEffectMagnitude);
        if (amount <= 0f)
            return false;

        if (!SpendEnergy(context, MathF.Max(0.05f, amount * 0.02f)))
            return false;

        var type = _prototype.Index(RadiationDamageType);
        _damageable.TryChangeDamage(target, new DamageSpecifier(type, FixedPoint2.New(amount)),
            ignoreResistances: true, origin: context.Artifact);
        context.Result.Applied.Add($"радиация {amount:0.##}");
        return true;
    }

    /// <summary>
    /// Removes the complete radiation pool from every living target in the supplied list. Unlike
    /// the single-target absorb operation, this is the area-scale version used by the simple
    /// “Поглощение радиации” constructor card.
    /// </summary>
    private bool ApplyRadiationAbsorptionArea(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var targets = InputList(context, node, "entities")
            .Where(uid => Exists(uid) && TryComp<DamageableComponent>(uid, out _))
            .Distinct()
            .Take(64)
            .ToList();
        if (targets.Count == 0)
            return false;

        var amount = Math.Clamp(MathF.Max(0f, InputNumber(context, node, "amount")) * context.Efficiency,
            0f, MaxEffectMagnitude);
        if (amount <= 0f)
            return false;

        if (!SpendEnergy(context, MathF.Max(0.05f, amount * 0.02f)))
            return false;

        var radiation = _prototype.Index(RadiationDamageType);
        var absorbed = 0;
        foreach (var target in targets)
        {
            if (!TryComp<DamageableComponent>(target, out var damageable))
                continue;

            var positive = _damageable.GetPositiveDamage((target, damageable));
            if (!positive.DamageDict.TryGetValue(radiation.ID, out var current) ||
                current <= FixedPoint2.Zero)
                continue;

            _damageable.TryChangeDamage(target,
                new DamageSpecifier(radiation, -current),
                ignoreResistances: true,
                origin: context.Artifact);
            absorbed++;
        }

        if (absorbed == 0)
            return false;

        context.Result.Applied.Add($"поглощена радиация: {absorbed} целей");
        return true;
    }

    private bool ApplyImpulse(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<PhysicsComponent>(target, out var physics) ||
            physics.BodyType == BodyType.Static || !OnSameMap(context.Artifact, target))
            return false;

        var amount = Math.Clamp(MathF.Max(0f, InputNumber(context, node, "amount")) * context.Efficiency,
            0f, 200f);
        if (amount <= 0f)
            return false;

        if (!SpendEnergy(context, MathF.Max(0.05f, amount * 0.02f)))
            return false;

        var direction = GetPosition(target) - GetPosition(context.Artifact);
        direction = direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        _physics.ApplyLinearImpulse(target, direction * amount, body: physics);

        context.Result.Applied.Add($"импульс {amount:0.##}");
        return true;
    }

    private bool ApplyVelocity(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<PhysicsComponent>(target, out var physics) ||
            physics.BodyType == BodyType.Static || !OnSameMap(context.Artifact, target))
            return false;

        var speed = Math.Clamp(InputNumber(context, node, "amount") * context.Efficiency, 0f, 30f);
        if (speed <= 0f || !SpendEnergy(context, MathF.Max(0.05f, speed * 0.01f)))
            return false;

        var direction = GetPosition(target) - GetPosition(context.Artifact);
        direction = direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        _physics.SetLinearVelocity(target, direction * speed, body: physics);
        context.Result.Applied.Add($"скорость {speed:0.##}");
        return true;
    }

    private bool ApplyMove(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<PhysicsComponent>(target, out var physics) ||
            physics.BodyType == BodyType.Static || !OnSameMap(context.Artifact, target))
            return false;

        var distance = Math.Clamp(InputNumber(context, node, "amount") * context.Efficiency, 0f, 5f);
        if (distance <= 0f || !SpendEnergy(context, MathF.Max(0.05f, distance * 0.05f)))
            return false;

        var direction = GetPosition(target) - GetPosition(context.Artifact);
        direction = direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        _transform.SetWorldPosition(target, GetPosition(target) + direction * distance);
        context.Result.Applied.Add($"перемещение {distance:0.##}");
        return true;
    }

    private bool ApplyEnergyTransfer(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target) || !TryComp<BatteryComponent>(target, out var battery))
            return false;

        var amount = MathF.Max(0f, InputNumber(context, node, "amount")) * context.Efficiency;
        if (amount <= 0f || !SpendEnergy(context, amount))
            return false;

        _battery.ChangeCharge((target, battery), amount);
        context.Result.Applied.Add($"энергия {amount:0.##}");
        return true;
    }

    private bool ApplyLight(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        var target = InputEntity(context, node, "entity");
        if (!Exists(target))
            return false;

        var energy = MathF.Max(0f, InputNumber(context, node, "amount")) * context.Efficiency;
        if (!SpendEnergy(context, MathF.Max(0.05f, energy * 0.01f)))
            return false;

        var light = EnsureComp<PointLightComponent>(target);
        _lights.SetEnabled(target, true, light);
        _lights.SetEnergy(target, energy, light);
        _lights.SetRadius(target, Math.Clamp(Param(node, "radius", 4f), 0f, 20f), light);

        context.Result.Applied.Add($"свет {energy:0.##}");
        return true;
    }

    private bool ApplyMarkers(RuntimeContext context, EffectGraphNode node)
    {
        if (!InputBool(context, node, "trigger", true))
            return false;

        if (!SpendEnergy(context, 0.05f))
            return false;

        var added = 0;
        foreach (var target in InputList(context, node, "entities"))
        {
            if (Exists(target))
            {
                context.Result.Markers.Add(GetPosition(target));
                added++;
            }
        }

        context.Result.Applied.Add($"маркеров {added}");
        return true;
    }

    private bool SpendEnergy(RuntimeContext context, float cost)
    {
        if (cost <= 0f)
            return true;

        if (context.Result.EnergyCost + cost > context.EnergyAvailable + 0.0001f)
        {
            context.Result.Error = "Недостаточно энергии для выполнения операции.";
            return false;
        }

        context.Result.EnergyCost += cost;
        return true;
    }

    private Vector2 GetPosition(EntityUid uid)
    {
        return Exists(uid) ? _transform.GetWorldPosition(uid) : Vector2.Zero;
    }

    private bool OnSameMap(EntityUid first, EntityUid second)
    {
        return Exists(first) && Exists(second) && Transform(first).MapID == Transform(second).MapID;
    }

    private static float Param(EffectGraphNode node, string id, float fallback)
    {
        var value = node.Parameters.GetValueOrDefault(id, fallback);
        return float.IsFinite(value) ? value : fallback;
    }

    private static float SafeDivide(float a, float b)
    {
        return MathF.Abs(b) < 0.0001f ? 0f : a / b;
    }

    private static float SafePow(float a, float b)
    {
        var result = MathF.Pow(a, b);
        return float.IsFinite(result) ? result : 0f;
    }

    private static float SafeRoot(float a, float b)
    {
        if (MathF.Abs(b) < 0.0001f)
            return 0f;

        var rounded = MathF.Round(b);
        var isInteger = MathF.Abs(b - rounded) < 0.0001f;
        var isOdd = isInteger && (int) rounded % 2 != 0;

        if (a < 0f && !isOdd)
            return 0f;

        var result = a < 0f
            ? -MathF.Pow(-a, 1f / b)
            : MathF.Pow(a, 1f / b);
        return float.IsFinite(result) ? result : 0f;
    }

    private object? FindInputValue(RuntimeContext context, EffectGraphNode node, string port)
    {
        var connection = context.Graph.Connections.FirstOrDefault(connection =>
            connection.ToNode == node.Id && connection.ToPort == port);

        if (connection == null)
            return null;

        if (!context.Outputs.TryGetValue(connection.FromNode, out var outputs))
            return null;

        return outputs.GetValueOrDefault(connection.FromPort);
    }

    private float InputNumber(RuntimeContext context, EffectGraphNode node, string port, float fallback = 0f)
    {
        return FindInputValue(context, node, port) is float value && float.IsFinite(value)
            ? value
            : fallback;
    }

    private bool InputBool(RuntimeContext context, EffectGraphNode node, string port, bool fallback = false)
    {
        return FindInputValue(context, node, port) is bool value ? value : fallback;
    }

    private EntityUid InputEntity(RuntimeContext context, EffectGraphNode node, string port)
    {
        return FindInputValue(context, node, port) is EntityUid value ? value : EntityUid.Invalid;
    }

    private Vector2 InputVector(RuntimeContext context, EffectGraphNode node, string port)
    {
        return FindInputValue(context, node, port) is Vector2 value ? value : Vector2.Zero;
    }

    private List<EntityUid> InputList(RuntimeContext context, EffectGraphNode node, string port)
    {
        return FindInputValue(context, node, port) is List<EntityUid> value
            ? value
            : new List<EntityUid>();
    }
}
