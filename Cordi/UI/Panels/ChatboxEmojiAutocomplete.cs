using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Panels;

public sealed class EmojiSuggestion
{
    public required string Token { get; init; }
    public required string Label { get; init; }
    public string? ImageUrl { get; init; }
}

public sealed class ChatboxEmojiAutocomplete
{
    private const int MaxResults = 8;
    private const int CatalogScan = 64;
    private const int MaxFragment = 32;

    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;
    private readonly List<EmojiSuggestion> _matches = new();
    private readonly List<EmojiCatalogEntry> _catalog = new();

    private string _fragment = string.Empty;
    private int _selected;
    private bool _dismissed;
    private Vector2 _boundsMin;
    private Vector2 _boundsMax;

    public ChatboxEmojiAutocomplete(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public bool IsOpen => _matches.Count > 0;

    public int ReplaceLength => _fragment.Length + 1;

    public int FragmentStart { get; private set; }

    private ChatboxConfig Config => _plugin.Config.Chatbox;

    private ChatboxService Chatbox => _plugin.Chatbox;

    public void Reset()
    {
        _matches.Clear();
        _catalog.Clear();
        _fragment = string.Empty;
        FragmentStart = 0;
        _selected = 0;
        _dismissed = false;
    }

    public void Dismiss()
    {
        _matches.Clear();
        _dismissed = true;
    }

    public void Update(ReadOnlySpan<byte> buffer, int caret)
    {
        if (!TryFragment(buffer, caret, out var start, out var fragment))
        {
            _matches.Clear();
            _fragment = string.Empty;
            FragmentStart = 0;
            _dismissed = false;
            return;
        }

        FragmentStart = start;

        if (string.Equals(fragment, _fragment, StringComparison.Ordinal))
        {
            if (_dismissed) _matches.Clear();
            return;
        }

        _fragment = fragment;
        _selected = 0;
        _dismissed = false;

        Rebuild(fragment);
    }

    public void MoveSelection(int delta)
    {
        if (_matches.Count == 0) return;

        _selected = (_selected + delta) % _matches.Count;
        if (_selected < 0) _selected += _matches.Count;
    }

    public EmojiSuggestion? Accept()
    {
        if (_matches.Count == 0) return null;

        return _matches[Math.Clamp(_selected, 0, _matches.Count - 1)];
    }

    public bool Covers(Vector2 point) =>
        _matches.Count > 0
        && point.X >= _boundsMin.X && point.X <= _boundsMax.X
        && point.Y >= _boundsMin.Y && point.Y <= _boundsMax.Y;

    public EmojiSuggestion? Draw(float x, float width, float bottom)
    {
        if (_matches.Count == 0) return null;

        var rowHeight = ImGui.GetTextLineHeight() + _theme.PadY(0.55f);
        var padding = _theme.PadY(0.3f);
        var height = _matches.Count * rowHeight + padding * 2f;

        var min = new Vector2(x, bottom - height);
        var max = new Vector2(x + width, bottom);

        _boundsMin = min;
        _boundsMax = max;

        var draw = ImGui.GetWindowDrawList();
        var radius = _theme.Radius(0.5f);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(_theme.PanelBg), radius);
        draw.AddRect(min, max, ImGui.GetColorU32(_theme.Border), radius);

        var windowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
        EmojiSuggestion? chosen = null;

        for (var i = 0; i < _matches.Count; i++)
        {
            var rowMin = new Vector2(min.X + padding, min.Y + padding + i * rowHeight);
            var rowMax = new Vector2(max.X - padding, rowMin.Y + rowHeight);

            if (windowHovered && ImGui.IsMouseHoveringRect(rowMin, rowMax))
            {
                _selected = i;
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) chosen = _matches[i];
            }

            if (i == _selected)
                draw.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(_theme.AccentSelected), _theme.Radius(0.35f));

            DrawRow(draw, _matches[i], rowMin, rowHeight);
        }

        return chosen;
    }

    private void DrawRow(ImDrawListPtr draw, EmojiSuggestion suggestion, Vector2 rowMin, float rowHeight)
    {
        var glyph = ImGui.GetTextLineHeight();
        var imageMin = new Vector2(rowMin.X + _theme.PadX(0.4f), rowMin.Y + (rowHeight - glyph) * 0.5f);

        Chatbox.ImageCache.Request(suggestion.ImageUrl);
        var texture = Chatbox.ImageCache.Get(suggestion.ImageUrl);

        if (texture != null)
        {
            var imageMax = imageMin + new Vector2(glyph, glyph);
            AnimatedTextureWrap.MarkVisible(texture, imageMin, imageMax);
            draw.AddImage(texture.Handle, imageMin, imageMax);
        }

        var textPos = new Vector2(
            imageMin.X + glyph + _theme.PadX(0.5f),
            rowMin.Y + (rowHeight - glyph) * 0.5f);

        draw.AddText(textPos, ImGui.GetColorU32(_theme.Text), suggestion.Label);
    }

    private void Rebuild(string fragment)
    {
        _matches.Clear();

        foreach (var group in _plugin.Emoji.Guilds.Groups)
        {
            foreach (var emote in group.Emotes)
            {
                if (!emote.Name.StartsWith(fragment, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Add(emote.Token, $":{emote.Name}:", emote.Url)) return;
            }
        }

        if (Config.PickerIncludeSeenEmotes)
        {
            foreach (var seen in Chatbox.Emotes.Snapshot())
            {
                if (!seen.Name.StartsWith(fragment, StringComparison.OrdinalIgnoreCase)) continue;
                if (_plugin.Emoji.Guilds.Contains(seen.Id)) continue;
                if (!Add(seen.Token, $":{seen.Name}:", seen.ImageUrl)) return;
            }
        }

        EmojiCatalog.Search(fragment, _catalog, CatalogScan);

        foreach (var entry in _catalog)
        {
            if (entry.Shortcode.Length == 0) continue;
            if (!entry.Shortcode.StartsWith(fragment, StringComparison.OrdinalIgnoreCase)) continue;

            var label = $":{entry.Shortcode}:";
            if (!Add(label, label, EmojiCatalog.ImageUrl(Config.TwemojiBaseUrl, entry.Glyph))) return;
        }
    }

    private bool Add(string token, string label, string? imageUrl)
    {
        foreach (var existing in _matches)
            if (string.Equals(existing.Token, token, StringComparison.Ordinal)) return true;

        _matches.Add(new EmojiSuggestion { Token = token, Label = label, ImageUrl = imageUrl });

        return _matches.Count < MaxResults;
    }

    private static bool TryFragment(ReadOnlySpan<byte> buffer, int caret, out int start, out string fragment)
    {
        start = 0;
        fragment = string.Empty;

        if (caret <= 0 || caret > buffer.Length) return false;

        var first = caret;
        while (first > 0 && caret - first < MaxFragment)
        {
            var b = buffer[first - 1];
            if (b == (byte)':') break;
            if (!IsFragmentByte(b)) return false;
            first--;
        }

        if (first == 0 || buffer[first - 1] != (byte)':') return false;
        if (first == caret) return false;

        var colon = first - 1;
        if (colon > 0 && !IsBoundaryByte(buffer[colon - 1])) return false;

        start = colon;
        fragment = Encoding.ASCII.GetString(buffer[first..caret]);

        return true;
    }

    private static bool IsFragmentByte(byte b) =>
        b is >= (byte)'a' and <= (byte)'z'
            or >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'0' and <= (byte)'9'
            or (byte)'_' or (byte)'+' or (byte)'-';

    private static bool IsBoundaryByte(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\n';
}
