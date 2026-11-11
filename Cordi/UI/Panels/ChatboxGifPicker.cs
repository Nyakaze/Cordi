using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed class ChatboxGifPicker
{
    public const string PopupId = "##chatbox-gif-popup";

    private const double SearchDelaySeconds = 0.6;
    private const string Attribution = "Powered by GIPHY";

    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;
    private readonly List<float> _columnHeights = new();

    private string _query = string.Empty;
    private string _typed = string.Empty;
    private string _search = string.Empty;
    private double _typedAt;
    private bool _showFavorites;
    private bool _searchActive;
    private bool _requestOpen;
    private bool _focusSearch;
    private bool _popupOpen;
    private Vector2 _chatboxPosition;
    private Vector2 _chatboxSize;
    private Vector2 _size;

    public ChatboxGifPicker(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public bool InputActive => _popupOpen;

    private ChatboxConfig Config => _plugin.Config.Chatbox;
    private ChatboxService Chatbox => _plugin.Chatbox;

    public void Open() => _requestOpen = true;

    public void SetChatboxBounds(Vector2 position, Vector2 size)
    {
        _chatboxPosition = position;
        _chatboxSize = size;
    }

    public void Draw(Action<string> send)
    {
        if (_requestOpen)
        {
            _requestOpen = false;
            _focusSearch = Config.GifPickerAutoFocusSearch;
            ResolveSize();
            ImGui.OpenPopup(PopupId);
        }

        if (!ImGui.IsPopupOpen(PopupId))
        {
            _popupOpen = false;
            return;
        }

        _theme.PreparePickerPopup(_chatboxPosition, _chatboxSize, Config.EmojiPickerPosition, _size);

        using var scope = _theme.PickerPopupScope(Config.BackgroundOpacity);

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse;

        if (!ImGui.BeginPopup(PopupId, flags))
        {
            _popupOpen = false;
            return;
        }

        _popupOpen = true;

        try
        {
            DrawPicker(send);
        }
        finally
        {
            ImGui.EndPopup();
        }
    }

    private void ResolveSize()
    {
        var saved = Config.GifPickerSize;

        _size = saved.X > 0f && saved.Y > 0f
            ? _theme.ClampPickerSize(saved)
            : _theme.PickerInitialSize(_chatboxSize, Config.EmojiPickerPosition);
    }

    private void DrawPicker(Action<string> send)
    {
        var side = Config.EmojiPickerPosition;
        var gripAtTop = side == ChatboxEmojiPickerPosition.Top;
        var grip = _theme.PickerGripSize();
        var width = ImGui.GetContentRegionAvail().X;

        _theme.PickerSearch(
            "##chatbox-gif-search",
            gripAtTop ? MathF.Max(_theme.Scaled(60f), width - grip) : width,
            ref _query,
            "Search GIPHY",
            ref _searchActive,
            _focusSearch);

        _focusSearch = false;

        UpdateSearch();

        _theme.SpacerY(0.4f);
        DrawHeader(width);
        _theme.SpacerY(0.3f);

        using (var child = ImRaii.Child("##chatbox-gif-scroll", new Vector2(0f, gripAtTop ? 0f : -grip), false))
        {
            if (child) DrawBody(send);
        }

        if (!_theme.PickerResizeGrip("##chatbox-gif-resize", side, ref _size)) return;

        Config.GifPickerSize = _size;
        _plugin.Config.Save();
    }

    private void UpdateSearch()
    {
        var now = ImGui.GetTime();

        if (!string.Equals(_query, _typed, StringComparison.Ordinal))
        {
            _typed = _query;
            _typedAt = now;
        }

        if (now - _typedAt >= SearchDelaySeconds) _search = _typed.Trim();
    }

    private void DrawHeader(float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = _theme.PickerChipHeight();
        var spacing = _theme.Gap(0.3f);
        var x = origin.X;

        if (_search.Length > 0)
        {
            _theme.PickerChip("##gif-chip-search", origin, $"Results for \"{_theme.Fit(_search, width * 0.5f)}\"", true);
        }
        else
        {
            var trending = "Trending";
            if (_theme.PickerChip("##gif-chip-trending", new Vector2(x, origin.Y), trending, !_showFavorites))
                _showFavorites = false;

            x += _theme.PickerChipWidth(trending) + spacing;

            var favorites = $"Favourites {Config.FavoriteGifs.Count}";
            if (_theme.PickerChip("##gif-chip-favorites", new Vector2(x, origin.Y), favorites, _showFavorites))
                _showFavorites = true;
        }

        var attribution = ImGui.CalcTextSize(Attribution);
        var attributionPos = new Vector2(origin.X + width - attribution.X, origin.Y + (height - attribution.Y) * 0.5f);

        ImGui.GetWindowDrawList().AddText(attributionPos, ImGui.GetColorU32(_theme.FaintText), Attribution);

        if (ImGui.IsMouseHoveringRect(attributionPos, attributionPos + attribution))
            ImGui.SetTooltip($"{Chatbox.Giphy.RequestsLastHour}/{GiphyClient.HourlyLimit} Giphy requests used this hour.\nCached results do not count.");

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + height));
        ImGui.Dummy(new Vector2(width, 0f));
    }

    private void DrawBody(Action<string> send)
    {
        var width = ImGui.GetContentRegionAvail().X;

        if (_search.Length == 0 && _showFavorites)
        {
            if (Config.FavoriteGifs.Count == 0)
            {
                _theme.PickerEmptyState(FontAwesomeIcon.Star, "Right-click a GIF to favourite it", width);
                return;
            }

            DrawMasonry(send, Config.FavoriteGifs, width);
            return;
        }

        if (!Chatbox.Giphy.HasKey)
        {
            _theme.PickerEmptyState(FontAwesomeIcon.Key, "Add a Giphy API key in Settings", width);
            _theme.MutedLabel("Settings -> Chatbox -> Content -> GIF Picker");
            return;
        }

        var results = Chatbox.Giphy.Get(_search);

        if (results.Gifs.Count == 0 && !results.Loading && results.Error == null)
        {
            _theme.PickerEmptyState(FontAwesomeIcon.Search, "No GIFs found", width);
            return;
        }

        DrawMasonry(send, results.Gifs, width);

        _theme.SpacerY(0.4f);

        using (ImRaii.TextWrapPos(0f))
        {
            if (results.Loading) _theme.MutedLabel("Loading...");
            else if (results.Error != null) _theme.MutedLabel(results.Error);
            else if (results.HasMore && _theme.SecondaryButton("Load more##chatbox-gif-more", new Vector2(width, 0f)))
                Chatbox.Giphy.LoadMore(results);
        }
    }

    private void DrawMasonry(Action<string> send, IReadOnlyList<GiphyGif> gifs, float width)
    {
        var spacing = _theme.Gap(0.4f);
        var columns = _theme.PickerMediaColumns(width, spacing);
        var columnWidth = (width - spacing * (columns - 1)) / columns;
        var origin = ImGui.GetCursorScreenPos();

        _columnHeights.Clear();
        for (var i = 0; i < columns; i++) _columnHeights.Add(0f);

        GiphyGif? toggled = null;

        for (var i = 0; i < gifs.Count; i++)
        {
            var column = 0;
            for (var c = 1; c < columns; c++)
            {
                if (_columnHeights[c] < _columnHeights[column]) column = c;
            }

            var gif = gifs[i];
            var height = columnWidth * Math.Clamp(gif.Aspect, 0.25f, 4f);
            var min = new Vector2(origin.X + column * (columnWidth + spacing), origin.Y + _columnHeights[column]);

            _columnHeights[column] += height + spacing;

            if (DrawTile(send, gif, i, min, new Vector2(columnWidth, height))) toggled = gif;
        }

        var total = 0f;
        foreach (var height in _columnHeights) total = MathF.Max(total, height);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, total - spacing)));

        if (toggled != null) ToggleFavorite(toggled);
    }

    private bool DrawTile(Action<string> send, GiphyGif gif, int index, Vector2 min, Vector2 size)
    {
        var max = min + size;
        if (!ImGui.IsRectVisible(min, max)) return false;

        ImGui.SetCursorScreenPos(min);
        ImGui.PushID(index);
        ImGui.InvisibleButton("##gif", size);
        ImGui.PopID();

        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var rightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var favorite = IsFavorite(gif);

        Chatbox.ImageCache.Request(gif.PreviewUrl);

        _theme.PickerMediaTile(
            min,
            max,
            hovered,
            favorite,
            Chatbox.ImageCache.Get(gif.PreviewUrl),
            AnimatedTextureWrap.MarkVisible);

        if (hovered)
            ImGui.SetTooltip($"{gif.Title}\nClick to send, right-click to {(favorite ? "remove from" : "add to")} favourites.");

        if (clicked)
        {
            send(gif.Link());
            if (!ImGui.GetIO().KeyShift) ImGui.CloseCurrentPopup();
        }

        return rightClicked;
    }

    private bool IsFavorite(GiphyGif gif) =>
        Config.FavoriteGifs.Exists(favorite => string.Equals(favorite.Id, gif.Id, StringComparison.Ordinal));

    private void ToggleFavorite(GiphyGif gif)
    {
        var removed = Config.FavoriteGifs.RemoveAll(favorite => string.Equals(favorite.Id, gif.Id, StringComparison.Ordinal));

        if (removed == 0)
        {
            Config.FavoriteGifs.Insert(0, new GiphyGif
            {
                Id = gif.Id,
                Title = gif.Title,
                PreviewUrl = gif.PreviewUrl,
                Aspect = gif.Aspect,
            });
        }

        _plugin.Config.Save();
    }
}
