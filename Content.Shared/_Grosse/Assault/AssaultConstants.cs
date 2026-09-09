using Content.Shared._Grosse.Pvp;
using Content.Shared.Physics;

namespace Content.Shared._Grosse.Assault;

public static class AssaultConstants
{
    public const string RulePrototypeId = "City14AssaultRule";
    public const string AttackersFaction = "AssaultAttackers";
    public const string DefendersFaction = "AssaultDefenders";
    public const string DefaultAttackersTeam = "AssaultAttackers";
    public const string DefaultDefendersTeam = "AssaultDefenders";
    public const string CaptureFixtureId = "assault-ctp";
    public const string CaptureVisualPrototypeId = "AssaultCapturePointVisual";
    public const string BlockerFixtureId = "blocker";

    public static CollisionGroup GetBlockerLayer(AssaultTeam team)
    {
        return team == AssaultTeam.Attackers
            ? CollisionGroup.AssaultAttackersImpassable
            : CollisionGroup.AssaultDefendersImpassable;
    }
}
