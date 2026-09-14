using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private const uint ChatLogActivateValueCount = 0x31;
    private const uint ChatLogActivatePlain = 0x05;
    private const uint ChatLogActivateWithText = 0x0C;

    private Hook<AddonChatLog.Delegates.OnRefresh>? _chatLogRefreshHook;
    private bool _chatLogHookAttempted;

    public bool ChatLogHookActive => _chatLogRefreshHook is { IsEnabled: true };

    private unsafe void EnsureChatLogHook()
    {
        if (_chatLogHookAttempted || _disposed) return;

        var addon = (AddonChatLog*)Service.GameGui.GetAddonByName("ChatLog").Address;
        if (addon == null || addon->VirtualTable == null) return;

        _chatLogHookAttempted = true;

        try
        {
            _chatLogRefreshHook = Service.GameInteropProvider.HookFromAddress<AddonChatLog.Delegates.OnRefresh>(
                (nint)addon->VirtualTable->OnRefresh,
                OnChatLogRefresh);

            _chatLogRefreshHook.Enable();

            _plugin.LogService.Debug("Chatbox", "Hooked the game chat activation");
        }
        catch (Exception ex)
        {
            _plugin.LogService.Log(
                "Chatbox",
                CordiLogLevel.Warning,
                "Could not hook the game chat activation, falling back to keybind polling",
                ex);
        }
    }

    private unsafe bool OnChatLogRefresh(AddonChatLog* addon, uint valueCount, AtkValue* values)
    {
        try
        {
            if (TryActivateFromGameChat(valueCount, values)) return true;
        }
        catch (Exception ex)
        {
            _plugin.LogService.Log("Chatbox", CordiLogLevel.Warning, "Game chat activation handler failed", ex);
        }

        return _chatLogRefreshHook!.Original(addon, valueCount, values);
    }

    private unsafe bool TryActivateFromGameChat(uint valueCount, AtkValue* values)
    {
        if (_disposed || !Config.Enabled) return false;
        if (valueCount != ChatLogActivateValueCount || values == null) return false;
        if (values->UInt is not (ChatLogActivatePlain or ChatLogActivateWithText)) return false;
        if (_plugin.ChatboxWindow?.IsOpen != true) return false;

        RevealDuringCinematic();

        var prefill = ReadActivationText(values + 2) ?? (CommandKeybindDown ? "/" : null);

        _plugin.LogService.Debug("Chatbox", $"Game chat activation {values->UInt:X2} prefill '{prefill}'");

        RequestInputFocus = true;
        PendingInputText = prefill;

        return true;
    }

    private static unsafe string? ReadActivationText(AtkValue* value)
    {
        if (value == null) return null;
        if ((value->Type & AtkValueType.TypeMask) != AtkValueType.String) return null;
        if (!value->String.HasValue) return null;

        var text = value->String.ToString();

        return text.Length > 0 ? text : null;
    }

    private void DisposeChatLogHook()
    {
        _chatLogRefreshHook?.Dispose();
        _chatLogRefreshHook = null;
    }
}
