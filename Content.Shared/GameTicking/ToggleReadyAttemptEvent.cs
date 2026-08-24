using Robust.Shared.Player;

namespace Content.Shared.GameTicking;

/// <summary>
/// Raised on the server before a player is marked ready in the pre-round lobby.
/// Cancel to block ready-up.
/// </summary>
public sealed class ToggleReadyAttemptEvent : CancellableEntityEventArgs
{
    public ICommonSession Player { get; }

    public ToggleReadyAttemptEvent(ICommonSession player)
    {
        Player = player;
    }
}
