using Content.Server.PatchHTN.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;

namespace Content.Server.PatchHTN.Systems;

/// <summary>
/// Accumulates <see cref="NPCIdleTrackerComponent.IdleTime"/> for NPCs that opt in to idle tracking.
/// Time only counts while the NPC has no target it could act on, so idle plans (e.g. burrowing)
/// trigger after a while without prey.
/// </summary>
public sealed partial class NPCIdleTrackingSystem : EntitySystem
{
    /// <summary>
    /// Blackboard key set by target acquiring tasks (see <see cref="Content.Server.NPC.HTN.PrimitiveTasks.Operators.UtilityOperator"/>).
    /// </summary>
    private const string TargetKey = "Target";

    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NPCIdleTrackerComponent, DamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<NPCIdleTrackerComponent, HTNComponent>();

        while (query.MoveNext(out var uid, out var tracker, out var htn))
        {
            // Dead or disabled NPCs don't need idle tracking.
            if (!htn.Enabled || !_mobState.IsAlive(uid))
                continue;

            tracker.IdleTime = HasTargetInVision(uid, htn) ? 0f : tracker.IdleTime + frameTime;
        }
    }

    /// <summary>
    /// Checks whether the blackboard's current target is still worth reacting to.
    /// </summary>
    private bool HasTargetInVision(EntityUid uid, HTNComponent htn)
    {
        var blackboard = htn.Blackboard;

        // The key may hold an entity that has since been removed, so the uid has to be validated
        // rather than merely checked for being set (EntityUid.IsValid only checks for a zero id).
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, EntityManager) ||
            TerminatingOrDeleted(target) ||
            !_mobState.IsAlive(target))
        {
            return false;
        }

        // Plans don't clear their effects, so after a hunt the blackboard keeps the last target.
        // Only a target still within vision counts, otherwise a prey that escaped would keep the
        // timer at zero forever.
        var radius = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(EntityManager), EntityManager);

        return Transform(uid).Coordinates.TryDistance(EntityManager, Transform(target).Coordinates, out var distance) &&
               distance <= radius;
    }

    /// <summary>
    /// Resets the idle timer, e.g. after the NPC did something that shouldn't immediately repeat.
    /// </summary>
    public void ResetIdleTime(EntityUid uid)
    {
        if (TryComp<NPCIdleTrackerComponent>(uid, out var tracker))
            tracker.IdleTime = 0f;
    }

    private void OnDamageChanged(EntityUid uid, NPCIdleTrackerComponent tracker, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        tracker.IdleTime = 0f;
    }
}
