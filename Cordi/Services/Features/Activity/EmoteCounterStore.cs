using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cordi.Configuration;

namespace Cordi.Services.Activity;

public readonly record struct EmoteCounterKey(ulong CharacterId, EmoteDirection Direction, ushort EmoteId);

public sealed class EmoteCounterStore
{
    private const string FileName = "emote_counters.json";
    private const string LogSource = "Activity";
    private const int FileVersion = 1;

    private static readonly TimeSpan ComboWindow = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly Dictionary<EmoteCounterKey, uint> _totals = new();
    private readonly Dictionary<EmoteCounterKey, uint> _session = new();
    private readonly Dictionary<EmoteCounterKey, ComboState> _combo = new();
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _tempPath;
    private readonly CordiLogService _log;

    private bool _dirty;

    public EmoteCounterStore(string configDirectory, CordiLogService log)
    {
        _path = Path.Combine(configDirectory, FileName);
        _tempPath = _path + ".tmp";
        _log = log;
    }

    public string FilePath => _path;

    public int Count
    {
        get
        {
            lock (_gate) return _totals.Count;
        }
    }

    private struct ComboState
    {
        public uint Count;
        public DateTime Expires;
    }

    private sealed class CounterEntry
    {
        public ulong CharacterId { get; set; }
        public int Direction { get; set; }
        public ushort EmoteId { get; set; }
        public uint Total { get; set; }
    }

    private sealed class CounterFile
    {
        public int Version { get; set; } = FileVersion;
        public List<CounterEntry> Counters { get; set; } = new();
    }

    public void Load()
    {
        lock (_gate)
        {
            _totals.Clear();
            _session.Clear();
            _combo.Clear();
        }

        try
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, "Could not remove a leftover emote counter temp file", ex);
        }

        try
        {
            if (!File.Exists(_path)) return;

            var parsed = JsonSerializer.Deserialize<CounterFile>(File.ReadAllText(_path));
            if (parsed?.Counters is not { } entries) return;

            lock (_gate)
            {
                foreach (var entry in entries)
                {
                    var key = new EmoteCounterKey(entry.CharacterId, (EmoteDirection)entry.Direction, entry.EmoteId);
                    _totals[key] = entry.Total;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, "Failed to load the emote counters", ex);
        }
    }

    public uint Increment(EmoteCounterKey key)
    {
        uint total;

        lock (_gate)
        {
            total = _totals.TryGetValue(key, out var stored) ? stored + 1u : 1u;
            _totals[key] = total;

            _session[key] = _session.TryGetValue(key, out var session) ? session + 1u : 1u;

            var now = DateTime.Now;
            var combo = _combo.TryGetValue(key, out var state) && now < state.Expires ? state.Count + 1u : 1u;
            _combo[key] = new ComboState { Count = combo, Expires = now + ComboWindow };

            _dirty = true;
        }

        return total;
    }

    public void AddTotal(EmoteCounterKey key, uint amount)
    {
        if (amount == 0) return;

        lock (_gate)
        {
            _totals[key] = _totals.TryGetValue(key, out var stored) ? stored + amount : amount;
            _dirty = true;
        }
    }

    public void SetTotal(EmoteCounterKey key, uint value)
    {
        lock (_gate)
        {
            if (_totals.TryGetValue(key, out var stored) && stored == value) return;

            _totals[key] = value;
            _dirty = true;
        }
    }

    public uint GetTotal(EmoteCounterKey key)
    {
        lock (_gate) return _totals.TryGetValue(key, out var value) ? value : 0u;
    }

    public uint GetSession(EmoteCounterKey key)
    {
        lock (_gate) return _session.TryGetValue(key, out var value) ? value : 0u;
    }

    public uint GetCombo(EmoteCounterKey key)
    {
        lock (_gate)
        {
            if (!_combo.TryGetValue(key, out var state)) return 0u;

            return DateTime.Now < state.Expires ? state.Count : 0u;
        }
    }

    public IReadOnlyList<KeyValuePair<EmoteCounterKey, uint>> Snapshot()
    {
        lock (_gate) return new List<KeyValuePair<EmoteCounterKey, uint>>(_totals);
    }

    public void ResetSession()
    {
        lock (_gate)
        {
            _session.Clear();
            _combo.Clear();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _totals.Clear();
            _session.Clear();
            _combo.Clear();
            _dirty = true;
        }

        FlushIfDirty();
    }

    public void FlushIfDirty()
    {
        CounterFile payload;

        lock (_gate)
        {
            if (!_dirty) return;

            _dirty = false;

            payload = new CounterFile();

            foreach (var (key, total) in _totals)
            {
                payload.Counters.Add(new CounterEntry
                {
                    CharacterId = key.CharacterId,
                    Direction = (int)key.Direction,
                    EmoteId = key.EmoteId,
                    Total = total,
                });
            }
        }

        try
        {
            File.WriteAllText(_tempPath, JsonSerializer.Serialize(payload, SerializerOptions));
            File.Move(_tempPath, _path, true);
        }
        catch (Exception ex)
        {
            lock (_gate) _dirty = true;

            _log.Log(LogSource, CordiLogLevel.Warning, "Failed to save the emote counters", ex);
        }
    }
}
