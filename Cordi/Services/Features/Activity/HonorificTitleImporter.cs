using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Cordi.Configuration;
using Cordi.Core;

namespace Cordi.Services.Activity;

public readonly record struct HonorificImportResult(bool Success, int Titles, int Characters, string Message);

public sealed class HonorificTitleImporter
{
    private const string LogSource = "Activity";
    private const string FileName = "Honorific.json";

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService _log;

    public HonorificTitleImporter(Configuration.Configuration config, CordiLogService log)
    {
        _config = config;
        _log = log;
    }

    public static string FilePath => Path.Combine(EmoteTitleImporter.PluginConfigsDirectory(), FileName);

    public static bool Available => File.Exists(FilePath);

    internal readonly record struct HonorificTitle(
        string CharacterName,
        uint WorldId,
        string Title,
        Vector3? Color,
        Vector3? Glow,
        int? GradientColourSet,
        int? GradientAnimationStyle);

    public HonorificImportResult Import(bool asGlobal)
    {
        List<HonorificTitle> titles;

        try
        {
            if (!File.Exists(FilePath))
                return new HonorificImportResult(false, 0, 0, $"Nothing found at {FileName}.");

            titles = Parse(File.ReadAllText(FilePath));
        }
        catch (Exception ex)
        {
            _log.Error(LogSource, $"Failed to read {FileName}", ex);
            return new HonorificImportResult(false, 0, 0, $"{FileName} could not be read: {ex.Message}");
        }

        var activity = _config.ActivityConfig;
        var presets = activity.CustomPresets;
        var template = presets.Count > 0
            ? presets[Math.Clamp(activity.ActiveCustomPreset, 0, presets.Count - 1)].Config
            : null;

        int imported = 0;
        var characters = new HashSet<(string, uint)>();

        foreach (var title in titles)
        {
            string name = asGlobal ? string.Empty : title.CharacterName;
            uint world = asGlobal ? 0 : title.WorldId;

            bool duplicate = presets.Any(preset =>
                (asGlobal ? preset.IsGlobal() : preset.BelongsTo(name, world))
                && preset.Config?.Format == title.Title);

            if (duplicate) continue;

            presets.Add(new ActivityPreset
            {
                Name = activity.UniqueCustomPresetName(title.Title),
                CharacterName = name,
                CharacterWorldId = world,
                Config = new ActivityTypeConfig
                {
                    Enabled = template?.Enabled ?? true,
                    Priority = template?.Priority ?? 0,
                    Format = title.Title,
                    Color = title.Color,
                    Glow = title.Glow,
                    GradientColourSet = title.GradientColourSet,
                    GradientAnimationStyle = title.GradientAnimationStyle,
                },
            });

            characters.Add((title.CharacterName, title.WorldId));
            imported++;
        }

        if (imported > 0) _config.Save();

        _log.Info(LogSource,
            $"Imported {imported} of {titles.Count} title(s) from Honorific as {(asGlobal ? "global" : "character")} presets.");

        return new HonorificImportResult(true, imported, characters.Count,
            imported == 0
                ? "Honorific read, nothing new to add."
                : $"Imported {imported} title(s) from {characters.Count} character(s).");
    }

    internal static List<HonorificTitle> Parse(string json)
    {
        var titles = new List<HonorificTitle>();

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });

        if (!document.RootElement.TryGetProperty("WorldCharacterDictionary", out var worlds)
            || worlds.ValueKind != JsonValueKind.Object)
            return titles;

        foreach (var world in worlds.EnumerateObject())
        {
            if (!uint.TryParse(world.Name, out var worldId) || world.Value.ValueKind != JsonValueKind.Object) continue;

            foreach (var character in world.Value.EnumerateObject())
            {
                if (character.Name.StartsWith('$') || character.Value.ValueKind != JsonValueKind.Object) continue;

                if (character.Value.TryGetProperty("DefaultTitle", out var defaultTitle))
                    AddTitle(titles, character.Name, worldId, defaultTitle);

                if (character.Value.TryGetProperty("CustomTitles", out var custom))
                {
                    if (custom.ValueKind == JsonValueKind.Object && custom.TryGetProperty("$values", out var values))
                        custom = values;

                    if (custom.ValueKind != JsonValueKind.Array) continue;

                    foreach (var entry in custom.EnumerateArray())
                        AddTitle(titles, character.Name, worldId, entry);
                }
            }
        }

        return titles;
    }

    private static void AddTitle(List<HonorificTitle> titles, string characterName, uint worldId, JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object) return;
        if (!entry.TryGetProperty("Title", out var text) || text.ValueKind != JsonValueKind.String) return;

        string title = text.GetString()?.Trim() ?? string.Empty;

        if (title.Length == 0) return;
        if (titles.Any(existing => existing.WorldId == worldId && existing.CharacterName == characterName && existing.Title == title)) return;

        titles.Add(new HonorificTitle(
            characterName,
            worldId,
            title,
            ReadVector(entry, "Color"),
            ReadVector(entry, "Glow"),
            ReadInt(entry, "GradientColourSet"),
            ReadInt(entry, "GradientAnimationStyle")));
    }

    private static Vector3? ReadVector(JsonElement entry, string property)
    {
        if (!entry.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object) return null;

        return value.TryGetProperty("X", out var x) && x.TryGetSingle(out var fx)
            && value.TryGetProperty("Y", out var y) && y.TryGetSingle(out var fy)
            && value.TryGetProperty("Z", out var z) && z.TryGetSingle(out var fz)
                ? new Vector3(fx, fy, fz)
                : null;
    }

    private static int? ReadInt(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;
}
