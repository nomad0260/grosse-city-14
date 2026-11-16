using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Content.Shared.Interaction;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCUseActionOnTargetSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NPCUseActionOnTargetComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<NPCUseActionOnTargetComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ActionEnt = _actions.AddAction(ent, ent.Comp.ActionId);
    }

    public bool TryUseTentacleAttack(Entity<NPCUseActionOnTargetComponent?> user, EntityUid target)
    {
        if (!Resolve(user, ref user.Comp, false))
            return false;

        if (_actions.GetAction(user.Comp.ActionEnt) is not {} action)
            return false;

        if (!_actions.ValidAction(action))
            return false;

        _actions.SetEventTarget(action, target);

        // NPC is serverside, no prediction :(
        _actions.PerformAction(user.Owner, action, predicted: false);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Tries to use the attack on the current target.
        var query = EntityQueryEnumerator<NPCUseActionOnTargetComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var comp, out var htn))
        {
            // Skip the blackboard and distance checks while the action is still recharging.
            if (_actions.GetAction(comp.ActionEnt) is not { } action || !_actions.ValidAction(action))
                continue;

            if (!htn.Blackboard.TryGetValue<EntityUid>(comp.TargetKey, out var target, EntityManager))
                continue;

            if (!CanUseOnTarget(uid, target, comp, out var targetCoordinates))
                continue;

            // Jump-like actions move along the entity's rotation, so look at the target first.
            FaceTarget(uid, targetCoordinates);

            TryUseTentacleAttack((uid, comp), target);
        }
    }

    /// <summary>
    /// Checks the configured range and line of sight to the target and returns its coordinates.
    /// </summary>
    private bool CanUseOnTarget(
        EntityUid uid,
        EntityUid target,
        NPCUseActionOnTargetComponent comp,
        out MapCoordinates targetCoordinates)
    {
        targetCoordinates = default;

        if (!TryComp(uid, out TransformComponent? xform) ||
            !TryComp(target, out TransformComponent? targetXform))
        {
            return false;
        }

        var ourMap = _transform.GetMapCoordinates(uid, xform);
        var targetMap = _transform.GetMapCoordinates(target, targetXform);

        if (ourMap.MapId != targetMap.MapId)
            return false;

        var distance = (targetMap.Position - ourMap.Position).Length();

        if (comp.MinRange > 0f && distance < comp.MinRange)
            return false;

        if (comp.MaxRange > 0f && distance > comp.MaxRange)
            return false;

        if (comp.RequireLineOfSight && !_interaction.InRangeUnobstructed(uid, target, distance))
            return false;

        targetCoordinates = targetMap;
        return true;
    }

    private void FaceTarget(EntityUid uid, MapCoordinates targetCoordinates)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var ourMap = _transform.GetMapCoordinates(uid, xform);
        var delta = targetCoordinates.Position - ourMap.Position;

        if (delta.LengthSquared() < 0.001f)
            return;

        // SetWorldRotation resolves the parent itself, so this is also safe when the NPC is in nullspace.
        _transform.SetWorldRotation(xform, delta.ToWorldAngle());
    }
}
