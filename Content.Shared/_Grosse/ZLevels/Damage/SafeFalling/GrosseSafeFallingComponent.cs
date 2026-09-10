/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.ZLevels.Damage.SafeFalling;

/// <summary>
/// Reduces damage from falling on this entity
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrosseSafeFallingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 0f;

    [DataField, AutoNetworkedField]
    public float StunMultiplier = 0f;
}
