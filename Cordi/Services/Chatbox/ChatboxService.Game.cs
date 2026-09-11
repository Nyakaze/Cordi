using System;
using System.Collections.Generic;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
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

    private static readonly IReadOnlyDictionary<XivChatType, UiConfigOption> GameSoundOptions =
        new Dictionary<XivChatType, UiConfigOption>
        {
            [XivChatType.TellIncoming] = UiConfigOption.IsLogTell,
            [XivChatType.Party] = UiConfigOption.IsLogParty,
            [XivChatType.CrossParty] = UiConfigOption.IsLogParty,
            [XivChatType.Alliance] = UiConfigOption.IsLogAlliance,
            [XivChatType.Ls1] = UiConfigOption.IsLogLs1,
            [XivChatType.Ls2] = UiConfigOption.IsLogLs2,
            [XivChatType.Ls3] = UiConfigOption.IsLogLs3,
            [XivChatType.Ls4] = UiConfigOption.IsLogLs4,
            [XivChatType.Ls5] = UiConfigOption.IsLogLs5,
            [XivChatType.Ls6] = UiConfigOption.IsLogLs6,
            [XivChatType.Ls7] = UiConfigOption.IsLogLs7,
            [XivChatType.Ls8] = UiConfigOption.IsLogLs8,
            [XivChatType.CrossLinkShell1] = UiConfigOption.IsLogCwls,
            [XivChatType.CrossLinkShell2] = UiConfigOption.IsLogCwls2,
            [XivChatType.CrossLinkShell3] = UiConfigOption.IsLogCwls3,
            [XivChatType.CrossLinkShell4] = UiConfigOption.IsLogCwls4,
            [XivChatType.CrossLinkShell5] = UiConfigOption.IsLogCwls5,
            [XivChatType.CrossLinkShell6] = UiConfigOption.IsLogCwls6,
            [XivChatType.CrossLinkShell7] = UiConfigOption.IsLogCwls7,
            [XivChatType.CrossLinkShell8] = UiConfigOption.IsLogCwls8,
            [XivChatType.FreeCompany] = UiConfigOption.IsLogFc,
            [XivChatType.NoviceNetwork] = UiConfigOption.IsLogBeginner,
            [XivChatType.PvPTeam] = UiConfigOption.IsLogPvpTeam,
        };

    private readonly Dictionary<UiConfigOption, bool> _gameSoundOriginals = new();
    private readonly HashSet<UiConfigOption> _gameSoundMuted = new();
    private readonly HashSet<UiConfigOption> _gameSoundDesired = new();
    private readonly HashSet<UiConfigOption> _gameSoundUnmuted = new();

    private const long GameSoundPollMs = 1000;

    private bool _gameSoundLoggedIn;
    private long _gameSoundCheckedAt;
    private bool _gameChatHidden;
    private bool _enterHeld;

    public bool InputActive { get; set; }

    public bool RequestInputFocus { get; set; }

    public bool GameFocused { get; private set; } = true;

    private bool AnyChatboxSurfaceOpen()
    {
        if (_plugin.ChatboxWindow?.IsOpen == true) return true;

        var manager = _plugin.ConversationWindows;
        if (manager == null) return false;

        foreach (var entry in ConversationSettings.Items)
        {
            if (manager.TryGet(entry.Id, out var window) && window.IsOpen) return true;
        }

        return false;
    }

    public void OnFrameworkUpdate()
    {
        if (_disposed) return;

        if (!AnyChatboxSurfaceOpen()) InputActive = false;

        GameFocused = GameWindowAlert.IsForeground();

        UpdateGameChatVisibility();
        UpdateGameSoundMutes();
        ClearGameWindowAlert();
        UpdateEnterCapture();
        SyncFromGameChatInput();
    }

    private void UpdateGameSoundMutes()
    {
        var loggedIn = Service.ClientState.IsLoggedIn;

        if (loggedIn != _gameSoundLoggedIn)
        {
            _gameSoundLoggedIn = loggedIn;

            if (!loggedIn)
            {
                RestoreGameSounds();
                return;
            }

            _gameSoundMuted.Clear();
            _gameSoundOriginals.Clear();
            _gameSoundCheckedAt = 0;
        }

        if (!loggedIn) return;

        var now = Environment.TickCount64;
        if (_gameSoundCheckedAt != 0 && now - _gameSoundCheckedAt < GameSoundPollMs) return;

        _gameSoundCheckedAt = now;

        CollectDesiredGameSoundMutes();

        if (_gameSoundDesired.SetEquals(_gameSoundMuted)) return;

        foreach (var option in GameSoundOptions.Values)
        {
            var mute = _gameSoundDesired.Contains(option);
            if (mute == _gameSoundMuted.Contains(option)) continue;

            if (mute)
            {
                if (MuteGameSound(option)) _gameSoundMuted.Add(option);
                continue;
            }

            UnmuteGameSound(option);
            _gameSoundMuted.Remove(option);
        }
    }

    private void CollectDesiredGameSoundMutes()
    {
        _gameSoundDesired.Clear();
        _gameSoundUnmuted.Clear();

        if (!Config.Enabled) return;

        foreach (var channel in Channels)
        {
            var config = channel.Config;
            if (config.IsSeparator || !config.Enabled) continue;

            var target = config.MuteGameSound ? _gameSoundDesired : _gameSoundUnmuted;

            foreach (var type in config.GameChatTypes)
            {
                if (GameSoundOptions.TryGetValue(type, out var option)) target.Add(option);
            }
        }

        _gameSoundDesired.ExceptWith(_gameSoundUnmuted);
    }

    private bool MuteGameSound(UiConfigOption option)
    {
        try
        {
            if (!Service.GameConfig.TryGet(option, out bool enabled)) return false;
            if (!enabled) return false;

            _gameSoundOriginals[option] = true;
            Service.GameConfig.Set(option, false);
            return true;
        }
        catch (Exception ex)
        {
            LogGameSoundFailure(option, ex);
            return false;
        }
    }

    private void UnmuteGameSound(UiConfigOption option)
    {
        if (!_gameSoundOriginals.Remove(option, out var original)) return;

        try
        {
            Service.GameConfig.Set(option, original);
        }
        catch (Exception ex)
        {
            LogGameSoundFailure(option, ex);
        }
    }

    private void RestoreGameSounds()
    {
        _gameSoundMuted.Clear();

        if (_gameSoundOriginals.Count == 0) return;

        foreach (var (option, original) in new List<KeyValuePair<UiConfigOption, bool>>(_gameSoundOriginals))
        {
            try
            {
                Service.GameConfig.Set(option, original);
            }
            catch (Exception ex)
            {
                LogGameSoundFailure(option, ex);
            }
        }

        _gameSoundOriginals.Clear();
    }

    private void LogGameSoundFailure(UiConfigOption option, Exception ex) =>
        _plugin.LogService.Log(
            "Chatbox",
            CordiLogLevel.Warning,
            $"Could not change the game chat sound setting {option}",
            ex);

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
