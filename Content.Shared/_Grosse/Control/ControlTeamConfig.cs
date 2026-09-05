using System.Diagnostics.CodeAnalysis;
using Content.Shared._Grosse.Control.Components;
using Content.Shared._Grosse.Pvp;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.Control;

public static class ControlTeamConfig
{
    public const string ComponentName = "StationControlConfig";

    public static StationControlConfigComponent? FromGameMap(GameMapPrototype? map)
    {
        if (map == null)
            return null;

        foreach (var station in map.Stations.Values)
        {
            if (station.StationComponentOverrides.TryGetComponent(ComponentName, out var component)
                && component is StationControlConfigComponent config)
                return config;
        }

        return null;
    }

    public static ProtoId<ControlTeamPrototype> GetId(StationControlConfigComponent? config, PvpTeam team)
    {
        if (config != null)
        {
            var id = team == PvpTeam.Attackers ? config.Attackers : config.Defenders;
            if (!string.IsNullOrEmpty(id))
                return id;
        }

        return team == PvpTeam.Attackers
            ? ControlConstants.DefaultAttackersTeam
            : ControlConstants.DefaultDefendersTeam;
    }

    public static bool TryGetTeam(
        IPrototypeManager proto,
        StationControlConfigComponent? config,
        PvpTeam team,
        [NotNullWhen(true)] out ControlTeamPrototype? teamProto)
    {
        return proto.TryIndex(GetId(config, team), out teamProto);
    }

    public static LocId GetName(IPrototypeManager proto, ProtoId<ControlTeamPrototype> teamId, PvpTeam slot)
    {
        if (proto.TryIndex(teamId, out ControlTeamPrototype? team) && !string.IsNullOrEmpty(team.Name))
            return team.Name;

        return slot == PvpTeam.Attackers
            ? "control-team-attackers"
            : "control-team-defenders";
    }
}
