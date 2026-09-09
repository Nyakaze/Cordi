using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.Services.Emojis;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed class ChatboxEmojiPicker
{
    public const string PopupId = "##chatbox-emoji-popup";

    private const int SearchLimit = 120;
    private const int SeenSearchLimit = 60;

    private const string FilterAll = "";
    private const string FilterFavorites = "fav";
    private const string FilterRecent = "recent";
    private const string FilterOwn = "own";
    private const string FilterOthers = "others";

    private static readonly Regex CustomToken = new(
        @"^<(?<a>a?):(?<name>[A-Za-z0-9_~]{2,32}):(?<id>\d{5,25})>$",
        RegexOptions.Compiled);

    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;

    private readonly List<EmojiCatalogEntry> _matches = new();
    private readonly List<ChatboxSeenEmote> _ownEmotes = new();
    private readonly List<ChatboxSeenEmote> _otherEmotes = new();
    private readonly List<(string Key, string Label)> _chips = new();

    private int _guildVersion = -1;
    private int _seenVersion = -1;

    private string _emojiQuery = string.Empty;
    private string _filter = FilterAll;
    private bool _searchActive;
    private bool _unicodeOpen;
    private int _column;
    private int _columns = 1;
    private float _cellSize;
    private float _cellSpacing;
    private int _cellId;
    private bool _requestOpen;
    private bool _popupOpen;

    public ChatboxEmojiPicker(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public bool InputActive => _popupOpen;

    private ChatboxConfig Config => _plugin.Config.Chatbox;
    private ChatboxService Chatbox => _plugin.Chatbox;
    private GuildEmoteCache GuildEmotes => _plugin.Emoji.Guilds;

    public void Open() => _requestOpen = true;

    public void Draw(Action<string, string?> insert)
    {
        if (_requestOpen)
        {
            _requestOpen = false;
            ImGui.OpenPopup(PopupId);
        }

        ImGui.SetNextWindowSize(new Vector2(_theme.Scaled(420f), _theme.Scaled(470f)), ImGuiCond.Always);

        using var scope = _theme.PickerPopupScope();

        if (!ImGui.BeginPopup(PopupId, ImGuiWindowFlags.NoMove))
        {
            _popupOpen = false;
            return;
        }

        _popupOpen = true;

        try
        {
            DrawPicker(insert);
        }
        finally
        {
            ImGui.EndPopup();
        }
    }

    private void DrawPicker(Action<string, string?> insert)
    {
        var width = ImGui.GetContentRegionAvail().X;

        _theme.PickerSearch("##chatbox-emoji-search", width, ref _emojiQuery, "Search emoji", ref _searchActive);
        _theme.SpacerY(0.4f);

        RefreshSeenEmotes();

        var query = _emojiQuery.Trim();

        if (query.Length == 0)
        {
            DrawFilterChips(width);
            _theme.SpacerY(0.3f);
        }

        using var child = ImRaii.Child("##chatbox-emoji-scroll", new Vector2(0f, 0f), false);
        if (!child) return;

        BeginGrid(_theme.Scaled(30f), _theme.Gap(0.35f));

        if (query.Length > 0) DrawEmojiSearchResults(insert, query);
        else DrawCategories(insert);

        EndGrid();
    }

    private void DrawFilterChips(float width)
    {
        BuildChips();
        if (_chips.Count <= 1) return;

        var spacing = _theme.Gap(0.3f);
        var height = _theme.PickerChipHeight();
        var origin = ImGui.GetCursorScreenPos();
        var x = origin.X;
        var y = origin.Y;

        for (var i = 0; i < _chips.Count; i++)
        {
            var chip = _chips[i];
            var chipWidth = _theme.PickerChipWidth(chip.Label);

            if (x > origin.X && x + chipWidth > origin.X + width)
            {
                x = origin.X;
                y += height + spacing;
            }

            if (_theme.PickerChip($"##emoji-chip-{i}", new Vector2(x, y), chip.Label, _filter == chip.Key))
                _filter = chip.Key;

            x += chipWidth + spacing;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, y + height));
    }

    private void BuildChips()
    {
        _chips.Clear();
        _chips.Add((FilterAll, "All"));

        if (Config.FavoriteEmojis.Count > 0) _chips.Add((FilterFavorites, "Favorites"));
        if (Config.RecentEmojis.Count > 0) _chips.Add((FilterRecent, "Frequent"));

        foreach (var guild in GuildEmotes.Groups)
        {
            if (guild.Emotes.Count == 0) continue;

            _chips.Add((GuildKey(guild.Guild), guild.Guild));
        }

        if (_ownEmotes.Count > 0) _chips.Add((FilterOwn, "Own"));
        if (Config.ShowOthersEmotes && _otherEmotes.Count > 0) _chips.Add((FilterOthers, "Others"));

        foreach (var chip in _chips)
            if (chip.Key == _filter) return;

        _filter = FilterAll;
    }

    private static string GuildKey(string guild) => $"g:{guild}";

    private bool Shows(string key) => _filter.Length == 0 || string.Equals(_filter, key, StringComparison.Ordinal);

    private void DrawCategories(Action<string, string?> insert)
    {
        var any = false;

        if (Shows(FilterFavorites) && Config.FavoriteEmojis.Count > 0)
        {
            Section("Favorites", Config.FavoriteEmojis.Count);
            foreach (var token in Config.FavoriteEmojis.ToArray())
                DrawTokenCell(insert, token);

            any = true;
        }

        if (Shows(FilterRecent) && Config.RecentEmojis.Count > 0)
        {
            Section("Frequently Used", Config.RecentEmojis.Count);
            foreach (var token in Config.RecentEmojis.ToArray())
                DrawTokenCell(insert, token);

            any = true;
        }

        foreach (var guild in GuildEmotes.Groups)
        {
            if (guild.Emotes.Count == 0) continue;
            if (!Shows(GuildKey(guild.Guild))) continue;

            Section(guild.Guild, guild.Emotes.Count);
            foreach (var emote in guild.Emotes)
                DrawTokenCell(insert, emote.Token, emote.Name);

            any = true;
        }

        if (Shows(FilterOwn) && _ownEmotes.Count > 0)
        {
            Section("Own Emojis", _ownEmotes.Count);
            foreach (var emote in _ownEmotes)
                DrawTokenCell(insert, emote.Token, emote.Name);

            any = true;
        }

        if (Shows(FilterOthers) && Config.ShowOthersEmotes && _otherEmotes.Count > 0)
        {
            Section("Others Emojis", _otherEmotes.Count);
            foreach (var emote in _otherEmotes)
                DrawTokenCell(insert, emote.Token, emote.Name);

            any = true;
        }

        if (_filter.Length > 0)
        {
            if (!any) EmptyState("Nothing in this category yet.");
            return;
        }

        DrawUnicodeSection(insert);
    }

    private void DrawUnicodeSection(Action<string, string?> insert)
    {
        EndGrid();
        _theme.SpacerY(0.3f);

        var width = ImGui.GetContentRegionAvail().X;
        if (_theme.PickerCollapsible("##chatbox-emoji-unicode", "Emoji", _unicodeOpen, width))
            _unicodeOpen = !_unicodeOpen;

        if (!_unicodeOpen) return;

        foreach (var group in EmojiCatalog.Groups)
        {
            Section(group.Name, group.Entries.Count);
            foreach (var entry in group.Entries)
                DrawTokenCell(insert, entry.Glyph, entry.Name);
        }
    }

    private void DrawEmojiSearchResults(Action<string, string?> insert, string query)
    {
        var any = false;
        var custom = 0;

        foreach (var guild in GuildEmotes.Groups)
        {
            foreach (var emote in guild.Emotes)
            {
                if (emote.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (custom == 0) Section("Custom", 0);

                DrawTokenCell(insert, emote.Token, emote.Name);
                custom++;
                any = true;
            }
        }

        any |= DrawSeenSearchResults(insert, query, _ownEmotes, "Own Emojis");

        if (Config.ShowOthersEmotes)
            any |= DrawSeenSearchResults(insert, query, _otherEmotes, "Others Emojis");

        EmojiCatalog.Search(query, _matches, SearchLimit);

        if (_matches.Count > 0)
        {
            Section("Emoji", _matches.Count);
            foreach (var entry in _matches)
                DrawTokenCell(insert, entry.Glyph, entry.Name);

            any = true;
        }

        if (!any) EmptyState("No emoji found.");
    }

    private bool DrawSeenSearchResults(
        Action<string, string?> insert,
        string query,
        List<ChatboxSeenEmote> source,
        string label)
    {
        var shown = 0;

        foreach (var emote in source)
        {
            if (shown >= SeenSearchLimit) break;
            if (emote.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

            if (shown == 0) Section(label, 0);

            DrawTokenCell(insert, emote.Token, emote.Name);
            shown++;
        }

        return shown > 0;
    }

    private void DrawTokenCell(Action<string, string?> insert, string token, string? name = null)
    {
        var custom = CustomToken.Match(token);
        var animated = custom.Success && custom.Groups["a"].Value.Length > 0;
        var id = custom.Success ? ulong.Parse(custom.Groups["id"].Value) : 0ul;

        var url = custom.Success
            ? EmojiTranslator.EmoteUrl(id, animated)
            : EmojiCatalog.ImageUrl(Config.TwemojiBaseUrl, token);

        name ??= custom.Success ? custom.Groups["name"].Value : EmojiCatalog.Find(token)?.Name ?? token;

        var favorite = Config.FavoriteEmojis.Contains(token);
        var clicked = Cell(url, name, favorite, out var rightClicked);

        if (rightClicked)
        {
            ToggleFavoriteEmoji(token);
            return;
        }

        if (!clicked) return;

        insert(token, url);
        PushRecent(token);

        if (!ImGui.GetIO().KeyShift) ImGui.CloseCurrentPopup();
    }

    private void ToggleFavoriteEmoji(string token)
    {
        if (!Config.FavoriteEmojis.Remove(token)) Config.FavoriteEmojis.Add(token);

        _plugin.Config.Save();
    }

    private void PushRecent(string token)
    {
        Config.RecentEmojis.Remove(token);
        Config.RecentEmojis.Insert(0, token);

        var limit = Math.Clamp(Config.EmojiPickerRecentLimit, 0, 128);
        while (Config.RecentEmojis.Count > limit) Config.RecentEmojis.RemoveAt(Config.RecentEmojis.Count - 1);

        _plugin.Config.Save();
    }

    private void BeginGrid(float cellSize, float spacing)
    {
        _cellSize = cellSize;
        _cellSpacing = spacing;
        _column = 0;
        _cellId = 0;

        var avail = MathF.Max(cellSize, ImGui.GetContentRegionAvail().X);
        _columns = Math.Max(1, (int)((avail + spacing) / (cellSize + spacing)));
    }

    private void AdvanceColumn()
    {
        _column++;
        if (_column >= _columns) _column = 0;
    }

    private void EndGrid()
    {
        if (_column != 0) ImGui.NewLine();
        _column = 0;
    }

    private void Section(string label, int count)
    {
        EndGrid();
        _theme.SpacerY(0.3f);

        _theme.PickerSectionHeader(
            label,
            count > 0 ? count.ToString() : string.Empty,
            MathF.Max(_cellSize, ImGui.GetContentRegionAvail().X));
    }

    private void EmptyState(string text)
    {
        EndGrid();
        _theme.PickerEmptyState(
            FontAwesomeIcon.SmileBeam,
            text,
            MathF.Max(_cellSize, ImGui.GetContentRegionAvail().X));
    }

    private bool Cell(string? url, string tooltip, bool favorite, out bool rightClicked)
    {
        rightClicked = false;

        if (_column > 0) ImGui.SameLine(0f, _cellSpacing);

        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(_cellSize, _cellSize);
        var min = origin;
        var max = origin + size;

        _cellId++;

        if (!ImGui.IsRectVisible(min, max))
        {
            ImGui.Dummy(size);
            AdvanceColumn();
            return false;
        }

        ImGui.PushID(_cellId);
        ImGui.InvisibleButton("##cell", size);
        ImGui.PopID();

        AdvanceColumn();

        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        rightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);

        Chatbox.ImageCache.Request(url);

        _theme.GridCell(
            min,
            max,
            hovered,
            favorite,
            Chatbox.ImageCache.Get(url),
            _cellSize * 0.12f,
            AnimatedTextureWrap.MarkVisible);

        if (hovered && tooltip.Length > 0) ImGui.SetTooltip(tooltip);

        return clicked;
    }

    private void RefreshSeenEmotes()
    {
        if (!Config.PickerIncludeSeenEmotes)
        {
            _ownEmotes.Clear();
            _otherEmotes.Clear();
            _seenVersion = -1;
            return;
        }

        var version = Chatbox.Emotes.Version;
        var guildVersion = GuildEmotes.Version;

        if (version == _seenVersion && guildVersion == _guildVersion) return;

        _seenVersion = version;
        _guildVersion = guildVersion;
        _ownEmotes.Clear();
        _otherEmotes.Clear();

        foreach (var emote in Chatbox.Emotes.Snapshot())
        {
            if (GuildEmotes.Contains(emote.Id)) continue;

            if (emote.Own) _ownEmotes.Add(emote);
            else _otherEmotes.Add(emote);
        }
    }
}
