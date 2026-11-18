using System;
using Content.Server.NPC.HTN;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Grosse: reacts to threats on sight instead of waiting for the periodic re-plan pass.
/// </summary>
/// <remarks>
/// The HTN planner is a shared, time-sliced queue, so with hundreds of NPCs on a map a freshly spawned
/// hostile can wait seconds before the mob in front of it gets a plan that includes it. This system keeps a
/// cheap check running (faction hostiles within a capped range) and asks the planner for a new plan the moment
/// something hostile appears, which lets <see cref="HTNComponent.ConstantlyReplan"/> stay off and stops every
/// NPC from re-planning on a timer for nothing. Scans are incremental, so the cost per tick does not grow with
/// the map's NPC count.
/// Sleeping NPCs are deliberately not scanned: they have no plan and no active updates, and the engine wakes
/// them itself (map init, mob state change, player detaching).
/// </remarks>
public sealed partial class NPCThreatWakeSystem : EntitySystem
{
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private const string TargetKey = "Target";

    /// <summary>
    /// Upper bound on the scan radius. Vision radii on this fork are large (antlions see 86 tiles), and the
    /// point of this check is a quick "is something hostile right here" answer, not a full perception sweep.
    /// </summary>
    private const float MaxReactionRange = 12f;

    /// <summary>
    /// How many NPCs one tick may check, so the cost stays flat regardless of the map's NPC count.
    /// </summary>
    private const int ScansPerTick = 32;

    private int _cursor;

    public override void Update(float frameTime)
    {
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

            if (scanned >= ScansPerTick)
                break;

            scanned++;
            WakeIfThreatened(uid, htn);
        }

        // Ran out of NPCs: start over next tick. Otherwise continue where this tick stopped.
        _cursor = scanned >= ScansPerTick ? _cursor + scanned : 0;
    }

    private void WakeIfThreatened(EntityUid uid, HTNComponent htn)
    {
        // A mob inside a container (locker, crate, another mob) is not in the world and cannot act on what it
        // "sees" through the container, so it must not wake up for it.
        if (!htn.Enabled || htn.Planning || _container.IsEntityInContainer(uid))
            return;

        // No extra throttle on purpose. HTNSystem.RequestPlan refuses to queue a second job while one is already
        // running, and <see cref="ScansPerTick"/> caps wake-ups at 32 per tick, so a hostile the plan cannot use
        // (unreachable, out of sight) cannot flood the planner. A PlanAccumulator-based cooldown was measured and
        // pushed crowd reaction from 26 to 40 ticks, outside the one-second budget.
        if (htn.Blackboard.TryGetValue<EntityUid>(TargetKey, out var target, EntityManager))
        {
            if (IsValidTarget(target))
                return;

            // The plan still points at something that is gone, so let planning run again.
            htn.Blackboard.Remove<EntityUid>(TargetKey);
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
