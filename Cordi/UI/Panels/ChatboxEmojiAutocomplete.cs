using System;
using System.Collections.Generic;
using System.Numerics;
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
    private string _previousInput = string.Empty;
    private int _selected;
    private bool _dismissed;

    public ChatboxEmojiAutocomplete(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public bool IsOpen => _matches.Count > 0;

    public int ReplaceLength => _fragment.Length + 1;

    public int Caret { get; private set; }

    private ChatboxConfig Config => _plugin.Config.Chatbox;

    private ChatboxService Chatbox => _plugin.Chatbox;

    public void Reset()
    {
        _matches.Clear();
        _catalog.Clear();
        _fragment = string.Empty;
        _previousInput = string.Empty;
        Caret = 0;
        _selected = 0;
        _dismissed = false;
    }

    public void Sync(string? input, int caret)
    {
        _previousInput = input ?? string.Empty;
        Caret = Math.Clamp(caret, 0, _previousInput.Length);
        _matches.Clear();
        _catalog.Clear();
        _fragment = string.Empty;
        _selected = 0;
        _dismissed = false;
    }

    public void Dismiss()
    {
        _matches.Clear();
        _dismissed = true;
    }

    public void Update(string input)
    {
        input ??= string.Empty;

        if (!string.Equals(input, _previousInput, StringComparison.Ordinal))
        {
            Caret = CaretFromDiff(_previousInput, input);
            _previousInput = input;
        }

        var fragment = FragmentAt(input, Caret);

        if (fragment == null)
        {
            _matches.Clear();
            _fragment = string.Empty;
            _dismissed = false;
            return;
        }

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

    public string? Accept()
    {
        if (_matches.Count == 0) return null;

        return _matches[Math.Clamp(_selected, 0, _matches.Count - 1)].Token;
    }

    public string? Draw(Vector2 inputMin, float width)
    {
        if (_matches.Count == 0) return null;

        var rowHeight = ImGui.GetTextLineHeight() + _theme.PadY(0.55f);
        var padding = _theme.PadY(0.3f);
        var height = _matches.Count * rowHeight + padding * 2f;

        var min = new Vector2(inputMin.X, inputMin.Y - _theme.Gap(0.3f) - height);
        var max = new Vector2(inputMin.X + width, min.Y + height);

        var draw = ImGui.GetWindowDrawList();
        var radius = _theme.Radius(0.5f);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(_theme.PanelBg), radius);
        draw.AddRect(min, max, ImGui.GetColorU32(_theme.Border), radius);

        var restore = ImGui.GetCursorScreenPos();
        string? chosen = null;

        for (var i = 0; i < _matches.Count; i++)
        {
            var rowMin = new Vector2(min.X + padding, min.Y + padding + i * rowHeight);
            var rowMax = new Vector2(max.X - padding, rowMin.Y + rowHeight);

            ImGui.SetCursorScreenPos(rowMin);
            ImGui.PushID(i);
            ImGui.InvisibleButton("##row", new Vector2(MathF.Max(1f, rowMax.X - rowMin.X), rowHeight));
            ImGui.PopID();

            if (ImGui.IsItemHovered()) _selected = i;
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) chosen = _matches[i].Token;

            if (i == _selected)
                draw.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(_theme.AccentSelected), _theme.Radius(0.35f));

            DrawRow(draw, _matches[i], rowMin, rowHeight);
        }

        ImGui.SetCursorScreenPos(restore);

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

    private static int CaretFromDiff(string previous, string current)
    {
        var max = Math.Min(previous.Length, current.Length);

        var prefix = 0;
        while (prefix < max && previous[prefix] == current[prefix]) prefix++;

        var suffix = 0;
        while (suffix < max - prefix && previous[^(suffix + 1)] == current[^(suffix + 1)]) suffix++;

        return current.Length - suffix;
    }

    private static string? FragmentAt(string input, int caret)
    {
        if (string.IsNullOrEmpty(input)) return null;

        caret = Math.Clamp(caret, 0, input.Length);

        var start = caret;
        while (start > 0 && caret - start < MaxFragment)
        {
            var c = input[start - 1];
            if (c == ':') break;
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '+' && c != '-') return null;
            start--;
        }

        if (start == 0 || input[start - 1] != ':') return null;
        if (start == caret) return null;

        var colon = start - 1;
        if (colon > 0 && !char.IsWhiteSpace(input[colon - 1])) return null;

        return input[start..caret];
    }
}
