using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.DarkMatter;

[NetSerializable, Serializable]
public enum ResearchTerminalUiKey : byte
{
    Key,
}

[NetSerializable, Serializable]
public sealed class MaterialStatEntry
{
    public readonly string Label;
    public readonly string Value;

    public MaterialStatEntry(string label, string value)
    {
        Label = label;
        Value = value;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchFormulaInfo
{
    public readonly string Id;
    public readonly string Name;
    public readonly string Author;
    public readonly string Created;
    public readonly int NodeCount;
    public readonly int Version;
    public readonly int TestCount;
    public readonly string LastResult;
    public readonly bool Reproducible;

    public ResearchFormulaInfo(string id, string name, string author, string created, int nodeCount,
        int version, int testCount, string lastResult, bool reproducible)
    {
        Id = id;
        Name = name;
        Author = author;
        Created = created;
        NodeCount = nodeCount;
        Version = version;
        TestCount = testCount;
        LastResult = lastResult;
        Reproducible = reproducible;
    }
}

/// <summary>
/// Display data for one physical feed of the matrix. The pool id identifies the random order,
/// while ContentId is the concrete reagent or material that satisfies it.
/// </summary>
[NetSerializable, Serializable]
public sealed class ResearchFeedInfo
{
    public readonly string Title;
    public readonly string Name;
    public readonly string PoolId;
    public readonly string ContentLabel;
    public readonly string ContentId;
    public readonly string Color;
    public readonly string IconPrototype;
    public readonly string LoadedName;
    public readonly string LoadedPrototype;
    public readonly bool HasItem;
    public readonly bool Matches;

    public ResearchFeedInfo(string title, string name, string poolId, string contentLabel, string contentId,
        string color, string iconPrototype, string loadedName, string loadedPrototype, bool hasItem, bool matches)
    {
        Title = title;
        Name = name;
        PoolId = poolId;
        ContentLabel = contentLabel;
        ContentId = contentId;
        Color = color;
        IconPrototype = iconPrototype;
        LoadedName = loadedName;
        LoadedPrototype = loadedPrototype;
        HasItem = hasItem;
        Matches = matches;
    }
}

/// <summary>
/// One terminal state. Draft and archive graphs are copies; the client can edit the draft freely
/// and only sends it back on an explicit action.
/// </summary>
[NetSerializable, Serializable]
public sealed class ResearchTerminalState : BoundUserInterfaceState
{
    public readonly EffectGraph Draft;
    public readonly List<ResearchFormulaInfo> Formulas;
    public readonly List<MaterialStatEntry> MaterialStats;

    public readonly string Status;

    public readonly bool AnalyzerPresent;
    public readonly bool SynthesizerPresent;

    public readonly string RequiredLiquidName;
    public readonly string RequiredOreName;
    public readonly ResearchFeedInfo LiquidFeed;
    public readonly ResearchFeedInfo OreFeed;

    public readonly bool HasLiquid;
    public readonly bool HasOre;
    public readonly bool HasClot;
    public readonly bool HasMaterial;
    public readonly bool AnalyzerHasCrystal;
    public readonly bool LiquidMatches;
    public readonly bool OreMatches;

    public readonly string LiquidName;
    public readonly string OreName;
    public readonly string ClotName;

    public readonly int Revision;
    public readonly string SelectedFormulaId;
    public readonly string LastSynthesis;

    public ResearchTerminalState(
        EffectGraph draft,
        List<ResearchFormulaInfo> formulas,
        List<MaterialStatEntry> materialStats,
        string status,
        bool analyzerPresent,
        bool synthesizerPresent,
        string requiredLiquidName,
        string requiredOreName,
        ResearchFeedInfo liquidFeed,
        ResearchFeedInfo oreFeed,
        bool hasLiquid,
        bool hasOre,
        bool hasClot,
        bool hasMaterial,
        bool analyzerHasCrystal,
        bool liquidMatches,
        bool oreMatches,
        string liquidName,
        string oreName,
        string clotName,
        int revision,
        string selectedFormulaId,
        string lastSynthesis)
    {
        Draft = draft;
        Formulas = formulas;
        MaterialStats = materialStats;
        Status = status;
        AnalyzerPresent = analyzerPresent;
        SynthesizerPresent = synthesizerPresent;
        RequiredLiquidName = requiredLiquidName;
        RequiredOreName = requiredOreName;
        LiquidFeed = liquidFeed;
        OreFeed = oreFeed;
        HasLiquid = hasLiquid;
        HasOre = hasOre;
        HasClot = hasClot;
        HasMaterial = hasMaterial;
        AnalyzerHasCrystal = analyzerHasCrystal;
        LiquidMatches = liquidMatches;
        OreMatches = oreMatches;
        LiquidName = liquidName;
        OreName = oreName;
        ClotName = clotName;
        Revision = revision;
        SelectedFormulaId = selectedFormulaId;
        LastSynthesis = lastSynthesis;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchSaveFormulaMessage : BoundUserInterfaceMessage
{
    public readonly string Name;
    public readonly EffectGraph Graph;

    public ResearchSaveFormulaMessage(string name, EffectGraph graph)
    {
        Name = name;
        Graph = graph;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchLoadFormulaMessage : BoundUserInterfaceMessage
{
    public readonly string Id;

    public ResearchLoadFormulaMessage(string id)
    {
        Id = id;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchDeleteFormulaMessage : BoundUserInterfaceMessage
{
    public readonly string Id;

    public ResearchDeleteFormulaMessage(string id)
    {
        Id = id;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchRenameFormulaMessage : BoundUserInterfaceMessage
{
    public readonly string Id;
    public readonly string Name;

    public ResearchRenameFormulaMessage(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchSynthesizeMessage : BoundUserInterfaceMessage
{
    public readonly string Name;

    public ResearchSynthesizeMessage(string name)
    {
        Name = name;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchCrystallizeMessage : BoundUserInterfaceMessage
{
    public readonly string Name;
    public readonly EffectGraph Graph;

    public ResearchCrystallizeMessage(string name, EffectGraph graph)
    {
        Name = name;
        Graph = graph;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchReflashCrystalMessage : BoundUserInterfaceMessage
{
    public readonly string Name;
    public readonly EffectGraph Graph;

    public ResearchReflashCrystalMessage(string name, EffectGraph graph)
    {
        Name = name;
        Graph = graph;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchTestMessage : BoundUserInterfaceMessage
{
    public readonly string Name;
    public readonly EffectGraph Graph;

    public ResearchTestMessage(string name, EffectGraph graph)
    {
        Name = name;
        Graph = graph;
    }
}

[NetSerializable, Serializable]
public sealed class ResearchCopyFormulaMessage : BoundUserInterfaceMessage
{
    public readonly string Id;

    public ResearchCopyFormulaMessage(string id)
    {
        Id = id;
    }
}

/// <summary>
/// Private marker update. The server sends it only to the session that owns the artifact;
/// other clients never receive the coordinates.
/// </summary>
[NetSerializable, Serializable]
public sealed class ResearchMarkerEvent : EntityEventArgs
{
    public readonly NetEntity Artifact;
    public readonly Vector2[] Positions;
    public readonly float Duration;

    public ResearchMarkerEvent(NetEntity artifact, Vector2[] positions, float duration)
    {
        Artifact = artifact;
        Positions = positions;
        Duration = duration;
    }
}
