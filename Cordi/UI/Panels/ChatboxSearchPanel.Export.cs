using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Panels;

public enum ChatboxExportFormat
{
    PlainText,
    Timestamped,
    Csv,
    Json,
}

public sealed partial class ChatboxSearchPanel
{
    private static readonly IReadOnlyList<DropdownItem> ExportFormatOptions = Options(
        (ChatboxExportFormat.PlainText, "Plain text"),
        (ChatboxExportFormat.Timestamped, "Text with timestamps"),
        (ChatboxExportFormat.Csv, "CSV"),
        (ChatboxExportFormat.Json, "JSON"));

    private readonly HashSet<long> _selected = new();

    private ChatboxExportFormat _format = ChatboxExportFormat.Timestamped;
    private string _exportStatus = string.Empty;
    private string _exportFolder = string.Empty;

    private void DrawExportBar(float width)
    {
        var results = _session.Results;
        if (results.Items.Count == 0) return;

        var height = _theme.Scaled(UiTheme.ControlHeight);
        var gap = _theme.Gap(0.5f);
        var buttonWidth = _theme.Scaled(104f);
        var pickerWidth = _theme.Scaled(180f);

        var origin = ImGui.GetCursorScreenPos();
        var x = origin.X;

        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (_theme.SecondaryButton($"Select all##{_id}-select-all", new Vector2(buttonWidth, height)))
            SelectAll(results);

        x += buttonWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (_theme.SecondaryButton($"Clear##{_id}-select-none", new Vector2(buttonWidth, height)))
            _selected.Clear();

        x += buttonWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        Picker("format", pickerWidth, () => _format, v => _format = v, ExportFormatOptions);

        x += pickerWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (_theme.PrimaryButton($"Copy##{_id}-copy", new Vector2(buttonWidth, height)))
            CopySelection(results);

        x += buttonWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (_theme.SecondaryButton($"Export file##{_id}-export", new Vector2(buttonWidth, height)))
            ExportSelection(results);

        if (_exportFolder.Length > 0)
        {
            x += buttonWidth + gap;
            ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
            if (_theme.SecondaryButton($"Open folder##{_id}-export-open", new Vector2(buttonWidth, height)))
                OpenExportFolder();
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + height));
        ImGui.Dummy(new Vector2(width, _theme.Gap(0.3f)));

        var count = CountSelected(results);
        var label = count == 0
            ? "Nothing ticked - copy and export use all results."
            : $"{count} line(s) ticked.";

        _theme.WrappedText(label, width, _theme.MutedText);

        if (_exportStatus.Length > 0) _theme.WrappedText(_exportStatus, width, _theme.MutedText);
    }

    private void SelectAll(ChatboxSearchResults results)
    {
        _selected.Clear();

        foreach (var message in results.Items)
            _selected.Add(message.Seq);
    }

    private int CountSelected(ChatboxSearchResults results)
    {
        if (_selected.Count == 0) return 0;

        var count = 0;

        foreach (var message in results.Items)
        {
            if (_selected.Contains(message.Seq)) count++;
        }

        return count;
    }

    private void ToggleSelection(long seq)
    {
        if (!_selected.Remove(seq)) _selected.Add(seq);
    }

    private void CopySelection(ChatboxSearchResults results)
    {
        var text = Render(results, _format);

        if (text.Length == 0)
        {
            _exportStatus = "Nothing to copy.";
            return;
        }

        ImGui.SetClipboardText(text);
        _exportStatus = "Copied to the clipboard.";
    }

    private void CopyOne(ChatboxMessage message)
    {
        ImGui.SetClipboardText(Line(message, _format == ChatboxExportFormat.PlainText
            ? ChatboxExportFormat.PlainText
            : ChatboxExportFormat.Timestamped));

        _exportStatus = "Copied one line to the clipboard.";
    }

    private void ExportSelection(ChatboxSearchResults results)
    {
        var text = Render(results, _format);

        if (text.Length == 0)
        {
            _exportStatus = "Nothing to export.";
            return;
        }

        try
        {
            var folder = Path.Combine(CordiPlugin.PluginInterface.ConfigDirectory.FullName, "exports");
            Directory.CreateDirectory(folder);

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(folder, $"chatbox-search-{stamp}.{Extension(_format)}");

            File.WriteAllText(path, text, Encoding.UTF8);

            _exportFolder = folder;
            _exportStatus = $"Exported to {path}";
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("UI", "Failed to export chatbox search results", ex);
            _exportStatus = $"Export failed: {ex.Message}";
        }
    }

    private void OpenExportFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_exportFolder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("UI", "Failed to open the export folder", ex);
        }
    }

    private static string Extension(ChatboxExportFormat format) => format switch
    {
        ChatboxExportFormat.Csv => "csv",
        ChatboxExportFormat.Json => "json",
        _ => "txt",
    };

    private string Render(ChatboxSearchResults results, ChatboxExportFormat format)
    {
        var selected = _selected.Count > 0;
        var builder = new StringBuilder();

        if (format == ChatboxExportFormat.Csv) builder.AppendLine("timestamp,channel,author,world,content");
        if (format == ChatboxExportFormat.Json) builder.Append('[');

        var first = true;

        foreach (var message in results.Items)
        {
            if (selected && !_selected.Contains(message.Seq)) continue;

            if (format == ChatboxExportFormat.Json)
            {
                if (!first) builder.Append(',');
                builder.AppendLine();
                builder.Append("  ").Append(JsonLine(message));
                first = false;
                continue;
            }

            builder.AppendLine(Line(message, format));
            first = false;
        }

        if (format != ChatboxExportFormat.Json) return first ? string.Empty : builder.ToString();

        if (first) return string.Empty;

        builder.AppendLine();
        builder.Append(']');
        return builder.ToString();
    }

    private string Line(ChatboxMessage message, ChatboxExportFormat format)
    {
        var author = message.AuthorWorld.Length > 0
            ? $"{message.AuthorName}@{message.AuthorWorld}"
            : message.AuthorName;

        var content = Flatten(message.RawContent);

        return format switch
        {
            ChatboxExportFormat.Csv => string.Join(',',
                Csv(message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Csv(Chatbox.ChannelDisplayName(message.ChannelId)),
                Csv(message.AuthorName),
                Csv(message.AuthorWorld),
                Csv(message.RawContent)),

            ChatboxExportFormat.PlainText => author.Length > 0 ? $"{author}: {content}" : content,

            _ => string.Create(CultureInfo.InvariantCulture,
                $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}] [{Chatbox.ChannelDisplayName(message.ChannelId)}] {author}: {content}"),
        };
    }

    private string JsonLine(ChatboxMessage message) => JsonSerializer.Serialize(new
    {
        seq = message.Seq,
        timestamp = message.Timestamp.ToString("o", CultureInfo.InvariantCulture),
        channel = Chatbox.ChannelDisplayName(message.ChannelId),
        channelId = message.ChannelId,
        author = message.AuthorName,
        world = message.AuthorWorld,
        content = message.RawContent,
    });

    private static string Csv(string value)
    {
        if (value.Length == 0) return string.Empty;

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string Flatten(string content)
    {
        if (content.IndexOfAny(NewlineChars) < 0) return content;

        return content
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
    }

    private static readonly char[] NewlineChars = { '\r', '\n', '\t' };
}
