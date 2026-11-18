using System;
using Content.Server.NPC.HTN;
using Content.Shared.CCVar;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Grosse: reacts to threats on sight instead of waiting for the periodic re-plan pass.
/// </summary>
/// <remarks>
/// The HTN planner is a shared, time-sliced queue, so with hundreds of NPCs on a map a freshly spawned
/// hostile can wait seconds before the mob in front of it gets a plan that includes it. This system keeps a
/// cheap check running (faction hostiles within a capped range) and asks the planner for a new plan the moment
/// something hostile appears, which lets <see cref="HTNComponent.ConstantlyReplan"/> stay off and stops every
/// NPC from re-planning on a timer for nothing. Scans are incremental (32 entities per tick by default), so the
/// per-tick cost is capped; a full pass over the map takes N/32 ticks at 30 TPS.
/// The budget and the scan interval are CVars (<see cref="CCVars.NPCThreatWakeScans"/>,
/// <see cref="CCVars.NPCThreatWakeInterval"/>), so a crowded server can trade reaction latency for tick time
/// without a rebuild.
/// This runs on the tick thread on purpose: every step below reads the entity manager (container lookup,
/// blackboard, component storage, the faction spatial query) or mutates the planner queue. None of that is
/// thread-safe, so the scan cannot be handed to a worker thread; the planner itself is already time-sliced by
/// <see cref="HTNSystem"/> and does not run here.
/// Sleeping NPCs are deliberately not scanned: they have no plan and no active updates, and the engine wakes
/// them itself (map init, mob state change, player detaching).
/// </remarks>
public sealed partial class NPCThreatWakeSystem : EntitySystem
{
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private const string TargetKey = "Target";

    /// <summary>
    /// Upper bound on the scan radius. Vision radii on this fork are large (antlions see 86 tiles), and the
    /// point of this check is a quick "is something hostile right here" answer, not a full perception sweep.
    /// </summary>
    private const float MaxReactionRange = 12f;

    /// <summary>
    /// How many NPCs one tick may check, so the cost stays flat regardless of the map's NPC count. Read from
    /// <see cref="CCVars.NPCThreatWakeScans"/>; the value here is only a fallback for the first tick.
    /// </summary>
    private const int DefaultScansPerTick = 32;

    /// <summary>
    /// Upper bound for <see cref="CCVars.NPCThreatWakeScans"/>, so a typo in the config cannot turn this back
    /// into an every-tick sweep over the whole map.
    /// </summary>
    private const int MaxScansPerTick = 256;

    /// <summary>
    /// Upper bound for <see cref="CCVars.NPCThreatWakeInterval"/>; anything above a couple of ticks already
    /// fails the crowd reaction budget, so there is no reason to allow more.
    /// </summary>
    private const int MaxScanInterval = 30;

    /// <summary>
    /// How long a mob waits before the wake system may ask for another plan. It only applies after an attempt:
    /// a hostile the plan cannot use leaves no target in the blackboard, and without this the mob would start
    /// a new plan as soon as the previous one finished, for as long as that hostile stood there.
    /// </summary>
    private static readonly TimeSpan WakeCooldown = TimeSpan.FromSeconds(0.5);

    private int _cursor;
    private int _scansPerTick = DefaultScansPerTick;
    private int _scanInterval = 1;
    private int _tickCounter;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, CCVars.NPCThreatWakeScans, SetScansPerTick, true);
        Subs.CVar(_config, CCVars.NPCThreatWakeInterval, SetScanInterval, true);
    }

    private void SetScansPerTick(int value)
    {
        _scansPerTick = Math.Clamp(value, 1, MaxScansPerTick);
    }

    private void SetScanInterval(int value)
    {
        _scanInterval = Math.Clamp(value, 1, MaxScanInterval);
    }

    public override void Update(float frameTime)
    {
        // One scan per <see cref="CCVars.NPCThreatWakeInterval"/> ticks. Defaults to every tick; the counter is
        // reset rather than decremented so a config change takes effect on the next scan.
        if (++_tickCounter < _scanInterval)
            return;

        _tickCounter = 0;

        var query = EntityQueryEnumerator<ActiveNPCComponent, HTNComponent>();
        var skipped = 0;
        var scanned = 0;

        while (query.MoveNext(out var uid, out _, out var htn))
        {
            if (skipped < _cursor)
            {
                skipped++;
                continue;
            }

            if (scanned >= _scansPerTick)
                break;

            scanned++;
            WakeIfThreatened(uid, htn);
        }

        // Ran out of NPCs: start over next tick. Otherwise continue where this tick stopped.
        _cursor = scanned >= _scansPerTick ? _cursor + scanned : 0;
    }

    private void WakeIfThreatened(EntityUid uid, HTNComponent htn)
    {
        // A mob inside a container (locker, crate, another mob) is not in the world and cannot act on what it
        // "sees" through the container, so it must not wake up for it.
        if (!htn.Enabled || htn.Planning || _container.IsEntityInContainer(uid))
            return;

        // One attempt per <see cref="WakeCooldown"/>. A hostile the plan cannot use (unreachable, out of sight)
        // leaves no target in the blackboard, so without this the mob would ask for a new plan the moment the
        // previous one finished and keep the planner busy for as long as that hostile stood there. The cooldown
        // is only paid after an attempt, so a hostile appearing next to an idle mob is still noticed at once.
        if (_timing.CurTime < htn.NextThreatWake)
            return;

        if (htn.Blackboard.TryGetValue<EntityUid>(TargetKey, out var target, EntityManager))
        {
            if (IsValidTarget(target))
                return;

            htn.NextThreatWake = _timing.CurTime + WakeCooldown;

            // The key is deliberately left in place. An NPC keeps running its current plan while the next one
            // is prepared (see HTNSystem.UpdateNPC), and operators such as MeleeOperator or GunOperator read
            // Target without a null check in Startup, so removing it here could throw KeyNotFoundException
            // mid-plan. A stale target makes the running plan fail on its own (operators use TryGetValue) and
            // that shutdown removes the key, exactly as when a target dies during combat.
            _htn.Replan(htn);
            return;
        }

        var range = MathF.Min(
            htn.Blackboard.GetValueOrDefault<float>(htn.Blackboard.GetVisionRadiusKey(EntityManager), EntityManager),
            MaxReactionRange);

        if (range <= 0f)
            return;

        foreach (var hostile in _faction.GetNearbyHostiles(uid, range))
        {
            if (!IsValidTarget(hostile))
                continue;

            htn.NextThreatWake = _timing.CurTime + WakeCooldown;
            _htn.Replan(htn);
            return;
        }
    }

    /// <summary>
    /// Whether the blackboard's target is still worth reacting to. Targets are not always mobs (for example a
    /// container an NPC is prying open), so entities without a mob state are left alone rather than cleared.
    /// </summary>
    private bool IsValidTarget(EntityUid target)
    {
        if (TerminatingOrDeleted(target))
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState))
            return true;

        return _mobState.IsAlive(target, mobState);
    }
}
