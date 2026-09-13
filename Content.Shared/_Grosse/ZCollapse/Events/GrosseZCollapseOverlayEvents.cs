/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.ZCollapse.Events;

/// <summary>
/// Raised when the server enables/disables the tile-stability debug overlay for a client.
/// After enabling, the client starts receiving <see cref="GrosseZCollapseOverlaySnapshotEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class GrosseZCollapseOverlayToggledEvent(bool isEnabled) : EntityEventArgs
{
    public readonly bool IsEnabled = isEnabled;
}

/// <summary>
/// Raised when per-tile stability data changed for one or more grids the client has opted into
/// viewing via the debug overlay.
/// </summary>
[Serializable, NetSerializable]
public sealed class GrosseZCollapseOverlaySnapshotEvent(Dictionary<NetEntity, Dictionary<Vector2i, int>> grids) : EntityEventArgs
{
    /// <summary>
    /// Key is grid uid. Value is stability per tile (absent tile = stability &lt;= 0 / uncolored).
    /// </summary>
    public readonly Dictionary<NetEntity, Dictionary<Vector2i, int>> Grids = grids;
}
