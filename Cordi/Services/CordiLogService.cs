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

    public void Log(string source, CordiLogLevel level, string message, Exception? exception = null,
        bool mute = false)
    {
        if (mute) return;

        var text = exception is null ? message : $"{message}: {exception.Message}";

        var entry = new CordiLogEntry
        {
            Timestamp = DateTime.Now,
            Source = source,
            Level = level,
            Message = text,
        };

        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }

        Forward(source, level, text, exception);
    }

    public void Debug(string source, string message, bool mute = false) =>
        Log(source, CordiLogLevel.Debug, message, mute: mute);

    public void Info(string source, string message, bool mute = false) =>
        Log(source, CordiLogLevel.Info, message, mute: mute);

    public void Warning(string source, string message, bool mute = false) =>
        Log(source, CordiLogLevel.Warning, message, mute: mute);

    public void Error(string source, string message, bool mute = false) =>
        Log(source, CordiLogLevel.Error, message, mute: mute);

    public void Error(string source, string message, Exception exception, bool mute = false) =>
        Log(source, CordiLogLevel.Error, message, exception, mute);

    private static void Forward(string source, CordiLogLevel level, string message, Exception? exception)
    {
        var log = Service.Log;

        if (log is null) return;

        var line = $"[{source}] {message}";

        switch (level)
        {
            case CordiLogLevel.Debug:
                log.Debug(line);
                break;

            case CordiLogLevel.Info:
                log.Info(line);
                break;

            case CordiLogLevel.Warning:
                log.Warning(line);
                break;

            default:
                if (exception is null) log.Error(line);
                else log.Error(exception, line);
                break;
        }
    }

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
