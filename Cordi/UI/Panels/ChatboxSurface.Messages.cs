using System;
using System.Globalization;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Domain;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface
{
    private const int ScrollSettleFrames = 3;
    private const float CullMargin = 240f;

    private readonly record struct RowMetrics(float Height, bool Grouped, int Repeats, bool Collapsed);

    private int _rowRepeats = 1;
    private long _scrollToSeq;
    private float _scrollToAlign = 0.5f;
    private long _anchorSeq;
    private float _anchorTop;
    private float _lastScrollY;
    private float _lastScrollMax;
    private bool _stickToBottom = true;
    private ChatboxChannelConfig? _drawChannel;
    private readonly System.Collections.Generic.HashSet<long> _revealedAds = new();
    private readonly System.Collections.Generic.List<ChatboxMessage> _drawBuffer = new();
    private readonly System.Collections.Generic.Dictionary<long, RowMetrics> _rowMetrics = new();
    private readonly System.Collections.Generic.List<float> _rowTops = new();
    private int _rowMetricsKey;
    private int _rowTopsValid;
    private long _rowTopsSeq0;
    private long _metricsScrollToSeq;
    private long _metricsHighlightSeq;
    private long _metricsDividerSeq;

    private bool HoveringRect(Vector2 min, Vector2 max) =>
        ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
        && ImGui.IsMouseHoveringRect(min, max)
        && !_autocomplete.Covers(ImGui.GetIO().MousePos);

    private void DrawMessages(ChatboxChannelState channel)
    {
        _drawChannel = channel.Config;

        if (Config.AutoScroll && _stickToBottom && _scrollToSeq == 0 && _scrollToBottomFrames == 0)
            Chatbox.ReleaseOlderHistory(channel);

        HookTranslation();
        DrainTranslationDirty();
        DrainOutgoingSends();

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

        if (DrawHistoryLoader(channel)) channel.SnapshotInto(_drawBuffer);

        SyncRowMetrics(MathF.Max(ImGui.GetContentRegionAvail().X, 80f), dividerSeq);

        var rowSpacing = ImRaii.PushStyle(
            ImGuiStyleVar.ItemSpacing,
            new Vector2(ImGui.GetStyle().ItemSpacing.X, 0f));

        var scrollY = ImGui.GetScrollY();
        var viewTop = scrollY - CullMargin;
        var viewBottom = scrollY + ImGui.GetWindowHeight() + CullMargin;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;

        var draw = ImGui.GetWindowDrawList();
        draw.ChannelsSplit(3);
        draw.ChannelsSetCurrent(2);

        ChatboxMessage? previous = null;
        var repeats = 1;
        var dividerPending = false;
        var dividerDone = false;
        var anchorTop = float.NaN;
        var nextAnchorSeq = 0L;
        var nextAnchorTop = 0f;

        var baseTop = ImGui.GetCursorPosY();
        var start = ResolveFirstRow(viewTop - baseTop);
        var culled = start > 0 ? _rowTops[start] : 0f;
        var cursor = baseTop + culled;

        if (start > 0 && dividerSeq != 0 && _drawBuffer[start - 1].Seq >= dividerSeq) dividerDone = true;

        for (var index = start; index < _drawBuffer.Count; index++)
        {
            var message = _drawBuffer[index];
            var pinned = message.Seq == _scrollToSeq || message.Seq == _highlightSeq;

            WriteRowTop(index, cursor - baseTop);

            if (dividerSeq != 0 && !dividerDone && message.Seq >= dividerSeq)
            {
                dividerPending = true;
                dividerDone = true;
            }

            RowMetrics metrics = default;
            var cached = !pinned && _rowMetrics.TryGetValue(message.Seq, out metrics);
            bool grouped;

            if (cached)
            {
                if (metrics.Collapsed && index + 1 < _drawBuffer.Count)
                {
                    repeats++;
                    continue;
                }

                grouped = metrics.Grouped;
            }
            else
            {
                if (index + 1 < _drawBuffer.Count && CollapsesInto(message, _drawBuffer[index + 1]))
                {
                    _rowMetrics[message.Seq] = new RowMetrics(0f, false, 0, true);
                    repeats++;
                    continue;
                }

                grouped = ShouldGroup(previous, message);
            }

            previous = message;

            if (cached
                && metrics.Repeats == repeats
                && (cursor + metrics.Height < viewTop || cursor > viewBottom))
            {
                if (message.Seq == _anchorSeq) anchorTop = cursor;
                if (nextAnchorSeq == 0 && cursor >= scrollY)
                {
                    nextAnchorSeq = message.Seq;
                    nextAnchorTop = cursor;
                }

                culled += metrics.Height;
                cursor += metrics.Height;
                repeats = 1;
                continue;
            }

            FlushCulledRows(ref culled, spacing);

            var top = ImGui.GetCursorPosY();
            if (message.Seq == _anchorSeq) anchorTop = top;
            if (nextAnchorSeq == 0 && top >= scrollY)
            {
                nextAnchorSeq = message.Seq;
                nextAnchorTop = top;
            }

            if (dividerPending)
            {
                DrawNewMessageDivider();
                dividerPending = false;
            }

            DrawMessage(draw, channel, message, grouped, repeats);

            cursor = ImGui.GetCursorPosY();
            _rowMetrics[message.Seq] = new RowMetrics(cursor - top, grouped, repeats, false);

            repeats = 1;
        }

        FlushCulledRows(ref culled, spacing);

        draw.ChannelsMerge();

        rowSpacing.Dispose();

        DrawLinkPopup();
        DrawMessageLanguageMenu();

        if (pendingJump != 0 && _scrollToSeq == pendingJump)
        {
            _scrollToSeq = 0;
            _anchorSeq = 0;
            nextAnchorSeq = 0;
        }
        else
        {
            ApplyScrollAnchor(anchorTop);
        }

        if (nextAnchorSeq != 0)
        {
            _anchorSeq = nextAnchorSeq;
            _anchorTop = nextAnchorTop;
        }

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
    }

    private bool DrawHistoryLoader(ChatboxChannelState channel)
    {
        if (!channel.HasMoreHistory) return false;

        var width = MathF.Max(ImGui.GetContentRegionAvail().X, 80f);

        ImGui.Dummy(new Vector2(0f, _theme.Gap(0.3f)));

        if (!_theme.DividerAction("##cordi-load-older", "Load older messages", width)) return false;
        if (!Chatbox.LoadOlderHistory(channel)) return false;

        _stickToBottom = false;
        _scrollToBottomFrames = 0;

        return true;
    }

    private void SyncRowMetrics(float width, long dividerSeq)
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
                ImGui.GetTextLineHeight()),
            HashCode.Combine(
                Config.GroupWindowSeconds,
                Config.CollapseRepeats,
                Config.CollapseWindowSeconds,
                Config.EnableReplies,
                TranslationMetricsKey()));

        if (key != _rowMetricsKey || dividerSeq != _metricsDividerSeq)
        {
            _rowMetricsKey = key;
            _metricsDividerSeq = dividerSeq;
            _metricsScrollToSeq = _scrollToSeq;
            _metricsHighlightSeq = _highlightSeq;
            ResetRowMetrics();
            return;
        }

        if (_scrollToSeq != _metricsScrollToSeq)
        {
            ForgetRow(_metricsScrollToSeq);
            ForgetRow(_scrollToSeq);
            _metricsScrollToSeq = _scrollToSeq;
        }

        if (_highlightSeq != _metricsHighlightSeq)
        {
            ForgetRow(_metricsHighlightSeq);
            ForgetRow(_highlightSeq);
            _metricsHighlightSeq = _highlightSeq;
        }

        if (_rowMetrics.Count > _drawBuffer.Count * 2 + 64)
        {
            ResetRowMetrics();
            return;
        }

        if (_rowTopsValid == 0)
        {
            _rowTopsSeq0 = _drawBuffer[0].Seq;
            return;
        }

        if (_drawBuffer[0].Seq != _rowTopsSeq0) InvalidateRowTops();
        else if (_rowTopsValid > _drawBuffer.Count) _rowTopsValid = _drawBuffer.Count;
    }

    private void ResetRowMetrics()
    {
        _rowMetrics.Clear();
        InvalidateRowTops();
    }

    private void InvalidateRowTops()
    {
        _rowTops.Clear();
        _rowTopsValid = 0;
        _rowTopsSeq0 = _drawBuffer.Count > 0 ? _drawBuffer[0].Seq : 0;
    }

    internal void ForgetRow(long seq)
    {
        if (seq == 0) return;
        if (!_rowMetrics.Remove(seq)) return;

        InvalidateRowTops();
    }

    private void WriteRowTop(int index, float top)
    {
        if (index < _rowTops.Count) _rowTops[index] = top;
        else if (index == _rowTops.Count) _rowTops.Add(top);
        else return;

        if (index >= _rowTopsValid) _rowTopsValid = index + 1;
    }

    private int ResolveFirstRow(float viewTop)
    {
        var limit = Math.Min(_rowTopsValid, _drawBuffer.Count);
        if (limit <= 1 || viewTop <= 0f) return 0;

        var lo = 0;
        var hi = limit - 1;
        var found = 0;

        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (_rowTops[mid] <= viewTop)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        while (found > 0 && _rowTops[found - 1] >= _rowTops[found]) found--;

        return found;
    }

    private bool CollapsesInto(ChatboxMessage message, ChatboxMessage next)
    {
        if (!Config.CollapseRepeats) return false;
        if (message.Seq == _scrollToSeq || message.Seq == _highlightSeq) return false;
        if (message.FilteredAsAd || next.FilteredAsAd) return false;
        if (message.Reply != null || next.Reply != null) return false;
        if (message.Attachments.Count > 0 || next.Attachments.Count > 0) return false;
        if (message.Origin != next.Origin || message.GameChatType != next.GameChatType) return false;
        if (!string.Equals(message.AuthorKey, next.AuthorKey, StringComparison.Ordinal)) return false;
        if (message.RawContent.Length == 0) return false;
        if (!string.Equals(message.RawContent, next.RawContent, StringComparison.Ordinal)) return false;

        return (next.Timestamp - message.Timestamp).TotalSeconds <= Config.CollapseWindowSeconds;
    }

    private static void FlushCulledRows(ref float height, float spacing)
    {
        if (height <= 0f) return;

        ImGui.Dummy(new Vector2(0f, MathF.Max(height - spacing, 0f)));
        height = 0f;
    }

    private void ApplyScrollAnchor(float anchorTop)
    {
        if (_anchorSeq == 0 || float.IsNaN(anchorTop)) return;
        if (_stickToBottom || _scrollToBottomFrames > 0 || _scrollToSeq != 0) return;

        var delta = anchorTop - _anchorTop;
        if (MathF.Abs(delta) < 0.5f) return;

        ImGui.SetScrollY(ImGui.GetScrollY() + delta);
        _lastScrollY += delta;
        _lastScrollMax = ImGui.GetScrollMaxY();
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
        ImDrawListPtr draw, ChatboxChannelState channel, ChatboxMessage message, bool grouped, int repeats)
    {
        Chatbox.EnsureSegments(channel, message);
        PrepareEmbeds(message);

        _rowRepeats = repeats;

        ImGui.PushID(unchecked((int)message.Seq));

        var systemLine = message.IsSystemLine && Config.CompactSystemMessages;

        var gap = Config.MessageSpacing * ImGuiHelpers.GlobalScale;
        var lineSpacing = Config.LineSpacing * ImGuiHelpers.GlobalScale;
        if (!grouped && !systemLine && gap > 0f) ImGui.Dummy(new Vector2(0, gap));

        draw.ChannelsSetCurrent(2);

        var origin = ImGui.GetCursorScreenPos();
        var width = MathF.Max(ImGui.GetContentRegionAvail().X, 80f);

        var concealed = message.FilteredAsAd && !_revealedAds.Contains(message.Seq);

        using (ImRaii.PushStyle(
                   ImGuiStyleVar.ItemSpacing,
                   new Vector2(ImGui.GetStyle().ItemSpacing.X, lineSpacing)))
        {
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
        }

        var end = ImGui.GetCursorScreenPos().Y - lineSpacing;
        var rowMin = new Vector2(origin.X - _theme.PadX(0.3f), origin.Y);
        var rowMax = new Vector2(origin.X + width, MathF.Max(end, origin.Y + ImGui.GetTextLineHeight()));
        var hovered = HoveringRect(rowMin, rowMax);

        if (Chatbox.ImageCache.HasUnloaded && ImGui.IsRectVisible(rowMin, rowMax)) RequestRowMedia(message);

        draw.ChannelsSetCurrent(0);
        DrawRowBackground(draw, message, rowMin, rowMax, hovered);
        draw.ChannelsSetCurrent(2);

        if (hovered && !concealed) DrawTranslationTooltip(message);

        if (hovered && Config.ShowHoverToolbar && !message.IsSystem && !concealed)
            DrawHoverToolbar(message, rowMin, rowMax);

        if (_scrollToSeq == message.Seq)
        {
            ImGui.SetScrollHereY(_scrollToAlign);
            _scrollToSeq = 0;
        }

        ImGui.PopID();
    }

    private void DrawRowBackground(ImDrawListPtr draw, ChatboxMessage message, Vector2 min, Vector2 max, bool hovered)
    {
        if (message.MentionsMe)
            _theme.HighlightRow(draw, min, max, Config.MentionHighlightColor, Config.MentionColor);
        else if (hovered)
            _theme.HoverRow(draw, min, max);

        if (_highlightSeq == message.Seq && DateTime.Now < _highlightUntil)
            _theme.HighlightRow(draw, min, max, Config.MentionHighlightColor);
    }

    private float DrawContent(ChatboxMessage message, float wrapWidth, Vector4 textColor, Action? prefix)
    {
        _flow.Begin(MathF.Max(wrapWidth, 60f), MeasureLineHeight(message), Config.LineSpacing * ImGuiHelpers.GlobalScale);
        prefix?.Invoke();

        if (ReplacesContent(message)) DrawTranslatedContent(message);
        else DrawSegments(message, textColor, EmoteSizeFor(message));

        if (_rowRepeats > 1) _flow.Text($"  ({_rowRepeats}x)", _theme.FaintText);

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
                    _flow.Text(segment.Text, segment.Color ?? textColor);
                    break;

                case SegmentKind.GameIcon:
                    DrawGameIconSegment(segment, textColor);
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

    private void DrawGameIconSegment(ContentSegment segment, Vector4 textColor)
    {
        if (GameFontIcons.TryResolve(
                segment.IconId, ImGui.GetTextLineHeight(), out var texture, out var size, out var uv0, out var uv1))
        {
            _flow.Icon(texture, size, uv0, uv1);
            return;
        }

        if (segment.Text.Length > 0) _flow.Text(segment.Text, segment.Color ?? textColor);
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
                _theme.OverlayIconButton(btnPos, btnSize, FontAwesomeIcon.Times, btnHovered, 0.65f);

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

            ImGui.Dummy(new Vector2(mini, mini));

            _theme.Avatar(
                origin,
                origin + new Vector2(mini, mini),
                avatar,
                mini * 0.5f,
                _theme.Accent,
                onImage: AnimatedTextureWrap.MarkVisible);

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
        var size = HoverToolbarButtonSize(rowMin, rowMax);
        var spacing = _theme.Gap(0.2f);
        var pad = _theme.Gap(0.2f);

        var (name, world) = Counterpart(message);
        var replyType = ReplyChatType(message);
        var canReply = replyType != XivChatType.None
                       && (!ChatTypes.IsTell(replyType) || name.Length > 0);
        var canOpenDm = Chatbox.CanOpenConversationWith(name, world);
        var canTranslate = ManualTranslationReady(message);

        var count = 2;
        if (canReply) count++;
        if (canOpenDm) count++;
        if (canTranslate) count++;

        var width = count * size + (count - 1) * spacing + 2f * pad;

        var panelMin = new Vector2(rowMax.X - width, rowMin.Y);
        var panelMax = new Vector2(rowMax.X, rowMin.Y + size + 2f * pad);

        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(panelMin, panelMax, ImGui.GetColorU32(_theme.PanelBg), _theme.Radius(0.7f));
        draw.AddRect(panelMin, panelMax, ImGui.GetColorU32(_theme.Border), _theme.Radius(0.7f));

        var cursor = panelMin + new Vector2(pad, pad);

        if (canReply)
        {
            var tooltip = ChatTypes.IsTell(replyType)
                ? $"Reply to {name}"
                : $"Reply in {ChatboxService.LabelFor(replyType)}";

            if (_theme.IconAction("chatbox-reply", cursor, FontAwesomeIcon.Reply, _theme.Accent, tooltip, size, size))
                ReplyTo(message, replyType);

            cursor.X += size + spacing;
        }

        if (canOpenDm)
        {
            var tooltip = Chatbox.ConversationsInOwnWindow
                ? $"Open the DM with {name} in a window"
                : $"Open the DM with {name} in the chatbox";

            if (_theme.IconAction(
                    "chatbox-open-dm", cursor, FontAwesomeIcon.CommentDots, _theme.Accent, tooltip, size, size))
                Chatbox.OpenConversationFor(name, world);

            cursor.X += size + spacing;
        }

        if (canTranslate)
        {
            var translated = message.HasTranslation;
            var tooltip = translated
                ? "Translate again - pick the language this message is written in"
                : "Translate this message";

            if (_theme.IconAction(
                    "chatbox-translate-message", cursor, FontAwesomeIcon.Language, _theme.Accent,
                    tooltip, size, size))
            {
                if (translated) OpenMessageLanguageMenu(message, cursor, cursor + new Vector2(size, size));
                else RequestTranslation(message);
            }

            cursor.X += size + spacing;
        }

        if (_theme.IconAction("chatbox-copy", cursor, FontAwesomeIcon.Copy, _theme.Accent, "Copy message", size, size))
            ImGui.SetClipboardText(message.RawContent);

        cursor.X += size + spacing;

        if (_theme.IconAction(
                "chatbox-copy-full", cursor, FontAwesomeIcon.FileAlt, _theme.Accent,
                "Copy with name, time and channel", size, size))
            ImGui.SetClipboardText(DescribeMessage(message));

        ImGui.SetCursorScreenPos(saved);
    }

    private static (string Name, string World) Counterpart(ChatboxMessage message)
    {
        if (message.TellTarget.Length > 0)
        {
            var at = message.TellTarget.IndexOf('@');

            return at > 0
                ? (message.TellTarget[..at], message.TellTarget[(at + 1)..])
                : (message.TellTarget, string.Empty);
        }

        return (message.AuthorName, message.AuthorWorld);
    }

    private static XivChatType ReplyChatType(ChatboxMessage message)
    {
        var type = message.GameChatType;

        if (ChatTypes.IsTell(type)) return XivChatType.TellOutgoing;

        if (type == XivChatType.CrossParty) type = XivChatType.Party;

        return ChatboxService.IsSendTargetAvailable(type) ? type : XivChatType.None;
    }

    private void ReplyTo(ChatboxMessage message, XivChatType type)
    {
        if (ChatTypes.IsTell(type))
        {
            var (name, world) = Counterpart(message);
            if (name.Length == 0) return;

            InsertText(world.Length > 0 ? $"/tell {name}@{world} " : $"/tell {name} ");
            _focusInput = true;
            return;
        }

        Config.LastSendChatType = type;
        _plugin.Config.Save();
        _focusInput = true;
    }

    private float HoverToolbarButtonSize(Vector2 rowMin, Vector2 rowMax) =>
        Math.Clamp(rowMax.Y - rowMin.Y - _theme.Gap(0.4f), _theme.Scaled(15f), _theme.Scaled(22f));

    private string DescribeMessage(ChatboxMessage message)
    {
        var author = message.AuthorWorld.Length > 0
            ? $"{message.AuthorName}@{message.AuthorWorld}"
            : message.AuthorName;

        var channel = Chatbox.ChannelDisplayName(message.ChannelId);

        return author.Length > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}] [{channel}] {author}: {message.RawContent}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}] [{channel}] {message.RawContent}");
    }

    private void DrawNewMessageDivider()
    {
        _theme.DividerTrailing("New", ImGui.GetContentRegionAvail().X, Config.UnreadBadgeColor);
    }

    private void JumpTo(ChatboxChannelState channel, long seq)
    {
        if (seq == 0 || channel.FindBySeq(seq) == null) return;

        _scrollToSeq = seq;
        _scrollToAlign = 0.5f;
        _scrollToBottomFrames = 0;
        _stickToBottom = false;
        _highlightSeq = seq;
        _highlightUntil = DateTime.Now.AddSeconds(2);
    }

    private void ScrollToUnread(ChatboxChannelState? channel, long dividerSeq)
    {
        if (dividerSeq == 0 || channel == null) return;
        if (!channel.Config.ScrollToFirstUnread || channel.FindBySeq(dividerSeq) == null) return;

        _scrollToSeq = dividerSeq;
        _scrollToAlign = 0.2f;
        _scrollToBottomFrames = 0;
        _stickToBottom = false;
    }

    private float EmoteSizeFor(ChatboxMessage message)
    {
        var line = ImGui.GetTextLineHeight();
        return message.OnlyEmotes && Config.JumboLoneEmotes
            ? line * MathF.Max(1f, Config.JumboEmoteScale)
            : line * MathF.Max(0.5f, Config.EmoteScale);
    }

    private float MeasureLineHeight(ChatboxMessage message)
    {
        var line = ImGui.GetTextLineHeight();
        return HasEmoteSegments(message) ? MathF.Max(line, EmoteSizeFor(message)) : line;
    }

    private static bool HasEmoteSegments(ChatboxMessage message)
    {
        var segments = message.Segments;

        for (var i = 0; i < segments.Count; i++)
            if (segments[i].Kind == SegmentKind.Emote)
                return true;

        return false;
    }

    private Vector4 TextColorFor(ChatboxMessage message)
    {
        if (message.IsSystem) return _theme.MutedText;

        return Config.ColorMessagesByChannel && AuthorColorFor(message) is { } color
            ? color
            : _theme.Text;
    }

    private Vector4? AuthorColorFor(ChatboxMessage message) =>
        _drawChannel != null && message.GameChatType != XivChatType.None
            ? Chatbox.ColorFor(_drawChannel, message.GameChatType)
            : message.AuthorColor;

    private Vector4 NameColorFor(ChatboxMessage message) =>
        (ChatTypes.IsParty(message.GameChatType) ? message.AuthorPrefixColor : null)
        ?? (Config.ColorNamesByChannel && AuthorColorFor(message) is { } color
            ? color
            : _theme.Accent);

    private string FormatTimestamp(DateTime timestamp) => Config.Timestamps switch
    {
        ChatboxTimestampStyle.Time => timestamp.ToString("HH:mm"),
        ChatboxTimestampStyle.TimeWithSeconds => timestamp.ToString("HH:mm:ss"),
        ChatboxTimestampStyle.DateAndTime => timestamp.ToString("dd.MM. HH:mm"),
        ChatboxTimestampStyle.Relative => RelativeTime(timestamp),
        _ => string.Empty,
    };

    private float TimestampGutter()
    {
        var sample = Config.Timestamps switch
        {
            ChatboxTimestampStyle.Time => "00:00",
            ChatboxTimestampStyle.TimeWithSeconds => "00:00:00",
            ChatboxTimestampStyle.DateAndTime => "00.00. 00:00",
            ChatboxTimestampStyle.Relative => "00m ago",
            _ => string.Empty,
        };

        return sample.Length == 0 ? 0f : ImGui.CalcTextSize(sample).X + _theme.Gap(0.75f);
    }

    private void DrawTimestampGutter(ChatboxMessage message, Vector4 color)
    {
        var stamp = FormatTimestamp(message.Timestamp);
        if (stamp.Length == 0) return;

        _flow.Text(stamp + " ", color);
        _flow.Indent(TimestampGutter());
    }

    private static string RelativeTime(DateTime timestamp)
    {
        var delta = DateTime.Now - timestamp;
        if (delta.TotalSeconds < 60) return "now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        return $"{(int)delta.TotalDays}d ago";
    }

    private void DrawAvatar(ChatboxMessage message, Vector2 origin, float size)
    {
        var texture = Config.ImageCacheEnabled && !string.IsNullOrEmpty(message.AvatarUrl)
            ? Chatbox.ImageCache.Get(message.AvatarUrl)
            : null;

        _theme.Avatar(
            origin,
            origin + new Vector2(size, size),
            texture,
            Config.RoundAvatars ? size * 0.5f : _theme.Radius(0.5f),
            DimColor(AuthorColorFor(message) ?? _theme.Accent, 0.8f),
            Initials(message.AuthorName),
            AnimatedTextureWrap.MarkVisible);
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
