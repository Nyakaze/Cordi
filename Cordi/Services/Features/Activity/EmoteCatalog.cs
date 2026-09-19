using System;
using System.Collections.Generic;
using System.Globalization;
using Lumina.Excel.Sheets;

namespace Cordi.Services.Activity;

public readonly record struct EmoteCatalogEntry(ushort Id, string Name, string Command);

public static class EmoteCatalog
{
    private static readonly Dictionary<ushort, string> Names = new();

    private static List<EmoteCatalogEntry>? _all;

    public static IReadOnlyList<EmoteCatalogEntry> All => _all ??= Build();

    public static string NameFor(ushort id)
    {
        if (Names.TryGetValue(id, out var cached)) return cached;

        var resolved = Resolve(id);
        Names[id] = resolved;

        return resolved;
    }

    private static string Resolve(ushort id)
    {
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Emote>();

            if (sheet.TryGetRow(id, out var row))
            {
                var name = row.Name.ToString();

                if (!string.IsNullOrWhiteSpace(name))
                    return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
            }
        }
        catch (Exception)
        {
        }

        return $"Emote #{id}";
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
