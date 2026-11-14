using System;
using Content.Server.Administration;
using Content.Server._Grosse.NPC.Systems;
using Content.Shared.Administration;
using Content.Shared._Grosse.NPC.Events;
using Robust.Shared.Console;

namespace Content.Server._Grosse.NPC.Commands;

/// <summary>
/// Включает и выключает визуализацию патрульных маршрутов у игрока.
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

        var enabled = true;
        string? patrolGroup = null;

        if (args.Length > 0)
        {
            if (string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase))
                enabled = false;
            else
                patrolGroup = args[0];
        }

        var entManager = IoCManager.Resolve<IEntityManager>();
        var visualizer = entManager.System<PatrolVisualizerSystem>();

        var data = enabled
            ? visualizer.CollectPatrolData(patrolGroup)
            : new PatrolVisualizerDataMessage();

        visualizer.SendVisualizationData(data, shell.Player);

        if (!enabled)
        {
            shell.WriteLine("Визуализация патрулей выключена.");
            return;
        }

        shell.WriteLine(patrolGroup != null
            ? $"Визуализация патрулей группы '{patrolGroup}' включена. Найдено групп: {data.Groups.Count}"
            : $"Визуализация всех патрулей включена. Найдено групп: {data.Groups.Count}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        return CompletionResult.FromHintOptions(
            new[] { "off", "security", "cargo", "engineering", "medical", "janitor" },
            "Группа патруля или 'off' для отключения");
    }
}
