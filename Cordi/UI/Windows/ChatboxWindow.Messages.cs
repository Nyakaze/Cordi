using System;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Windows;

public sealed partial class ChatboxWindow
{
    private const int ScrollSettleFrames = 3;
    private const float CullMargin = 240f;

    private readonly record struct RowMetrics(float Height, bool Grouped);

    private long _scrollToSeq;
    private float _lastScrollY;
    private float _lastScrollMax;
    private bool _stickToBottom = true;
    private ChatboxChannelConfig? _drawChannel;
    private readonly System.Collections.Generic.HashSet<long> _revealedAds = new();
    private readonly System.Collections.Generic.List<ChatboxMessage> _drawBuffer = new();
    private readonly System.Collections.Generic.Dictionary<long, RowMetrics> _rowMetrics = new();
    private int _rowMetricsKey;

    private bool HoveringRect(Vector2 min, Vector2 max) =>
        ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows)
        && ImGui.IsMouseHoveringRect(min, max)
        && !_autocomplete.Covers(ImGui.GetIO().MousePos);

    private void DrawMessages(ChatboxChannelState channel)
    {
        _drawChannel = channel.Config;

        channel.SnapshotInto(_drawBuffer);
        if (_drawBuffer.Count == 0)
        {
            ImGui.Dummy(new Vector2(0, _theme.Gap()));
            _theme.MutedLabel($"No messages in {channel.Config.Name} yet.");
            return;
        }

        UpdateStickToBottom();
        var dividerSeq = Config.ShowNewMessageDivider ? channel.DividerSeq : 0;
        var pendingJump = _scrollToSeq;

        SyncRowMetrics(MathF.Max(ImGui.GetContentRegionAvail().X, 80f), _drawBuffer.Count);

        var scrollY = ImGui.GetScrollY();
        var viewTop = scrollY - CullMargin;
        var viewBottom = scrollY + ImGui.GetWindowHeight() + CullMargin;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;

        var draw = ImGui.GetWindowDrawList();
        draw.ChannelsSplit(3);
        draw.ChannelsSetCurrent(2);

        ChatboxMessage? previous = null;
        foreach (var message in _drawBuffer)
        {
            if (dividerSeq != 0 && message.Seq == dividerSeq) DrawNewMessageDivider();

            var grouped = ShouldGroup(previous, message);
            previous = message;

            var top = ImGui.GetCursorPosY();
            if (TryCullRow(message, grouped, top, viewTop, viewBottom, spacing)) continue;

            DrawMessage(draw, channel, message, grouped);
            _rowMetrics[message.Seq] = new RowMetrics(ImGui.GetCursorPosY() - top, grouped);
        }

        draw.ChannelsMerge();

        DrawLinkPopup();

        if (pendingJump != 0 && _scrollToSeq == pendingJump) _scrollToSeq = 0;

        ImGui.Dummy(new Vector2(0f, 4f * ImGuiHelpers.GlobalScale));

        if (_scrollToBottomFrames > 0)
        {
            ImGui.SetScrollHereY(1f);
            _scrollToBottomFrames--;
        }
        else if (pendingJump == 0 && Config.AutoScroll && _stickToBottom)
        {
            ImGui.SetScrollHereY(1f);
        }

        if (_stickToBottom && channel.DividerSeq != 0 && Chatbox.WindowFocused)
        {
            Chatbox.ClearDivider(channel);
        }
    }

    private void SyncRowMetrics(float width, int count)
    {
        var key = HashCode.Combine(
            HashCode.Combine(
                (int)(width * 4f),
                (int)Config.Layout,
                Config.ShowAvatars,
                Config.AvatarSize,
                Config.MessageSpacing,
                Config.LineSpacing,
                Config.EmoteScale,
                Config.JumboEmoteScale),
            HashCode.Combine(
                (int)Config.Timestamps,
                (int)Config.NameStyle,
                Config.ShowReplyPreview,
                Config.GroupConsecutive,
                Config.CompactSystemMessages,
                Config.JumboLoneEmotes,
                ImGuiHelpers.GlobalScale,
                ImGui.GetTextLineHeight()));

        if (key != _rowMetricsKey)
        {
            _rowMetricsKey = key;
            _rowMetrics.Clear();
            return;
        }

        if (_rowMetrics.Count > count * 2 + 64) _rowMetrics.Clear();
    }

    private bool TryCullRow(
        ChatboxMessage message, bool grouped, float top, float viewTop, float viewBottom, float spacing)
    {
        if (message.Seq == _scrollToSeq || message.Seq == _highlightSeq) return false;
        if (!_rowMetrics.TryGetValue(message.Seq, out var metrics)) return false;
        if (metrics.Grouped != grouped) return false;
        if (top + metrics.Height >= viewTop && top <= viewBottom) return false;

        ImGui.Dummy(new Vector2(0f, MathF.Max(metrics.Height - spacing, 0f)));
        return true;
    }

    private void UpdateStickToBottom()
    {
        var scrollY = ImGui.GetScrollY();
        var maxScroll = ImGui.GetScrollMaxY();
        var threshold = 6f * ImGuiHelpers.GlobalScale;
        var scrolledUp = maxScroll >= _lastScrollMax - 0.5f && scrollY < _lastScrollY - 0.5f;
        var grew = maxScroll > _lastScrollMax + 0.5f;

        if (_scrollToBottomFrames > 0)
        {
            if (scrolledUp) _scrollToBottomFrames = 0;
            else if (grew) _scrollToBottomFrames = ScrollSettleFrames;
        }

        if (_scrollToBottomFrames > 0)
            _stickToBottom = true;
        else if (scrolledUp)
            _stickToBottom = false;
        else if (maxScroll <= 0f || scrollY >= maxScroll - threshold)
            _stickToBottom = true;

        _lastScrollY = scrollY;
        _lastScrollMax = maxScroll;
    }

    private bool ShouldGroup(ChatboxMessage? previous, ChatboxMessage message)
    {
        if (!Config.GroupConsecutive || previous == null) return false;
        if (Config.Layout == ChatboxLayout.Compact) return false;
        if (message.Reply != null || previous.IsSystem || message.IsSystem) return false;
        if (message.FilteredAsAd || previous.FilteredAsAd) return false;
        if (!string.Equals(previous.AuthorKey, message.AuthorKey, StringComparison.Ordinal)) return false;
        if (previous.Origin != message.Origin || previous.GameChatType != message.GameChatType) return false;

        return (message.Timestamp - previous.Timestamp).TotalSeconds <= Config.GroupWindowSeconds;
    }

    private void DrawMessage(
        ImDrawListPtr draw, ChatboxChannelState channel, ChatboxMessage message, bool grouped)
    {
        Chatbox.EnsureSegments(channel, message);
        PrepareEmbeds(message);

        ImGui.PushID(unchecked((int)message.Seq));

        var systemLine = message.IsSystemLine && Config.CompactSystemMessages;

        if (!grouped && !systemLine) ImGui.Dummy(new Vector2(0, Config.MessageSpacing * ImGuiHelpers.GlobalScale));

        draw.ChannelsSetCurrent(2);

        var origin = ImGui.GetCursorScreenPos();
        var width = MathF.Max(ImGui.GetContentRegionAvail().X, 80f);

        var concealed = message.FilteredAsAd && !_revealedAds.Contains(message.Seq);

        if (message.Reply != null && Config.ShowReplyPreview && Config.EnableReplies && !concealed)
            DrawReplyLine(channel, message.Reply, width);

        if (concealed)
        {
            DrawBlockedNotice(message);
        }
        else if (systemLine)
        {
            DrawSystemLine(message, width);
        }
        else
        {
            switch (Config.Layout)
            {
                case ChatboxLayout.Compact:
                    DrawCompact(message, width);
                    break;
                default:
                    DrawCozy(message, grouped, width);
                    break;
            }
        }

        var end = ImGui.GetCursorScreenPos();
        var rowMin = new Vector2(origin.X - _theme.PadX(0.3f), origin.Y - 1f);
        var rowMax = new Vector2(origin.X + width, MathF.Max(end.Y, origin.Y + ImGui.GetTextLineHeight()) + 1f);
        var hovered = HoveringRect(rowMin, rowMax);

        if (Chatbox.ImageCache.HasUnloaded && ImGui.IsRectVisible(rowMin, rowMax)) RequestRowMedia(message);

        draw.ChannelsSetCurrent(0);
        DrawRowBackground(draw, message, rowMin, rowMax, hovered);
        draw.ChannelsSetCurrent(2);

        if (hovered && Config.ShowHoverToolbar && !message.IsSystem && !concealed)
            DrawHoverToolbar(message, rowMin, rowMax);

        if (_scrollToSeq == message.Seq)
        {
            ImGui.SetScrollHereY(0.5f);
            _scrollToSeq = 0;
        }

        ImGui.PopID();
    }

    private void DrawRowBackground(ImDrawListPtr draw, ChatboxMessage message, Vector2 min, Vector2 max, bool hovered)
    {
        if (message.MentionsMe)
        {
            draw.AddRectFilled(min, max, ImGui.GetColorU32(Config.MentionHighlightColor));
            var accentWidth = 2f * ImGuiHelpers.GlobalScale;
            draw.AddRectFilled(min, new Vector2(min.X + accentWidth, max.Y), ImGui.GetColorU32(Config.MentionColor));
        }
        else if (hovered)
        {
            draw.AddRectFilled(min, max, ImGui.GetColorU32(_theme.Hover), _theme.Radius(0.4f));
        }

        if (_highlightSeq == message.Seq && DateTime.Now < _highlightUntil)
            draw.AddRectFilled(min, max, ImGui.GetColorU32(Config.MentionHighlightColor));
    }

    private float DrawContent(ChatboxMessage message, float wrapWidth, Vector4 textColor, Action? prefix)
    {
        _flow.Begin(MathF.Max(wrapWidth, 60f), MeasureLineHeight(message), Config.LineSpacing * ImGuiHelpers.GlobalScale);
        prefix?.Invoke();
        DrawSegments(message, textColor, EmoteSizeFor(message));
        return _flow.End();
    }

    private void DrawSegments(ChatboxMessage message, Vector4 textColor, float emoteSize)
    {
        foreach (var segment in message.Segments)
        {
            switch (segment.Kind)
            {
                case SegmentKind.LineBreak:
                    _flow.NewLine();
                    break;

                case SegmentKind.Emote:
                    DrawEmoteSegment(segment, emoteSize, textColor);
                    break;

                case SegmentKind.Mention:
                    DrawMentionSegment(segment);
                    break;

                case SegmentKind.ChannelRef:
                    _flow.Pill(segment.Text, DimColor(_theme.Accent, 0.25f), _theme.Accent, _theme.Radius(0.4f));
                    break;

                case SegmentKind.GameLink:
                    DrawGameLink(message, segment);
                    break;

                case SegmentKind.AutoTranslate:
                    _flow.Pill(segment.Text, DimColor(_theme.Accent, 0.18f), Lighten(_theme.Accent), _theme.Radius(0.35f));
                    break;

                case SegmentKind.Link:
                {
                    var url = segment.Url ?? segment.Text;
                    if (IsHiddenLink(url)) break;
                    if (_flow.Link(segment.Text, Config.LinkColor, Config.OutlineLinks)) OpenLink(url);
                    break;
                }

                default:
                    _flow.Text(segment.Text, segment.Color ?? textColor);
                    break;
            }
        }
    }

    private void RequestRowMedia(ChatboxMessage message)
    {
        var cache = Chatbox.ImageCache;

        foreach (var media in _embedMedia)
            cache.Request(media);

        foreach (var attachment in message.Attachments)
            cache.Request(attachment);

        foreach (var segment in message.Segments)
        {
            if (segment.Kind == SegmentKind.Emote) cache.Request(segment.ImageUrl);
        }

        cache.Request(message.AvatarUrl);
        cache.Request(message.Reply?.AvatarUrl);
    }

    private void DrawEmoteSegment(ContentSegment segment, float emoteSize, Vector4 textColor)
    {
        var texture = Config.ImageCacheEnabled && !string.IsNullOrEmpty(segment.ImageUrl)
            ? Chatbox.ImageCache.Get(segment.ImageUrl)
            : null;

        if (texture != null) _flow.Image(texture, emoteSize, segment.Text);
        else _flow.Text(segment.Text, segment.Color ?? textColor);
    }

    private void DrawMentionSegment(ContentSegment segment)
    {
        var background = DimColor(Config.MentionColor, segment.TargetsMe ? 0.75f : 0.30f);
        var foreground = segment.TargetsMe ? new Vector4(1f, 1f, 1f, 1f) : Lighten(Config.MentionColor);
        _flow.Pill(segment.Text, background, foreground, _theme.Radius(0.4f));
    }

    private void DrawAttachments(ChatboxMessage message, float width)
    {
        if (message.Attachments.Count == 0) return;

        foreach (var attachment in message.Attachments)
        {
            if (Chatbox.IsEmbedHidden(message.Seq, attachment)) continue;

            var texture = Config.ImageCacheEnabled && IsImageUrl(attachment)
                ? Chatbox.ImageCache.Get(attachment)
                : null;

            if (texture == null)
            {
                if (ImGui.SmallButton(ShortUrl(attachment))) OpenLink(attachment);
                continue;
            }

            var maxWidth = MathF.Min(width, 320f * ImGuiHelpers.GlobalScale);
            var ratio = texture.Height / MathF.Max(1f, texture.Width);
            var size = new Vector2(MathF.Min(maxWidth, texture.Width), 0f);
            size.Y = size.X * ratio;

            var origin = ImGui.GetCursorScreenPos();
            AnimatedTextureWrap.MarkVisible(texture, size);
            ImGui.Image(texture.Handle, size);
            var imageMax = origin + size;
            var hovered = HoveringRect(origin, imageMax);

            var btnSize = 18f * ImGuiHelpers.GlobalScale;
            var btnPos = new Vector2(origin.X + size.X - btnSize - 3f * ImGuiHelpers.GlobalScale, origin.Y + 3f * ImGuiHelpers.GlobalScale);
            var btnMax = btnPos + new Vector2(btnSize, btnSize);
            var btnHovered = HoveringRect(btnPos, btnMax);

            if (hovered || btnHovered)
            {
                var draw = ImGui.GetWindowDrawList();
                draw.AddRectFilled(btnPos, btnMax, ImGui.GetColorU32(btnHovered ? UiTheme.ColorDanger : new Vector4(0f, 0f, 0f, 0.65f)), _theme.Radius(0.3f));
                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    var icon = FontAwesomeIcon.Times.ToIconString();
                    var iconSize = ImGui.CalcTextSize(icon);
                    draw.AddText(btnPos + (new Vector2(btnSize, btnSize) - iconSize) * 0.5f, 0xFFFFFFFF, icon);
                }

                if (btnHovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip("Hide image");
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        Chatbox.HideEmbed(message.Seq, attachment);
                        return;
                    }
                }
                else if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) OpenLink(attachment);
                }
            }
        }
    }

    private void DrawReplyLine(ChatboxChannelState channel, ChatboxReplyRef reply, float width)
    {
        var gutter = Config.ShowAvatars && Config.Layout == ChatboxLayout.Cozy
            ? MathF.Max(16f, Config.AvatarSize) * ImGuiHelpers.GlobalScale + _theme.Gap(0.8f)
            : 0f;

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + gutter);

        var avatar = Config.ShowAvatars && !string.IsNullOrEmpty(reply.AvatarUrl)
            ? Chatbox.ImageCache.Get(reply.AvatarUrl)
            : null;

        if (avatar != null)
        {
            var mini = ImGui.GetTextLineHeight();
            var origin = ImGui.GetCursorScreenPos();
            AnimatedTextureWrap.MarkVisible(avatar, new Vector2(mini, mini));
            ImGui.Dummy(new Vector2(mini, mini));
            ImGui.GetWindowDrawList().AddImageRounded(
                avatar.Handle,
                origin,
                origin + new Vector2(mini, mini),
                Vector2.Zero,
                Vector2.One,
                0xFFFFFFFF,
                mini * 0.5f);
            ImGui.SameLine(0, _theme.Gap(0.3f));
        }

        var excerpt = reply.Excerpt.Replace('\n', ' ');
        ImGui.TextColored(_theme.MutedText, $"↰ {reply.AuthorName}: {excerpt}");

        if (!ImGui.IsItemHovered() || _autocomplete.Covers(ImGui.GetIO().MousePos)) return;

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) JumpTo(channel, reply.Seq);
    }

    private void DrawHoverToolbar(ChatboxMessage message, Vector2 rowMin, Vector2 rowMax)
    {
        var saved = ImGui.GetCursorScreenPos();
        var size = ImGui.GetFrameHeight();
        var spacing = _theme.Gap(0.3f);
        var buttons = Config.EnableReplies ? 2 : 1;
        var x = rowMax.X - buttons * (size + spacing);

        ImGui.SetCursorScreenPos(new Vector2(x, rowMin.Y - size * 0.35f));

        if (Config.EnableReplies)
        {
            if (_theme.IconButton("##chatbox-reply", FontAwesomeIcon.Reply, "Reply")) BeginReply(message);
            ImGui.SameLine(0, spacing);
        }

        if (_theme.IconButton("##chatbox-copy", FontAwesomeIcon.Copy, "Copy text"))
            ImGui.SetClipboardText(message.RawContent);

        ImGui.SetCursorScreenPos(saved);
    }

    private void DrawNewMessageDivider()
    {
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetTextLineHeight();
        var color = ImGui.GetColorU32(Config.UnreadBadgeColor);
        var label = "New";
        var labelWidth = ImGui.CalcTextSize(label).X + _theme.PadX(0.6f);
        var y = origin.Y + height * 0.5f;

        draw.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + width - labelWidth, y), color, 1f);
        draw.AddText(new Vector2(origin.X + width - labelWidth + _theme.PadX(0.3f), origin.Y), color, label);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void JumpTo(ChatboxChannelState channel, long seq)
    {
        if (seq == 0 || channel.FindBySeq(seq) == null) return;

        _scrollToSeq = seq;
        _scrollToBottomFrames = 0;
        _stickToBottom = false;
        _highlightSeq = seq;
        _highlightUntil = DateTime.Now.AddSeconds(2);
    }

    private float EmoteSizeFor(ChatboxMessage message)
    {
        var line = ImGui.GetTextLineHeight();
        return message.OnlyEmotes && Config.JumboLoneEmotes
            ? line * MathF.Max(1f, Config.JumboEmoteScale)
            : line * MathF.Max(0.5f, Config.EmoteScale);
    }

    private float MeasureLineHeight(ChatboxMessage message) =>
        MathF.Max(ImGui.GetTextLineHeight(), EmoteSizeFor(message));

    private Vector4 TextColorFor(ChatboxMessage message) =>
        message.IsSystem ? _theme.MutedText : _theme.Text;

    private Vector4? AuthorColorFor(ChatboxMessage message) =>
        _drawChannel != null && message.GameChatType != XivChatType.None
            ? Chatbox.ColorFor(_drawChannel, message.GameChatType)
            : message.AuthorColor;

    private Vector4 NameColorFor(ChatboxMessage message) =>
        Config.ColorNamesByChannel && AuthorColorFor(message) is { } color
            ? color
            : _theme.Accent;

    private string FormatTimestamp(DateTime timestamp) => Config.Timestamps switch
    {
        ChatboxTimestampStyle.Time => timestamp.ToString("HH:mm"),
        ChatboxTimestampStyle.TimeWithSeconds => timestamp.ToString("HH:mm:ss"),
        ChatboxTimestampStyle.DateAndTime => timestamp.ToString("dd.MM. HH:mm"),
        ChatboxTimestampStyle.Relative => RelativeTime(timestamp),
        _ => string.Empty,
    };

    private static string RelativeTime(DateTime timestamp)
    {
        var delta = DateTime.Now - timestamp;
        if (delta.TotalSeconds < 60) return "now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        return $"{(int)delta.TotalDays}d ago";
    }

    private void DrawAvatar(ChatboxMessage message, float size)
    {
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));

        var draw = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(size, size);
        var rounding = Config.RoundAvatars ? size * 0.5f : _theme.Radius(0.5f);

        var texture = Config.ImageCacheEnabled && !string.IsNullOrEmpty(message.AvatarUrl)
            ? Chatbox.ImageCache.Get(message.AvatarUrl)
            : null;

        if (texture != null)
        {
            AnimatedTextureWrap.MarkVisible(texture, min, max);
            draw.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFF, rounding);
            return;
        }

        var accent = AuthorColorFor(message) ?? _theme.Accent;
        draw.AddRectFilled(min, max, ImGui.GetColorU32(DimColor(accent, 0.8f)), rounding);

        var initials = Initials(message.AuthorName);
        var textSize = ImGui.CalcTextSize(initials);
        draw.AddText(min + (new Vector2(size, size) - textSize) * 0.5f, 0xFFFFFFFF, initials);
    }

    private static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
            : char.ToUpperInvariant(parts[0][0]).ToString();
    }

    private static Vector4 DimColor(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, alpha);

    private static Vector4 Lighten(Vector4 color) => new(
        MathF.Min(1f, color.X + 0.35f),
        MathF.Min(1f, color.Y + 0.35f),
        MathF.Min(1f, color.Z + 0.35f),
        1f);

    private static bool IsImageUrl(string url)
    {
        var trimmed = url.Split('?')[0];
        return trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string ShortUrl(string url) => url.Length <= 48 ? url : url[..45] + "...";

    private static void OpenLink(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
