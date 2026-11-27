using Content.Client.Lobby.UI;
using Content.Shared.Administration;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.Commands;

/// <summary>
/// Dev-only helper for comparing the live lobby with the design reference:
/// cycles the overlay between off, 50% and 100% opacity.
/// </summary>
[AnyCommand]
public sealed partial class LobbyReferenceCommand : LocalizedCommands
{
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;

    public override string Command => "lobbyref";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_userInterfaceManager.ActiveScreen is not LobbyGui lobby)
        {
            shell.WriteError(LocalizationManager.GetString($"cmd-{Command}-error", ("command", Command)));
            return;
        }

        var stage = lobby.CycleReferenceOverlay();
        shell.WriteLine(LocalizationManager.GetString($"cmd-{Command}-result",
            ("command", Command), ("percent", stage * 50)));
    }
}
