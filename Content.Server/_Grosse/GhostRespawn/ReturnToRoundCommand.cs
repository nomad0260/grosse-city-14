using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Grosse.GhostRespawn;

[AnyCommand]
public sealed partial class ReturnToRoundCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "grosse_return_to_round";
    public string Description => Loc.GetString("ghost-respawn-command-desc");
    public string Help => Loc.GetString("ghost-respawn-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("ghost-respawn-not-player"));
            return;
        }

        _entities.System<GhostRespawnSystem>().OpenMenu(player);
    }
}
