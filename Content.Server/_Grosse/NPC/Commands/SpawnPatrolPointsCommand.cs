using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Commands;

/// <summary>
/// Команда для создания точек патруля вокруг указанной позиции (для тестирования)
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SpawnPatrolPointsCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "spawnpatrolpoints";
    public string Description => "Spawns patrol points around the given coordinates for testing. Usage: spawnpatrolpoints <x> <y> <mapId> [group] [count] [radius]";
    public string Help => $"Usage: {Command} <x> <y> <mapId> [group=default] [count=4] [radius=5]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IoCManager.InjectDependencies(this);

        if (args.Length < 3)
        {
            shell.WriteError($"Not enough arguments!\n{Help}");
            return;
        }

        if (!float.TryParse(args[0], out var x))
        {
            shell.WriteError($"Invalid X coordinate: {args[0]}");
            return;
        }

        if (!float.TryParse(args[1], out var y))
        {
            shell.WriteError($"Invalid Y coordinate: {args[1]}");
            return;
        }

        if (!int.TryParse(args[2], out var mapIdInt))
        {
            shell.WriteError($"Invalid map ID: {args[2]}");
            return;
        }

        var mapId = new MapId(mapIdInt);
        var group = args.Length > 3 ? args[3] : "default";
        var count = args.Length > 4 && int.TryParse(args[4], out var c) ? c : 4;
        var radius = args.Length > 5 && float.TryParse(args[5], out var r) ? r : 5f;

        var mapSystem = _entManager.System<SharedMapSystem>();
        var centerCoords = new EntityCoordinates(_entManager.System<SharedMapSystem>().GetMap(mapId), x, y);

        shell.WriteLine($"Spawning {count} patrol points (group '{group}') in radius {radius} around ({x}, {y}) on map {mapId}...");

        for (var i = 0; i < count; i++)
        {
            var angle = (i * 2 * Math.PI) / count; // Равномерное распределение по кругу
            var offsetX = radius * (float)Math.Cos(angle);
            var offsetY = radius * (float)Math.Sin(angle);

            var pointCoords = centerCoords.Offset(new Vector2(offsetX, offsetY));
            var point = _entManager.SpawnEntity("PatrolPoint", pointCoords);

            shell.WriteLine($"  Point {i + 1}/{count} spawned at ({x + offsetX:F2}, {y + offsetY:F2}) - UID: {point}");
        }

        shell.WriteLine($"Done! Spawned {count} patrol points.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}
