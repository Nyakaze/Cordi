using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Windows;

public sealed partial class ChatboxWindow
{
    private enum BadgePlacement
    {
        TopRight,
        MiddleRight,
    }

    private readonly List<(ChatboxChannelState Channel, string Label, float Width)> _tabItems = new();
    private readonly List<int> _tabRows = new();

    private IEnumerable<ChatboxChannelState> NavChannels()
    {
        if (Config.ShowCombinedChannel) yield return Chatbox.Combined;

        foreach (var channel in Chatbox.Channels)
        {
            if (!channel.Config.Enabled || !channel.Config.ShowInNav) continue;
            yield return channel;
        }
    }

    private void DrawServerRail()
    {
        var scale = ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;
        var indicatorSpace = 8f * ImGuiHelpers.GlobalScale;
        var available = MathF.Max(ImGui.GetContentRegionAvail().X, 24f);
        var size = MathF.Max(20f, MathF.Min(Config.RailIconSize * scale, available - indicatorSpace));
        var activeId = Chatbox.ResolveActiveChannelId();

        ImGui.Dummy(new Vector2(0, _theme.Gap(0.4f)));

        foreach (var channel in NavChannels())
        {
            DrawRailItem(channel, channel.Id == activeId, size, indicatorSpace, available);
            ImGui.Dummy(new Vector2(0, _theme.Gap(0.5f)));
        }
    }

    private void DrawRailItem(ChatboxChannelState channel, bool isActive, float size, float indicatorSpace, float available)
    {
        var offset = indicatorSpace + MathF.Max(0f, available - indicatorSpace - size) * 0.5f;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        var origin = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton($"##rail-{channel.Id}", new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();

        var draw = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(size, size);
        var rounding = isActive || hovered ? size * 0.30f : size * 0.5f;
        var accent = channel.Config.Color;

        var background = isActive
            ? accent
            : hovered
                ? new Vector4(accent.X * 0.55f, accent.Y * 0.55f, accent.Z * 0.55f, 1f)
                : _theme.FrameBg;

        draw.AddRectFilled(min, max, ImGui.GetColorU32(background), rounding);

        Chatbox.ImageCache.Request(channel.Config.IconUrl);

        var texture = string.IsNullOrWhiteSpace(channel.Config.IconUrl)
            ? null
            : Chatbox.ImageCache.Get(channel.Config.IconUrl);

        if (texture != null)
        {
            AnimatedTextureWrap.MarkVisible(texture, min, max);
            draw.AddImageRounded(
                texture.Handle,
                min,
                max,
                Vector2.Zero,
                Vector2.One,
                0xFFFFFFFF,
                rounding);
        }
        else
        {
            var label = RailLabel(channel);
            var textSize = ImGui.CalcTextSize(label);
            var foreground = isActive ? new Vector4(1f, 1f, 1f, 1f) : _theme.Text;
            draw.AddText(
                min + (new Vector2(size, size) - textSize) * 0.5f,
                ImGui.GetColorU32(foreground),
                label);
        }

        DrawRailIndicator(draw, min, size, isActive, hovered, HasUnread(channel));
        DrawMentionBadge(draw, min, max, channel, BadgePlacement.TopRight);
        DrawChannelContext(channel, hovered);

        if (clicked) Activate(channel);
    }

    private void DrawRailIndicator(ImDrawListPtr draw, Vector2 min, float size, bool isActive, bool hovered, bool unread)
    {
        if (!isActive && !unread) return;
        if (!isActive && !Config.ShowUnreadDot) return;

        var width = 4f * ImGuiHelpers.GlobalScale;
        var height = isActive ? size * 0.65f : hovered ? size * 0.4f : width * 2f;
        var top = min.Y + (size - height) * 0.5f;
        var left = min.X - width - 4f * ImGuiHelpers.GlobalScale;

        draw.AddRectFilled(
            new Vector2(left, top),
            new Vector2(left + width, top + height),
            ImGui.GetColorU32(_theme.Text),
            width * 0.5f);
    }

    private void DrawChannelList()
    {
        var activeId = Chatbox.ResolveActiveChannelId();
        ImGui.Dummy(new Vector2(0, _theme.Gap(0.3f)));

        foreach (var channel in NavChannels())
            DrawChannelRow(channel, channel.Id == activeId);
    }

    private void DrawChannelRow(ChatboxChannelState channel, bool isActive)
    {
        var height = ImGui.GetFrameHeight();
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton($"##row-{channel.Id}", new Vector2(MathF.Max(width, 40f), height));
        var hovered = ImGui.IsItemHovered();

        var draw = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(MathF.Max(width, 40f), height);

        if (isActive || hovered)
        {
            var background = isActive ? _theme.Active : _theme.Hover;
            draw.AddRectFilled(min, max, ImGui.GetColorU32(background), _theme.Radius(0.5f));
        }

        var unread = HasUnread(channel);
        var padding = _theme.PadX(0.6f);
        var textY = min.Y + (height - ImGui.GetTextLineHeight()) * 0.5f;

        if (unread && Config.ShowUnreadDot)
        {
            var radius = 3f * ImGuiHelpers.GlobalScale;
            draw.AddCircleFilled(
                new Vector2(min.X + radius + 1f, min.Y + height * 0.5f),
                radius,
                ImGui.GetColorU32(_theme.Text));
        }

        var prefix = channel.Id == ChatboxService.CombinedChannelId ? "≡ " : "# ";
        var prefixSize = ImGui.CalcTextSize(prefix);
        draw.AddText(new Vector2(min.X + padding, textY), ImGui.GetColorU32(channel.Config.Color), prefix);

        var nameColor = isActive || unread ? _theme.Text : _theme.MutedText;
        draw.AddText(new Vector2(min.X + padding + prefixSize.X, textY), ImGui.GetColorU32(nameColor), channel.Config.Name);

        DrawMentionBadge(draw, min, max, channel, BadgePlacement.MiddleRight, padding);
        DrawChannelContext(channel, hovered);

        if (clicked) Activate(channel);
    }

    private float LayoutTabs(float available)
    {
        _tabItems.Clear();
        _tabRows.Clear();

        var scale = ImGuiHelpers.GlobalScale * UiTheme.GlobalFontScale;
        var spacing = _theme.Gap(0.4f);
        var fixedWidth = Config.TabWidth * scale;
        var rowWidth = 0f;

        foreach (var channel in NavChannels())
        {
            var label = TabLabel(channel);
            var width = fixedWidth > 1f
                ? fixedWidth
                : ImGui.CalcTextSize(label).X + _theme.PadX(1.4f) + BadgeSpace(channel);
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

            _tabItems.Add((channel, label, width));
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
                DrawNavTab(item.Channel, item.Channel.Id == activeId, item.Label, item.Width);
            }
        }

        if (Config.TabSide == ChatboxTabSide.Top) ImGui.Separator();
    }

