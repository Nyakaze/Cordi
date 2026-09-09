using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Cordi.UI.Panels;

public enum ChatboxSuggestionKind
{
    Emoji,
    Command,
    Argument,
    Translate,
}

public sealed class ChatboxSuggestion
{
    public required string Token { get; init; }
    public required string Label { get; init; }
    public string? ImageUrl { get; init; }
    public string Detail { get; init; } = string.Empty;
    public ChatboxSuggestionKind Kind { get; init; }
    public FontAwesomeIcon Icon { get; init; }
}

public sealed class ChatboxAutocomplete
{
    private const int MaxResults = 64;
    private const int VisibleRows = 8;
    private const int CatalogScan = 128;
    private const int MaxFragment = 32;

    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;
    private readonly List<ChatboxSuggestion> _matches = new();
    private readonly List<EmojiCatalogEntry> _catalog = new();
    private readonly List<ChatboxCommandEntry> _commands = new();
    private readonly List<string> _arguments = new();
    private readonly List<ChatboxAutoTranslateEntry> _translations = new();
    private readonly List<UiSuggestionItem> _rows = new();

    private ChatboxSuggestionKind _kind = ChatboxSuggestionKind.Emoji;
    private string _owner = string.Empty;
    private string _fragment = string.Empty;
    private string _hint = string.Empty;
    private int _selected;
    private int _scroll;
    private bool _dismissed;
    private bool _translating;
    private Vector2 _boundsMin;
    private Vector2 _boundsMax;

