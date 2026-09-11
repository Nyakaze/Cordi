using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    public const string ConversationIdPrefix = "dm:";
    public const uint MinConversationSound = 1;
    public const uint MaxConversationSound = 16;

    private readonly Dictionary<string, ChatboxChannelConfig> _conversationConfigs = new(StringComparer.Ordinal);

    private static readonly TimeSpan TellFailureWindow = TimeSpan.FromSeconds(10);

    private DateTime _lastConversationSound = DateTime.MinValue;
    private bool _taskbarFlashing;

    private string _recentTellChannelId = string.Empty;
    private string _recentTellName = string.Empty;
    private DateTime _recentTellStamp = DateTime.MinValue;

    private ConversationSettings ConversationSettings => Config.Conversations;

    public static bool IsConversationId(string? id) =>
        !string.IsNullOrEmpty(id) && id.StartsWith(ConversationIdPrefix, StringComparison.Ordinal);

    public static string ConversationId(string name, string world) =>
        ConversationIdPrefix + NormalizeKeyPart(name) + "@" + NormalizeKeyPart(world);

    private static string NormalizeKeyPart(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public bool ConversationsEnabled => ConversationSettings.Enabled;

    public bool ConversationsInOwnWindow => ConversationSettings.OpenInOwnWindow;

    public IReadOnlyList<ConversationConfig> AllConversations => ConversationSettings.Items;

    public IReadOnlyList<ChatboxChannelState> OpenConversations()
    {
        var result = new List<ChatboxChannelState>();

        foreach (var entry in OrderedConversations())
        {
            if (ConversationSettings.OpenInOwnWindow) continue;

            var state = GetChannel(entry.Id);
            if (state != null) result.Add(state);
        }

        return result;
    }

    private IEnumerable<ConversationConfig> OrderedConversations() =>
        ConversationSettings.Items
            .Where(c => c.Open)
            .OrderByDescending(c => c.Pinned)
            .ThenByDescending(c => c.LastActivityTicks)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

    public ConversationConfig? FindConversation(string id) =>
        ConversationSettings.Items.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));

    public ChatboxChannelState? OpenConversation(string name, string world, bool activate)
    {
        name = name?.Trim() ?? string.Empty;
        world = world?.Trim() ?? string.Empty;

        if (name.Length == 0) return null;

        var id = ConversationId(name, world);
        var entry = FindConversation(id);

        if (entry == null)
        {
            entry = new ConversationConfig { Id = id, Name = name, World = world };
            ConversationSettings.Items.Add(entry);
        }
        else
        {
            entry.Name = name;
            if (world.Length > 0) entry.World = world;
        }

        entry.Open = true;
        entry.LastActivityTicks = DateTime.UtcNow.Ticks;

        var state = EnsureConversationChannel(entry);
        MaybeImportXivimHistory(entry);
        _plugin.Config.Save();
        _plugin.ConversationWindows?.Sync();

        if (!activate || state == null) return state;

        if (IsConversationDetached(state.Id)) _plugin.ConversationWindows?.Focus(state.Id);
        else SetActiveChannel(state.Id);

        return state;
    }

    public ChatboxChannelState? OpenConversationFor(string name, string world)
    {
        if (!Config.Enabled || !ConversationSettings.Enabled) return null;

        var state = OpenConversation(name, world, activate: true);
        if (state == null) return null;

        if (IsConversationDetached(state.Id)) return state;

        var window = _plugin.ChatboxWindow;
        if (window != null) window.IsOpen = true;

        RequestInputFocus = true;
        return state;
    }

    public bool SuppressesGameLog(XivChatType type, string name, string world)
    {
        var settings = ConversationSettings;

        if (!Config.Enabled || !settings.Enabled || !settings.SuppressGameLog) return false;
        if (settings.TellRouting == ConversationTellRouting.ChannelsOnly) return false;
        if (type != XivChatType.TellIncoming && type != XivChatType.TellOutgoing) return false;
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (string.Equals(name, ChatboxMessage.SystemSender, StringComparison.Ordinal)) return false;

        if (GetChannel(ConversationId(name, world)) != null) return true;

        return type == XivChatType.TellIncoming ? settings.AutoOpenIncoming : settings.AutoOpenOutgoing;
    }

    public void CloseConversation(string id)
    {
        var entry = FindConversation(id);
        if (entry == null) return;

        entry.Open = false;

        var state = GetChannel(id);
        if (state != null) PersistState(state);

        _channelLock.EnterWriteLock();
        try
        {
            _channels.Remove(id);
        }
        finally
        {
            _channelLock.ExitWriteLock();
        }

        _conversationConfigs.Remove(id);
        _plugin.Config.Save();
        _plugin.ConversationWindows?.Sync();
        SetChannelViewed(id, false);

        if (!string.Equals(ActiveChannelId, id, StringComparison.Ordinal)) return;

        var fallback = Channels.FirstOrDefault(c => c.Config.Enabled && !IsConversationId(c.Id));
        SetActiveChannel(fallback?.Id ?? string.Empty);
    }

    public void RemoveConversation(string id)
    {
        CloseConversation(id);

        var entry = FindConversation(id);
        if (entry == null) return;

        ConversationSettings.Items.Remove(entry);
        _plugin.Config.Save();
        _plugin.ConversationWindows?.Sync();
    }

    private void TouchConversation(string id)
    {
        var entry = FindConversation(id);
        if (entry == null) return;

        entry.LastActivityTicks = DateTime.UtcNow.Ticks;
    }

    private void RestoreConversations()
    {
        var settings = ConversationSettings;

        if (!settings.ReopenOnLogin)
        {
            foreach (var entry in settings.Items)
                entry.Open = false;

            return;
        }

        if (!settings.Enabled) return;

        foreach (var entry in settings.Items.Where(e => e.Open).ToList())
            EnsureConversationChannel(entry);

        _plugin.ConversationWindows?.Sync();
    }

    public void RefreshConversationChannels()
    {
        foreach (var entry in ConversationSettings.Items.Where(e => e.Open).ToList())
        {
            if (GetChannel(entry.Id) == null) continue;

            BuildConversationChannel(entry);
        }

        _plugin.ConversationWindows?.Sync();
    }

    private ChatboxChannelState? EnsureConversationChannel(ConversationConfig entry)
    {
        var config = BuildConversationChannel(entry);
        ChatboxChannelState? created = null;

        _channelLock.EnterWriteLock();
        try
        {
            if (_channels.TryGetValue(entry.Id, out var existing))
            {
                existing.Config = config;
            }
            else
            {
                created = new ChatboxChannelState(config);
                _channels[entry.Id] = created;
            }
        }
        finally
        {
            _channelLock.ExitWriteLock();
        }

        if (created == null) return GetChannel(entry.Id);

        Hydrate(created);
        return created;
    }

    private ChatboxChannelConfig BuildConversationChannel(ConversationConfig entry)
    {
        if (!_conversationConfigs.TryGetValue(entry.Id, out var config))
        {
            config = new ChatboxChannelConfig
            {
                Id = entry.Id,
                GameChatTypes = new List<XivChatType> { XivChatType.TellIncoming, XivChatType.TellOutgoing },
            };

            _conversationConfigs[entry.Id] = config;
        }

        var settings = ConversationSettings;

        config.Name = settings.ShowWorldInLabel ? entry.Label : entry.Name;
        config.ShortLabel = ConversationInitials(entry.Name);
        config.Color = settings.Color;
        config.OverrideChatColor = false;
        config.Enabled = true;
        config.ShowInNav = true;
        config.IsSeparator = false;
        config.Order = int.MaxValue;
        config.SendGameChatType = XivChatType.None;
        config.MuteNotifications = settings.MuteNotifications;
        config.MuteGameSound = settings.MuteGameSound;
        config.TreatAllAsMention = settings.TreatAllAsMention;
        config.MaxMessages = 0;
        config.PersistHistory = true;
        config.FilterAdvertisements = false;

        return config;
    }

    private static string ConversationInitials(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return "@";

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";

        return trimmed.Length >= 2 ? trimmed[..2].ToUpperInvariant() : trimmed.ToUpperInvariant();
    }

    public string ConversationTargetFor(ChatboxChannelState channel)
    {
        var entry = FindConversation(channel.Id);
        if (entry == null) return string.Empty;

        return string.IsNullOrEmpty(entry.World) ? entry.Name : $"{entry.Name}@{entry.World}";
    }

    private ChatboxChannelState? ResolveConversationTarget(XivChatType type, string name, string world)
    {
        var settings = ConversationSettings;

        if (!settings.Enabled || settings.TellRouting == ConversationTellRouting.ChannelsOnly) return null;
        if (type != XivChatType.TellIncoming && type != XivChatType.TellOutgoing) return null;
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (string.Equals(name, ChatboxMessage.SystemSender, StringComparison.Ordinal)) return null;

        var id = ConversationId(name, world);
        var existing = GetChannel(id);

        if (existing != null)
        {
            TouchConversation(id);
            return existing;
        }

        var autoOpen = type == XivChatType.TellIncoming
            ? settings.AutoOpenIncoming
            : settings.AutoOpenOutgoing;

        return autoOpen ? OpenConversation(name, world, activate: false) : null;
    }

    private void RememberTellTarget(string channelId, string name)
    {
        _recentTellChannelId = channelId;
        _recentTellName = name;
        _recentTellStamp = DateTime.UtcNow;
    }

    private void HandleTellFailure(string text)
    {
        var channelId = _recentTellChannelId;
        var name = _recentTellName;
        var stamp = _recentTellStamp;

        _recentTellChannelId = string.Empty;
        _recentTellName = string.Empty;
        _recentTellStamp = DateTime.MinValue;

        if (channelId.Length == 0) return;
        if (DateTime.UtcNow - stamp > TellFailureWindow) return;
        if (!IsTellFailure(text, name)) return;

        PostConversationSystemMessage(channelId, text);
    }

    private static bool IsTellFailure(string text, string name)
    {
        if (text.Length == 0) return false;

        if (text.StartsWith("Unable to send /tell.", StringComparison.Ordinal)) return true;

        if (string.Equals(
                text,
                "Your message was not heard. You must wait before using /tell, /say, /yell, or /shout again.",
                StringComparison.Ordinal))
            return true;

        const string prefix = "Message to ";
        const string suffix = " could not be sent.";

        if (name.Length == 0) return false;
        if (!text.StartsWith(prefix, StringComparison.Ordinal)) return false;
        if (!text.EndsWith(suffix, StringComparison.Ordinal)) return false;

        return text[prefix.Length..^suffix.Length]
            .Trim()
            .StartsWith(name, StringComparison.OrdinalIgnoreCase);
    }

    private void PostConversationSystemMessage(string channelId, string text)
    {
        var target = GetChannel(channelId);
        if (target == null) return;

        var entry = new ChatboxMessage
        {
            ChannelId = target.Id,
            Origin = ChatboxOrigin.System,
            AuthorName = ChatboxMessage.SystemSender,
            RawContent = text,
            Segments = new[] { ContentSegment.PlainText(text) },
        };

        Publish(target, entry, notify: false, announceConversation: false);
    }

    private void NotifyConversation(ChatboxChannelState channel, ChatboxMessage entry, bool isActive)
    {
        var settings = ConversationSettings;
        if (!settings.Enabled) return;

        var outgoing = entry.GameChatType == XivChatType.TellOutgoing;

        if (!outgoing && !isActive && !settings.MuteNotifications)
        {
            PlayConversationSound();
            AlertGameWindow();
        }

        var autoOpen = outgoing ? settings.AutoOpenOutgoing : settings.AutoOpenIncoming;
        if (!autoOpen) return;

        ShowConversation(channel, outgoing);
    }

    private void AlertGameWindow()
    {
        var settings = ConversationSettings;

        if (!settings.FlashTaskbar && !settings.FocusGameWindow) return;
        if (GameWindowAlert.IsForeground()) return;

        if (settings.FlashTaskbar) _taskbarFlashing = true;

        _ = Service.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                if (settings.FlashTaskbar) GameWindowAlert.Flash();
                if (settings.FocusGameWindow) GameWindowAlert.BringToFront();
            }
            catch (Exception ex)
            {
                _plugin.LogService.Error("Chatbox", "Failed to alert the game window.", ex);
            }
        });
    }

    private void ClearGameWindowAlert()
    {
        if (!_taskbarFlashing) return;
        if (!GameFocused) return;

        _taskbarFlashing = false;

        try
        {
            GameWindowAlert.StopFlashing();
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("Chatbox", "Failed to stop the taskbar flash.", ex);
        }
    }

    private void ShowConversation(ChatboxChannelState channel, bool outgoing)
    {
        if (IsConversationDetached(channel.Id))
        {
            var manager = _plugin.ConversationWindows;
            if (manager == null) return;

            manager.Sync();

            if (!manager.TryGet(channel.Id, out var detached)) return;

            detached.IsOpen = true;

            if (outgoing && ConversationSettings.AutoFocusOutgoing) detached.Activate();

            return;
        }

        var window = _plugin.ChatboxWindow;
        if (window == null) return;

        window.IsOpen = true;

        if (!outgoing) return;

        SetActiveChannel(channel.Id);

        if (ConversationSettings.AutoFocusOutgoing) RequestInputFocus = true;
    }

    public bool IsConversationDetached(string id) =>
        FindConversation(id) != null && ConversationSettings.OpenInOwnWindow;

    private void PlayConversationSound()
    {
        var settings = ConversationSettings;
        if (!settings.PlaySound) return;

        var now = DateTime.UtcNow;
        if (now - _lastConversationSound < TimeSpan.FromSeconds(1)) return;

        _lastConversationSound = now;

        if (settings.UseCustomSound)
        {
            _plugin.Audio.Play(settings.CustomSoundPath, settings.CustomSoundVolume);
            return;
        }

        PlayConversationGameSound(settings.SoundEffect);
    }

    public void PreviewConversationSound()
    {
        _lastConversationSound = DateTime.MinValue;

        if (ConversationSettings.UseCustomSound)
        {
            PlayConversationSound();
            return;
        }

        Service.Framework.RunOnFrameworkThread(PlayConversationSound);
    }

    private unsafe void PlayConversationGameSound(uint effect)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect(Math.Clamp(effect, MinConversationSound, MaxConversationSound));
        }
        catch (Exception ex)
        {
            _plugin.LogService.Error("Chatbox", "Failed to play the conversation sound.", ex);
        }
    }
}
