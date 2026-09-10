/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

namespace Content.Server._Grosse.ZLevels.Gravity;

/// <summary>
/// When placed on a map entity (e.g. via zLevelsComponentOverrides), automatically ensures
/// every grid on this map has inherent gravity enabled — both at component init and when new grids appear.
/// </summary>
[RegisterComponent]
public sealed partial class GrosseAutoGridGravityComponent : Component
{
}
