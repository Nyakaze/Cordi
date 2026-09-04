using System;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private const int VkReturn = 0x0D;
    private const string GameLinkMarker = "\ue0bb";

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

        var window = _plugin.ChatboxWindow;
        if (window == null) return;

        var addon = (AddonChatLog*)Service.GameGui.GetAddonByName("ChatLog").Address;
        if (addon == null || !addon->AtkUnitBase.IsReady) return;

        var input = addon->TextInput;
        if (input == null) return;

        var textNode = input->AtkComponentInputBase.AtkTextNode;
        if (textNode == null) return;

        var text = textNode->NodeText.ToString();
        if (string.IsNullOrWhiteSpace(text) || !text.Contains(GameLinkMarker, StringComparison.Ordinal)) return;

        window.InsertText(text.Trim());
        input->SetText(string.Empty);
        textNode->SetText(string.Empty);

        if (!window.IsOpen) window.IsOpen = true;
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
