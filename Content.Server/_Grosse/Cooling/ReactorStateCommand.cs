using System;
using Content.Shared._Grosse.Containment;
using Robust.Shared.Console;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Печатает телеметрию ядра РТМ в консоль: удобно сверять состояние с консолью управления
/// и проверять последствия смены патрона или хладагента.
/// Доступна всем игрокам: это справочная команда, а не отладочная.
/// </summary>
public sealed partial class ReactorStateCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "reactor-state";
    public override string Description => "Prints RTM core telemetry";
    public override string Help => "reactor-state <net entity>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Usage: reactor-state <net entity>");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var net) || !_entities.TryGetEntity(net, out var uid))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.TryGetComponent<ContainmentCoreComponent>(uid, out var core))
        {
            shell.WriteError("Target entity has no ContainmentCore component");
            return;
        }

        shell.WriteLine($"Ядро: {core.State}, работает: {core.IsRunning}");
        shell.WriteLine($"Стабильность {core.Stability:0.0}/100, сдерживание {core.ContainmentIntegrity:0.0}/100");
        shell.WriteLine($"Плотность {core.Density:0.0}, расширение {core.Expansion:0.0}, сжатие {core.Compression:0.0} (порог {core.CompressionThreshold:0}, избыток {core.CompressionExcess:0.0})");
        shell.WriteLine($"Температура {core.Temperature:0} K, радиация {core.Radiation:0}");
        shell.WriteLine($"Тепло: вход {core.HeatInput:0.0}, отвод {core.CoolingCapacity:0.0}, эффективность охлаждения {core.CoolingEfficiency:P0}");
        shell.WriteLine($"Энергия: {core.EnergyOutput:0.0}/{core.MaxEnergyOutput:0}; мощность ядра {core.PowerScale * 100f:0}%");
    }
}
