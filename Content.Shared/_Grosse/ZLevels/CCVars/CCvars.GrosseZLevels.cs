/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Ported from crystallpunk-14/crystall-edge (MIT sublicense).

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float>
        GrosseBaseFallingDamage = CVarDef.Create("zlevels.grosse_base_falling_damage", 0.75f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        GrosseBaseFallingOtherDamage = CVarDef.Create("zlevels.grosse_base_falling_other_damage", 0.4f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        GrosseBaseFallingStunTime = CVarDef.Create("zlevels.grosse_base_falling_stun_time", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        GrosseBaseFallingOtherStunTime = CVarDef.Create("zlevels.grosse_base_falling_other_stun_time", 0.06f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> ZLevelsPhysicsTickRate =
        CVarDef.Create("zlevels.grosse_physics.tick_rate", 60, CVar.ARCHIVE);

    public static readonly CVarDef<bool> ZLevelsPhysicsClientSimulation =
        CVarDef.Create("zlevels.grosse_physics.client_simulation", true, CVar.ARCHIVE | CVar.CLIENT);

    /**
     * Physics
     */

    public static readonly CVarDef<float>
        GrosseZLevelsPhysicsGravityForce = CVarDef.Create("grosse.zlevels.physics.gravity_force", 9.8f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        GrosseZLevelsPhysicsVelocityLimit = CVarDef.Create("grosse.zlevels.physics.velocity_limit", 20f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The minimum speed required to trigger LandEvent events.
    /// </summary>
    public static readonly CVarDef<float>
        GrosseZLevelsPhysicsImpactVelocity = CVarDef.Create("grosse.zlevels.physics.impact_velocity", 3f, CVar.SERVER | CVar.REPLICATED);

    /**
     * Rendering
     */

    public static readonly CVarDef<int>
        GrosseZLevelsRenderingMaxZLevelsBelowRendering = CVarDef.Create("grosse.zlevels.rendering.max_zLevels_below_rendering", 1, CVar.SERVER | CVar.REPLICATED);

    /**
     * Audio
     */

    /// <summary>
    /// How many decibels of volume are subtracted from a PVS-positioned sound for every Z-level
    /// it is away from the listener.
    /// </summary>
    public static readonly CVarDef<float>
        GrosseZLevelsAudioPerLevelAttenuation = CVarDef.Create("grosse.zlevels.audio.per_level_attenuation_db", 9f, CVar.ARCHIVE | CVar.CLIENT);

    /// <summary>
    /// Occlusion added to a cross-Z-level sound when an opaque tile blocks the floor/ceiling between
    /// the source and the listener, on top of the flat per-level attenuation.
    /// </summary>
    public static readonly CVarDef<float>
        GrosseZLevelsAudioFloorOcclusion = CVarDef.Create("grosse.zlevels.audio.floor_occlusion", 3f, CVar.ARCHIVE | CVar.CLIENT);
}
