using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface
{
    private readonly List<(ChatboxChannelState? Channel, string Label, float Width, bool Closable)> _tabItems = new();
    private readonly List<int> _tabRows = new();

    private List<(ChatboxChannelState? Channel, string Label, bool Closable)> NavItems()
    {
        var states = new Dictionary<string, ChatboxChannelState>(StringComparer.Ordinal);
        foreach (var channel in Chatbox.Channels)
            states[channel.Id] = channel;

        var items = new List<(ChatboxChannelState? Channel, string Label, bool Closable)>();

        AppendConversationItems(items);

        string? pending = null;

        var ordered = Config.Channels
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var config in ordered)
        {
            if (!config.Enabled || !config.ShowInNav) continue;

            if (config.IsSeparator)
            {
                if (items.Count > 0 && items[^1].Channel != null) pending = config.Name.Trim();
                continue;
            }

            if (!states.TryGetValue(config.Id, out var state)) continue;

            if (pending != null)
            {
                items.Add((null, pending, false));
                pending = null;
            }

            items.Add((state, string.Empty, false));
        }

        return items;
    }

    private void AppendConversationItems(List<(ChatboxChannelState? Channel, string Label, bool Closable)> items)
    {
        if (!Chatbox.ConversationsEnabled) return;

        var conversations = Chatbox.OpenConversations();
        if (conversations.Count == 0) return;

        var label = Config.Conversations.SectionLabel?.Trim() ?? string.Empty;
        if (label.Length > 0) items.Add((null, label, false));

        foreach (var conversation in conversations)
            items.Add((conversation, string.Empty, true));

        items.Add((null, string.Empty, false));
    }

    private void DrawServerRail()
    {
        var scale = ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;
        var indicatorSpace = 8f * ImGuiHelpers.GlobalScale;
        var stable = UiTheme.StableContentWidth(24f);
        var available = MathF.Max(ImGui.GetContentRegionAvail().X, 24f);
        var size = MathF.Max(20f, MathF.Min(Config.RailIconSize * scale, stable - indicatorSpace));
        var activeId = Chatbox.ResolveActiveChannelId();

        ImGui.Dummy(new Vector2(0, _theme.Gap(0.4f)));

        foreach (var item in NavItems())
        {
            if (item.Channel == null)
            {
                DrawRailSeparator(item.Label, size, indicatorSpace, available);
                continue;
            }

            DrawRailItem(item.Channel, item.Channel.Id == activeId, size, indicatorSpace, available, item.Closable);
            ImGui.Dummy(new Vector2(0, _theme.Gap(0.5f)));
        }
    }

    private void DrawRailSeparator(string label, float size, float indicatorSpace, float available)
    {
        var width = MathF.Max(16f, size * 0.6f);
        var offset = indicatorSpace + MathF.Max(0f, available - indicatorSpace - width) * 0.5f;

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        _theme.DividerMark(width, label);
    }

    private void DrawRailItem(
        ChatboxChannelState channel,
        bool isActive,
        float size,
        float indicatorSpace,
        float available,
        bool closable)
    {
        var offset = indicatorSpace + MathF.Max(0f, available - indicatorSpace - size) * 0.5f;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        var hit = _theme.NavRailTile(
            $"##rail-{channel.Id}",
            size,
            NavVisual(channel, isActive, RailLabel(channel), ChannelIcon(channel), closable),
            AnimatedTextureWrap.MarkVisible);

        DrawChannelContext(channel, hit.Hovered);

        if (hit.Closed) Chatbox.CloseConversation(channel.Id);
        else if (hit.Clicked) Activate(channel);
    }

    private IDalamudTextureWrap? ChannelIcon(ChatboxChannelState channel)
    {
        var url = channel.Config.IconUrl;
        if (string.IsNullOrWhiteSpace(url)) return null;

        Chatbox.ImageCache.Request(url);

        return Chatbox.ImageCache.Get(url);
    }

    private UiNavItem NavVisual(
        ChatboxChannelState channel,
        bool isActive,
        string label,
        IDalamudTextureWrap? image = null,
        bool closable = false) => new()
    {
        Label = label,
        Accent = channel.Config.Color,
        Active = isActive,
        Unread = HasUnread(channel),
        ShowUnreadDot = Config.ShowUnreadDot,
        BadgeText = MentionBadgeText(channel),
        BadgeColor = Config.UnreadBadgeColor,
        Closable = closable,
        FlashAmount = FlashAmount(channel),
        FlashColor = _theme.Accent,
        Image = image,
    };

    private float FlashAmount(ChatboxChannelState channel)
    {
        if (!ChatboxService.IsConversationId(channel.Id)) return 0f;

        var settings = Config.Conversations;
        if (!settings.Flash || !HasUnread(channel)) return 0f;

        return settings.NoFlashing ? 1f : _theme.PulseAmount(settings.FlashPeriodMs);
    }

    private void DrawChannelList()
    {
        var activeId = Chatbox.ResolveActiveChannelId();
        ImGui.Dummy(new Vector2(0, _theme.Gap(0.3f)));

        foreach (var item in NavItems())
        {
            if (item.Channel == null)
            {
                DrawChannelListSeparator(item.Label);
                continue;
            }

            DrawChannelRow(item.Channel, item.Channel.Id == activeId, item.Closable);
        }
    }

    private void DrawChannelListSeparator(string label) =>
        _theme.DividerRow(label, MathF.Max(ImGui.GetContentRegionAvail().X, 24f), _theme.PadX(0.6f));

    private void DrawChannelRow(ChatboxChannelState channel, bool isActive, bool closable)
    {
        var hit = _theme.NavListRow(
            $"##row-{channel.Id}",
            ImGui.GetContentRegionAvail().X,
            NavVisual(channel, isActive, channel.Config.Name, ChannelIcon(channel), closable),
            AnimatedTextureWrap.MarkVisible);

        DrawChannelContext(channel, hit.Hovered);

        if (hit.Closed) Chatbox.CloseConversation(channel.Id);
        else if (hit.Clicked) Activate(channel);
    }

    private float LayoutTabs(float available)
    {
        _tabItems.Clear();
        _tabRows.Clear();

        var scale = ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;
        var spacing = _theme.Gap(0.4f);
        var fixedWidth = Config.TabWidth * scale;
        var rowWidth = 0f;

        foreach (var item in NavItems())
        {
            string label;
            float width;

            if (item.Channel == null)
            {
                label = item.Label.Length > 0 ? item.Label.ToUpperInvariant() : string.Empty;
                width = label.Length > 0
                    ? ImGui.CalcTextSize(label).X + _theme.PadX(0.8f)
                    : _theme.PadX(0.9f);
            }
            else
            {
                label = TabLabel(item.Channel);
                width = fixedWidth > 1f
                    ? fixedWidth
                    : ImGui.CalcTextSize(label).X
                      + _theme.PadX(1.4f)
                      + MathF.Max(BadgeSpace(item.Channel), item.Closable ? _theme.NavCloseSize() + _theme.Gap(0.3f) : 0f)
                      + _theme.NavIconSpace(ChannelIcon(item.Channel));
            }

            width = MathF.Min(width, available);

            if (_tabItems.Count == 0 || rowWidth + spacing + width > available)
            {
                _tabRows.Add(_tabItems.Count);
                rowWidth = width;
            }
            else
            {
                rowWidth += spacing + width;
            }

            _tabItems.Add((item.Channel, label, width, item.Closable));
        }

        return _tabRows.Count;
    }

    private float MeasureHorizontalNavHeight()
    {
        var rows = LayoutTabs(MathF.Max(ImGui.GetContentRegionAvail().X, 40f));
        if (rows <= 0) return 0f;

        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        return rows * (ImGui.GetFrameHeight() + spacing) + spacing * 2f + 1f;
    }

    private void DrawHorizontalNav()
    {
        var available = MathF.Max(ImGui.GetContentRegionAvail().X, 40f);
        if (Config.TabSide == ChatboxTabSide.Top) LayoutTabs(available);
        if (_tabItems.Count == 0) return;

        if (Config.TabSide == ChatboxTabSide.Bottom) ImGui.Separator();

        var activeId = Chatbox.ResolveActiveChannelId();
        var spacing = _theme.Gap(0.4f);
        var startX = ImGui.GetCursorPosX();

        for (var row = 0; row < _tabRows.Count; row++)
        {
            var from = _tabRows[row];
            var to = row + 1 < _tabRows.Count ? _tabRows[row + 1] : _tabItems.Count;

            var rowWidth = 0f;
            for (var i = from; i < to; i++)
                rowWidth += _tabItems[i].Width + (i > from ? spacing : 0f);

            var offset = Config.NavSide == ChatboxNavSide.Right
                ? MathF.Max(0f, available - rowWidth)
                : 0f;

            for (var i = from; i < to; i++)
            {
                if (i > from) ImGui.SameLine(0, spacing);
                else ImGui.SetCursorPosX(startX + offset);

                var item = _tabItems[i];
                if (item.Channel == null)
                    DrawNavSeparator(item.Label, item.Width);
                else
                    DrawNavTab(item.Channel, item.Channel.Id == activeId, item.Label, item.Width, item.Closable);
            }
        }

        if (Config.TabSide == ChatboxTabSide.Top) ImGui.Separator();
    }

    private void DrawNavSeparator(string label, float width) =>
        _theme.DividerCell(label, width, ImGui.GetFrameHeight());

    private void DrawNavTab(ChatboxChannelState channel, bool isActive, string label, float width, bool closable)
    {
        var hit = _theme.NavTab(
            $"##tab-{channel.Id}",
            width,
            NavVisual(channel, isActive, label, ChannelIcon(channel), closable),
            onImage: AnimatedTextureWrap.MarkVisible);

        DrawChannelContext(channel, hit.Hovered);

        if (hit.Closed) Chatbox.CloseConversation(channel.Id);
        else if (hit.Clicked) Activate(channel);
    }

    private float BadgeSpace(ChatboxChannelState channel) =>
        _theme.NavBadgeSpace(MentionBadgeText(channel));

    private string MentionBadgeText(ChatboxChannelState channel)
    {
        if (!Config.ShowMentionBadge || channel.Config.MuteNotifications || channel.MentionCount <= 0)
            return string.Empty;

        return channel.MentionCount > 99 ? "99+" : channel.MentionCount.ToString();
    }

    private void DrawChannelContext(ChatboxChannelState channel, bool hovered)
    {
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var tooltip = channel.Config.Name;
            if (channel.UnreadCount > 0) tooltip += $"\n{channel.UnreadCount} unread";
            if (channel.MentionCount > 0) tooltip += $"\n{channel.MentionCount} mentions";
            ImGui.SetTooltip(tooltip);
        }

        using var popup = ImRaii.ContextPopupItem($"##ctx-{channel.Id}");
        if (!popup) return;

        if (ImGui.MenuItem("Mark as read"))
        {
            channel.MarkRead();
            Chatbox.ClearDivider(channel);
        }

        if (ImGui.MenuItem("Clear messages")) channel.Clear();

        if (!ChatboxService.IsConversationId(channel.Id)) return;

        ImGui.Separator();

        if (ImGui.MenuItem("Close conversation")) Chatbox.CloseConversation(channel.Id);
    }

    private bool HasUnread(ChatboxChannelState channel) =>
        !channel.Config.MuteNotifications && channel.UnreadCount > 0;

    private void Activate(ChatboxChannelState channel)
    {
        var dividerSeq = Chatbox.SetActiveChannel(channel.Id);

        _scrollToSeq = 0;
        _scrollToBottomFrames = ScrollSettleFrames;
        ScrollToUnread(channel, dividerSeq);
    }

    private static string RailLabel(ChatboxChannelState channel)
    {
        if (!string.IsNullOrWhiteSpace(channel.Config.ShortLabel))
            return channel.Config.ShortLabel;

        var name = channel.Config.Name.Trim();
        if (name.Length == 0) return "#";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }

    private static string TabLabel(ChatboxChannelState channel) => channel.Config.Name;
}
