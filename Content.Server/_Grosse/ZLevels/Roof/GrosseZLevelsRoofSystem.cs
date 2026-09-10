/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared._Grosse.ZLevels.Roof;

namespace Content.Server._Grosse.ZLevels.Roof;

public sealed partial class GrosseZLevelsRoofSystem : GrosseSharedZLevelsRoofSystem
{
    private readonly HashSet<Vector2i> _roofMap = new();

    public override void Initialize()
    {
        base.Initialize();

        InitMaps();
        InitGrids();
    }
}
