using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
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
        var buttons = Config.ShowEmojiPicker ? 1 : 0;

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + _theme.PickerCaptionHeight());

        var escapeConsumed = false;

        if (_autocomplete.IsOpen && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _autocomplete.Dismiss();
            escapeConsumed = true;
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
            _clearSelection = true;
        }

        using var emoteFont = _emoteFont.Push();

        _theme.PushInputScope();
        var submitted = ImGui.InputTextWithHint(
            "##chatbox-input",
            $"Message {channel.Config.Name}",
            ref _input,
            ChatboxConfig.InputBufferLength,
            ImGuiInputTextFlags.EnterReturnsTrue
            | ImGuiInputTextFlags.CallbackAlways
            | ImGuiInputTextFlags.CallbackCompletion
            | ImGuiInputTextFlags.CallbackHistory,
            _inputCallback);
        _theme.PopInputScope();

        var inputMin = ImGui.GetItemRectMin();
        var inputMax = ImGui.GetItemRectMax();
        var inputActive = ImGui.IsItemActive();
        var pickerOpen = false;

        _inputMin = inputMin;
        _inputWidth = ImGui.GetItemRectSize().X;

        DrawInlineEmotes(inputMin, inputMax);

        if (submitted && _autocomplete.IsOpen)
        {
            QueueCompletion(_autocomplete.Accept());
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

        if (inputActive && !pickerOpen && !escapeConsumed && _replyTarget != null && ImGui.IsKeyPressed(ImGuiKey.Escape))
            CancelReply();

        if (!inputActive && !_inputWasActive && _pendingToken == null) _autocomplete.Reset();

        _inputWasActive = inputActive;

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
            tooltip,
            active == XivChatType.None ? null : Chatbox.ColorFor(channel.Config, active));
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

        Chatbox.Send(channel.Id, _emoteFont.Expand(_input), _replyTarget);

        _autocomplete.Reset();
        _pendingToken = null;
        _replyTarget = null;
        _input = string.Empty;
        if (Config.KeepFocusAfterSend) _focusInput = true;
        _scrollToBottomFrames = ScrollSettleFrames;
    }

    private void DrawAutocomplete()
    {
        if (!_autocomplete.IsOpen || _inputWidth <= 0f) return;

        var bottom = ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y - _theme.Gap(0.3f);
        QueueCompletion(_autocomplete.Draw(_inputMin.X, _inputWidth, bottom));
    }

    private void QueueCompletion(EmojiSuggestion? suggestion)
    {
        if (suggestion == null) return;

        _pendingToken = SlotFor(suggestion.Token, suggestion.ImageUrl);
        _pendingStart = _autocomplete.FragmentStart;
        _pendingLength = _autocomplete.ReplaceLength;
        _autocomplete.Reset();
        _focusInput = true;
    }

    private void DrawInlineEmotes(Vector2 inputMin, Vector2 inputMax)
    {
        if (_input.Length == 0) return;

        var slots = 0;
        foreach (var c in _input)
            if (ChatboxEmoteFont.IsSlot(c)) slots++;

        if (slots == 0) return;

        var state = ImGuiP.GetInputTextState(ImGui.GetID("##chatbox-input"));
        var scrollX = state.IsNull ? 0f : state.ScrollX;

        var padding = ImGui.GetStyle().FramePadding;
        var glyph = ImGui.GetTextLineHeight();
        var origin = new Vector2(inputMin.X + padding.X - scrollX, inputMin.Y + padding.Y);

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(inputMin, inputMax, true);

        for (var i = 0; i < _input.Length; i++)
        {
            if (!_emoteFont.TryResolve(_input[i], out _, out var url)) continue;
            if (string.IsNullOrEmpty(url)) continue;

            var offset = i == 0 ? 0f : ImGui.CalcTextSize(_input[..i]).X;
            var min = new Vector2(origin.X + offset, origin.Y);
            if (min.X > inputMax.X || min.X + glyph < inputMin.X) continue;

            Chatbox.ImageCache.Request(url);
            var texture = Chatbox.ImageCache.Get(url);
            if (texture == null) continue;

            var max = min + new Vector2(glyph, glyph);
            AnimatedTextureWrap.MarkVisible(texture, min, max);
            draw.AddImage(texture.Handle, min, max);
        }

        draw.PopClipRect();
    }

    private string SlotFor(string token, string? url)
    {
        if (string.IsNullOrEmpty(url) || !_emoteFont.Available) return token;

        return _emoteFont.Reserve(token, url).ToString();
    }

    private unsafe int InputCallback(ImGuiInputTextCallbackDataPtr data)
    {
        if (_pendingToken != null)
        {
            ApplyPending(data);
            return 0;
        }

        if (data.EventFlag == ImGuiInputTextFlags.CallbackHistory)
        {
            if (_autocomplete.IsOpen)
                _autocomplete.MoveSelection(data.EventKey == ImGuiKey.UpArrow ? -1 : 1);

            return 0;
        }

        if (data.EventFlag == ImGuiInputTextFlags.CallbackCompletion)
        {
            var token = _autocomplete.Accept();
            if (token == null) return 0;

            _pendingToken = SlotFor(token.Token, token.ImageUrl);
            _pendingStart = _autocomplete.FragmentStart;
            _pendingLength = _autocomplete.ReplaceLength;
            _autocomplete.Reset();
            ApplyPending(data);

            return 0;
        }

        if (_clearSelection)
        {
            _clearSelection = false;
            data.CursorPos = data.BufTextLen;
            data.ClearSelection();
        }

        _autocomplete.Update(data.BufTextSpan, data.CursorPos);

        return 0;
    }

    private unsafe void ApplyPending(ImGuiInputTextCallbackDataPtr data)
    {
        var token = _pendingToken!;
        _pendingToken = null;
        _clearSelection = false;

        var start = Math.Clamp(_pendingStart, 0, data.BufTextLen);
        var length = Math.Clamp(_pendingLength, 0, data.BufTextLen - start);
        var text = token + " ";

        data.DeleteChars(start, length);
        data.InsertChars(start, text);
        data.CursorPos = start + Encoding.UTF8.GetByteCount(text);
        data.ClearSelection();

        _autocomplete.Reset();
    }

    public void InsertText(string token, string? url = null)
    {
        if (string.IsNullOrEmpty(token)) return;

        var text = SlotFor(token, url);

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
