/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).


using Content.Shared._Grosse.ZLevels.Core.EntitySystems;
using Robust.Shared.Analyzers;

namespace Content.Shared._Grosse.ZLevels.Ghost;

public abstract partial class GrosseSharedZLevelGhostMoverSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrosseZLevelGhostMoverComponent, GrosseZLevelActionDown>(OnZLevelDown);
        SubscribeLocalEvent<GrosseZLevelGhostMoverComponent, GrosseZLevelActionUp>(OnZLevelUp);
    }
    [Dependency] private GrosseSharedZLevelsSystem _zLevel = null!;
    private void OnZLevelDown(Entity<GrosseZLevelGhostMoverComponent> ent, ref GrosseZLevelActionDown args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveDown(ent);
    }
    private void OnZLevelUp(Entity<GrosseZLevelGhostMoverComponent> ent, ref GrosseZLevelActionUp args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveUp(ent);
    }
}
