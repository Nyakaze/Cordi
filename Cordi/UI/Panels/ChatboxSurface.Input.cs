using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Cordi.Configuration;
using Cordi.Domain;
using Cordi.Services.Chatbox;
using Cordi.UI.Components;
using Cordi.UI.Panels;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface
{
    private const uint InputFocusSoundEffect = 35;

    private static readonly Dictionary<XivChatType, string> SendTypeKeys = new();

    private readonly List<DropdownItem> _sendTargets = new();

    private ulong _sendTargetMask;
    private int _sendTargetAllowed = -1;
    private string _sendTargetLabel = string.Empty;
    private string _sendTargetTooltip = string.Empty;
    private bool _sendTargetPinned;
    private bool _sendTargetsBuilt;
    private string _inputHint = string.Empty;
    private string _inputHintName = string.Empty;
    private string _tellTargetId = string.Empty;
    private string _tellRecipient = string.Empty;
    private string _tellTooltip = string.Empty;

    private static string SendTypeKey(XivChatType type)
    {
        if (SendTypeKeys.TryGetValue(type, out var key)) return key;

        key = type.ToString();
        SendTypeKeys[type] = key;
        return key;
    }

    private string InputHint(string channelName)
    {
        if (_inputHintName.Equals(channelName, StringComparison.Ordinal)) return _inputHint;

        _inputHintName = channelName;
        _inputHint = "Message " + channelName;

        return _inputHint;
    }

    private void DrawInputBar(ChatboxChannelState channel)
    {
        var startY = ImGui.GetCursorPosY();
        DrawInputBarContent(channel);
        _measuredInputHeight = MathF.Max(0f, ImGui.GetCursorPosY() - startY);
    }

    private void DrawInputBarContent(ChatboxChannelState channel)
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
        var translateVisible = OutgoingButtonVisible;

        var cinematicButton = !Detached && ChatboxService.CinematicActive;

        var reserved = 0f;
        if (translateVisible) reserved += spacing + IconButtonWidth(FontAwesomeIcon.Language);
        if (Config.ShowEmojiPicker) reserved += spacing + IconButtonWidth(FontAwesomeIcon.Smile);
        if (cinematicButton) reserved += spacing + IconButtonWidth(FontAwesomeIcon.EyeSlash);

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + _theme.PickerCaptionHeight());

        var escapeConsumed = false;

        if (_autocomplete.IsOpen && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _autocomplete.Dismiss();
            escapeConsumed = true;
        }

        DrawSendTargetPicker(channel, buttonWidth);
        ImGui.SameLine(0, spacing);

        ImGui.SetNextItemWidth(MathF.Max(80f, ImGui.GetContentRegionAvail().X - reserved));

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
            InputHint(channel.Config.Name),
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
        DrawOutgoingMarker(inputMin, inputMax);

        if (submitted && _autocomplete.HasMatches)
        {
            var accepted = _autocomplete.Accept();

            if (accepted?.Kind == ChatboxSuggestionKind.Command)
            {
                CompleteInput(accepted);
            }
            else
            {
                QueueCompletion(accepted);
                submitted = false;
            }
        }

        if (translateVisible) DrawOutgoingButton(spacing);

        if (Config.ShowEmojiPicker)
        {
            ImGui.SameLine(0, spacing);
            if (_theme.IconButton("##chatbox-emoji", FontAwesomeIcon.Smile, "Emoji")) _picker.Open();

            _picker.Draw(InsertText);
            pickerOpen = _picker.InputActive;
        }

        if (cinematicButton)
        {
            ImGui.SameLine(0, spacing);

            var hidden = _theme.IconButton(
                "##chatbox-cinematic-hide",
                FontAwesomeIcon.EyeSlash,
                "Hide the chatbox again. Your chat keybind brings it back.");

            if (hidden) Chatbox.HideDuringCinematic();
        }

        Chatbox.InputActive = inputActive || pickerOpen;

        if (inputActive && !pickerOpen && !escapeConsumed && _replyTarget != null && ImGui.IsKeyPressed(ImGuiKey.Escape))
            CancelReply();

        if (!inputActive && !_inputWasActive && _pendingToken == null) _autocomplete.Reset();

        if (inputActive && !_inputWasActive) OnInputFocused();

        _inputWasActive = inputActive;

        if (submitted) Submit(channel);
    }

    private static float IconButtonWidth(FontAwesomeIcon icon)
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);

        return ImGui.CalcTextSize(icon.ToIconString()).X + ImGui.GetStyle().FramePadding.X * 2f;
    }

    private void DrawSendTargetPicker(ChatboxChannelState channel, float size)
    {
        if (ChatboxService.IsConversationId(channel.Id))
        {
            DrawConversationTarget(channel, size);
            return;
        }

        var pinned = Chatbox.IsSendTypePinned(channel.Config);
        var active = Chatbox.ResolveSendType(channel.Config);

        var label = active != XivChatType.None
            ? ChatboxService.LabelFor(active)
            : "Pick a channel";

        SyncSendTargets(channel.Config, label, pinned);

        _theme.IconPicker(
            "chatbox-send-target",
            new Vector2(size, size),
            FontAwesomeIcon.CommentDots,
            label,
            MathF.Max(_theme.Scaled(120f), ImGui.GetContentRegionAvail().X * 0.5f),
            _sendTargets,
            SendTypeKey(active),
            key =>
            {
                if (!Enum.TryParse<XivChatType>(key, out var parsed)) return;

                Chatbox.SetSendType(channel.Config, parsed);
            },
            _theme.Scaled(260f),
            !pinned,
            _sendTargetTooltip,
            active == XivChatType.None ? null : Chatbox.ColorFor(channel.Config, active));
    }

    private void SyncSendTargets(ChatboxChannelConfig config, string label, bool pinned)
    {
        var allowed = config.SendGameChatTypes;
        var mask = 0UL;

        for (var i = 0; i < ChatTypes.Sendable.Length && i < 64; i++)
        {
            var type = ChatTypes.Sendable[i];

            if (!ChatboxService.IsSendTargetAvailable(type)) continue;
            if (allowed.Count > 0 && !allowed.Contains(type)) continue;

            mask |= 1UL << i;
        }

        if (_sendTargetsBuilt
            && mask == _sendTargetMask
            && pinned == _sendTargetPinned
            && allowed.Count == _sendTargetAllowed
            && label.Equals(_sendTargetLabel, StringComparison.Ordinal))
            return;

        _sendTargetsBuilt = true;
        _sendTargetMask = mask;
        _sendTargetPinned = pinned;
        _sendTargetAllowed = allowed.Count;
        _sendTargetLabel = label;

        _sendTargets.Clear();

        for (var i = 0; i < ChatTypes.Sendable.Length && i < 64; i++)
        {
            if ((mask & (1UL << i)) == 0) continue;

            var type = ChatTypes.Sendable[i];

            _sendTargets.Add(new DropdownItem
            {
                Key = SendTypeKey(type),
                Label = ChatboxService.LabelFor(type),
                Group = ChatTypes.SendGroup(type),
            });
        }

        _sendTargetTooltip = pinned
            ? $"Sending as {label}\nFixed by this channel's \"Send as\" setting."
            : allowed.Count > 1
                ? $"Sending as {label}\nSwap between this channel's \"Send as\" chat types."
                : $"Sending as {label}\nPick the game chat channel to send in.";
    }

    private void DrawConversationTarget(ChatboxChannelState channel, float size)
    {
        if (!channel.Id.Equals(_tellTargetId, StringComparison.Ordinal))
        {
            _tellTargetId = channel.Id;
            _tellRecipient = Chatbox.ConversationTargetFor(channel);
            _tellTooltip = _tellRecipient.Length == 0
                ? "This conversation has no recipient."
                : "Sending a tell to " + _tellRecipient;
        }

        var recipient = _tellRecipient;
        var label = recipient.Length == 0 ? "No recipient" : recipient;

        _theme.IconPicker(
            "chatbox-send-tell",
            new Vector2(size, size),
            FontAwesomeIcon.Envelope,
            label,
            MathF.Max(_theme.Scaled(120f), ImGui.GetContentRegionAvail().X * 0.5f),
            Array.Empty<DropdownItem>(),
            string.Empty,
            _ => { },
            _theme.Scaled(260f),
            false,
            _tellTooltip,
            Config.Conversations.Color);
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

        var text = _emoteFont.Expand(_input);

        var sendType = ChatboxService.IsConversationId(channel.Id)
            ? XivChatType.TellOutgoing
            : Chatbox.ResolveSendType(channel.Config);

        if (NeedsCommandConfirmation(sendType, text, out var command))
        {
            _pendingCommandChannel = channel.Id;
            _pendingCommandText = text;
            _pendingCommandReply = _replyTarget;
            _pendingCommandName = command;
            _pendingCommandTarget = ChatboxService.LabelFor(sendType);
            return;
        }

        Dispatch(channel.Id, text, _replyTarget);
    }

    private void Dispatch(string channelId, string text, ChatboxReplyRef? reply)
    {
        if (AutoTranslateReady(text)) QueueOutgoingTranslation(channelId, text, reply);
        else Chatbox.Send(channelId, text, reply);

        Chatbox.InputHistory.Add(text);

        ResetHistory();
        _autocomplete.Reset();
        _pendingToken = null;
        _replyTarget = null;
        _input = string.Empty;

        if (Config.KeepFocusAfterSend)
        {
            _focusInput = true;
            _silentFocus = true;
        }

        _scrollToBottomFrames = ScrollSettleFrames;
    }

    private unsafe void OnInputFocused()
    {
        if (_silentFocus)
        {
            _silentFocus = false;
            return;
        }

        if (!Config.PlaySoundOnInputFocus) return;

        UIGlobals.PlaySoundEffect(InputFocusSoundEffect);
    }

    private bool NeedsCommandConfirmation(XivChatType sendType, string text, out string command)
    {
        command = string.Empty;

        if (!Config.WarnOnMissingSlash) return false;
        if (!ChatTypes.IsPublic(sendType)) return false;

        var entry = ChatboxCommandCatalog.FindMissingSlash(text.TrimStart());
        if (entry == null) return false;

        command = entry.Command;
        return true;
    }

    private void DrawCommandGuard()
    {
        var text = _pendingCommandText;
        if (text == null) return;

        if (!_commandGuardOpen)
        {
            _theme.OpenConfirmDialog(CommandGuardPopupId);
            _commandGuardOpen = true;
        }

        var result = _theme.ConfirmDialog(
            CommandGuardPopupId,
            "You really want to send that?",
            $"\"{_pendingCommandName}\" is a plugin command, but the leading slash is missing. This will be sent as plain text to everyone in {_pendingCommandTarget}.",
            "Send anyway",
            "Keep editing");

        if (result == UiConfirmResult.None)
        {
            if (ImGui.IsPopupOpen(CommandGuardPopupId)) return;

            ClearCommandGuard();
            _focusInput = true;
            return;
        }

        if (result == UiConfirmResult.Confirmed)
            Dispatch(_pendingCommandChannel, text, _pendingCommandReply);
        else
            _focusInput = true;

        ClearCommandGuard();
    }

    private void ClearCommandGuard()
    {
        _pendingCommandText = null;
        _pendingCommandReply = null;
        _pendingCommandChannel = string.Empty;
        _pendingCommandName = string.Empty;
        _pendingCommandTarget = string.Empty;
        _commandGuardOpen = false;
    }

    private void DrawAutocomplete()
    {
        if (!_autocomplete.IsOpen || _inputWidth <= 0f) return;

        var bottom = ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y - _theme.Gap(0.3f);
        QueueCompletion(_autocomplete.Draw(_inputMin.X, _inputWidth, bottom));
    }

    private void CompleteInput(ChatboxSuggestion suggestion)
    {
        var bytes = Encoding.UTF8.GetBytes(_input);
        var start = Math.Clamp(_autocomplete.FragmentStart, 0, bytes.Length);
        var length = Math.Clamp(_autocomplete.ReplaceLength, 0, bytes.Length - start);

        _input = Encoding.UTF8.GetString(bytes, 0, start)
                 + SlotFor(suggestion.Token, suggestion.ImageUrl)
                 + Encoding.UTF8.GetString(bytes, start + length, bytes.Length - start - length);

        _autocomplete.Reset();
        _pendingToken = null;
        ResetHistory();
    }

    private void QueueCompletion(ChatboxSuggestion? suggestion)
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
            else
                RecallHistory(data, data.EventKey == ImGuiKey.UpArrow ? 1 : -1);

            return 0;
        }

        if (data.EventFlag == ImGuiInputTextFlags.CallbackCompletion)
        {
            var token = _autocomplete.Accept();
            if (token == null)
            {
                _autocomplete.OpenTranslate(data.BufTextSpan, data.CursorPos);
                return 0;
            }

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

        if (_historyOffset != 0)
        {
            if (data.BufTextSpan.SequenceEqual(_historyBytes))
            {
                if (_autocomplete.IsOpen) _autocomplete.Reset();

                return 0;
            }

            ResetHistory();
        }

        _autocomplete.Update(data.BufTextSpan, data.CursorPos);

        return 0;
    }

    private void RecallHistory(ImGuiInputTextCallbackDataPtr data, int delta)
    {
        var history = Chatbox.InputHistory;
        if (history.Count == 0) return;

        if (_historyOffset == 0)
        {
            if (delta < 0) return;

            _historyDraft = Encoding.UTF8.GetString(data.BufTextSpan);
        }

        var offset = Math.Clamp(_historyOffset + delta, 0, history.Count);
        if (offset == _historyOffset) return;

        string text;
        if (offset == 0) text = _historyDraft;
        else if (!history.TryGet(offset, out text)) return;

        _historyOffset = offset;
        _historyBytes = Encoding.UTF8.GetBytes(text);

        data.DeleteChars(0, data.BufTextLen);
        if (text.Length > 0) data.InsertChars(0, text);

        data.CursorPos = data.BufTextLen;
        data.ClearSelection();

        _autocomplete.Reset();
    }

    private void ResetHistory()
    {
        _historyOffset = 0;
        _historyDraft = string.Empty;
        _historyBytes = Array.Empty<byte>();
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
