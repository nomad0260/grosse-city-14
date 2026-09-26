using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Cars;

/// <summary>
/// Marks the turret entity and points back at the hull that owns it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrosseCarTurretComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Hull;

    /// <summary>
    /// How far in front of the turret the shell is moved so it clears the hull.
    /// </summary>
    [DataField]
    public float Muzzle = 2.2f;
}
