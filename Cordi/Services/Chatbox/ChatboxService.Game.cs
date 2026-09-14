using System;
using System.Collections.Generic;
using Cordi.Core;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
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

    private const long KeybindPollMs = 5000;

    private struct KeybindSlots
    {
        public VirtualKey Key1;
        public byte Modifier1;
        public VirtualKey Key2;
        public byte Modifier2;
    }

    private long _keybindCheckedAt;
    private KeybindSlots _chatKeybind;
    private KeybindSlots _commandKeybind;
    private bool _chatKeyHeld;
    private bool _commandKeyHeld;

    public bool InputActive { get; set; }

    public bool RequestInputFocus { get; set; }

    public string? PendingInputText { get; set; }

    public bool GameFocused { get; private set; } = true;

    private bool? _cinematicOverride;

    public static bool CinematicActive => CordiPlugin.CinematicActive;

    public bool CinematicHidesChatbox
    {
        get
        {
            if (!CordiPlugin.CinematicActive) return false;
            if (_cinematicOverride is { } visible) return !visible;

            return CordiPlugin.GposeActive ? Config.HideInGpose : Config.HideInCutscene;
        }
    }

    public void RevealDuringCinematic()
    {
        if (CordiPlugin.CinematicActive) _cinematicOverride = true;
    }

    public void HideDuringCinematic() => _cinematicOverride = false;

    private void UpdateCinematicState()
    {
        if (!CordiPlugin.CinematicActive) _cinematicOverride = null;
    }

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

        UpdateCinematicState();
        UpdateGameChatVisibility();
        UpdateGameSoundMutes();
        ClearGameWindowAlert();
        EnsureChatLogHook();
        UpdateChatKeybindCapture();
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

    private void UpdateChatKeybindCapture()
    {
        if (!Config.Enabled || !Service.ClientState.IsLoggedIn)
        {
            _chatKeyHeld = false;
            _commandKeyHeld = false;
            _keybindCheckedAt = 0;
            return;
        }

        var now = Environment.TickCount64;

        if (_keybindCheckedAt == 0 || now - _keybindCheckedAt >= KeybindPollMs)
        {
            _keybindCheckedAt = now;
            RefreshChatKeybinds();
        }

        if (ChatLogHookActive) return;

        if (IsGameTextInputActive())
        {
            _chatKeyHeld = true;
            _commandKeyHeld = true;
            return;
        }

        if (TryConsumeKeybind(_commandKeybind, ref _commandKeyHeld, "/")) return;

        TryConsumeKeybind(_chatKeybind, ref _chatKeyHeld, null);
    }

    private bool CommandKeybindDown => TryMatchKeybind(_commandKeybind, out _);

    private bool TryConsumeKeybind(in KeybindSlots bind, ref bool held, string? prefill)
    {
        if (!TryMatchKeybind(bind, out var key))
        {
            held = false;
            return false;
        }

        if (held) return false;
        held = true;

        if (!_plugin.ChatboxWindow.IsOpen) return false;
        if (InputActive) return false;

        RevealDuringCinematic();

        Service.KeyState[key] = false;
        RequestInputFocus = true;
        PendingInputText = prefill;
        return true;
    }

    private static bool TryMatchKeybind(in KeybindSlots bind, out VirtualKey key)
    {
        if (MatchKeyCombo(bind.Key1, bind.Modifier1))
        {
            key = bind.Key1;
            return true;
        }

        if (MatchKeyCombo(bind.Key2, bind.Modifier2))
        {
            key = bind.Key2;
            return true;
        }

        key = VirtualKey.NO_KEY;
        return false;
    }

    private static bool MatchKeyCombo(VirtualKey key, byte modifier)
    {
        if (key == VirtualKey.NO_KEY) return false;
        if (!Service.KeyState.IsVirtualKeyValid(key)) return false;
        if (!Service.KeyState[key]) return false;

        var shift = Service.KeyState[VirtualKey.SHIFT];
        var control = Service.KeyState[VirtualKey.CONTROL];
        var alt = Service.KeyState[VirtualKey.MENU];

        return shift == ((modifier & 1) != 0)
               && control == ((modifier & 2) != 0)
               && alt == ((modifier & 4) != 0);
    }

    private void RefreshChatKeybinds()
    {
        var chat = ReadKeybind(InputId.CMD_CHAT, VirtualKey.RETURN);
        var command = ReadKeybind(InputId.CMD_COMMAND, VirtualKey.NO_KEY);

        if (!SlotsEqual(chat, _chatKeybind) || !SlotsEqual(command, _commandKeybind))
        {
            _plugin.LogService.Debug(
                "Chatbox",
                $"Chat keybinds: focus={DescribeSlots(chat)} command={DescribeSlots(command)}");
        }

        _chatKeybind = chat;
        _commandKeybind = command;
    }

    private static unsafe KeybindSlots ReadKeybind(InputId id, VirtualKey fallback)
    {
        var slots = default(KeybindSlots);
        var input = UIInputData.Instance();

        if (input != null)
        {
            var bind = input->GetKeybind(id);

            if (bind != null)
            {
                var settings = bind->KeySettings;

                if (settings.Length > 0)
                {
                    slots.Key1 = RemapInvalidVirtualKey((VirtualKey)(int)settings[0].Key);
                    slots.Modifier1 = (byte)settings[0].KeyModifier;
                }

                if (settings.Length > 1)
                {
                    slots.Key2 = RemapInvalidVirtualKey((VirtualKey)(int)settings[1].Key);
                    slots.Modifier2 = (byte)settings[1].KeyModifier;
                }
            }
        }

        if (slots.Key1 == VirtualKey.NO_KEY && slots.Key2 == VirtualKey.NO_KEY) slots.Key1 = fallback;

        return slots;
    }

    private static bool SlotsEqual(in KeybindSlots left, in KeybindSlots right) =>
        left.Key1 == right.Key1
        && left.Modifier1 == right.Modifier1
        && left.Key2 == right.Key2
        && left.Modifier2 == right.Modifier2;

    private static string DescribeSlots(in KeybindSlots slots) =>
        $"[{slots.Modifier1}+{slots.Key1}, {slots.Modifier2}+{slots.Key2}]";

    private static VirtualKey RemapInvalidVirtualKey(VirtualKey key) => key switch
    {
        VirtualKey.F23 => VirtualKey.OEM_2,
        (VirtualKey)140 => VirtualKey.OEM_7,
        _ => key,
    };

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
