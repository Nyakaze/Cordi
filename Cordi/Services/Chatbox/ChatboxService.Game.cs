using System;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private const int VkReturn = 0x0D;

    private static readonly string[] GameChatAddons =
    {
        "ChatLog",
        "ChatLogPanel_0",
        "ChatLogPanel_1",
        "ChatLogPanel_2",
        "ChatLogPanel_3",
    };

    private bool _gameChatHidden;
    private bool _enterHeld;

    public bool InputActive { get; set; }

    public bool RequestInputFocus { get; set; }

    public void OnFrameworkUpdate()
    {
        if (_disposed) return;

        if (!_plugin.ChatboxWindow.IsOpen) InputActive = false;

        UpdateGameChatVisibility();
        UpdateEnterCapture();
        SyncFromGameChatInput();
    }

    private unsafe void SyncFromGameChatInput()
    {
        if (!Config.Enabled || !Service.ClientState.IsLoggedIn) return;

        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("ChatLog").Address;
        if (addon == null || !addon->IsReady) return;

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text) continue;

            var textNode = (AtkTextNode*)node;
            var text = textNode->NodeText.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;

            // When game links an item, it contains the item link character '' or brackets '['
            if (text.Contains('') || text.StartsWith('['))
            {
                _plugin.ChatboxWindow?.InsertText(text);
                textNode->SetText(string.Empty);
                if (_plugin.ChatboxWindow != null && !_plugin.ChatboxWindow.IsOpen)
                    _plugin.ChatboxWindow.IsOpen = true;
                break;
            }
        }
    }

    private void UpdateGameChatVisibility()
    {
        var hide = Config.Enabled && Config.HideGameChat && Service.ClientState.IsLoggedIn;
        if (!hide && !_gameChatHidden) return;

        SetGameChatVisible(!hide);
        _gameChatHidden = hide;
    }

    private static unsafe void SetGameChatVisible(bool visible)
    {
        foreach (var name in GameChatAddons)
        {
            var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName(name).Address;
            if (addon == null || !addon->IsReady) continue;
            if (addon->IsVisible == visible) continue;

            addon->IsVisible = visible;
        }
    }

    private void UpdateEnterCapture()
    {
        if (!Config.Enabled || !Service.ClientState.IsLoggedIn)
        {
            _enterHeld = false;
            return;
        }

        if (!Service.KeyState[VkReturn])
        {
            _enterHeld = false;
            return;
        }

        if (_enterHeld) return;
        _enterHeld = true;

        if (!_plugin.ChatboxWindow.IsOpen) return;
        if (InputActive) return;
        if (IsGameTextInputActive()) return;

        RequestInputFocus = true;
        Service.KeyState[VkReturn] = false;
    }

    private static unsafe bool IsGameTextInputActive()
    {
        var module = RaptureAtkModule.Instance();
        return module != null && module->IsTextInputActive();
    }

    private void RestoreGameChat()
    {
        if (!_gameChatHidden) return;
        _gameChatHidden = false;

        try { SetGameChatVisible(true); }
        catch (Exception ex) { Service.Log.Debug($"[Chatbox] Could not restore the game chat: {ex.Message}"); }
    }
}
