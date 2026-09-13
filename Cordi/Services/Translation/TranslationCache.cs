using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cordi.Services.Translation;

public sealed class TranslationCache
{
    private const string FileName = "translation_cache.json";
    private const string LogSource = "Translation";

    private readonly LinkedList<CacheEntry> _order = new();
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _index = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly string _path;
    private readonly Func<int> _limit;
    private readonly CordiLogService _log;

    private int _saveScheduled;

    public TranslationCache(string configDirectory, Func<int> limit, CordiLogService log)
    {
        _path = Path.Combine(configDirectory, FileName);
        _limit = limit;
        _log = log;
    }

    public int Count
    {
        get
        {
            lock (_gate) return _index.Count;
        }
    }

    private readonly record struct CacheEntry(string Key, string Translated, string? Source);

    public static string KeyFor(string? sourceIso, string targetIso, string text) =>
        (sourceIso ?? string.Empty) + "" + targetIso + "" + text;

    public bool TryGet(string key, out string translated, out string? source)
    {
        lock (_gate)
        {
            if (!_index.TryGetValue(key, out var node))
            {
                translated = string.Empty;
                source = null;
                return false;
            }

            _order.Remove(node);
            _order.AddLast(node);
            translated = node.Value.Translated;
            source = node.Value.Source;
            return true;
        }
    }

    public void Store(string key, string translated, string? source)
    {
        var limit = Math.Max(16, _limit());

        lock (_gate)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _order.AddLast(existing);
                return;
            }

            while (_index.Count >= limit && _order.First != null)
            {
                var oldest = _order.First;
                _order.RemoveFirst();
                _index.Remove(oldest.Value.Key);
            }

            _index[key] = _order.AddLast(new CacheEntry(key, translated, source));
        }

        ScheduleSave();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _order.Clear();
            _index.Clear();
        }

        ScheduleSave();
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var entries = JsonSerializer.Deserialize<List<string[]>>(File.ReadAllText(_path));
            if (entries == null) return;

            var limit = Math.Max(16, _limit());
            var start = entries.Count > limit ? entries.Count - limit : 0;

            lock (_gate)
            {
                _order.Clear();
                _index.Clear();

                for (var i = start; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry.Length < 2 || _index.ContainsKey(entry[0])) continue;

                    var source = entry.Length > 2 && !string.IsNullOrEmpty(entry[2]) ? entry[2] : null;

                    _index[entry[0]] = _order.AddLast(new CacheEntry(entry[0], entry[1], source));
                }
            }
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, "Failed to load the translation cache", ex);

            lock (_gate)
            {
                _order.Clear();
                _index.Clear();
            }
        }
    }

    public void Save()
    {
        List<string[]> snapshot;

        lock (_gate)
        {
            snapshot = new List<string[]>(_order.Count);
            foreach (var entry in _order)
                snapshot.Add([entry.Key, entry.Translated, entry.Source ?? string.Empty]);
        }

        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(snapshot));
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, "Failed to save the translation cache", ex);
        }
    }

    private void ScheduleSave()
    {
        if (Interlocked.Exchange(ref _saveScheduled, 1) == 1) return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(2000).ConfigureAwait(false);
            Interlocked.Exchange(ref _saveScheduled, 0);
            Save();
        });
    }
}
