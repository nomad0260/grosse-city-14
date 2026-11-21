using System;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.PatchHTN.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.PatchHTN.Systems;

/// <summary>
/// Handles hiding / revealing NPCs that bury themselves via <see cref="BurrowComponent"/>.
/// </summary>
public sealed partial class BurrowSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCIdleTrackingSystem _idle = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;

    /// <summary>
    /// How often a buried NPC checks whether something hostile walked into its sight. A buried mob's plan is
    /// parked on a wait task, so nothing else would ever notice the enemy for it.
    /// </summary>
    private const float ThreatCheckInterval = 0.5f;

    /// <summary>
    /// Upper bound on that check. Vision radii on this fork are large (antlions see 86 tiles), and the point is
    /// a quick "is something hostile close enough to react to" answer, not a full perception sweep.
    /// </summary>
    private const float MaxThreatRange = 32f;

    private float _threatCheckAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BurrowComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BurrowComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        _threatCheckAccumulator += frameTime;
        var checkThreats = _threatCheckAccumulator >= ThreatCheckInterval;

        if (checkThreats)
            _threatCheckAccumulator = 0f;

        var query = EntityQueryEnumerator<BurrowComponent>();
        var now = _timing.CurTime;

        while (query.MoveNext(out var uid, out var burrow))
        {
            if (!burrow.Burrowed)
                continue;

            if (now - burrow.BurrowedAt >= TimeSpan.FromSeconds(burrow.MaxBurrowTime))
            {
                TrySurface(uid, burrow);
                continue;
            }

            if (!checkThreats || !HasHostileInSight(uid))
                continue;

            // Surface and ask for a fresh plan right away: the current plan is sitting on its wait task and
            // would otherwise keep the antlion in the ground until the wait is over.
            TrySurface(uid, burrow);

            if (TryComp<HTNComponent>(uid, out var htn))
                _htn.Replan(htn);
        }
    }

    public bool TryBurrow(EntityUid uid, BurrowComponent? burrow = null)
    {
        if (!Resolve(uid, ref burrow, false) || burrow.Burrowed || TerminatingOrDeleted(uid))
            return false;

        if (!CanBurrowAt(uid, burrow))
            return false;

        burrow.Burrowed = true;
        burrow.BurrowedAt = _timing.CurTime;

        // VisibilityComponent cannot hide a normal-layer entity: the engine's visibility mask always keeps the
        // Normal bit set (VisibilitySystem.GetParentVisibilityMask starts at 1), so removing the layer left
        // buried mobs fully visible. Stealth is the one networked way to make a mob invisible to players.
        burrow.AddedStealth = !HasComp<StealthComponent>(uid);
        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, -1f, stealth);
        _stealth.SetEnabled(uid, true, stealth);

        if (burrow.DigInSound != null)
            _audio.PlayPvs(burrow.DigInSound, uid);

        return true;
    }

    public bool TrySurface(EntityUid uid, BurrowComponent? burrow = null)
    {
        if (!Resolve(uid, ref burrow, false) || !burrow.Burrowed)
            return false;

        burrow.Burrowed = false;

        if (!TerminatingOrDeleted(uid) && burrow.AddedStealth)
            RemComp<StealthComponent>(uid);

        burrow.AddedStealth = false;

        // Any surfacing restarts the idle timer, otherwise the NPC would dig straight back in on its next plan.
        _idle.ResetIdleTime(uid);

        if (burrow.DigOutSound != null)
            _audio.PlayPvs(burrow.DigOutSound, uid);

        return true;
    }

    /// <summary>
    /// Checks <see cref="BurrowComponent.AllowedTiles"/>. An empty list allows any tile.
    /// </summary>
    public bool CanBurrowAt(EntityUid uid, BurrowComponent burrow)
    {
        if (burrow.AllowedTiles.Count == 0)
            return true;

        if (!TryComp(uid, out TransformComponent? xform) ||
            xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        return _map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef) &&
               !tileRef.Tile.IsEmpty &&
               burrow.AllowedTiles.Contains(_tileDefMan[tileRef.Tile.TypeId].ID);
    }

    /// <summary>
    /// Whether a living hostile of this NPC's faction is within sight right now.
    /// </summary>
    private bool HasHostileInSight(EntityUid uid)
    {
        var range = MaxThreatRange;

        if (TryComp<HTNComponent>(uid, out var htn))
        {
            var vision = htn.Blackboard.GetValueOrDefault<float>(
                htn.Blackboard.GetVisionRadiusKey(EntityManager), EntityManager);
            range = MathF.Min(vision, MaxThreatRange);
        }

        if (range <= 0f)
            return false;

        foreach (var hostile in _faction.GetNearbyHostiles(uid, range))
        {
            if (TerminatingOrDeleted(hostile) || !_mobState.IsAlive(hostile))
                continue;

            return true;
        }

        return false;
    }

    private void OnDamageChanged(EntityUid uid, BurrowComponent burrow, DamageChangedEvent args)
    {
        if (args.DamageIncreased && burrow.Burrowed)
            TrySurface(uid, burrow);
    }

    private void OnShutdown(EntityUid uid, BurrowComponent burrow, ComponentShutdown args)
    {
        if (!burrow.Burrowed)
            return;

        burrow.Burrowed = false;

        if (!Terminating(uid) && burrow.AddedStealth)
            RemComp<StealthComponent>(uid);

        burrow.AddedStealth = false;
    }
}
