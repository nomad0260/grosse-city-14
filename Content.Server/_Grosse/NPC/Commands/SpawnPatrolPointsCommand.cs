using System;
using System.Numerics;
using Content.Server.Administration;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared.Administration;
using Content.Shared._Grosse.NPC.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._Grosse.NPC.Commands;

/// <summary>
/// Создаёт кольцо патрульных точек вокруг координат — для тестов и отладки.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SpawnPatrolPointsCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "spawnpatrolpoints";
    public string Description => "Spawns patrol points around the given coordinates for testing.";
    public string Help => $"Usage: {Command} <x> <y> <mapId> [group=default] [count=4] [radius=5]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IoCManager.InjectDependencies(this);

        if (args.Length < 3)
        {
            shell.WriteError($"Not enough arguments!\n{Help}");
            return;
        }

        if (!float.TryParse(args[0], out var x) || !float.TryParse(args[1], out var y))
        {
            shell.WriteError($"Invalid coordinates: '{args[0]}', '{args[1]}'");
            return;
        }

        if (!int.TryParse(args[2], out var mapIdInt))
        {
            shell.WriteError($"Invalid map ID: {args[2]}");
            return;
        }

        var group = args.Length > 3 ? args[3] : "default";

        var count = 4;

        if (args.Length > 4 && (!int.TryParse(args[4], out count) || count <= 0))
        {
            shell.WriteError($"Invalid count: {args[4]}");
            return;
        }

        var radius = 5f;

        if (args.Length > 5 && (!float.TryParse(args[5], out radius) || radius <= 0f))
        {
            shell.WriteError($"Invalid radius: {args[5]}");
            return;
        }

        var mapSystem = _entManager.System<SharedMapSystem>();
        var mapUid = mapSystem.GetMap(new MapId(mapIdInt));

        if (mapUid == EntityUid.Invalid)
        {
            shell.WriteError($"Map {mapIdInt} does not exist.");
            return;
        }

        var pointManager = _entManager.System<NpcPointManagerSystem>();
        var center = new EntityCoordinates(mapUid, x, y);

        shell.WriteLine($"Spawning {count} patrol points (group '{group}') in radius {radius} around ({x}, {y}) on map {mapIdInt}...");

        for (var i = 0; i < count; i++)
        {
            var angle = i * MathF.Tau / count;
            var offset = new Vector2(radius * MathF.Cos(angle), radius * MathF.Sin(angle));

            var point = _entManager.SpawnEntity("PatrolPoint", center.Offset(offset));

            // Группа задаётся после спавна, а реестр индексирует точки по
            // ComponentStartup — поэтому точку нужно переиндексировать явно.
            if (_entManager.TryGetComponent<PatrolPointComponent>(point, out var component))
            {
                component.Group = group;
                pointManager.ReindexPatrolPoint(point);
            }

            shell.WriteLine($"  Point {i + 1}/{count} spawned at ({x + offset.X:F2}, {y + offset.Y:F2}) - UID: {point}");
        }

        shell.WriteLine($"Done! Spawned {count} patrol points.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}
