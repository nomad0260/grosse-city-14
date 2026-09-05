using System.Diagnostics.CodeAnalysis;
using Content.Shared._Grosse.Control.Components;
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

    public static ProtoId<ControlTeamPrototype> GetId(StationControlConfigComponent? config, ControlTeam team)
    {
        if (config != null)
        {
            var id = team == ControlTeam.TeamA ? config.TeamA : config.TeamB;
            if (!string.IsNullOrEmpty(id))
                return id;
        }

        return team == ControlTeam.TeamA
            ? ControlConstants.DefaultTeamA
            : ControlConstants.DefaultTeamB;
    }

    public static bool TryGetTeam(
        IPrototypeManager proto,
        StationControlConfigComponent? config,
        ControlTeam team,
        [NotNullWhen(true)] out ControlTeamPrototype? teamProto)
    {
        return proto.TryIndex(GetId(config, team), out teamProto);
    }

    public static LocId GetName(IPrototypeManager proto, ProtoId<ControlTeamPrototype> teamId, ControlTeam slot)
    {
        if (proto.TryIndex(teamId, out ControlTeamPrototype? team) && !string.IsNullOrEmpty(team.Name))
            return team.Name;

        return slot == ControlTeam.TeamA
            ? "control-team-a"
            : "control-team-b";
    }
}
