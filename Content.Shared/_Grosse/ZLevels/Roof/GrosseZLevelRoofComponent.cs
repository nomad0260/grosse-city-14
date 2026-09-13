/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.ZLevels.Roof;

/// <summary>
/// Marks a mapGrid entity (supports both planetmap + grids) for automatic roof management driven by tile changes in the Z-level system.
/// Systems use this marker to add, update, or remove roof tiles when the underlying tiles change.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GrosseZLevelRoofComponent : Component;
