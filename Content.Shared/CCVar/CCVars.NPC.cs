using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> NPCMaxUpdates =
        CVarDef.Create("npc.max_updates", 128);

    public static readonly CVarDef<bool> NPCEnabled = CVarDef.Create("npc.enabled", true);

    /// <summary>
    ///     Should NPCs pathfind when steering. For debug purposes.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathfinding = CVarDef.Create("npc.pathfinding", true);

    /// <summary>
    ///     Grosse: how many NPCs the threat-wake scan may check in one tick. The scan is capped by this value,
    ///     not by the number of NPCs on the map, so the per-tick cost stays flat. Raising it reacts faster on
    ///     crowded maps, lowering it reduces the per-tick cost.
    /// </summary>
    public static readonly CVarDef<int> NPCThreatWakeScans =
        CVarDef.Create("npc.threat_wake_scans", 32, CVar.SERVERONLY);

    /// <summary>
    ///     Grosse: how many ticks pass between threat-wake scans. 1 scans every tick. Values above 2 stretch the
    ///     time needed for one full pass over the map, which delays reaction in a crowd past the one-second
    ///     budget PatchHTNReactionTest checks.
    /// </summary>
    public static readonly CVarDef<int> NPCThreatWakeInterval =
        CVarDef.Create("npc.threat_wake_interval", 1, CVar.SERVERONLY);
}