    private void DrawNavTab(ChatboxChannelState channel, bool isActive, string label, float width)
    {
        var height = ImGui.GetFrameHeight();
        var origin = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton($"##tab-{channel.Id}", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();

        var draw = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(width, height);

        var background = isActive ? _theme.TabActive : hovered ? _theme.TabHovered : _theme.Tab;
        draw.AddRectFilled(min, max, ImGui.GetColorU32(background), _theme.Radius(0.6f));

        if (isActive)
        {
            var barHeight = 2f * ImGuiHelpers.GlobalScale;
            draw.AddRectFilled(
                new Vector2(min.X + _theme.PadX(0.4f), max.Y - barHeight),
                new Vector2(max.X - _theme.PadX(0.4f), max.Y),
                ImGui.GetColorU32(channel.Config.Color),
                barHeight);
        }

        var unread = HasUnread(channel);
        var badgeSpace = BadgeSpace(channel);
        var textSize = ImGui.CalcTextSize(label);
        var color = isActive || unread ? _theme.Text : _theme.MutedText;
        var textX = min.X + MathF.Max(_theme.PadX(0.3f), (width - badgeSpace - textSize.X) * 0.5f);
        draw.AddText(
            new Vector2(textX, min.Y + (height - textSize.Y) * 0.5f),
            ImGui.GetColorU32(color),
            label);

        if (unread && Config.ShowUnreadDot && badgeSpace <= 0f)
        {
            var radius = 3f * ImGuiHelpers.GlobalScale;
            draw.AddCircleFilled(
                new Vector2(max.X - radius - 2f, min.Y + radius + 2f),
                radius,
                ImGui.GetColorU32(_theme.Text));
        }

        DrawMentionBadge(draw, min, max, channel, BadgePlacement.MiddleRight, _theme.PadX(0.3f));
        DrawChannelContext(channel, hovered);

        if (clicked) Activate(channel);
    }

    private Vector2 MentionBadgeSize(ChatboxChannelState channel)
    {
        if (!Config.ShowMentionBadge || channel.Config.MuteNotifications || channel.MentionCount <= 0)
            return Vector2.Zero;

        var textSize = ImGui.CalcTextSize(MentionBadgeText(channel));
        var height = textSize.Y + 1f * ImGuiHelpers.GlobalScale;
        return new Vector2(MathF.Max(height, textSize.X + 5f * ImGuiHelpers.GlobalScale), height);
    }

    private float BadgeSpace(ChatboxChannelState channel)
    {
        var size = MentionBadgeSize(channel);
        return size.X > 0f ? size.X + _theme.Gap(0.4f) : 0f;
    }

    private static string MentionBadgeText(ChatboxChannelState channel) =>
        channel.MentionCount > 99 ? "99+" : channel.MentionCount.ToString();

    private void DrawMentionBadge(
        ImDrawListPtr draw,
        Vector2 itemMin,
        Vector2 itemMax,
        ChatboxChannelState channel,
        BadgePlacement placement,
        float inset = 0f)
    {
        var size = MentionBadgeSize(channel);
        if (size.X <= 0f) return;

        var right = itemMax.X - inset;
        var bottom = placement == BadgePlacement.TopRight
            ? itemMin.Y + inset + size.Y
            : (itemMin.Y + itemMax.Y + size.Y) * 0.5f;

        var max = new Vector2(right, bottom);
        var min = max - size;

        var text = MentionBadgeText(channel);
        var textSize = ImGui.CalcTextSize(text);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(Config.UnreadBadgeColor), size.Y * 0.5f);
        draw.AddText(min + (size - textSize) * 0.5f, 0xFFFFFFFF, text);
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

        if (ImGui.MenuItem("Mark as read")) channel.MarkRead();
        if (ImGui.MenuItem("Clear messages")) channel.Clear();
    }

    private bool HasUnread(ChatboxChannelState channel) =>
        !channel.Config.MuteNotifications && channel.UnreadCount > 0;

    private void Activate(ChatboxChannelState channel)
    {
        Chatbox.SetActiveChannel(channel.Id);
        _scrollToBottomFrames = ScrollSettleFrames;
        _focusInput = true;
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
