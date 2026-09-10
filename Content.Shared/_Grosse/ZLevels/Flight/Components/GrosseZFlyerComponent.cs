/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.ZLevels.Flight.Components;

/// <summary>
/// A basic component that allows entities to fly between z-levels.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), Access(typeof(GrosseSharedZFlightSystem))]
public sealed partial class GrosseZFlyerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public int TargetMapHeight = 0;

    [DataField]
    public float FlightSpeed = 1.5f;

    [DataField]
    public float DefaultGravityIntensity = 1f;

    /// <summary>
    /// Spawned on client only every tick in flight state
    /// </summary>
    [DataField]
    public EntProtoId? FlightVfx;

    [DataField]
    public TimeSpan NextVfx = TimeSpan.Zero;
}
