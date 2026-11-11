using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.NPC.Events;
using Content.Shared._Grosse.NPC.Events;
using Content.Server.NPC.Systems;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared.NPC.Components;
using Content.Shared._Grosse.NPC.Components;
using Robust.Shared.Console;
using Robust.Shared.Player;
using System.Linq;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;

namespace Content.Server._Grosse.NPC.Commands;

/// <summary>
/// Команда для включения/выключения визуализации патрулей
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ShowPatrolsCommand : IConsoleCommand
{
    public string Command => "showpatrols";
    public string Description => "Показать/скрыть визуализацию патрульных маршрутов";
    public string Help => "showpatrols [группа] - показать патрули (опционально фильтр по группе)\nshowpatrols off - выключить визуализацию";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
        {
            shell.WriteError("Эта команда может быть использована только игроком.");
            return;
        }

        var entManager = IoCManager.Resolve<IEntityManager>();
        var transform = entManager.System<SharedTransformSystem>();

        bool enabled = true;
        string? patrolGroup = null;

        if (args.Length > 0)
        {
            if (args[0].ToLower() == "off")
            {
                enabled = false;
            }
            else
            {
                patrolGroup = args[0];
            }
        }

        // Собираем данные напрямую
        var message = new PatrolVisualizerDataMessage();

        if (enabled)
        {
            var groupedPoints = new Dictionary<string, List<(EntityUid uid, PatrolPointComponent comp)>>();

            var query = entManager.EntityQueryEnumerator<PatrolPointComponent>();
            while (query.MoveNext(out var uid, out var point))
            {
                if (patrolGroup != null && point.Group != patrolGroup)
                    continue;

                if (!groupedPoints.ContainsKey(point.Group))
                    groupedPoints[point.Group] = new();

                groupedPoints[point.Group].Add((uid, point));
            }

            foreach (var (groupName, points) in groupedPoints)
            {
                var groupData = new PatrolGroupData
                {
                    GroupName = groupName,
                    Points = new List<PatrolPointData>()
                };

                var sortedPoints = points.OrderBy(p => p.comp.Order ?? 999).ToList();

                foreach (var entry in sortedPoints)
                {
                    var pointUid = entry.uid;
                    var pointComp = entry.comp;

                    if (!entManager.TryGetComponent<TransformComponent>(pointUid, out var xform))
                        continue;

                    groupData.Points.Add(new PatrolPointData
                    {
                        Entity = entManager.GetNetEntity(pointUid),
                        Coordinates = entManager.GetNetCoordinates(xform.Coordinates),
                        Order = pointComp.Order,
                        WaitTime = pointComp.WaitTime,
                        LookAround = pointComp.LookAround
                    });
                }

                if (groupData.Points.Count > 0)
                    message.Groups.Add(groupData);
            }
        }

        // Отправляем данные клиенту через систему визуализации
        var visualizerSystem = entManager.System<PatrolVisualizerSystem>();
        visualizerSystem.SendVisualizationData(message, shell.Player);

        if (enabled)
        {
            if (patrolGroup != null)
                shell.WriteLine($"Визуализация патрулей группы '{patrolGroup}' включена. Найдено групп: {message.Groups.Count}");
            else
                shell.WriteLine($"Визуализация всех патрулей включена. Найдено групп: {message.Groups.Count}");
        }
        else
        {
            shell.WriteLine("Визуализация патрулей выключена.");
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                new[] { "off", "security", "cargo", "engineering", "medical", "janitor" },
                "Группа патруля или 'off' для отключения");
        }

        return CompletionResult.Empty;
    }
}
