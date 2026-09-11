using System;
using System.Numerics;
using Cordi.Domain;
using Cordi.Services.Chatbox;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Windows;

public sealed partial class ChatboxWindow
{
    private void DrawCozy(ChatboxMessage message, bool grouped, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var avatarSize = Config.ShowAvatars ? MathF.Max(16f, Config.AvatarSize) * scale : 0f;
        var gutter = avatarSize > 0f ? avatarSize + _theme.Gap(0.8f) : 0f;

        if (avatarSize > 0f)
        {
            ImGui.BeginGroup();
            if (grouped) ImGui.Dummy(new Vector2(avatarSize, 1f));
            else DrawAvatar(message, avatarSize);
            ImGui.EndGroup();
            ImGui.SameLine(0, _theme.Gap(0.8f));
        }

        ImGui.BeginGroup();
        if (!grouped) DrawHeaderLine(message);
        DrawContent(message, width - gutter, TextColorFor(message), null);
        DrawTranslationLine(message, width - gutter);
        DrawAttachments(message, width - gutter);
        DrawEmbeds(message, width - gutter);
        ImGui.EndGroup();
    }

    private void DrawSystemLine(ChatboxMessage message, float width)
    {
        DrawContent(message, width, AuthorColorFor(message) ?? _theme.MutedText,
            () => DrawTimestampGutter(message, _theme.FaintText));

        DrawAttachments(message, width);
        DrawEmbeds(message, width);
    }

    private void DrawCompact(ChatboxMessage message, float width)
    {
        DrawContent(message, width, TextColorFor(message), () =>
        {
            DrawTimestampGutter(message, _theme.MutedText);
            DrawTellDirectionInline(message);
            DrawAuthorNameInline(message);
        });
        DrawTranslationLine(message, width);
        DrawAttachments(message, width);
        DrawEmbeds(message, width);
    }

    private static bool IsTell(ChatboxMessage message) =>
        ChatTypes.IsTell(message.GameChatType);

    private Vector4 TellDirectionColor(ChatboxMessage message) =>
        message.GameChatType == XivChatType.TellOutgoing ? _theme.MutedText : _theme.Accent;

    private static string TellDirectionGlyph(ChatboxMessage message) =>
        (message.GameChatType == XivChatType.TellOutgoing
            ? FontAwesomeIcon.ArrowRight
            : FontAwesomeIcon.ArrowLeft).ToIconString();

    private static string TellDirectionTooltip(ChatboxMessage message) =>
        message.GameChatType == XivChatType.TellOutgoing ? "Outgoing tell" : "Incoming tell";

    private bool DrawTellDirectionIcon(ChatboxMessage message)
    {
        if (!IsTell(message)) return false;

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(TellDirectionColor(message), TellDirectionGlyph(message));

        if (ImGui.IsItemHovered()) ImGui.SetTooltip(TellDirectionTooltip(message));
        return true;
    }

    private void DrawTellDirectionInline(ChatboxMessage message)
    {
        if (!IsTell(message)) return;

        using (ImRaii.PushFont(UiBuilder.IconFont))
            _flow.Text(TellDirectionGlyph(message), TellDirectionColor(message));

        _flow.Text(" ", _theme.MutedText);
    }

    private void DrawBlockedNotice(ChatboxMessage message)
    {
        var stamp = FormatTimestamp(message.Timestamp);
        var caption = stamp.Length > 0
            ? $"{stamp}  Blocked by Advertisement Filter"
            : "Blocked by Advertisement Filter";

        var padX = _theme.PadX(0.4f);
        var padY = _theme.PadX(0.2f);
        var textSize = ImGui.CalcTextSize(caption);
        var box = new Vector2(textSize.X + padX * 2f, textSize.Y + padY * 2f);
        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton($"##chatbox-blocked-{message.Seq}", box);
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + box,
            ImGui.GetColorU32(hovered ? _theme.Hover : _theme.FrameBg), _theme.Radius(0.4f));
        draw.AddText(origin + new Vector2(padX, padY),
            ImGui.GetColorU32(hovered ? _theme.Text : _theme.MutedText), caption);

        if (hovered) ImGui.SetTooltip("Click to reveal the blocked message");
        if (clicked) _revealedAds.Add(message.Seq);
    }

    private static bool CanOpenPlayerMenu(ChatboxMessage message) =>
        message.Origin == ChatboxOrigin.Game
        && !message.IsSystemLine
        && message.AuthorName.Length > 0;

    private void DrawAuthorPrefix(ChatboxMessage message)
    {
        if (message.AuthorPrefix.Length == 0) return;

        ImGui.TextColored(message.AuthorPrefixColor ?? NameColorFor(message), message.AuthorPrefix);
        ImGui.SameLine(0, _theme.Gap(0.35f));
    }

    private void DrawAuthorName(ChatboxMessage message)
    {
        DrawAuthorPrefix(message);

        var color = NameColorFor(message);
        var text = message.DisplayName(Config.NameStyle);

        if (!CanOpenPlayerMenu(message))
        {
            ImGui.TextColored(color, text);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var size = ImGui.CalcTextSize(text);
        var pad = _theme.PadX(0.25f);
        var min = new Vector2(origin.X - pad, origin.Y - 1f);
        var max = new Vector2(origin.X + size.X + pad, origin.Y + size.Y + 1f);

        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows)
                      && ImGui.IsMouseHoveringRect(min, max);

        if (hovered)
        {
            ImGui.GetWindowDrawList().AddRectFilled(
                min, max, ImGui.GetColorU32(DimColor(color, 0.28f)), _theme.Radius(0.35f));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.TextColored(color, text);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) OpenPlayerPopup(message);
    }

    private void DrawAuthorNameInline(ChatboxMessage message)
    {
        var color = NameColorFor(message);
        var text = message.DisplayName(Config.NameStyle);

        if (message.AuthorPrefix.Length > 0)
            _flow.Text(message.AuthorPrefix + " ", message.AuthorPrefixColor ?? color);

        if (!CanOpenPlayerMenu(message))
        {
            _flow.Text(text + ": ", color);
            return;
        }

        _flow.Pill(text + ":", DimColor(color, 0.28f), color, _theme.Radius(0.35f), out var hovered, true);
        _flow.Text(" ", color);

        if (!hovered || !ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows)) return;

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) OpenPlayerPopup(message);
    }

    private void DrawHeaderLine(ChatboxMessage message)
    {
        if (message.IsSystem)
        {
            ImGui.TextColored(_theme.MutedText, "System");
        }
        else
        {
            if (DrawTellDirectionIcon(message)) ImGui.SameLine(0, _theme.Gap(0.35f));
            DrawAuthorName(message);
        }

        var stamp = FormatTimestamp(message.Timestamp);
        if (stamp.Length == 0) return;

        ImGui.SameLine(0, _theme.Gap(0.5f));
        ImGui.TextColored(_theme.MutedText, stamp);
    }
}
