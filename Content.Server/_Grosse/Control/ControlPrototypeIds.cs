using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Control;

/// <summary>
/// Typed ids for Control YAML prototypes. Kept on the server so the YAML linter
/// can validate them against loaded server prototype kinds.
/// </summary>
public static class ControlPrototypeIds
{
    public static readonly ProtoId<GamePresetPrototype> Preset = "City14Control";
    public static readonly ProtoId<GameMapPoolPrototype> MapPool = "ControlMapPool";
    public static readonly ProtoId<GameMapPrototype> StubMap = "ControlStub";
}
