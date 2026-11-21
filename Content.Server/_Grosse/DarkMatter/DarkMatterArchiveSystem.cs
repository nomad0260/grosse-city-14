using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Shared._Grosse.DarkMatter;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Server._Grosse.DarkMatter;

/// <summary>
/// Scientific archive stored in the server's user-data directory. It survives round restarts,
/// which is what makes a published formula reusable in later shifts.
/// </summary>
public sealed partial class DarkMatterArchiveSystem : EntitySystem
{
    private static readonly ResPath ArchivePath = new("/dark_matter_archive.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    [Dependency] private IResourceManager _resources = default!;

    public List<ResearchFormulaRecord> Records { get; private set; } = new();

    public override void Initialize()
    {
        base.Initialize();
        Load();
    }

    public void Load()
    {
        Records = new List<ResearchFormulaRecord>();

        try
        {
            if (!_resources.UserData.Exists(ArchivePath) ||
                !_resources.UserData.TryReadAllText(ArchivePath, out var json) ||
                string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var loaded = JsonSerializer.Deserialize<List<ResearchFormulaRecord>>(json, JsonOptions);
            if (loaded == null)
                return;

            Records = loaded
                .Where(record => record != null)
                .Select(Normalize)
                .ToList();
        }
        catch (Exception exception)
        {
            // A corrupt archive must not prevent the rest of the server from starting.
            Log.Error($"Не удалось прочитать архив формул: {exception}");
            Records = new List<ResearchFormulaRecord>();
        }
    }

    private static ResearchFormulaRecord Normalize(ResearchFormulaRecord record)
    {
        // JSON can contain nulls even for non-nullable fields, especially after a manual edit
        // or a partial write. Repair the record before it reaches Clone() or the UI.
        record.Name = string.IsNullOrWhiteSpace(record.Name) ? "Без названия" : record.Name.Trim();
        if (record.Name.Length > 64)
            record.Name = record.Name[..64];

        if (string.IsNullOrWhiteSpace(record.Id))
            record.Id = Guid.NewGuid().ToString("N");

        record.Author ??= string.Empty;
        record.Created ??= string.Empty;
        record.MaterialName ??= string.Empty;
        record.LastResult ??= string.Empty;
        record.Graph ??= new EffectGraph();
        record.Graph.Nodes ??= new List<EffectGraphNode>();
        record.Graph.Connections ??= new List<EffectGraphConnection>();
        record.Graph.Nodes.RemoveAll(node => node == null);
        record.Graph.Connections.RemoveAll(connection => connection == null);

        foreach (var node in record.Graph.Nodes)
        {
            node.Operation ??= string.Empty;
            node.Parameters ??= new Dictionary<string, float>();
        }

        if (record.Graph.Version <= 0)
            record.Graph.Version = EffectGraph.CurrentVersion;

        return record;
    }

    public void Save()
    {
        try
        {
            _resources.UserData.WriteAllText(ArchivePath, JsonSerializer.Serialize(Records, JsonOptions));
        }
        catch (Exception exception)
        {
            // Callers already keep the in-memory archive; report the persistence failure instead
            // of pretending the formula was saved to disk.
            Log.Error($"Не удалось сохранить архив формул: {exception}");
        }
    }

    public void MergeInto(List<ResearchFormulaRecord> target)
    {
        foreach (var record in Records)
        {
            if (target.All(existing => existing.Id != record.Id))
                target.Add(record.Clone());
        }
    }

    public void Upsert(ResearchFormulaRecord record)
    {
        var index = Records.FindIndex(existing => existing.Id == record.Id);
        if (index >= 0)
            Records[index] = record.Clone();
        else
            Records.Add(record.Clone());
    }

    public void Remove(string id)
    {
        Records.RemoveAll(record => record.Id == id);
    }
}
