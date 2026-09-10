/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.ZLevels.Ghost;

/// <summary>
/// component that allows you to quickly move between Z levels
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrosseZLevelGhostMoverComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "GrosseActionZLevelUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelUpActionEntity;

    [DataField]
    public EntProtoId DownActionProto = "GrosseActionZLevelDown";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelDownActionEntity;
}

/// <summary>
/// Should be relayed upon using the action.
/// </summary>
public sealed partial class GrosseZLevelActionUp : InstantActionEvent;

/// <summary>
/// Should be relayed upon using the action.
/// </summary>
public sealed partial class GrosseZLevelActionDown : InstantActionEvent;
