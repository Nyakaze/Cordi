using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSearchPanel
{
    private const int ExcerptLength = 400;

    private readonly StringBuilder _excerpt = new();

    private ChatboxMessage? _context;
    private string _hiddenNote = string.Empty;
    private int _hiddenSignature = -1;

    private void DrawStatus(float width)
    {
        if (_validation.Length > 0)
        {
            _theme.WrappedText(_validation, width, UiTheme.TileRed);
            return;
        }

        var results = _session.Results;

        if (results.Error.Length > 0)
        {
            _theme.WrappedText(results.Error, width, UiTheme.TileRed);
            return;
        }

        if (_session.Running)
        {
            _theme.WrappedText("Searching history...", width, _theme.MutedText);
            return;
        }

        if (!_session.HasRun)
        {
            _theme.WrappedText(
                "Searches every message Cordi ever stored, not just the ones currently loaded.",
                width,
                _theme.MutedText);

            DrawHiddenNote(width);
            return;
        }

        var summary = string.Create(CultureInfo.InvariantCulture,
            $"{results.Items.Count} result(s) from {results.Scanned} scanned row(s) in {results.Elapsed.TotalMilliseconds:0} ms");

        _theme.WrappedText(summary, width, _theme.MutedText);

        DrawHiddenNote(width);

        if (results.LimitReached)
            _theme.WrappedText("Stopped at the result limit. Raise it or narrow the filters.", width, UiTheme.TileAmber);

        if (results.ScanCapReached)
            _theme.WrappedText("Stopped early to keep the database responsive.", width, UiTheme.TileAmber);
    }

    private void DrawHiddenNote(float width)
    {
        var signature = 0;

        foreach (var category in Query.ExcludedCategories)
            signature = signature * 31 + (int)category + 1;

        if (signature != _hiddenSignature)
        {
            _hiddenSignature = signature;
            _hiddenNote = signature == 0
                ? string.Empty
                : "Hiding " + string.Join(", ", Query.ExcludedCategories.ConvertAll(ChatboxSearchCategories.Label)) + ".";
        }

        if (_hiddenNote.Length == 0) return;

        _theme.WrappedText(_hiddenNote, width, _theme.MutedText);
    }

    private void DrawResults(float width, float height)
    {
        var results = _session.Results;

        using var child = ImRaii.Child($"##{_id}-results", new Vector2(width, height), true);
        if (!child) return;

        if (results.Items.Count == 0)
        {
            if (_session.HasRun && !_session.Running && results.Error.Length == 0)
                _theme.MutedLabel("No message matched.");

            return;
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var rowHeight = lineHeight * 2f + _theme.Gap(0.35f);
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var rowTotal = rowHeight + spacing;

        var scrollY = ImGui.GetScrollY();
        var viewHeight = ImGui.GetWindowHeight();
        var count = results.Items.Count;

        var first = Math.Clamp((int)(scrollY / rowTotal) - 2, 0, Math.Max(0, count - 1));
        var last = Math.Clamp((int)((scrollY + viewHeight) / rowTotal) + 2, first, count);

        if (first > 0) ImGui.Dummy(new Vector2(0f, MathF.Max(first * rowTotal - spacing, 0f)));

        for (var index = first; index < last; index++)
            DrawResultRow(results.Items[index], rowHeight, lineHeight);

        if (last < count) ImGui.Dummy(new Vector2(0f, MathF.Max((count - last) * rowTotal - spacing, 0f)));

        DrawRowMenu();
    }

    private void DrawResultRow(ChatboxMessage message, float rowHeight, float lineHeight)
    {
        var available = MathF.Max(ImGui.GetContentRegionAvail().X, 40f);
        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.Selectable(
            $"##{_id}-row-{message.Seq}",
            _selected.Contains(message.Seq),
            ImGuiSelectableFlags.None,
            new Vector2(available, rowHeight));

        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            _context = message;
            ImGui.OpenPopup($"##{_id}-row-menu");
        }

        var draw = ImGui.GetWindowDrawList();
        var padX = _theme.Gap(0.4f);
        var boxSize = MathF.Floor(lineHeight * 0.85f);
        var boxMin = new Vector2(origin.X + padX, origin.Y + (rowHeight - boxSize) * 0.5f);
        var boxMax = boxMin + new Vector2(boxSize, boxSize);
        var textX = padX + boxSize + padX;

        var accent = ImGui.GetColorU32(_theme.Accent);
        var muted = ImGui.GetColorU32(_theme.MutedText);
        var body = ImGui.GetColorU32(ImGuiCol.Text);

        draw.PushClipRect(origin, origin + new Vector2(available, rowHeight), true);

        var ticked = _selected.Contains(message.Seq);
        draw.AddRect(boxMin, boxMax, ticked ? accent : muted, _theme.Radius() * 0.5f);

        if (ticked)
            draw.AddRectFilled(boxMin + new Vector2(3f, 3f), boxMax - new Vector2(3f, 3f), accent, _theme.Radius() * 0.5f);

        var header = string.Create(CultureInfo.CurrentCulture,
            $"{message.Timestamp:yyyy-MM-dd HH:mm}  -  {Chatbox.ChannelDisplayName(message.ChannelId)}");

        draw.AddText(origin + new Vector2(textX, 0f), muted, header);

        var headerWidth = ImGui.CalcTextSize(header).X;
        var author = message.AuthorWorld.Length > 0
            ? $"  -  {message.AuthorName}@{message.AuthorWorld}"
            : $"  -  {message.AuthorName}";

        draw.AddText(origin + new Vector2(textX + headerWidth, 0f), accent, author);
        draw.AddText(origin + new Vector2(textX, lineHeight), body, Excerpt(message.RawContent));

        draw.PopClipRect();

        if (hovered && !InsideBox(boxMin, boxMax)) DrawResultTooltip(message);

        if (!clicked) return;

        if (InsideBox(boxMin, boxMax))
        {
            ToggleSelection(message.Seq);
            return;
        }

        OnOpenMessage?.Invoke(message);
    }

    private static bool InsideBox(Vector2 min, Vector2 max)
    {
        var mouse = ImGui.GetMousePos();
        return mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
    }

    private void DrawRowMenu()
    {
        using var popup = ImRaii.Popup($"##{_id}-row-menu");
        if (!popup) return;

        var message = _context;
        if (message == null) return;

        if (ImGui.Selectable("Copy message")) ImGui.SetClipboardText(message.RawContent);
        if (ImGui.Selectable("Copy line")) CopyOne(message);

        if (ImGui.Selectable("Copy player"))
        {
            ImGui.SetClipboardText(message.AuthorWorld.Length > 0
                ? $"{message.AuthorName}@{message.AuthorWorld}"
                : message.AuthorName);
        }

        ImGui.Separator();

        if (ImGui.Selectable(_selected.Contains(message.Seq) ? "Untick line" : "Tick line"))
            ToggleSelection(message.Seq);

        if (ImGui.Selectable("Tick everything")) SelectAll(_session.Results);
        if (ImGui.Selectable("Untick everything")) _selected.Clear();
    }

    private void DrawResultTooltip(ChatboxMessage message)
    {
        using var tooltip = ImRaii.Tooltip();
        using var wrap = ImRaii.TextWrapPos(_theme.Scaled(420f));

        ImGui.TextColored(_theme.MutedText, string.Create(CultureInfo.CurrentCulture,
            $"{message.Timestamp:F} - {Chatbox.ChannelDisplayName(message.ChannelId)}"));

        ImGui.TextColored(_theme.Accent, message.AuthorWorld.Length > 0
            ? $"{message.AuthorName}@{message.AuthorWorld}"
            : message.AuthorName);

        ImGui.TextUnformatted(message.RawContent);

        if (message.Attachments.Count > 0)
            ImGui.TextColored(_theme.MutedText, $"{message.Attachments.Count} attachment(s)");
    }

    private string Excerpt(string content)
    {
        _excerpt.Clear();

        var limit = Math.Min(content.Length, ExcerptLength);
        var lastWasSpace = false;

        for (var i = 0; i < limit; i++)
        {
            var character = content[i];

            if (character is '\n' or '\r' or '\t') character = ' ';

            if (character == ' ')
            {
                if (lastWasSpace) continue;
                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            _excerpt.Append(character);
        }

        if (content.Length > ExcerptLength) _excerpt.Append('…');

        return _excerpt.ToString();
    }
}
