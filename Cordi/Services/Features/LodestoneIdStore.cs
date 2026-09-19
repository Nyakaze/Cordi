using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Cordi.Services.Features;

public sealed class LodestoneIdStore
{
    private const string FileName = "lodestone_ids.json";
    private const string LogSource = "Lodestone";
    private const int FileVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly ConcurrentDictionary<string, string> _ids = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _writeGate = new();
    private readonly string _path;
    private readonly string _tempPath;
    private readonly CordiLogService _log;

    private bool _dirty;

    public LodestoneIdStore(string configDirectory, CordiLogService log)
    {
        _path = Path.Combine(configDirectory, FileName);
        _tempPath = _path + ".tmp";
        _log = log;
    }

    public string FilePath => _path;

    public int Count => _ids.Count;

    public IReadOnlyCollection<string> Keys => _ids.Keys.ToArray();

    private sealed class IdFile
    {
        public int Version { get; set; } = FileVersion;
        public Dictionary<string, string> Ids { get; set; } = new();
    }

    public void Load(IDictionary<string, string> legacy)
    {
        try
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, "Could not remove a leftover lodestone id temp file", ex);
        }

        try
        {
            if (File.Exists(_path) &&
                JsonSerializer.Deserialize<IdFile>(File.ReadAllText(_path))?.Ids is { } stored)
            {
                foreach (var pair in stored)
                {
                    if (!string.IsNullOrEmpty(pair.Value)) _ids[pair.Key] = pair.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, "Failed to load the lodestone ids", ex);
        }

        if (legacy.Count == 0) return;

        foreach (var pair in legacy)
        {
            if (!string.IsNullOrEmpty(pair.Value)) _ids.TryAdd(pair.Key, pair.Value);
        }

        var migrated = legacy.Count;
        legacy.Clear();
        _dirty = true;
        Flush();

        _log.Info(LogSource, $"Migrated {migrated} lodestone ids out of the configuration into {FileName}");
    }

    public bool TryGet(string key, out string id) => _ids.TryGetValue(key, out id!);

    public void Set(string key, string id)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(id)) return;
        if (_ids.TryGetValue(key, out var stored) && stored == id) return;

        _ids[key] = id;
        _dirty = true;
        Flush();
    }

    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_ids);

    public void Clear()
    {
        _ids.Clear();
        _dirty = true;
        Flush();
    }

    public void Flush()
    {
        lock (_writeGate)
        {
            if (!_dirty) return;

            _dirty = false;

            var payload = new IdFile { Ids = new Dictionary<string, string>(_ids) };

            try
            {
                File.WriteAllText(_tempPath, JsonSerializer.Serialize(payload, SerializerOptions));
                File.Move(_tempPath, _path, true);
            }
            catch (Exception ex)
            {
                _dirty = true;
                _log.Log(LogSource, CordiLogLevel.Warning, "Failed to save the lodestone ids", ex);
            }
        }
    }
}
