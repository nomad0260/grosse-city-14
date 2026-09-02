using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared._Grosse.CCVars;

[CVarDefs]
public sealed class GCCVars
{
    /// <summary>
    /// Maximum number of players that can occupy the same Assault class at once.
    /// 0 disables the limit. 1 means each class can only be taken by one player.
    /// </summary>
    [CVarControl(AdminFlags.Server, min: 0, max: 32)]
    public static readonly CVarDef<int> AssaultMaxPerClass =
        CVarDef.Create("assault.max_per_class", 1, CVar.ARCHIVE | CVar.REPLICATED | CVar.SERVER);
}
