using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Cordi.Services;

public enum CordiLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

public sealed class CordiLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Source { get; init; } = string.Empty;
    public CordiLogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class CordiLogService
{
    private readonly List<CordiLogEntry> _entries = new();
    private readonly object _lock = new();

    public int MaxEntries { get; set; } = 5000;

    public void Log(string source, CordiLogLevel level, string message)
    {
        var entry = new CordiLogEntry
        {
            Timestamp = DateTime.Now,
            Source = source,
            Level = level,
            Message = message,
        };

        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }
    }

    public void Debug(string source, string message) => Log(source, CordiLogLevel.Debug, message);
    public void Info(string source, string message) => Log(source, CordiLogLevel.Info, message);
    public void Warning(string source, string message) => Log(source, CordiLogLevel.Warning, message);
    public void Error(string source, string message) => Log(source, CordiLogLevel.Error, message);

    public List<CordiLogEntry> GetEntries()
    {
        lock (_lock)
        {
            return new List<CordiLogEntry>(_entries);
        }
    }

    public List<string> GetSources()
    {
        lock (_lock)
        {
            return _entries.Select(e => e.Source).Distinct().OrderBy(s => s).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
