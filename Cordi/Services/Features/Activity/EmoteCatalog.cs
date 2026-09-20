using System;
using System.Collections.Generic;
using System.Globalization;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Activity;

public readonly record struct EmoteCatalogEntry(ushort Id, string Name, string Command);

public static class EmoteCatalog
{
    private static readonly Dictionary<ushort, string> Names = new();
    private static readonly Dictionary<ushort, string> Commands = new();

    private static List<EmoteCatalogEntry>? _all;

    public static IReadOnlyList<EmoteCatalogEntry> All => _all ??= Build();

    public static string NameFor(ushort id)
    {
        if (Names.TryGetValue(id, out var cached)) return cached;

        Resolve(id);

        return Names[id];
    }

    public static string CommandFor(ushort id)
    {
        if (Commands.TryGetValue(id, out var cached)) return cached;

        Resolve(id);

        return Commands[id];
    }

    private static void Resolve(ushort id)
    {
        var name = string.Empty;
        var command = string.Empty;

        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Emote>();

            if (sheet.TryGetRow(id, out var row))
            {
                name = row.Name.ToString();

                if (row.TextCommand.IsValid)
                {
                    var raw = row.TextCommand.Value.Command.ToString();

                    if (!string.IsNullOrEmpty(raw))
                        command = raw.StartsWith('/') ? raw : "/" + raw;
                }
            }
        }
        catch (Exception)
        {
        }

        if (string.IsNullOrWhiteSpace(name) && command.Length > 1)
            name = command.TrimStart('/');

        Names[id] = string.IsNullOrWhiteSpace(name)
            ? $"Emote #{id}"
            : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);

        Commands[id] = command;
    }

    private static List<EmoteCatalogEntry> Build()
    {
        var entries = new List<EmoteCatalogEntry>();

        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Emote>();

            foreach (var row in sheet)
            {
                if (row.RowId == 0 || row.RowId > ushort.MaxValue) continue;

                var name = row.Name.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var command = string.Empty;

                if (row.TextCommand.IsValid)
                {
                    var raw = row.TextCommand.Value.Command.ToString();

                    if (!string.IsNullOrEmpty(raw))
                        command = raw.StartsWith('/') ? raw : "/" + raw;
                }

                var id = (ushort)row.RowId;
                var display = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);

                Names[id] = display;
                Commands[id] = command;
                entries.Add(new EmoteCatalogEntry(id, display, command));
            }
        }
        catch (Exception)
        {
        }

        entries.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return entries;
    }
}