    public ChatboxAutocomplete(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public bool IsOpen => _matches.Count > 0 || _hint.Length > 0;

    public bool HasMatches => _matches.Count > 0;

    public int ReplaceLength { get; private set; }

    public int FragmentStart { get; private set; }

    private ChatboxConfig Config => _plugin.Config.Chatbox;

    private ChatboxService Chatbox => _plugin.Chatbox;

    public void Reset()
    {
        _matches.Clear();
        _catalog.Clear();
        _commands.Clear();
        _arguments.Clear();
        _translations.Clear();
        _rows.Clear();
        _kind = ChatboxSuggestionKind.Emoji;
        _owner = string.Empty;
        _fragment = string.Empty;
        _hint = string.Empty;
        FragmentStart = 0;
        ReplaceLength = 0;
        _selected = 0;
        _scroll = 0;
        _dismissed = false;
        _translating = false;
    }

    public void Dismiss()
    {
        _matches.Clear();
        _hint = string.Empty;
        _dismissed = true;
        _translating = false;
    }

    public void OpenTranslate(ReadOnlySpan<byte> buffer, int caret)
    {
        _translating = true;
        _dismissed = false;
        UpdateTranslate(buffer, caret, true);
    }

    public void Update(ReadOnlySpan<byte> buffer, int caret)
    {
        if (_translating)
        {
            UpdateTranslate(buffer, caret, false);
            return;
        }

        if (TryEmojiFragment(buffer, caret, out var start, out var fragment))
        {
            Apply(ChatboxSuggestionKind.Emoji, string.Empty, start, fragment, fragment.Length + 1);
            return;
        }

        if (TryCommandFragment(buffer, caret, out var kind, out var owner, out start, out fragment, out var replace))
        {
            Apply(kind, owner, start, fragment, replace);
            return;
        }

        _matches.Clear();
        _hint = string.Empty;
        _fragment = string.Empty;
        _owner = string.Empty;
        FragmentStart = 0;
        ReplaceLength = 0;
        _dismissed = false;
    }

    public void MoveSelection(int delta)
    {
        if (_matches.Count == 0) return;

        _selected = (_selected + delta) % _matches.Count;
        if (_selected < 0) _selected += _matches.Count;

        ClampScroll();
    }

    private void ClampScroll()
    {
        if (_matches.Count <= VisibleRows)
        {
            _scroll = 0;
            return;
        }

        if (_selected < _scroll) _scroll = _selected;
        else if (_selected >= _scroll + VisibleRows) _scroll = _selected - VisibleRows + 1;

        _scroll = Math.Clamp(_scroll, 0, _matches.Count - VisibleRows);
    }

    public ChatboxSuggestion? Accept()
    {
        if (_matches.Count == 0) return null;

        return _matches[Math.Clamp(_selected, 0, _matches.Count - 1)];
    }

    public bool Covers(Vector2 point) =>
        IsOpen
        && point.X >= _boundsMin.X && point.X <= _boundsMax.X
        && point.Y >= _boundsMin.Y && point.Y <= _boundsMax.Y;

    public ChatboxSuggestion? Draw(float x, float width, float bottom)
    {
        if (!IsOpen) return null;

        ClampScroll();

        var visible = Math.Min(_matches.Count, VisibleRows);
        var height = _theme.SuggestionPanelHeight(visible, _hint.Length > 0);

        _boundsMin = new Vector2(x, bottom - height);
        _boundsMax = new Vector2(x + width, bottom);

        BuildRows(visible);

        var hit = _theme.SuggestionPanel(
            _boundsMin,
            _boundsMax,
            _rows,
            _scroll,
            _matches.Count,
            _selected,
            _hint,
            AnimatedTextureWrap.MarkVisible);

        if (hit.Hovered >= 0) _selected = hit.Hovered;

        return hit.Clicked >= 0 ? _matches[hit.Clicked] : null;
    }

    private void BuildRows(int visible)
    {
        _rows.Clear();

        for (var row = 0; row < visible; row++)
        {
            var suggestion = _matches[_scroll + row];
            IDalamudTextureWrap? image = null;

            if (suggestion.Kind == ChatboxSuggestionKind.Emoji)
            {
                Chatbox.ImageCache.Request(suggestion.ImageUrl);
                image = Chatbox.ImageCache.Get(suggestion.ImageUrl);
            }

            _rows.Add(new UiSuggestionItem
            {
                Label = suggestion.Label,
                Detail = suggestion.Detail,
                Icon = suggestion.Icon,
                Image = image,
            });
        }
    }

    private void UpdateTranslate(ReadOnlySpan<byte> buffer, int caret, bool force)
    {
        if (caret < 0 || caret > buffer.Length)
        {
            Reset();
            return;
        }

        var start = caret;
        while (start > 0 && !IsBoundaryByte(buffer[start - 1])) start--;

        var end = caret;
        while (end < buffer.Length && !IsBoundaryByte(buffer[end])) end++;

        Apply(ChatboxSuggestionKind.Translate, string.Empty, start, Decode(buffer[start..caret]), end - start, force);
    }

    private void Apply(ChatboxSuggestionKind kind, string owner, int start, string fragment, int replace, bool force = false)
    {
        FragmentStart = start;
        ReplaceLength = replace;

        if (!force
            && kind == _kind
            && string.Equals(owner, _owner, StringComparison.Ordinal)
            && string.Equals(fragment, _fragment, StringComparison.Ordinal))
        {
            if (_dismissed)
            {
                _matches.Clear();
                _hint = string.Empty;
            }

            return;
        }

        _kind = kind;
        _owner = owner;
        _fragment = fragment;
        _selected = 0;
        _scroll = 0;
        _dismissed = false;

        Rebuild();
    }

    private void Rebuild()
    {
        _matches.Clear();
        _hint = string.Empty;

        switch (_kind)
        {
            case ChatboxSuggestionKind.Emoji:
                RebuildEmoji(_fragment);
                break;
            case ChatboxSuggestionKind.Command:
                RebuildCommands(_fragment);
                break;
            case ChatboxSuggestionKind.Argument:
                RebuildArguments(_owner, _fragment);
                break;
            case ChatboxSuggestionKind.Translate:
                RebuildTranslate(_fragment);
                break;
        }
    }

    private void RebuildTranslate(string fragment)
    {
        ChatboxAutoTranslate.Search(fragment, _translations, MaxResults);

        foreach (var entry in _translations)
        {
            _matches.Add(new ChatboxSuggestion
            {
                Token = entry.Token,
                Label = entry.Text,
                Detail = entry.Title,
                Kind = ChatboxSuggestionKind.Translate,
                Icon = FontAwesomeIcon.Language,
            });
        }

        _hint = _matches.Count > 0 ? "Auto-translate" : "Auto-translate  no matches";
    }

    private void RebuildCommands(string fragment)
    {
        ChatboxCommandCatalog.Search(fragment, _commands, MaxResults);

        foreach (var entry in _commands)
        {
            _matches.Add(new ChatboxSuggestion
            {
                Token = entry.Command,
                Label = entry.Command,
                Detail = entry.Description,
                Kind = ChatboxSuggestionKind.Command,
                Icon = entry.Source == ChatboxCommandSource.Plugin
                    ? FontAwesomeIcon.Plug
                    : FontAwesomeIcon.Terminal,
            });
        }
    }

    private void RebuildArguments(string command, string fragment)
    {
        if (ChatboxCommandArguments.TakesPlayerTarget(command))
        {
            ChatboxCommandArguments.SearchPlayers(_plugin, fragment, _arguments, MaxResults);

            foreach (var target in _arguments)
            {
                _matches.Add(new ChatboxSuggestion
                {
                    Token = target,
                    Label = target,
                    Kind = ChatboxSuggestionKind.Argument,
                    Icon = FontAwesomeIcon.User,
                });
            }
        }

        if (fragment.Length > 0) return;

        var entry = ChatboxCommandCatalog.Find(command);
        if (entry == null) return;

        _hint = entry.Description.Length > 0
            ? $"{entry.Command}  {entry.Description}"
            : entry.Command;
    }

    private void RebuildEmoji(string fragment)
    {
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

        _matches.Add(new ChatboxSuggestion
        {
            Token = token,
            Label = label,
            ImageUrl = imageUrl,
            Kind = ChatboxSuggestionKind.Emoji,
        });

        return _matches.Count < MaxResults;
    }

    private static bool TryCommandFragment(
        ReadOnlySpan<byte> buffer,
        int caret,
        out ChatboxSuggestionKind kind,
        out string command,
        out int start,
        out string fragment,
        out int replace)
    {
        kind = ChatboxSuggestionKind.Command;
        command = string.Empty;
        start = 0;
        fragment = string.Empty;
        replace = 0;

        if (caret <= 0 || caret > buffer.Length) return false;
        if (buffer.Length == 0 || buffer[0] != (byte)'/') return false;

        var nameEnd = 1;
        while (nameEnd < buffer.Length && buffer[nameEnd] != (byte)' ') nameEnd++;

        if (caret <= nameEnd)
        {
            if (nameEnd > MaxFragment) return false;

            start = 0;
            replace = nameEnd;
            fragment = Decode(buffer[1..caret]);

            return true;
        }

        command = Decode(buffer[..nameEnd]);
        kind = ChatboxSuggestionKind.Argument;

        var argStart = caret;
        while (argStart > nameEnd + 1 && buffer[argStart - 1] != (byte)' ') argStart--;

        var argEnd = caret;
        while (argEnd < buffer.Length && buffer[argEnd] != (byte)' ') argEnd++;

        if (argEnd - argStart > MaxFragment * 2) return false;

        start = argStart;
        replace = argEnd - argStart;
        fragment = Decode(buffer[argStart..caret]);

        return true;
    }

    private static bool TryEmojiFragment(ReadOnlySpan<byte> buffer, int caret, out int start, out string fragment)
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
        fragment = Decode(buffer[first..caret]);

        return true;
    }

    private static string Decode(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);

    private static bool IsFragmentByte(byte b) =>
        b is >= (byte)'a' and <= (byte)'z'
            or >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'0' and <= (byte)'9'
            or (byte)'_' or (byte)'+' or (byte)'-';

    private static bool IsBoundaryByte(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\n';
}
