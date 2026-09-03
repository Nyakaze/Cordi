using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.UI.Components;
using Cordi.UI.Panels;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;

namespace Cordi.UI.Windows;

public sealed partial class ChatboxWindow
{
    private void DrawInputBar(ChatboxChannelState channel)
    {
        if (_replyTarget != null && Config.EnableReplies) DrawReplyStrip();

        if (!Chatbox.CanSend(channel))
        {
            Chatbox.InputActive = false;
            _theme.MutedLabel(channel.Id == ChatboxService.CombinedChannelId
                ? "Select a channel to send messages."
                : "This channel is read-only.");
            return;
        }

        var spacing = _theme.Gap(0.4f);
        var buttonWidth = ImGui.GetFrameHeight();
        var buttons = Config.ShowEmojiPicker ? 2 : 1;
        DrawSendTargetPicker(channel, buttonWidth);
        ImGui.SameLine(0, spacing);

        ImGui.SetNextItemWidth(MathF.Max(
            80f,
            ImGui.GetContentRegionAvail().X - buttonWidth * buttons - spacing * buttons));

        if (_focusInput)
        {
            ImGui.SetKeyboardFocusHere();
            _focusInput = false;
        }

        _theme.PushInputScope();
        var submitted = ImGui.InputTextWithHint(
            "##chatbox-input",
            $"Message {channel.Config.Name}",
            ref _input,
            ChatboxConfig.InputBufferLength,
            ImGuiInputTextFlags.EnterReturnsTrue);
        _theme.PopInputScope();

        var inputActive = ImGui.IsItemActive();
        var pickerOpen = false;

        if (Config.ShowEmojiPicker)
        {
            ImGui.SameLine(0, spacing);
            if (_theme.IconButton("##chatbox-emoji", FontAwesomeIcon.Smile, "Emoji")) _picker.Open();

            _picker.Draw(InsertText);
            pickerOpen = _picker.InputActive;
        }

        Chatbox.InputActive = inputActive || pickerOpen;

        ImGui.SameLine(0, spacing);
        if (_theme.IconButton("##chatbox-send", FontAwesomeIcon.PaperPlane, "Send")) submitted = true;

        if (inputActive && !pickerOpen && _replyTarget != null && ImGui.IsKeyPressed(ImGuiKey.Escape)) CancelReply();

        if (submitted) Submit(channel);
    }

    private void DrawSendTargetPicker(ChatboxChannelState channel, float height)
    {
        var pinned = Chatbox.IsSendTypePinned(channel.Config);
        var active = Chatbox.ResolveSendType(channel.Config);

        var preview = active != XivChatType.None
            ? ChatboxService.LabelFor(active)
            : "Pick channel";

        var items = new List<DropdownItem>();
        foreach (var type in ChatboxService.SendableChatTypes)
        {
            if (!ChatboxService.IsSendTargetAvailable(type)) continue;
            items.Add(new DropdownItem { Key = type.ToString(), Label = ChatboxService.LabelFor(type) });
        }

        var width = _theme.CompactPickerWidth(preview, 70f, 170f);
        var popupWidth = MathF.Max(width, _theme.Scaled(240f));

        var tooltip = pinned
            ? $"Sending as {preview}\nFixed by this channel's \"Send as\" setting."
            : $"Sending as {preview}\nPick the game chat channel to send in.";

        _theme.CompactPicker(
            "chatbox-send-target",
            new Vector2(width, height),
            preview,
            items,
            active.ToString(),
            key =>
            {
                if (!Enum.TryParse<XivChatType>(key, out var parsed)) return;

                Config.LastSendChatType = parsed;
                _plugin.Config.Save();
            },
            popupWidth,
            !pinned,
            tooltip);
    }

    private void DrawReplyStrip()
    {
        var reply = _replyTarget!;
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = MathF.Max(80f, ImGui.GetContentRegionAvail().X);
        var height = ImGui.GetTextLineHeightWithSpacing();

        draw.AddRectFilled(
            origin,
            origin + new Vector2(width, height),
            ImGui.GetColorU32(_theme.FrameBg),
            _theme.Radius(0.4f));

        var excerpt = reply.Excerpt.Replace('\n', ' ');
        ImGui.SetCursorScreenPos(origin + new Vector2(_theme.PadX(0.5f), (height - ImGui.GetTextLineHeight()) * 0.5f));
        ImGui.TextColored(_theme.MutedText, $"Replying to {reply.AuthorName}: {excerpt}");

        ImGui.SetCursorScreenPos(new Vector2(origin.X + width - ImGui.GetFrameHeight(), origin.Y));
        if (_theme.IconButton("##chatbox-cancel-reply", FontAwesomeIcon.Times, "Cancel reply")) CancelReply();

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + height + _theme.Gap(0.2f)));
    }

    private void Submit(ChatboxChannelState channel)
    {
        if (string.IsNullOrWhiteSpace(_input)) return;

        Chatbox.Send(channel.Id, _input, _replyTarget);

        _replyTarget = null;
        _input = string.Empty;
        if (Config.KeepFocusAfterSend) _focusInput = true;
        _scrollToBottomFrames = ScrollSettleFrames;
    }

    public void InsertText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (string.IsNullOrEmpty(_input))
        {
            _input = text;
        }
        else if (_input.EndsWith(' '))
        {
            _input += text;
        }
        else
        {
            _input += " " + text;
        }

        _focusInput = true;
    }
}
