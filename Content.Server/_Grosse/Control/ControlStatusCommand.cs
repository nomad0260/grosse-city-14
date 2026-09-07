using Content.Server.Administration;
using Content.Shared._Grosse.Control;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Control;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class ControlStatusCommand : LocalizedEntityCommands
{
    [Dependency] private ControlRuleSystem _control = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override string Command => "controlstatus";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_control.TryGetRuleForStatus(out var rule))
        {
            shell.WriteLine(Loc.GetString("control-cmd-inactive"));
            return;
        }

        var queued = 0;
        foreach (var slot in rule.Players.Values)
        {
            if (slot.InWaveQueue)
                queued++;
        }

        shell.WriteLine(Loc.GetString("control-cmd-status",
            ("phase", rule.Phase),
            ("teamAName", Loc.GetString(ControlTeamConfig.GetName(_proto, rule.TeamAId, ControlTeam.TeamA))),
            ("teamBName", Loc.GetString(ControlTeamConfig.GetName(_proto, rule.TeamBId, ControlTeam.TeamB))),
            ("teamA", rule.TeamAScore),
            ("teamB", rule.TeamBScore),
            ("cap", rule.ScoreCap),
            ("players", rule.Players.Count)));
        shell.WriteLine(Loc.GetString("control-cmd-queue", ("queued", queued)));
    }
}
