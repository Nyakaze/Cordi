using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Cordi.Configuration;
using Cordi.Core;

namespace Cordi.Services.Activity;

public readonly record struct EmoteImportResult(bool Success, int Characters, int Counters, int Rules, string Message);

public sealed class EmoteTitleImporter
{
    private const string LogSource = "Activity";
    private const string PatMeFileName = "PatMe.json";
    private const string PatMeHonorificFileName = "PatMeHonorific.json";

    private static readonly Dictionary<string, ushort[]> PatMeEmoteIds = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Pet", new ushort[] { 105 } },
        { "Dote", new ushort[] { 146 } },
        { "Hug", new ushort[] { 112, 113 } },
        { "Embrace", new ushort[] { 113 } },
    };

    private static readonly Regex ScribanToken = new(@"\{\{\s*([a-z_]+)\s*\}\}", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ScribanPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        { "total_count", "{total}" },
        { "session_count", "{session}" },
        { "combo_count", "{combo}" },
    };

    private static readonly JsonSerializerOptions ReaderOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly CordiPlugin _plugin;
    private readonly EmoteCounterStore _counters;

    public EmoteTitleImporter(CordiPlugin plugin, EmoteCounterStore counters)
    {
        _plugin = plugin;
        _counters = counters;
    }

    public static string PatMePath => Path.Combine(PluginConfigsDirectory(), PatMeFileName);

    public static string PatMeHonorificPath => Path.Combine(PluginConfigsDirectory(), PatMeHonorificFileName);

    public static bool PatMeAvailable => File.Exists(PatMePath);

    public static bool PatMeHonorificAvailable => File.Exists(PatMeHonorificPath);

    private sealed class PatMeCounter
    {
        public string Name { get; set; } = string.Empty;
        public uint Value { get; set; }
    }

    private sealed class PatMeOwner
    {
        public string Name { get; set; } = string.Empty;
        public ulong CID { get; set; }
        public List<PatMeCounter> Counters { get; set; } = new();
    }

    private sealed class PatMeFile
    {
        public List<PatMeOwner> EmoteData { get; set; } = new();
    }

    private sealed class HonorificTitleData
    {
        public bool IsPrefix { get; set; }
        public System.Numerics.Vector3? Color { get; set; }
        public System.Numerics.Vector3? Glow { get; set; }
        public int? GradientColourSet { get; set; }
        public int? GradientAnimationStyle { get; set; }
    }

    private sealed class HonorificRule
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = string.Empty;
        public string TitleTemplate { get; set; } = string.Empty;
        public HonorificTitleData? TitleDataConfig { get; set; }
        public List<ushort> EmoteIds { get; set; } = new();
        public int Direction { get; set; }
        public List<ulong> CharacterIds { get; set; } = new();
    }

    private sealed class HonorificCounterKey
    {
        public ushort EmoteId { get; set; }
        public int Direction { get; set; }
        public ulong CharacterId { get; set; }
    }

    private sealed class HonorificCounter
    {
        public HonorificCounterKey? Key { get; set; }
        public uint Value { get; set; }
    }

    private sealed class HonorificFile
    {
        public List<HonorificRule> EmoteConfigs { get; set; } = new();
        public List<HonorificCounter> Counters { get; set; } = new();
    }

    public EmoteImportResult ImportPatMe()
    {
        if (!Read<PatMeFile>(PatMePath, out var parsed, out var error))
            return new EmoteImportResult(false, 0, 0, 0, error);

        var characters = 0;
        var imported = 0;

        foreach (var owner in parsed!.EmoteData)
        {
            if (owner.CID == 0) continue;

            var touched = false;

            foreach (var counter in owner.Counters)
            {
                if (counter.Value == 0) continue;
                if (!PatMeEmoteIds.TryGetValue(counter.Name, out var ids) || ids.Length == 0) continue;

                uint known = 0;

                foreach (var id in ids)
                    known += _counters.GetTotal(new EmoteCounterKey(owner.CID, EmoteDirection.Receiving, id));

                if (counter.Value <= known) continue;

                var primary = new EmoteCounterKey(owner.CID, EmoteDirection.Receiving, ids[0]);
                var current = _counters.GetTotal(primary);

                _counters.SetTotal(primary, current + (counter.Value - known));

                imported++;
                touched = true;
            }

            if (touched) characters++;
        }

        _counters.FlushIfDirty();

        _plugin.LogService.Info(LogSource,
            $"Imported {imported} counter(s) for {characters} character(s) from PatMe.");

        return new EmoteImportResult(true, characters, imported, 0,
            imported == 0
                ? "PatMe read, nothing new to add."
                : $"Imported {imported} counter(s) across {characters} character(s).");
    }

    public EmoteImportResult ImportPatMeHonorific()
    {
        if (!Read<HonorificFile>(PatMeHonorificPath, out var parsed, out var error))
            return new EmoteImportResult(false, 0, 0, 0, error);

        var settings = _plugin.Config.ActivityConfig.EmoteTitles;

        var counters = 0;
        var characters = new HashSet<ulong>();

        foreach (var entry in parsed!.Counters)
        {
            if (entry.Key is not { } key) continue;

            var direction = key.Direction == (int)EmoteDirection.Giving
                ? EmoteDirection.Giving
                : EmoteDirection.Receiving;

            _counters.SetTotal(new EmoteCounterKey(key.CharacterId, direction, key.EmoteId), entry.Value);

            characters.Add(key.CharacterId);
            counters++;
        }

        var rules = 0;

        foreach (var source in parsed.EmoteConfigs)
        {
            if (source.EmoteIds.Count == 0) continue;
            if (string.IsNullOrWhiteSpace(source.TitleTemplate)) continue;

            var direction = source.Direction == (int)EmoteDirection.Giving
                ? EmoteDirection.Giving
                : EmoteDirection.Receiving;

            if (settings.Rules.Any(existing =>
                    existing.Direction == direction
                    && existing.EmoteIds.Count == source.EmoteIds.Count
                    && existing.EmoteIds.All(source.EmoteIds.Contains)))
                continue;

            settings.Rules.Add(new EmoteTitleRule
            {
                Enabled = source.Enabled,
                Name = string.IsNullOrWhiteSpace(source.Name) ? DefaultRuleName(direction, source.EmoteIds) : source.Name,
                EmoteIds = new List<ushort>(source.EmoteIds),
                Direction = direction,
                CharacterIds = new List<ulong>(source.CharacterIds),
                Format = ConvertTemplate(source.TitleTemplate),
                PrefixTitle = source.TitleDataConfig?.IsPrefix ?? false,
                Color = source.TitleDataConfig?.Color,
                Glow = source.TitleDataConfig?.Glow,
                GradientColourSet = source.TitleDataConfig?.GradientColourSet,
                GradientAnimationStyle = source.TitleDataConfig?.GradientAnimationStyle,
            });

            rules++;
        }

        _counters.FlushIfDirty();

        if (rules > 0) _plugin.Config.Save();

        _plugin.LogService.Info(LogSource,
            $"Imported {rules} rule(s) and {counters} counter(s) from PatMeHonorific.");

        return new EmoteImportResult(true, characters.Count, counters, rules,
            rules == 0 && counters == 0
                ? "PatMeHonorific read, nothing new to add."
                : $"Imported {rules} rule(s) and {counters} counter(s).");
    }

    internal static string ConvertTemplate(string template) =>
        ScribanToken.Replace(template, match =>
            ScribanPlaceholders.TryGetValue(match.Groups[1].Value, out var placeholder)
                ? placeholder
                : match.Value);

    private static string DefaultRuleName(EmoteDirection direction, IReadOnlyList<ushort> ids)
    {
        var label = ids.Count > 0 ? EmoteCatalog.NameFor(ids[0]) : "Emote";

        return direction == EmoteDirection.Receiving ? $"Receiving {label}" : $"Giving {label}";
    }

    private static string PluginConfigsDirectory() =>
        Path.GetFullPath(Path.Combine(CordiPlugin.PluginInterface.ConfigDirectory.FullName, ".."));

    private bool Read<T>(string path, out T? parsed, out string error) where T : class
    {
        parsed = null;
        error = string.Empty;

        try
        {
            if (!File.Exists(path))
            {
                error = $"Nothing found at {Path.GetFileName(path)}.";
                return false;
            }

            parsed = JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReaderOptions);

            if (parsed is not null) return true;

            error = $"{Path.GetFileName(path)} could not be read.";
            return false;
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error(LogSource, $"Failed to read {Path.GetFileName(path)}", ex);
            error = $"{Path.GetFileName(path)} could not be read: {ex.Message}";

            return false;
        }
    }
}
