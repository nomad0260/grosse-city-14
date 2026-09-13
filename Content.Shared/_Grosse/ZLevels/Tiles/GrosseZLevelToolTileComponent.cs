/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared.Tools.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.ZLevels.Tiles;

/// <summary>
/// Marker for entities that also have <see cref="ToolTileCompatibleComponent"/>, allowing them to
/// deconstruct the tile on the z-level directly above the wielder while <see cref="Content.Shared._Grosse.ZLevels.Core.Components.GrosseZLevelViewerComponent.LookUp"/>
/// is enabled. Reuses <see cref="ToolTileCompatibleComponent.Delay"/> and <see cref="ToolTileCompatibleComponent.RequiresUnobstructed"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(GrosseZLevelToolTileSystem))]
public sealed partial class GrosseZLevelToolTileComponent : Component;
