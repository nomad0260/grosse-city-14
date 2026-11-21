using System;
using Content.Server.Administration;
using Content.Shared._Grosse.Cooling;
using Content.Shared.Administration;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Заправляет охладитель РТМ указанным хладагентом, чтобы не варить химию ради проверки.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class ReactorFillCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override string Command => "reactor-fill";
    public override string Description => "Fills an RTM cooling block with a coolant reagent";
    public override string Help => "reactor-fill <net entity> <reagent> <amount>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError("Usage: reactor-fill <net entity> <reagent> <amount>");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var net) || !_entities.TryGetEntity(net, out var uid))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.TryGetComponent<CoolingBlockCoolantComponent>(uid, out var coolant))
        {
            shell.WriteError("Target entity has no CoolingBlockCoolant component");
            return;
        }

        if (!_prototype.HasIndex<ReagentPrototype>(args[1]))
        {
            shell.WriteError($"Unknown reagent {args[1]}");
            return;
        }

        if (!float.TryParse(args[2], out var amount) || amount <= 0f)
        {
            shell.WriteError("Amount must be a positive number");
            return;
        }

        var solutions = _entities.System<SharedSolutionContainerSystem>();

        if (!solutions.TryGetSolution(uid.Value, coolant.Solution, out var solutionEnt, out _))
        {
            shell.WriteError($"Target has no solution '{coolant.Solution}'");
            return;
        }

        solutions.TryAddReagent(solutionEnt.Value, args[1], FixedPoint2.New(amount), out var accepted);
        shell.WriteLine($"Добавлено {accepted:0} единиц {args[1]}.");
    }
}
