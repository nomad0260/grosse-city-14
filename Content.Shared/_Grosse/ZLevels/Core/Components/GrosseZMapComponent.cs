/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.ZLevels.Core.Components;

/// <summary>
/// Automatically added to the map when it appears in zLevelNetwork.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class GrosseZMapComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid NetworkUid;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? MapAbove;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? MapBelow;

    [DataField, AutoNetworkedField]
    public int Depth;
}
