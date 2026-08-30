using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Assault.Components;

/// <summary>
/// Applied to the real door after MapInit. Blocks tools and keeps Godmode; unlocking only opens the door.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AssaultGateSealComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Unlocked;
}
