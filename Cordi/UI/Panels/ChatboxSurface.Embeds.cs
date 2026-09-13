using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
    private readonly List<string> _embedUrls = new();
    private readonly List<string?> _embedMedia = new();
    private readonly HashSet<string> _hiddenLinks = new(StringComparer.OrdinalIgnoreCase);

    private void PrepareEmbeds(ChatboxMessage message)
    {
        _embedUrls.Clear();
        _embedMedia.Clear();
        _hiddenLinks.Clear();

        if (!Config.EnableLinkEmbeds || Config.MaxEmbedsPerMessage <= 0) return;
        if (message.FilteredAsAd && !Config.EmbedFilteredMessages) return;

        foreach (var segment in message.Segments)
        {
            if (segment.Kind != SegmentKind.Link) continue;

            var url = segment.Url ?? segment.Text;
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (Chatbox.IsEmbedHidden(message.Seq, url)) continue;
            if (!ChatboxEmbedCache.IsFetchable(url, out _)) continue;
            if (_embedUrls.Contains(url, StringComparer.OrdinalIgnoreCase)) continue;

            _embedUrls.Add(url);
            _embedMedia.Add(ResolveMedia(url));
            if (_embedUrls.Count >= Config.MaxEmbedsPerMessage) break;
        }

        if (!Config.HideMediaLinks || !Config.EmbedImages || !Config.ImageCacheEnabled) return;

        for (var i = 0; i < _embedUrls.Count; i++)
        {
            var media = _embedMedia[i];
            if (media == null) continue;
            if (Chatbox.ImageCache.Get(media) == null) continue;

            _hiddenLinks.Add(_embedUrls[i]);
        }
    }

    private string? ResolveMedia(string url)
    {
        if (IsImageUrl(url)) return url;

        var embed = Chatbox.EmbedCache.Get(url);
        return embed is { IsMedia: true } ? embed.ImageUrl : null;
    }

    private bool IsHiddenLink(string url) => _hiddenLinks.Count > 0 && _hiddenLinks.Contains(url);

    private void DrawEmbeds(ChatboxMessage message, float width)
    {
        if (_embedUrls.Count == 0) return;

        var maxWidth = MathF.Min(width, Config.EmbedMaxWidth * ImGuiHelpers.GlobalScale);

        for (var i = 0; i < _embedUrls.Count; i++)
        {
            var link = _embedUrls[i];
            var media = _embedMedia[i];

            if (media != null)
            {
                DrawEmbedMedia(message, link, media, maxWidth);
                continue;
            }

            var embed = Chatbox.EmbedCache.Get(link);
            if (embed is not { HasCard: true }) continue;

            ImGui.Dummy(new Vector2(0f, _theme.Gap(0.3f)));
            DrawEmbedCard(message, embed, maxWidth);
        }
    }

    private void DrawEmbedMedia(ChatboxMessage message, string link, string imageUrl, float maxWidth)
    {
        if (!Config.EmbedImages || !Config.ImageCacheEnabled) return;

        var texture = Chatbox.ImageCache.Get(imageUrl);
        if (texture == null) return;

        ImGui.Dummy(new Vector2(0f, _theme.Gap(0.3f)));
        DrawEmbedImage(message, texture, link, maxWidth);
    }

    private void DrawEmbedCard(ChatboxMessage message, ChatboxLinkEmbed embed, float maxWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var barWidth = 4f * scale;
        var padding = _theme.PadX(0.6f);
        var innerWidth = MathF.Max(80f, maxWidth - barWidth - padding * 2f);

        var origin = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(origin + new Vector2(barWidth + padding, padding));

        ImGui.BeginGroup();
        DrawEmbedBody(message, embed, innerWidth);
        ImGui.EndGroup();

        var contentMax = ImGui.GetItemRectMax();
        var boxMax = new Vector2(MathF.Max(contentMax.X + padding, origin.X + barWidth + padding * 2f),
            contentMax.Y + padding);

        var draw = ImGui.GetWindowDrawList();

        draw.ChannelsSetCurrent(1);
        _theme.AccentCard(draw, origin, boxMax, _theme.CardBg, Config.LinkColor, barWidth);
        draw.ChannelsSetCurrent(2);

        var btnSize = 18f * scale;
        var btnPos = new Vector2(boxMax.X - btnSize - 3f * scale, origin.Y + 3f * scale);
        var btnMax = btnPos + new Vector2(btnSize, btnSize);
        var cardHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.IsMouseHoveringRect(origin, boxMax);
        var btnHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.IsMouseHoveringRect(btnPos, btnMax);

        if (cardHovered || btnHovered)
        {
            _theme.OverlayIconButton(btnPos, btnSize, FontAwesomeIcon.Times, btnHovered);

            if (btnHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("Hide embed");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    Chatbox.HideEmbed(message.Seq, embed.Url);
                    return;
                }
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, boxMax.Y));
        ImGui.Dummy(new Vector2(boxMax.X - origin.X, 1f));
    }

    private void DrawEmbedBody(ChatboxMessage message, ChatboxLinkEmbed embed, float innerWidth)
    {
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + innerWidth);

        try
        {
            DrawEmbedContent(message, embed, innerWidth);
        }
        finally
        {
            ImGui.PopTextWrapPos();
        }
    }

    private void DrawEmbedContent(ChatboxMessage message, ChatboxLinkEmbed embed, float innerWidth)
    {
        DrawEmbedSiteLine(embed, innerWidth);

        if (embed.Title.Length > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Config.LinkColor);
            ImGui.TextWrapped(embed.Title);
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) OpenLink(embed.Url);
            }
        }

        if (embed.Description.Length > 0)
            ImGui.TextColored(_theme.MutedText, embed.Description);

        if (!Config.EmbedImages || !Config.ImageCacheEnabled || string.IsNullOrEmpty(embed.ImageUrl)) return;

        var texture = Chatbox.ImageCache.Get(embed.ImageUrl);
        if (texture == null) return;

        ImGui.Dummy(new Vector2(0f, _theme.Gap(0.25f)));
        DrawEmbedImage(message, texture, embed.Url, innerWidth);
    }

    private void DrawEmbedSiteLine(ChatboxLinkEmbed embed, float innerWidth)
    {
        if (embed.SiteName.Length == 0) return;

        var iconSize = ImGui.GetTextLineHeight();
        Chatbox.ImageCache.Request(embed.IconUrl);

        var icon = Config.ImageCacheEnabled && !string.IsNullOrEmpty(embed.IconUrl)
            ? Chatbox.ImageCache.Get(embed.IconUrl)
            : null;

        if (icon != null)
        {
            AnimatedTextureWrap.MarkVisible(icon, new Vector2(iconSize, iconSize));
            ImGui.Image(icon.Handle, new Vector2(iconSize, iconSize));
            ImGui.SameLine(0, _theme.Gap(0.35f));
        }

        ImGui.TextColored(_theme.MutedText, Clip(embed.SiteName, innerWidth));
    }

    private void DrawEmbedImage(ChatboxMessage message, IDalamudTextureWrap texture, string url, float maxWidth)
    {
        var maxHeight = MathF.Max(60f, Config.EmbedImageMaxHeight * ImGuiHelpers.GlobalScale);
        var width = MathF.Min(maxWidth, texture.Width);
        var height = width * (texture.Height / MathF.Max(1f, texture.Width));

        if (height > maxHeight)
        {
            width *= maxHeight / height;
            height = maxHeight;
        }

        var origin = ImGui.GetCursorScreenPos();
        AnimatedTextureWrap.MarkVisible(texture, new Vector2(width, height));
        ImGui.Image(texture.Handle, new Vector2(width, height));

        var imageMax = origin + new Vector2(width, height);
        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.IsMouseHoveringRect(origin, imageMax);

        var btnSize = 18f * ImGuiHelpers.GlobalScale;
        var btnPos = new Vector2(origin.X + width - btnSize - 3f * ImGuiHelpers.GlobalScale, origin.Y + 3f * ImGuiHelpers.GlobalScale);
        var btnMax = btnPos + new Vector2(btnSize, btnSize);
        var btnHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.IsMouseHoveringRect(btnPos, btnMax);

        if (hovered || btnHovered)
        {
            _theme.OverlayIconButton(btnPos, btnSize, FontAwesomeIcon.Times, btnHovered, 0.65f);

            if (btnHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("Hide embed");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    Chatbox.HideEmbed(message.Seq, url);
                    return;
                }
            }
            else if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) OpenLink(url);
            }
        }
    }

    private static string Clip(string text, float available)
    {
        if (ImGui.CalcTextSize(text).X <= available) return text;

        var length = text.Length;
        while (length > 1 && ImGui.CalcTextSize(text[..length] + "…").X > available)
            length--;

        return text[..length] + "…";
    }
}
