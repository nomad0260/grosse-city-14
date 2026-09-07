using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Camera.Components;

/// <summary>
/// Adjusts camera recoil when the gun is wielded, and raises the camera kick magnitude cap.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrosseGunWieldBonusComponent : Component
{
    /// <summary>
    /// The maximum magnitude of the kick applied to the camera at any point.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float KickMagnitudeMax = 6f;

    /// <summary>
    /// Added to <see cref="Content.Shared.Weapons.Ranged.Components.GunComponent.CameraRecoilScalar"/> while wielded.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float CameraRecoilScalar = 1f;
}
