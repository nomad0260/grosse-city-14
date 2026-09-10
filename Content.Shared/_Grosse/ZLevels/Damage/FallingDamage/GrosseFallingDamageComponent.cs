/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.ZLevels.Damage.FallingDamage;

/// <summary>
/// Additional damage when falling on this entity
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrosseFallingDamageComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public DamageSpecifier Damage = new();
}
