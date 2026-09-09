using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Text;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxAutoTranslateEntry
{
    public required uint Group { get; init; }
    public required uint Key { get; init; }
    public required string Text { get; init; }
    public required string Title { get; init; }

    public string Token => $"{ChatboxAutoTranslate.TagPrefix}{Group},{Key}>";
}

public static class ChatboxAutoTranslate
{
    public const string TagPrefix = "<at:";

    private const string LogSource = "Chatbox";
    private const int MaxTagLength = 24;

    private static readonly List<ChatboxAutoTranslateEntry> Entries = new();
    private static readonly HashSet<(uint Group, uint Key)> Valid = new();
    private static readonly object Gate = new();

    private static CordiLogService? _log;
    private static bool _loaded;

    public static void Preload(CordiLogService log)
    {
        _log = log;
        Task.Run(EnsureLoaded);
    }

    public static void Search(string fragment, List<ChatboxAutoTranslateEntry> results, int limit)
    {
        results.Clear();
        EnsureLoaded();

        List<ChatboxAutoTranslateEntry>? prefixed = null;
        List<ChatboxAutoTranslateEntry>? contained = null;

        lock (Gate)
        {
            foreach (var entry in Entries)
            {
                switch (Rank(entry, fragment))
                {
                    case 0:
                        if (results.Count < limit) results.Add(entry);
                        break;

                    case 1:
                        prefixed ??= new List<ChatboxAutoTranslateEntry>();
                        if (prefixed.Count < limit) prefixed.Add(entry);
                        break;

                    case 2:
                        contained ??= new List<ChatboxAutoTranslateEntry>();
                        if (contained.Count < limit) contained.Add(entry);
                        break;
                }

                if (results.Count >= limit && prefixed?.Count >= limit && contained?.Count >= limit) break;
            }
        }

        Fill(results, prefixed, limit);
        Fill(results, contained, limit);
    }

    public static bool TryExpandTags(string text, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        if (string.IsNullOrEmpty(text)) return false;
        if (text.IndexOf(TagPrefix, StringComparison.Ordinal) < 0) return false;

        EnsureLoaded();

        var buffer = new List<byte>(Encoding.UTF8.GetByteCount(text));
        var index = 0;
        var replaced = false;

        while (index < text.Length)
        {
            var open = text.IndexOf(TagPrefix, index, StringComparison.Ordinal);
            if (open < 0) break;

            var close = text.IndexOf('>', open + TagPrefix.Length);
            if (close < 0) break;

            if (!TryParseTag(text.AsSpan(open + TagPrefix.Length, close - open - TagPrefix.Length), out var group, out var key))
            {
                AppendUtf8(buffer, text.AsSpan(index, close + 1 - index));
                index = close + 1;
                continue;
            }

            AppendUtf8(buffer, text.AsSpan(index, open - index));
            buffer.AddRange(CreateFixedPayload(group, key));
            replaced = true;
            index = close + 1;
        }

        if (!replaced) return false;

        AppendUtf8(buffer, text.AsSpan(index));
        bytes = buffer.ToArray();

        return true;
    }

    private static void Fill(List<ChatboxAutoTranslateEntry> results, List<ChatboxAutoTranslateEntry>? source, int limit)
    {
        if (source == null) return;

        foreach (var entry in source)
        {
            if (results.Count >= limit) return;
            results.Add(entry);
        }
    }

    private static int Rank(ChatboxAutoTranslateEntry entry, string fragment)
    {
        if (fragment.Length == 0) return 1;

        if (entry.Text.Equals(fragment, StringComparison.OrdinalIgnoreCase)) return 0;
        if (entry.Text.StartsWith(fragment, StringComparison.OrdinalIgnoreCase)) return 1;
        if (entry.Text.Contains(fragment, StringComparison.OrdinalIgnoreCase)) return 2;

        if (entry.Title.Length == 0) return -1;

        if (entry.Title.Equals(fragment, StringComparison.OrdinalIgnoreCase)) return 0;
        if (entry.Title.StartsWith(fragment, StringComparison.OrdinalIgnoreCase)) return 1;
        if (entry.Title.Contains(fragment, StringComparison.OrdinalIgnoreCase)) return 2;

        return -1;
    }

    private static bool TryParseTag(ReadOnlySpan<char> body, out uint group, out uint key)
    {
        group = 0;
        key = 0;

        if (body.Length == 0 || body.Length > MaxTagLength) return false;

        var comma = body.IndexOf(',');
        if (comma <= 0 || comma == body.Length - 1) return false;

        if (!uint.TryParse(body[..comma], out group)) return false;
        if (!uint.TryParse(body[(comma + 1)..], out key)) return false;

        lock (Gate) return Valid.Contains((group, key));
    }

