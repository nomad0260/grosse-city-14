using Content.Server.Administration;
using Content.Shared._Grosse.Assault;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Grosse.Assault;

[AdminCommand(AdminFlags.Admin)]
public sealed class AssaultStatusCommand : LocalizedEntityCommands
{
    [Dependency] private AssaultRuleSystem _assault = default!;

    public override string Command => "assaultstatus";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_assault.TryGetRuleForStatus(out var rule))
        {
            shell.WriteLine(Loc.GetString("assault-cmd-inactive"));
            return;
        }

        shell.WriteLine(Loc.GetString("assault-cmd-status",
            ("phase", rule.Phase),
            ("zone", rule.CurrentZone + 1),
            ("total", rule.TotalZones),
            ("atk", rule.AttackersTickets),
            ("def", rule.DefendersTickets),
            ("players", rule.Players.Count)));

        var queued = 0;
        foreach (var slot in rule.Players.Values)
        {
            if (slot.InWaveQueue)
                queued++;
        }

        shell.WriteLine(Loc.GetString("assault-cmd-queue", ("queued", queued)));
    }
}
