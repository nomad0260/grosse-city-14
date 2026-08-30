using Content.Shared._Grosse.Assault.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Prying.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map.Components;

namespace Content.Shared._Grosse.Assault;

public sealed partial class SharedAssaultGateSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private SharedGodmodeSystem _godmode = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    public const float GateDoorMaxDistance = 0.75f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AssaultGateComponent, MapInitEvent>(OnGateMapInit);
        SubscribeLocalEvent<AssaultGateSealComponent, ToolUseAttemptEvent>(OnToolAttempt);
        SubscribeLocalEvent<AssaultGateSealComponent, WeldableAttemptEvent>(OnWeldAttempt);
        SubscribeLocalEvent<AssaultGateSealComponent, BeforePryEvent>(OnPryAttempt);
    }

    private void OnGateMapInit(Entity<AssaultGateComponent> ent, ref MapInitEvent args)
    {
        foreach (var door in FindGateDoors(ent, Transform(ent)))
        {
            SealDoor(door);
        }
    }

    public void UnlockForZone(int zone)
    {
        var query = EntityQueryEnumerator<AssaultGateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gate, out var xform))
        {
            if (gate.Opened || gate.UnlocksForZone != zone)
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

        var seal = EnsureComp<AssaultGateSealComponent>(door);
        seal.Unlocked = false;
        Dirty(door, seal);
    }

    public void UnlockDoor(EntityUid door)
    {
        if (TryComp<AssaultGateSealComponent>(door, out var seal))
        {
            seal.Unlocked = true;
            Dirty(door, seal);
        }

        if (TryComp<DoorBoltComponent>(door, out var bolt))
            _doors.ForceSetBoltsDown((door, bolt), false);

        if (TryComp<DoorComponent>(door, out var doorComp) && doorComp.State is DoorState.Closed or DoorState.Closing)
            _doors.StartOpening(door, doorComp);
    }

    private void OnToolAttempt(Entity<AssaultGateSealComponent> ent, ref ToolUseAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnWeldAttempt(Entity<AssaultGateSealComponent> ent, ref WeldableAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnPryAttempt(Entity<AssaultGateSealComponent> ent, ref BeforePryEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        args.Message = "assault-gate-tools-blocked";
    }

    public List<EntityUid> FindGateDoors(Entity<AssaultGateComponent> gate, TransformComponent xform)
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
