using Content.Shared._Grosse.Control.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Prying.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map.Components;

namespace Content.Shared._Grosse.Control;

public sealed partial class SharedControlGateSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private SharedGodmodeSystem _godmode = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    public const float GateDoorMaxDistance = 0.75f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ControlGateComponent, MapInitEvent>(OnGateMapInit);
        SubscribeLocalEvent<ControlGateSealComponent, ToolUseAttemptEvent>(OnToolAttempt);
        SubscribeLocalEvent<ControlGateSealComponent, WeldableAttemptEvent>(OnWeldAttempt);
        SubscribeLocalEvent<ControlGateSealComponent, BeforePryEvent>(OnPryAttempt);
    }

    private void OnGateMapInit(Entity<ControlGateComponent> ent, ref MapInitEvent args)
    {
        foreach (var door in FindGateDoors(ent, Transform(ent)))
        {
            SealDoor(door);
        }
    }

    public void UnlockAll()
    {
        var query = EntityQueryEnumerator<ControlGateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gate, out var xform))
        {
            if (gate.Opened)
                continue;

            foreach (var door in FindGateDoors((uid, gate), xform))
            {
                UnlockDoor(door);
            }

            gate.Opened = true;
        }
    }

    public void SealDoor(EntityUid door)
    {
        if (!HasComp<DoorComponent>(door))
            return;

        if (TryComp<DoorComponent>(door, out var doorComp) && doorComp.State == DoorState.Open)
            _doors.StartClosing(door, doorComp);

        var bolt = EnsureComp<DoorBoltComponent>(door);
        _doors.ForceSetBoltsDown((door, bolt), true);

        EnsureComp<GodmodeComponent>(door);
        _godmode.EnableGodmode(door);

        var seal = EnsureComp<ControlGateSealComponent>(door);
        seal.Unlocked = false;
        Dirty(door, seal);
    }

    public void UnlockDoor(EntityUid door)
    {
        if (TryComp<ControlGateSealComponent>(door, out var seal))
        {
            seal.Unlocked = true;
            Dirty(door, seal);
        }

        if (TryComp<DoorBoltComponent>(door, out var bolt))
            _doors.ForceSetBoltsDown((door, bolt), false);

        if (TryComp<DoorComponent>(door, out var doorComp) && doorComp.State is DoorState.Closed or DoorState.Closing)
            _doors.StartOpening(door, doorComp);
    }

    private void OnToolAttempt(Entity<ControlGateSealComponent> ent, ref ToolUseAttemptEvent args)
    {
        if (ent.Comp.Unlocked)
            return;

        args.Cancel();
    }

    private void OnWeldAttempt(Entity<ControlGateSealComponent> ent, ref WeldableAttemptEvent args)
    {
        if (ent.Comp.Unlocked)
            return;

        args.Cancel();
    }

    private void OnPryAttempt(Entity<ControlGateSealComponent> ent, ref BeforePryEvent args)
    {
        if (ent.Comp.Unlocked || args.Cancelled)
            return;

        args.Cancelled = true;
        args.Message = "control-gate-tools-blocked";
    }

    public List<EntityUid> FindGateDoors(Entity<ControlGateComponent> gate, TransformComponent xform)
    {
        var doors = new List<EntityUid>();
        if (HasComp<DoorComponent>(gate))
            doors.Add(gate);

        if (xform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var gridComp))
        {
            var tile = _maps.CoordinatesToTile(grid, gridComp, xform.Coordinates);
            foreach (var ent in _maps.GetAnchoredEntities(grid, gridComp, tile))
            {
                if (ent != gate.Owner && HasComp<DoorComponent>(ent) && !doors.Contains(ent))
                    doors.Add(ent);
            }
        }

        if (doors.Count > 0)
            return doors;

        foreach (var ent in _lookup.GetEntitiesInRange(xform.Coordinates, GateDoorMaxDistance, LookupFlags.Static | LookupFlags.Sundries))
        {
            if (ent != gate.Owner && HasComp<DoorComponent>(ent) && !doors.Contains(ent))
                doors.Add(ent);
        }

        return doors;
    }
}
