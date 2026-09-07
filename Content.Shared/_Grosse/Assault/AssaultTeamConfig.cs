using System.Diagnostics.CodeAnalysis;
using Content.Shared._Grosse.Assault.Components;
using Content.Shared._Grosse.Pvp;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Assault;

public static class AssaultTeamConfig
{
    public const string ComponentName = "StationAssaultConfig";

    public static StationAssaultConfigComponent? FromGameMap(GameMapPrototype? map)
    {
        if (map == null)
            return null;

        foreach (var station in map.Stations.Values)
        {
            if (station.StationComponentOverrides.TryGetComponent(ComponentName, out var component)
                && component is StationAssaultConfigComponent config)
                return config;
        }

        return null;
    }

    public static ProtoId<AssaultTeamPrototype> GetId(StationAssaultConfigComponent? config, AssaultTeam team)
    {
        if (config != null)
        {
            var id = team == AssaultTeam.Attackers ? config.Attackers : config.Defenders;
            if (!string.IsNullOrEmpty(id))
                return id;
        }

        return team == AssaultTeam.Attackers
            ? AssaultConstants.DefaultAttackersTeam
            : AssaultConstants.DefaultDefendersTeam;
    }

    public static bool TryGetTeam(
        IPrototypeManager proto,
        StationAssaultConfigComponent? config,
        AssaultTeam team,
        [NotNullWhen(true)] out AssaultTeamPrototype? teamProto)
    {
        return proto.TryIndex(GetId(config, team), out teamProto);
    }

    public static LocId GetName(IPrototypeManager proto, ProtoId<AssaultTeamPrototype> teamId, AssaultTeam slot)
    {
        if (proto.TryIndex(teamId, out AssaultTeamPrototype? team) && !string.IsNullOrEmpty(team.Name))
            return team.Name;

        return slot == AssaultTeam.Attackers
            ? "assault-team-attackers"
            : "assault-team-defenders";
    }
}
