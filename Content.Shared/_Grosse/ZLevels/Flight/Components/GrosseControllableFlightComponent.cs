/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.ZLevels.Flight.Components;

/// <summary>
/// Allows an entity to control its own flight status
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true),
 Access(typeof(GrosseSharedZFlightSystem))]
public sealed partial class GrosseControllableFlightComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "GrosseActionZFlightUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelUpActionEntity;

    [DataField]
    public EntProtoId DownActionProto = "GrosseActionZFlightDown";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelDownActionEntity;

    [DataField]
    public EntProtoId ToggleActionProto = "GrosseActionZFlightToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelToggleActionEntity;

    [DataField]
    public TimeSpan? StartFlightDoAfter;
}
