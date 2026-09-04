using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private readonly List<ChatboxMessage> _awaitingSource = new();
    private Hook<RaptureLogModule.Delegates.AddMsgSourceEntry>? _sourceHook;

    private unsafe void InitializeSourceHook()
    {
        try
        {
            _sourceHook = Service.GameInteropProvider.HookFromAddress<RaptureLogModule.Delegates.AddMsgSourceEntry>(
                RaptureLogModule.MemberFunctionPointers.AddMsgSourceEntry,
                OnAddMsgSourceEntry);

            _sourceHook.Enable();
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("Chatbox", "Failed to hook chat message source resolver", ex);
            _sourceHook = null;
        }
    }

    private void DisposeSourceHook()
    {
        _sourceHook?.Dispose();
        _sourceHook = null;
        _awaitingSource.Clear();
    }

    private void BeginSourceCapture() => _awaitingSource.Clear();

    private void AwaitSource(ChatboxMessage message) => _awaitingSource.Add(message);

    private unsafe void OnAddMsgSourceEntry(
        RaptureLogModule* module, ulong contentId, ulong accountId, int messageIndex, ushort worldId, ushort chatType)
    {
        try
        {
            _sourceHook!.Original(module, contentId, accountId, messageIndex, worldId, chatType);
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("Chatbox", "Chat message source resolver threw", ex);
            return;
        }

        if (_awaitingSource.Count == 0) return;

        foreach (var message in _awaitingSource)
        {
            message.SenderContentId = contentId;
            message.SenderAccountId = accountId;
            message.SenderWorldId = worldId;
        }

        _awaitingSource.Clear();
    }
}
