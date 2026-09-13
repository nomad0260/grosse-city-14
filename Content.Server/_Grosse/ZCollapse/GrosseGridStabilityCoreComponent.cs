/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

namespace Content.Server._Grosse.ZCollapse;

/// <summary>
/// Keeps a floating grid aloft: when anchored, seeds the tile it stands on with
/// <see cref="LevitationForce"/> stability, which then flood-fills outward across the grid.
/// </summary>
[RegisterComponent]
public sealed partial class GrosseGridStabilityCoreComponent : Component
{
    [DataField]
    public int LevitationForce = 20;
}
