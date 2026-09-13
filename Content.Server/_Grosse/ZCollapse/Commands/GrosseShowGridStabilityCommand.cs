/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Grosse.ZCollapse.Commands;

/// <summary>
/// Toggles the ZCollapse tile-stability debug overlay for the calling player.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class GrosseShowGridStabilityCommand : LocalizedEntityCommands
{
    [Dependency] private GrosseZCollapseSystem _collapse = default!;

    public override string Command => "showgridstability";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var session = shell.Player;
        if (session == null)
            return;

        _collapse.ToggleDebugView(session);
    }
}