    private static byte[] CreateFixedPayload(uint group, uint key) =>
        new SeStringBuilder()
            .BeginMacro(MacroCode.Fixed)
            .AppendUIntExpression(group - 1)
            .AppendUIntExpression(key)
            .EndMacro()
            .ToArray();

    private static void AppendUtf8(List<byte> buffer, ReadOnlySpan<char> text)
    {
        if (text.Length == 0) return;

        var scratch = new byte[Encoding.UTF8.GetByteCount(text)];
        Encoding.UTF8.GetBytes(text, scratch);
        buffer.AddRange(scratch);
    }

    private static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                Load();
            }
            catch (Exception ex)
            {
                _log?.Error(LogSource, "Failed to build the auto-translate catalog", ex);
            }
        }
    }

    private static void Load()
    {
        var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Completion>();
        if (sheet == null) return;

        foreach (var row in sheet)
        {
            var lookup = ReadLookup(row.LookupTable);

            try
            {
                if (lookup.Length == 0)
                {
                    var text = row.Text.ExtractText();
                    if (text.Length == 0) continue;

                    Add(row.Group, row.RowId, text, row.GroupTitle.ExtractText());
                    continue;
                }

                if (lookup == "@") continue;

                LoadFromSheet(row.Group, lookup);
            }
            catch (Exception ex)
            {
                _log?.Debug(LogSource, $"Auto-translate lookup \"{lookup}\" skipped: {ex.Message}");
            }
        }
    }

    private static void LoadFromSheet(uint group, string lookup)
    {
        if (!TryParseLookup(lookup, out var sheetName, out var rows, out var columns)) return;

        var sheet = Service.DataManager.Excel.GetSheet<RawRow>(name: sheetName);
        if (sheet.Count == 0) return;

        if (columns.Count == 0) columns.Add(0);
        if (rows.Count == 0) rows.Add((0, (int)sheet.GetRowAt(sheet.Count - 1).RowId + 1));

        foreach (var (start, end) in rows)
        {
            for (var i = start; i < end; i++)
            {
                if (!sheet.TryGetRow((uint)i, out var raw)) continue;

                foreach (var column in columns)
                {
                    var text = raw.ReadStringColumn(column).ExtractText();
                    if (text.Length == 0) continue;

                    Add(group, (uint)i, text, string.Empty);
                }
            }
        }
    }

    private static bool TryParseLookup(
        string lookup,
        out string sheetName,
        out List<(int Start, int End)> rows,
        out List<int> columns)
    {
        rows = new List<(int Start, int End)>();
        columns = new List<int>();

        var open = lookup.IndexOf('[');
        if (open < 0)
        {
            sheetName = lookup;
            return sheetName.Length > 0;
        }

        sheetName = lookup[..open];
        if (sheetName.Length == 0) return false;

        var close = lookup.LastIndexOf(']');
        if (close <= open) return false;

        foreach (var part in lookup[(open + 1)..close].Split(','))
        {
            if (part.Length == 0 || part == "noun") continue;

            if (part.StartsWith("col-", StringComparison.Ordinal))
            {
                if (!int.TryParse(part[4..], out var column)) return false;

                columns.Add(column);
                continue;
            }

            var dash = part.IndexOf('-');
            if (dash > 0)
            {
                if (!int.TryParse(part[..dash], out var first)) return false;
                if (!int.TryParse(part[(dash + 1)..], out var last)) return false;

                rows.Add((first, last + 1));
                continue;
            }

            if (!int.TryParse(part, out var single)) return false;

            rows.Add((single, single + 1));
        }

        return true;
    }

    private static string ReadLookup(ReadOnlySeString value)
    {
        var builder = new StringBuilder();

        foreach (var payload in value)
        {
            if (payload.Type == ReadOnlySePayloadType.Text)
            {
                builder.Append(Encoding.UTF8.GetString(payload.Body.Span));
                continue;
            }

            if (payload.MacroCode != MacroCode.Num) continue;
            if (!payload.TryGetExpression(out var expression)) continue;
            if (expression.TryGetInt(out var number)) builder.Append(number.ToString(CultureInfo.InvariantCulture));
        }

        return builder.Replace(" ", string.Empty).ToString();
    }

    private static void Add(uint group, uint key, string text, string title)
    {
        Entries.Add(new ChatboxAutoTranslateEntry
        {
            Group = group,
            Key = key,
            Text = text,
            Title = title,
        });

        Valid.Add((group, key));
    }
}
