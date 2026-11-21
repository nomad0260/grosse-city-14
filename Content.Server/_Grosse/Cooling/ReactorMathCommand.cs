using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Grosse.Containment;
using Content.Shared._Grosse.Cooling;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Печатает лестницу хладагентов: теплоотвод одного охладителя, сколько блоков
/// нужно каждому патрону и какой эффект даёт жидкость.
/// Доступна всем игрокам: это справочная команда, а не отладочная.
/// </summary>
public sealed partial class ReactorMathCommand : LocalizedCommands
{

    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _factory = default!;

    private static readonly string[] CartridgeIds =
    {
        "DarkCoreCartridgeAlpha",
        "DarkCoreCartridgeBeta",
        "DarkCoreCartridgeGamma",
        "DarkCoreCartridgeDigamma",
    };

    public override string Command => "reactor-math";
    public override string Description => "Prints the RTM coolant ladder and cartridge heat balance";
    public override string Help => "reactor-math [base cooling capacity, default 120]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var baseCapacity = DarkCoreCoolantMath.DefaultCoolingCapacity;

        if (args.Length > 0 && !float.TryParse(args[0], out baseCapacity))
        {
            shell.WriteError("Cooling capacity must be a number");
            return;
        }

        var coolants = CoolantCatalog.CreateDefault();

        var cartridges = new List<(DarkCoreCartridgeType Kind, string Name, float Heat)>();

        foreach (var id in CartridgeIds)
        {
            if (!_prototype.TryIndex<EntityPrototype>(id, out var proto))
                continue;

            // Components.Values хранит обёртки ComponentRegistryEntry: OfType<> здесь
            // всегда давал пустую последовательность, и таблица патронов не печаталась.
            if (proto.Components.TryGetComponent(
                    _factory.GetComponentName<DarkCoreCartridgeComponent>(), out var component) &&
                component is DarkCoreCartridgeComponent cartridge)
            {
                cartridges.Add((
                    cartridge.Kind,
                    DarkCoreCartridgeData.GetName(cartridge.Kind),
                    DarkCoreCoolantMath.CalculateEmitterHeat(cartridge.Heat)));
            }
        }

        shell.WriteLine($"Тепловой баланс РТМ: один охладитель = {baseCapacity:0} ед.");

        if (cartridges.Count > 0)
        {
            shell.WriteLine("Тепло эмиттера: " + string.Join(" | ",
                cartridges.Select(c => $"{c.Name} {c.Heat:0}")));
        }

        shell.WriteLine($"{"Хладагент",-30}{"Отвод",8}{"Канистра",10}  Хватает: А/Б/Г/Д    Эффект");
        shell.WriteLine(new string('-', 110));

        foreach (var coolant in coolants)
        {
            var capacity = DarkCoreCoolantMath.CalculateCapacity(coolant, baseCapacity);
            var consumption = DarkCoreCoolantMath.CalculateConsumptionPerSecond(coolant, 0.5f);
            var runtime = DarkCoreCoolantMath.CalculateCanisterRuntime(consumption);

            var fits = string.Join("/", cartridges.Select(c =>
                capacity + 0.01f >= c.Heat ? "да" : "нет"));

            var name = _prototype.TryIndex(coolant.Reagent, out ReagentPrototype? reagent)
                ? reagent.LocalizedName
                : coolant.Reagent.Id;

            shell.WriteLine($"{name,-30}{capacity,8:0.0}{runtime,9:0}с  {fits,-16}    {DescribeEffect(coolant)}");
        }

        shell.WriteLine(string.Empty);
        shell.WriteLine("Формула: поток = Flow / вязкость; отвод = база × теплоотвод × √теплоёмкость × поток.");
        shell.WriteLine("Один эмиттер — один охладитель: блок должен отводить не меньше тепла патрона.");
        shell.WriteLine("Сборка по умолчанию: четыре эмиттера и четыре охладителя, по одному блоку на эмиттер.");
        shell.WriteLine("Стабильность влияет на загрязнение (2 − стабильность), а не на теплоотвод.");
    }

    private static string DescribeEffect(CoolantData data)
    {
        var parts = new List<string>();

        if (data.WearReduction > 0f)
            parts.Add($"износ −{data.WearReduction:P0}");
        if (data.WearIncrease > 0f)
            parts.Add($"износ +{data.WearIncrease:P0}");
        if (data.HeatGenerationReduction > 0f)
            parts.Add($"тепло эмиттеров −{data.HeatGenerationReduction:P0}");
        if (data.StabilizationBonus > 0f)
            parts.Add($"стабилизация +{data.StabilizationBonus:P0}");
        if (data.EnergyBonus > 0f)
            parts.Add($"энергия +{data.EnergyBonus:P0}");
        if (data.TemperatureTolerance > 0f)
            parts.Add($"+{data.TemperatureTolerance:0} K к порогу");
        if (Math.Abs(data.ConsumptionMultiplier - 1f) > 0.001f)
            parts.Add($"расход {data.ConsumptionMultiplier:P0}");

        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint("Base cooling capacity, default 120");

        return CompletionResult.Empty;
    }
}
