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

public sealed class ChatboxConversationPicker
{
    public const string PopupId = "##chatbox-dm-popup";

    private const int RecentLimit = 12;
    private const int SearchLimit = 100;

    private static readonly Comparison<ConversationConfig> RecentOrder = (a, b) =>
    {
        var pinned = b.Pinned.CompareTo(a.Pinned);
        if (pinned != 0) return pinned;

        var activity = b.LastActivityTicks.CompareTo(a.LastActivityTicks);

        return activity != 0
            ? activity
            : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    };

    private readonly CordiPlugin _plugin;
    private readonly UiTheme _theme;
    private readonly List<ConversationConfig> _entries = new();

    private string _query = string.Empty;
    private bool _searchActive;
    private bool _requestOpen;
    private bool _popupOpen;
    private Vector2 _chatboxPosition;
    private Vector2 _chatboxSize;
    private Vector2 _size;

    public ChatboxConversationPicker(CordiPlugin plugin, UiTheme theme)
    {
        _plugin = plugin;
        _theme = theme;
    }

    public bool InputActive => _popupOpen;

    private ChatboxConfig Config => _plugin.Config.Chatbox;
    private ConversationSettings Settings => Config.Conversations;
    private ChatboxService Chatbox => _plugin.Chatbox;

    public void Open()
    {
        _requestOpen = true;
        _query = string.Empty;
    }

    public void SetChatboxBounds(Vector2 position, Vector2 size)
    {
        _chatboxPosition = position;
        _chatboxSize = size;
    }

    public void Draw()
    {
        if (_requestOpen)
        {
            _requestOpen = false;
            _size = _theme.PickerInitialSize(_chatboxSize, Config.EmojiPickerPosition);
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
            DrawPicker();
        }
        finally
        {
            ImGui.EndPopup();
        }
    }

    private void DrawPicker()
    {
        _theme.PickerSearch(
            "##chatbox-dm-search",
            ImGui.GetContentRegionAvail().X,
            ref _query,
            "Search conversations",
            ref _searchActive);

        _theme.SpacerY(0.4f);

        var query = _query.Trim();
        var searching = query.Length > 0;

        Collect(query);

        using var child = ImRaii.Child("##chatbox-dm-scroll", Vector2.Zero, false);
        if (!child) return;

        var width = MathF.Max(_theme.Scaled(40f), ImGui.GetContentRegionAvail().X);

        if (_entries.Count == 0)
        {
            _theme.PickerEmptyState(
                FontAwesomeIcon.Envelope,
                searching ? "Nobody matches that." : "No conversations yet.",
                width);

            return;
        }

        _theme.PickerSectionHeader(
            searching ? "Results" : "Recent",
            _entries.Count.ToString(),
            width);

        for (var i = 0; i < _entries.Count; i++) DrawRow(_entries[i]);
    }

    private void Collect(string query)
    {
        _entries.Clear();

        var all = Chatbox.AllConversations;

        if (query.Length == 0)
        {
            for (var i = 0; i < all.Count; i++) _entries.Add(all[i]);

            _entries.Sort(RecentOrder);

            if (_entries.Count > RecentLimit) _entries.RemoveRange(RecentLimit, _entries.Count - RecentLimit);

            return;
        }

        for (var i = 0; i < all.Count && _entries.Count < SearchLimit; i++)
        {
            var entry = all[i];

            if (entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                && entry.World.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            _entries.Add(entry);
        }

        _entries.Sort(RecentOrder);
    }

    private void DrawRow(ConversationConfig entry)
    {
        var state = Chatbox.GetChannel(entry.Id);
        var muted = state?.Config.MuteNotifications ?? false;

        var hit = _theme.NavListRow(
            "##chatbox-dm-" + entry.Id,
            ImGui.GetContentRegionAvail().X,
            new UiNavItem
            {
                Label = Settings.ShowWorldInLabel ? entry.Label : entry.Name,
                Accent = Settings.Color,
                Unread = !muted && state is { UnreadCount: > 0 },
                ShowUnreadDot = Config.ShowUnreadDot,
                BadgeText = BadgeText(state, muted),
                BadgeColor = Config.UnreadBadgeColor,
            });

        if (hit.Hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!hit.Clicked) return;

        Chatbox.OpenConversationFor(entry.Name, entry.World);
        ImGui.CloseCurrentPopup();
    }

    private string BadgeText(ChatboxChannelState? state, bool muted)
    {
        if (state == null || muted || !Config.ShowMentionBadge || state.MentionCount <= 0) return string.Empty;

        return state.MentionCount > 99 ? "99+" : state.MentionCount.ToString();
    }
}
