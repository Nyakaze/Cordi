using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed class ChatboxEmojiPicker
{
    public const string PopupId = "##chatbox-emoji-popup";

    private const int SearchLimit = 120;
    private const int SeenSearchLimit = 60;

    private static readonly Regex CustomToken = new(
        @"^<(?<a>a?):(?<name>[A-Za-z0-9_~]{2,32}):(?<id>\d{5,25})>$",
        RegexOptions.Compiled);

    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;

    private readonly List<EmojiCatalogEntry> _matches = new();
    private readonly List<GuildEmoteGroup> _guildEmotes = new();
    private readonly HashSet<ulong> _guildEmoteIds = new();
    private readonly List<ChatboxSeenEmote> _seenEmotes = new();

    private DateTime _guildEmotesRefreshedAt = DateTime.MinValue;
    private int _seenVersion = -1;

    private string _emojiQuery = string.Empty;
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

    public void Open() => _requestOpen = true;

    public void Draw(Action<string> insert)
    {
        if (_requestOpen)
        {
            _requestOpen = false;
            ImGui.OpenPopup(PopupId);
        }

        var scale = ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;
        ImGui.SetNextWindowSize(new Vector2(400f * scale, 440f * scale), ImGuiCond.Always);

        if (!ImGui.BeginPopup(PopupId, ImGuiWindowFlags.NoMove))
        {
            _popupOpen = false;
            return;
        }

        _popupOpen = true;

        try
        {
            DrawEmojiGrid(insert);
        }
        finally
        {
            ImGui.EndPopup();
        }
    }

    private void DrawEmojiGrid(Action<string> insert)
    {
        DrawSearch("##chatbox-emoji-search", "Search emoji", ref _emojiQuery);

        using var child = ImRaii.Child("##chatbox-emoji-scroll", new Vector2(0f, 0f), false);
        if (!child) return;

        var scale = ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;
        BeginGrid(30f * scale, _theme.Gap(0.35f));

        RefreshGuildEmotes();
        RefreshSeenEmotes();

        var query = _emojiQuery.Trim();
        if (query.Length > 0)
        {
            DrawEmojiSearchResults(insert, query);
            EndGrid();
            return;
        }

        if (Config.FavoriteEmojis.Count > 0)
        {
            Section("Favorites");
            foreach (var token in Config.FavoriteEmojis.ToArray())
                DrawTokenCell(insert, token);
        }

        if (Config.RecentEmojis.Count > 0)
        {
            Section("Frequently Used");
            foreach (var token in Config.RecentEmojis.ToArray())
                DrawTokenCell(insert, token);
        }

        foreach (var guild in _guildEmotes)
        {
            if (guild.Emotes.Count == 0) continue;

            Section(guild.Name);
            foreach (var emote in guild.Emotes)
                DrawTokenCell(insert, emote.Token, emote.Name);
        }

        if (_seenEmotes.Count > 0)
        {
            Section("Seen in Chat");
            foreach (var emote in _seenEmotes)
                DrawTokenCell(insert, emote.Token, emote.Name);
        }

        foreach (var group in EmojiCatalog.Groups)
        {
            Section(group.Name);
            foreach (var entry in group.Entries)
                DrawTokenCell(insert, entry.Glyph, entry.Name);
        }

        EndGrid();
    }

    private void DrawEmojiSearchResults(Action<string> insert, string query)
    {
        var any = false;

        foreach (var guild in _guildEmotes)
        {
            foreach (var emote in guild.Emotes)
            {
                if (emote.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (!any)
                {
                    Section("Custom");
                    any = true;
                }

                DrawTokenCell(insert, emote.Token, emote.Name);
            }
        }

        var seenShown = 0;
        foreach (var emote in _seenEmotes)
        {
            if (seenShown >= SeenSearchLimit) break;
            if (emote.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

            if (seenShown == 0) Section("Seen in Chat");

            DrawTokenCell(insert, emote.Token, emote.Name);
            seenShown++;
            any = true;
        }

        EmojiCatalog.Search(query, _matches, SearchLimit);
        if (_matches.Count > 0)
        {
            Section("Emoji");
            foreach (var entry in _matches)
                DrawTokenCell(insert, entry.Glyph, entry.Name);
            any = true;
        }

        if (!any) ImGui.TextDisabled("No emoji found.");
    }

    private void DrawTokenCell(Action<string> insert, string token, string? name = null)
    {
        var custom = CustomToken.Match(token);
        var animated = custom.Success && custom.Groups["a"].Value.Length > 0;
        var id = custom.Success ? ulong.Parse(custom.Groups["id"].Value) : 0ul;

        var url = custom.Success
            ? ChatboxContentParser.CustomEmoteUrl(id, animated)
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

        insert(token);
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

    private void DrawSearch(string id, string hint, ref string value)
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        _theme.PushInputScope();
        ImGui.InputTextWithHint(id, hint, ref value, 64);
        _theme.PopInputScope();
        _theme.SpacerY(0.2f);
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

    private void Section(string label)
    {
        EndGrid();
        _theme.SpacerY(0.2f);
        ImGui.TextDisabled(label);
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

        var draw = ImGui.GetWindowDrawList();

        if (hovered)
            draw.AddRectFilled(min, max, ImGui.GetColorU32(_theme.Hover), _theme.Radius(0.4f));

        if (favorite)
            draw.AddRect(min, max, ImGui.GetColorU32(_theme.Accent), _theme.Radius(0.4f), ImDrawFlags.None, 1f);

        Chatbox.ImageCache.Request(url);
        var texture = Chatbox.ImageCache.Get(url);

        if (texture != null)
        {
            var pad = _cellSize * 0.12f;
            var imageMin = min + new Vector2(pad, pad);
            var imageMax = max - new Vector2(pad, pad);

            AnimatedTextureWrap.MarkVisible(texture, imageMin, imageMax);
            draw.AddImage(texture.Handle, imageMin, imageMax);
        }

        if (hovered && tooltip.Length > 0) ImGui.SetTooltip(tooltip);

        return clicked;
    }

    private void RefreshGuildEmotes()
    {
        if (DateTime.UtcNow - _guildEmotesRefreshedAt < TimeSpan.FromSeconds(30)) return;

        _guildEmotesRefreshedAt = DateTime.UtcNow;

        var client = _plugin.Discord?.Client;
        if (client == null) return;

        _guildEmotes.Clear();
        _guildEmoteIds.Clear();

        foreach (var guild in client.Guilds.Values)
        {
            var group = new GuildEmoteGroup { Name = guild.Name ?? "Server" };

            foreach (var emoji in guild.Emojis.Values)
            {
                if (string.IsNullOrEmpty(emoji.Name)) continue;

                group.Emotes.Add(new GuildEmote
                {
                    Name = emoji.Name,
                    Token = $"<{(emoji.IsAnimated ? "a" : string.Empty)}:{emoji.Name}:{emoji.Id}>",
                });

                _guildEmoteIds.Add(emoji.Id);
            }

            if (group.Emotes.Count > 0) _guildEmotes.Add(group);
        }

        _seenVersion = -1;
    }

    private void RefreshSeenEmotes()
    {
        if (!Config.PickerIncludeSeenEmotes)
        {
            if (_seenEmotes.Count > 0) _seenEmotes.Clear();
            _seenVersion = -1;
            return;
        }

        var version = Chatbox.Emotes.Version;
        if (version == _seenVersion) return;

        _seenVersion = version;
        _seenEmotes.Clear();

        foreach (var emote in Chatbox.Emotes.Snapshot())
        {
            if (_guildEmoteIds.Contains(emote.Id)) continue;

            _seenEmotes.Add(emote);
        }
    }

    private sealed class GuildEmoteGroup
    {
        public string Name { get; init; } = string.Empty;
        public List<GuildEmote> Emotes { get; } = new();
    }

    private sealed class GuildEmote
    {
        public string Name { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
    }
}
