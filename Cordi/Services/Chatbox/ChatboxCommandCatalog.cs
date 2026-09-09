using System;
using System.Collections.Generic;
using System.Text;

namespace Cordi.Services.Chatbox;

public enum ChatboxCommandSource
{
    Plugin,
    Game,
}

public sealed class ChatboxCommandEntry
{
    public required string Command { get; init; }
    public required string Description { get; init; }
    public required ChatboxCommandSource Source { get; init; }
}

public static class ChatboxCommandCatalog
{
    private const int MaxDescription = 96;

    private static readonly List<ChatboxCommandEntry> GameCommands = new();
    private static readonly Dictionary<string, ChatboxCommandEntry> Lookup = new(StringComparer.OrdinalIgnoreCase);
    private static bool _gameLoaded;

    public static void Search(string fragment, List<ChatboxCommandEntry> results, int limit)
    {
        results.Clear();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, info) in Service.CommandManager.Commands)
        {
            if (!Matches(name, fragment)) continue;
            if (!seen.Add(name)) continue;

            results.Add(new ChatboxCommandEntry
            {
                Command = name,
                Description = Clean(info.HelpMessage),
                Source = ChatboxCommandSource.Plugin,
            });
        }

        foreach (var entry in LoadGameCommands())
        {
            if (!Matches(entry.Command, fragment)) continue;
            if (!seen.Add(entry.Command)) continue;

            results.Add(entry);
        }

        results.Sort(Compare);

        if (results.Count > limit)
            results.RemoveRange(limit, results.Count - limit);
    }

    public static ChatboxCommandEntry? Find(string command)
    {
        if (command.Length < 2) return null;

        if (Service.CommandManager.Commands.TryGetValue(command, out var info))
        {
            return new ChatboxCommandEntry
            {
                Command = command,
                Description = Clean(info.HelpMessage),
                Source = ChatboxCommandSource.Plugin,
            };
        }

        LoadGameCommands();

        return Lookup.TryGetValue(command, out var entry) ? entry : null;
    }

    private static bool Matches(string command, string fragment)
    {
        if (command.Length < 2 || command[0] != '/') return false;
        if (fragment.Length == 0) return true;

        return command.AsSpan(1).StartsWith(fragment, StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(ChatboxCommandEntry a, ChatboxCommandEntry b)
    {
        if (a.Source != b.Source) return a.Source.CompareTo(b.Source);
        if (a.Command.Length != b.Command.Length) return a.Command.Length - b.Command.Length;

        return string.Compare(a.Command, b.Command, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ChatboxCommandEntry> LoadGameCommands()
    {
        if (_gameLoaded) return GameCommands;
        _gameLoaded = true;

        var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TextCommand>();
        if (sheet == null) return GameCommands;

        foreach (var row in sheet)
        {
            var description = Clean(row.Description.ExtractText());

            AddGameCommand(row.Command.ExtractText(), description);
            AddGameCommand(row.ShortCommand.ExtractText(), description);
            AddGameCommand(row.Alias.ExtractText(), description);
            AddGameCommand(row.ShortAlias.ExtractText(), description);
        }

        return GameCommands;
    }

    private static void AddGameCommand(string command, string description)
    {
        command = command.Trim();

        if (command.Length < 2 || command[0] != '/') return;
        if (Lookup.ContainsKey(command)) return;

        var entry = new ChatboxCommandEntry
        {
            Command = command,
            Description = description,
            Source = ChatboxCommandSource.Game,
        };

        Lookup[command] = entry;
        GameCommands.Add(entry);
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var builder = new StringBuilder(text.Length);
        var space = false;

        foreach (var c in text)
        {
            if (c is '\n' or '\r' or '\t' or ' ')
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);

            if (builder.Length >= MaxDescription) break;
        }

        return builder.ToString();
    }
}
