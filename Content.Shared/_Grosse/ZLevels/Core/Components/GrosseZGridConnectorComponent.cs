/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

namespace Content.Shared._Grosse.ZLevels.Core.Components;

/// <summary>
/// When anchored, links this entity's parent grid to the grid on the z-level directly above,
/// provided a tile exists at this position on that upper grid.
/// Multiple connector entities can independently maintain the same grid pair.
/// </summary>
[RegisterComponent]
public sealed partial class GrosseZGridConnectorComponent : Component
{
}
