using System.Linq;
using Content.Server.Administration;
using Content.Server.NPC.Components;
using Content.Server._Grosse.NPC.Components;
using Content.Shared.Administration;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Commands;

/// <summary>
/// Показывает все стратегические точки и их состояние: активность, теги,
/// фракцию, слоты и позицию. Помогает понять, почему NPC «не видит» точку.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class StrategicPointsCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "strategicpoints";
    public string Description => "Lists strategic points (active, tags, faction, slots, position) and NPC trackers.";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IoCManager.InjectDependencies(this);

        var count = 0;

        var query = _entManager.EntityQueryEnumerator<StrategicPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var point, out var xform))
        {
            count++;
            var occupied = point.Slots.Count(s => s.Owner != null);
            shell.WriteLine($"{Name(uid)} active={point.IsActive} tags=[{string.Join(",", point.Tags)}] faction={point.Faction ?? "-"} " +
                            $"priority={point.Priority} slots={occupied}/{point.Slots.Count} (cap={point.Capacity}) " +
                            $"map={xform.MapID} pos={xform.Coordinates.Position}");

            foreach (var slot in point.Slots.Where(s => s.Owner != null))
            {
                shell.WriteLine($"    slot {slot.Index}: {slot.State} by {Name(slot.Owner!.Value)}");
            }
        }

        shell.WriteLine($"Total strategic points: {count}");

        var trackers = _entManager.EntityQueryEnumerator<NpcPointTrackerComponent>();
        while (trackers.MoveNext(out var uid, out var tracker))
        {
            shell.WriteLine($"NPC {Name(uid)} faction={tracker.Faction ?? "-"} tags=[{string.Join(",", tracker.StrategicTags)}] " +
                            $"current={tracker.CurrentStrategicPoint?.ToString() ?? "-"}");
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }

    private string Name(EntityUid uid)
    {
        return _entManager.TryGetComponent<MetaDataComponent>(uid, out var meta)
            ? $"{meta.EntityName} ({uid})"
            : uid.ToString();
    }
}
