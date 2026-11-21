using System;
using Content.Server.Administration;
using Content.Shared._Grosse.Containment;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Ставит патрон в эмиттер РТМ напрямую, без печати и ручной вставки.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class ReactorCartridgeCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _factory = default!;

    public override string Command => "reactor-cartridge";
    public override string Description => "Installs an emitter cartridge into an RTM stabilizer";
    public override string Help => "reactor-cartridge <net entity> <alpha|beta|gamma|digamma>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Usage: reactor-cartridge <net entity> <alpha|beta|gamma|digamma>");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var net) || !_entities.TryGetEntity(net, out var uid))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.TryGetComponent<ContainmentStabilizerComponent>(uid, out var emitter))
        {
            shell.WriteError("Target entity has no ContainmentStabilizer component");
            return;
        }

        if (!TryParseKind(args[1], out var kind))
        {
            shell.WriteError("Unknown cartridge type. Use alpha, beta, gamma or digamma");
            return;
        }

        var prototype = DarkCoreCartridgeData.GetEntityId(kind);

        if (!_prototype.TryIndex<EntityPrototype>(prototype, out var proto))
        {
            shell.WriteError($"Unknown prototype {prototype}");
            return;
        }

        // Components.Values хранит обёртки ComponentRegistryEntry, поэтому
        // OfType<DarkCoreCartridgeComponent>() всегда давал пустой результат.
        if (!proto.Components.TryGetComponent(
                _factory.GetComponentName<DarkCoreCartridgeComponent>(), out var component) ||
            component is not DarkCoreCartridgeComponent data)
        {
            shell.WriteError($"Prototype {prototype} has no DarkCoreCartridge component");
            return;
        }

        emitter.HasCartridge = true;
        emitter.Enabled = true;
        emitter.CartridgeType = data.Kind;
        emitter.CartridgeResource = data.Resource;
        emitter.CartridgeMaxResource = data.MaxResource;
        emitter.StabilizationCoefficient = data.Stabilization;
        emitter.CompressionCoefficient = data.Compression;
        emitter.EnergyCoefficient = data.Energy;
        emitter.HeatCoefficient = data.Heat;
        emitter.CartridgeWearRate = data.WearRate;

        _entities.Dirty(uid.Value, emitter);

        shell.WriteLine($"Установлен {DarkCoreCartridgeData.GetName(kind)}-патрон: ресурс {data.Resource:0}, тепло {data.Heat:0.0}.");
    }

    private static bool TryParseKind(string value, out DarkCoreCartridgeType kind)
    {
        switch (value.ToLowerInvariant())
        {
            case "alpha":
                kind = DarkCoreCartridgeType.Alpha;
                return true;
            case "beta":
                kind = DarkCoreCartridgeType.Beta;
                return true;
            case "gamma":
                kind = DarkCoreCartridgeType.Gamma;
                return true;
            case "digamma":
                kind = DarkCoreCartridgeType.Digamma;
                return true;
            default:
                kind = DarkCoreCartridgeType.Alpha;
                return false;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 2)
            return CompletionResult.FromHintOptions(
                new[] { "alpha", "beta", "gamma", "digamma" },
                "cartridge type");

        return CompletionResult.Empty;
    }
}
