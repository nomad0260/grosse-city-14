using System;
using System.Collections.Generic;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.DarkMatter;

public enum DarkMatterMaterialKind : byte
{
    Liquid,
    Mineral,
    Metal,
    Organic,
    Experimental,
}

/// <summary>
/// Data-driven pool entry for the liquid and solid components of a synthesis.
/// New materials can be added in YAML without changing the server's synthesis code.
/// </summary>
[Prototype("darkMatterMaterial")]
public sealed partial class DarkMatterMaterialPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField] public string Name = "неизвестный компонент";
    [DataField] public DarkMatterMaterialKind Kind = DarkMatterMaterialKind.Liquid;
    [DataField] public string Description = string.Empty;
    [DataField] public string Color = "#9B7CFF";
    [DataField] public float Weight = 1f;

    [DataField] public float Density = 1f;
    [DataField] public float Strength = 1f;
    [DataField] public float EnergyCapacity = 100f;
    [DataField] public float Stability = 60f;
    [DataField] public float RadiationActivity = 0f;
    [DataField] public float ThermalResistance = 1f;
    [DataField] public float Degradation = 0.1f;
    [DataField] public float Efficiency = 1f;
    [DataField] public float Purity = 0.8f;

    /// <summary>Reagent prototypes that can supply this liquid during matrix synthesis.</summary>
    [DataField] public List<string> ReagentIds = new();

    /// <summary>Material prototype ids that can supply this solid during matrix synthesis.</summary>
    [DataField] public List<string> MaterialIds = new();
}

/// <summary>
/// A material sample with independently tunable physical properties. The same structure is used
/// by analysis, matrix synthesis and crystals.
/// </summary>
[RegisterComponent]
public sealed partial class ResearchMaterialComponent : Component
{
    [DataField]
    public string MaterialName = "неизвестный материал";

    [DataField]
    public float Density = 1f;

    [DataField]
    public float Strength = 1f;

    [DataField]
    public float Hardness = 1f;

    [DataField]
    public float ThermalConductivity = 1f;

    [DataField]
    public float ElectricalConductivity = 1f;

    [DataField]
    public float ThermalResistance = 1f;

    [DataField]
    public float RadiationResistance = 1f;

    [DataField]
    public float EnergyCapacity = 100f;

    [DataField]
    public float Stability = 100f;

    [DataField]
    public float Reactivity = 1f;

    [DataField]
    public float Degradation = 0f;

    [DataField]
    public float Purity = 1f;

    [DataField]
    public float DarkMatter = 0f;

    /// <summary>Радиационная активность материала.</summary>
    [DataField]
    public float RadiationActivity = 0f;

    /// <summary>Множитель эффективности записанных в кристалл воздействий.</summary>
    [DataField]
    public float EffectEfficiency = 1f;

    [DataField]
    public string Color = "#9b7cff";
}

[NetSerializable, Serializable]
public enum DarkMatterMachineKind : byte
{
    Analyzer,
    Synthesizer,
}

/// <summary>
/// Shared machine implementation for the simplified EНК. The analyzer uses one sample slot;
/// the KS-1 matrix uses two feed slots and produces a material sample.
/// </summary>
[RegisterComponent]
public sealed partial class DarkMatterMachineComponent : Component
{
    public const string SlotId = "dark_matter_slot";
    public const string LiquidSlotId = "dark_matter_liquid";
    public const string OreSlotId = "dark_matter_ore";

    [DataField]
    public DarkMatterMachineKind Kind = DarkMatterMachineKind.Analyzer;

    [DataField]
    public ItemSlot Slot = new();

    /// <summary>The two physical feed slots of the KS-1 matrix.</summary>
    [DataField]
    public ItemSlot LiquidSlot = new();

    [DataField]
    public ItemSlot OreSlot = new();
}

/// <summary>
/// An archived formula. It owns a copy of the graph, so loading and editing it never changes a
/// crystal that was already produced.
/// </summary>
[DataDefinition, NetSerializable, Serializable]
public sealed partial class ResearchFormulaRecord
{
    [DataField]
    public string Id = string.Empty;

    [DataField]
    public string Name = "Без названия";

    [DataField]
    public string Author = string.Empty;

    [DataField]
    public string Created = string.Empty;

    [DataField]
    public int Version = EffectGraph.CurrentVersion;

    [DataField]
    public string MaterialName = string.Empty;

    [DataField]
    public int TestCount;

    [DataField]
    public string LastResult = string.Empty;

    [DataField]
    public bool Reproducible;

    [DataField]
    public EffectGraph Graph = new();

    public ResearchFormulaRecord Clone()
    {
        return new ResearchFormulaRecord
        {
            Id = Id,
            Name = Name,
            Author = Author,
            Created = Created,
            Version = Version,
            MaterialName = MaterialName,
            TestCount = TestCount,
            LastResult = LastResult,
            Reproducible = Reproducible,
            Graph = Graph.Clone(),
        };
    }
}

[RegisterComponent]
public sealed partial class ResearchTerminalComponent : Component
{
    [DataField]
    public float LinkRange = 12f;

    [DataField]
    public List<ResearchFormulaRecord> Archive = new();

    [DataField]
    public EffectGraph Draft = new();

    [DataField]
    public string SelectedFormulaId = string.Empty;

    [DataField]
    public List<string> ExperimentLog = new();

    [DataField]
    public int Revision;

    [DataField]
    public string LastStatus = "Терминал готов к работе.";

    [DataField]
    public string LastLiquid = string.Empty;

    [DataField]
    public string LastOre = string.Empty;

    [DataField]
    public string LastSynthesis = string.Empty;

    [DataField]
    public float RefreshInterval = 0.5f;

    [ViewVariables]
    public TimeSpan NextRefresh;
}

/// <summary>
/// Runtime for a crystallized formula. Energy is spent per execution; instability causes damage
/// and can force the artifact offline.
/// </summary>
[RegisterComponent]
public sealed partial class ResearchArtifactComponent : Component
{
    [ViewVariables]
    public string FormulaName = "Неизвестная формула";

    [ViewVariables]
    public EffectGraph Graph = new();

    [ViewVariables]
    public EntityUid? CrystalOwner;

    [ViewVariables]
    public float Energy = 100f;

    [ViewVariables]
    public float MaxEnergy = 100f;

    [ViewVariables]
    public float RechargeRate = 0.25f;

    [ViewVariables]
    public float Stability = 100f;

    [ViewVariables]
    public float Degradation;

    [ViewVariables]
    public float TickInterval = 1f;

    [ViewVariables]
    public float Accumulator;

    /// <summary>Эффективность воздействий, унаследованная от материала.</summary>
    [ViewVariables]
    public float EffectEfficiency = 1f;

    [ViewVariables]
    public float RadiationActivity;

    /// <summary>Накопленное тепло. Перегрев разрушает структуру.</summary>
    [ViewVariables]
    public float Heat;

    [ViewVariables]
    public float MaxHeat = 100f;

    [ViewVariables]
    public bool Enabled = true;

    [ViewVariables]
    public string LastError = string.Empty;
}
