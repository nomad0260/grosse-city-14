using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Grosse.CommanderTerminal;

/// <summary>
/// Decides who receives squad membership, the same way revolutionaries filter their icons.
/// </summary>
public sealed partial class SharedCommanderGroupSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommanderGroupMemberComponent, ComponentGetStateAttemptEvent>(OnMemberStateAttempt);
        SubscribeLocalEvent<CommanderGroupOrderComponent, ComponentGetStateAttemptEvent>(OnOrderStateAttempt);
        SubscribeLocalEvent<CommanderGroupMaskComponent, ClothingGotEquippedEvent>(OnMaskEquipped);
        SubscribeLocalEvent<CommanderGroupMaskComponent, ClothingGotUnequippedEvent>(OnMaskUnequipped);
    }

    public bool WearsGroupMask(EntityUid viewer)
    {
        return _inventory.TryGetSlotEntity(viewer, "mask", out var mask)
               && HasComp<CommanderGroupMaskComponent>(mask);
    }

    private void OnMemberStateAttempt(Entity<CommanderGroupMemberComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanSee(ent.Owner, args.Player);
    }

    private void OnOrderStateAttempt(Entity<CommanderGroupOrderComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanSee(ent.Owner, args.Player);
    }

    private void OnMaskEquipped(Entity<CommanderGroupMaskComponent> ent, ref ClothingGotEquippedEvent args)
    {
        DirtyGroups();
    }

    private void OnMaskUnequipped(Entity<CommanderGroupMaskComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        DirtyGroups();
    }

    /// <summary>
    /// Owner, the same squad, and anyone wearing the GEO mask receive the state.
    /// A null player is a replay.
    /// </summary>
    private bool CanSee(EntityUid target, ICommonSession? player)
    {
        if (player?.AttachedEntity is not { } viewer)
            return true;

        if (viewer == target || WearsGroupMask(viewer))
            return true;

        return TryComp<CommanderGroupMemberComponent>(viewer, out var viewerGroup)
               && TryComp<CommanderGroupMemberComponent>(target, out var targetGroup)
               && viewerGroup.GroupId.Length > 0
               && viewerGroup.GroupId == targetGroup.GroupId;
    }

    private void DirtyGroups()
    {
        if (!_net.IsServer)
            return;

        var members = EntityQueryEnumerator<CommanderGroupMemberComponent>();
        while (members.MoveNext(out var uid, out var member))
        {
            Dirty(uid, member);
        }

        var orders = EntityQueryEnumerator<CommanderGroupOrderComponent>();
        while (orders.MoveNext(out var uid, out var order))
        {
            Dirty(uid, order);
        }
    }
}
