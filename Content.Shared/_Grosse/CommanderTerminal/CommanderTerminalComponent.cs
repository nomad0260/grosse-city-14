using Content.Shared.Radio;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Grosse.CommanderTerminal;

/// <summary>
/// Wall console that lists civil protection, builds groups, and sends a beacon-relative order over radio.
/// </summary>
[RegisterComponent]
public sealed partial class CommanderTerminalComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "CMB";

    [DataField]
    public ProtoId<DepartmentPrototype> Department = "City14CivilProtection";
}
