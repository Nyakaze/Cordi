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
            _theme.MutedLabel("This channel is read-only.");
            return;
        }

        var spacing = _theme.Gap(0.4f);
        var buttonWidth = ImGui.GetFrameHeight();
        var buttons = Config.ShowEmojiPicker ? 2 : 1;

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + _theme.PickerCaptionHeight());

        var escapeConsumed = false;

        if (_autocomplete.IsOpen)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) _autocomplete.MoveSelection(1);
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) _autocomplete.MoveSelection(-1);

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                _autocomplete.Dismiss();
                escapeConsumed = true;
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.Tab))
            {
                ApplyCompletion(_autocomplete.Accept());
            }
        }

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

        var inputMin = ImGui.GetItemRectMin();
        var inputWidth = ImGui.GetItemRectSize().X;
        var inputActive = ImGui.IsItemActive();
        var pickerOpen = false;

        _autocomplete.Update(_input);

        if (submitted && _autocomplete.IsOpen)
        {
            ApplyCompletion(_autocomplete.Accept());
            submitted = false;
        }

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

        if (inputActive && !pickerOpen && !escapeConsumed && _replyTarget != null && ImGui.IsKeyPressed(ImGuiKey.Escape))
            CancelReply();

        var picked = _autocomplete.Draw(inputMin, inputWidth);
        if (picked != null) ApplyCompletion(picked);

        if (submitted) Submit(channel);
    }

    private void DrawSendTargetPicker(ChatboxChannelState channel, float size)
    {
        var pinned = Chatbox.IsSendTypePinned(channel.Config);
        var active = Chatbox.ResolveSendType(channel.Config);

        var label = active != XivChatType.None
            ? ChatboxService.LabelFor(active)
            : "Pick a channel";

        var items = new List<DropdownItem>();
        foreach (var type in ChatboxService.SendableChatTypes)
        {
            if (!ChatboxService.IsSendTargetAvailable(type)) continue;
            items.Add(new DropdownItem
            {
                Key = type.ToString(),
                Label = ChatboxService.LabelFor(type),
                Group = ChatboxService.SendGroupFor(type),
            });
        }

        var tooltip = pinned
            ? $"Sending as {label}\nFixed by this channel's \"Send as\" setting."
            : $"Sending as {label}\nPick the game chat channel to send in.";

        _theme.IconPicker(
            "chatbox-send-target",
            new Vector2(size, size),
            FontAwesomeIcon.CommentDots,
            label,
            MathF.Max(_theme.Scaled(120f), ImGui.GetContentRegionAvail().X * 0.5f),
            items,
            active.ToString(),
            key =>
            {
                if (!Enum.TryParse<XivChatType>(key, out var parsed)) return;

                Config.LastSendChatType = parsed;
                _plugin.Config.Save();
            },
            _theme.Scaled(260f),
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

        _autocomplete.Reset();
        _replyTarget = null;
        _input = string.Empty;
        if (Config.KeepFocusAfterSend) _focusInput = true;
        _scrollToBottomFrames = ScrollSettleFrames;
    }

    private void ApplyCompletion(string? token)
    {
        if (string.IsNullOrEmpty(token)) return;

        var caret = Math.Clamp(_autocomplete.Caret, 0, _input.Length);
        var start = caret - _autocomplete.ReplaceLength;
        if (start < 0) return;

        _input = _input[..start] + token + " " + _input[caret..];
        _autocomplete.Sync(_input, start + token.Length + 1);
        _focusInput = true;
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
