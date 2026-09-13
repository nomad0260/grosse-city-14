/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Grosse.ZLevels.Mapping.Prototypes;

[Prototype("zMap")]
public sealed partial class GrosseZLevelMapPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ResPath> Maps = new();

    /// <summary>
    /// Shared components for all zLevels maps
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();
}
