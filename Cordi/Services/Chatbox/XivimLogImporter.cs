using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cordi.Core;

namespace Cordi.Services.Chatbox;

public sealed class XivimLogLine
{
    public DateTime Timestamp { get; init; }
    public string Author { get; init; } = string.Empty;
    public string World { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public StringBuilder Text { get; } = new();

    public string Label => World.Length == 0 ? Author : $"{Author}@{World}";
}

public sealed partial class XivimLogImporter
{
    private const string PluginFolderName = "Messenger";
    private const string TimestampFormat = "yyyy.MM.dd HH:mm:ss zzz";
    private const long MaxLogBytes = 32L * 1024 * 1024;

    private readonly CordiPlugin _plugin;

    public XivimLogImporter(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    [GeneratedRegex(@"^\[(.+?)\] From (.+?)@(\p{L}+): (.*)$")]
    private static partial Regex MessageLine();

    [GeneratedRegex(@"^\[(.+?)\] System: (.*)$")]
    private static partial Regex SystemLine();

    public string DefaultFolder()
    {
        try
        {
            var own = CordiPlugin.PluginInterface.ConfigDirectory.Parent;
            if (own != null) return Path.Combine(own.FullName, PluginFolderName);
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("Chatbox", "Failed to resolve the XIVInstantMessenger log folder.", ex);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher",
            "pluginConfigs",
            PluginFolderName);
    }

    public string ResolveFolder()
    {
        var configured = _plugin.Config.Chatbox.Conversations.XivimLogFolder?.Trim() ?? string.Empty;
        return configured.Length > 0 ? configured : DefaultFolder();
    }

    public IReadOnlyList<string> SearchFolders()
    {
        var root = ResolveFolder();
        var folders = new List<string> { root };

        try
        {
            if (Directory.Exists(root))
            {
                foreach (var child in Directory.EnumerateDirectories(root))
                    folders.Add(child);
            }
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("Chatbox", "Failed to enumerate the XIVInstantMessenger log folders.", ex);
        }

        return folders;
    }

    public IReadOnlyList<string> FindLogFiles()
    {
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in SearchFolders())
        {
            if (!Directory.Exists(folder)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*.txt", SearchOption.TopDirectoryOnly))
                {
                    if (!IsCorrespondentFile(file)) continue;
                    if (seen.Add(Path.GetFileName(file))) files.Add(file);
                }
            }
            catch (Exception ex)
            {
                _plugin.LogService.Error("Chatbox", $"Failed to list XIVInstantMessenger logs in '{folder}'.", ex);
            }
        }

        return files;
    }

    public static bool TrySplitFileName(string path, out string name, out string world)
    {
        name = string.Empty;
        world = string.Empty;

        var label = Path.GetFileNameWithoutExtension(path);
        var at = label.LastIndexOf('@');

        if (at <= 0 || at >= label.Length - 1) return false;

        name = label[..at].Trim();
        world = label[(at + 1)..].Trim();

        return name.Length > 0 && world.Length > 0;
    }

    private static bool IsCorrespondentFile(string path) =>
        TrySplitFileName(path, out _, out _);

    public string? LocateLog(string name, string world)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(world)) return null;

        var fileName = $"{name.Trim()}@{world.Trim()}.txt";

        foreach (var folder in SearchFolders())
        {
            var candidate = Path.Combine(folder, fileName);

            try
            {
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception ex)
            {
                _plugin.LogService.Error("Chatbox", $"Failed to probe '{candidate}'.", ex);
            }
        }

        return null;
    }

    public bool HasLogFor(string name, string world) => LocateLog(name, world) != null;

    public IReadOnlyList<XivimLogLine> Read(string path)
    {
        var result = new List<XivimLogLine>();

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > MaxLogBytes) return result;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);

            string? raw;
            while ((raw = reader.ReadLine()) != null)
                Consume(raw, result);
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("Chatbox", $"Failed to read the XIVInstantMessenger log '{path}'.", ex);
        }

        return result;
    }

    private static void Consume(string raw, List<XivimLogLine> result)
    {
        var line = raw.TrimEnd('\r');
        if (line.Length == 0) return;

        var message = MessageLine().Match(line);

        if (message.Success && TryParseTimestamp(message.Groups[1].Value, out var stamp))
        {
            var entry = new XivimLogLine
            {
                Timestamp = stamp,
                Author = message.Groups[2].Value.Trim(),
                World = message.Groups[3].Value.Trim(),
            };

            entry.Text.Append(message.Groups[4].Value);
            result.Add(entry);
            return;
        }

        var system = SystemLine().Match(line);

        if (system.Success && TryParseTimestamp(system.Groups[1].Value, out var systemStamp))
        {
            var entry = new XivimLogLine
            {
                Timestamp = systemStamp,
                Author = ChatboxMessage.SystemSender,
                IsSystem = true,
            };

            entry.Text.Append(system.Groups[2].Value);
            result.Add(entry);
            return;
        }

        if (result.Count == 0) return;

        result[^1].Text.Append('\n').Append(line);
    }

    private static bool TryParseTimestamp(string value, out DateTime timestamp)
    {
        if (DateTimeOffset.TryParseExact(
                value,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            timestamp = parsed.LocalDateTime;
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            timestamp = parsed.LocalDateTime;
            return true;
        }

        timestamp = default;
        return false;
    }
}
