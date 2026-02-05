using Cordi;
using Cordi.Services.Discord;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Configuration;

namespace Cordi.Services;

public class EmoteLogEntry
{
    public DateTime Timestamp { get; set; }
    public string User { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public string Emote { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int Count { get; set; } = 1;
    public ulong DiscordMessageId { get; set; }
    public ulong GameObjectId { get; set; }
}

public class EmoteLogService : IDisposable
{
    private readonly CordiPlugin _plugin;
    private readonly IPluginLog _logger;
    private readonly List<EmoteLogEntry> _logs = new();
    public IReadOnlyList<EmoteLogEntry> Logs => _logs;

    private delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> _hookEmote;


    public class DiscordEmoteState
    {
        public ulong MessageId;
        public string User;
        public string World;
        public string EmoteName;
        public string Command;
        public int Count;
        public DateTime LastUpdate;
        public DateTime FirstSeen;
        public ulong GameObjectId;
        public bool EmotedBack;
    }

    private readonly ConcurrentDictionary<ulong, DiscordEmoteState> _messageIdCache = new();
    public ConcurrentDictionary<ulong, DiscordEmoteState> MessageIdCache => _messageIdCache;

    private readonly ConcurrentDictionary<string, DiscordEmoteState> _activeDiscordEmotes = new();
    public ConcurrentDictionary<string, DiscordEmoteState> ActiveDiscordEmotes => _activeDiscordEmotes;
    private readonly TimeSpan _spamThreshold = TimeSpan.FromSeconds(60);

    public EmoteLogService(CordiPlugin plugin)
    {
        _plugin = plugin;
        _logger = Service.Log;


        _hookEmote = Service.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
        _hookEmote.Enable();
    }

    public void Initialize()
    {
        if (_plugin.Discord != null)
        {
            _plugin.Discord.OnReactionAdded += OnDiscordReactionAdded;
        }
    }

    private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            if (_plugin.Config.EmoteLog.Enabled)
            {
                var localPlayer = Service.ObjectTable[0];
                if (localPlayer != null)
                {

                    bool windowOpen = _plugin.EmoteLogWindow.IsOpen;
                    bool detectClosed = _plugin.Config.EmoteLog.DetectWhenClosed;

                    if (!windowOpen && !detectClosed) return;


                    if (targetId == localPlayer.GameObjectId)
                    {

                        var instigator = Service.ObjectTable.FirstOrDefault(x => (ulong)x.Address == instigatorAddr);

                        if (instigator is IPlayerCharacter player)
                        {

                            if (player.GameObjectId != localPlayer.GameObjectId || _plugin.Config.EmoteLog.IncludeSelf)
                            {
                                LogEmote(player, emoteId);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in Emote Detour");
        }

        _hookEmote.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }

    private void LogEmote(IPlayerCharacter player, ushort emoteId)
    {
        string emoteName = $"Emote #{emoteId}";
        string emoteCommand = "";

        try
        {
            var emoteSheet = Service.DataManager.GetExcelSheet<Emote>();
            if (emoteSheet.TryGetRow(emoteId, out var emoteRow))
            {
                var name = emoteRow.Name.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    emoteName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                }

                if (emoteRow.TextCommand.IsValid)
                {
                    var rawCmd = emoteRow.TextCommand.Value.Command.ToString();
                    if (!string.IsNullOrEmpty(rawCmd))
                    {
                        emoteCommand = rawCmd.StartsWith("/") ? rawCmd : "/" + rawCmd;

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            emoteCommand = emoteCommand.TrimStart('/');
                            emoteName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(emoteCommand);
                            emoteCommand = "/" + emoteCommand;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, $"Failed to resolve emote name/command for ID {emoteId}");
        }

        lock (_logs)
        {
            if (_plugin.Config.EmoteLog.CollapseDuplicates)
            {
                var existing = _logs.LastOrDefault(x => x.User == player.Name.ToString() && x.World == player.HomeWorld.Value.Name.ToString() && x.Emote == emoteName);
                if (existing != null)
                {
                    existing.Count++;
                    existing.Timestamp = DateTime.Now;
                    existing.Command = emoteCommand;

                    _logs.Remove(existing);
                    _logs.Add(existing);

                    _logger.Info($"[EmoteLog] Collapsed: {existing.User} used {existing.Emote} [{existing.Count}]");

                    _ = ProcessDiscordEmote(existing.User, existing.World, existing.GameObjectId, emoteName, emoteCommand, existing.Count);
                    return;
                }
            }

            var entry = new EmoteLogEntry
            {
                Timestamp = DateTime.Now,
                User = player.Name.ToString(),
                World = player.HomeWorld.Value.Name.ToString(),
                Emote = emoteName,
                Command = emoteCommand,
                Count = 1,
                DiscordMessageId = 0,
                GameObjectId = player.GameObjectId
            };

            _logs.Add(entry);
            if (_logs.Count > 200) _logs.RemoveAt(0);

            _logger.Info($"[EmoteLog] Logged: {entry.User} used {entry.Emote} (Cmd: {entry.Command})");
            _plugin.Config.Stats.IncrementEmotesTracked();
            _plugin.Config.Stats.RecordEmote(player.Name.ToString(), player.HomeWorld.Value.Name.ToString());

            var blacklistEntry = _plugin.Config.EmoteLog.Blacklist.FirstOrDefault(x => x.Name == player.Name.ToString() && x.World == player.HomeWorld.Value.Name.ToString());
            if (blacklistEntry?.DisableDiscord == true) return;

            _ = ProcessDiscordEmote(player.Name.ToString(), player.HomeWorld.Value.Name.ToString(), player.GameObjectId, emoteName, emoteCommand, 1);
        }
    }

    public void SimulateEmote(string name, string world, string emoteName, string command)
    {
        ulong fakeId = (ulong)name.GetHashCode();
        _ = ProcessDiscordEmote(name, world, fakeId, emoteName, command, 1);
    }

    private async Task ProcessDiscordEmote(string name, string world, ulong gameObjectId, string emoteName, string command, int uiCount)
    {
        if (!_plugin.Config.EmoteLog.DiscordEnabled) return;

        if (string.IsNullOrEmpty(_plugin.Config.EmoteLog.ChannelId)) return;
        if (!ulong.TryParse(_plugin.Config.EmoteLog.ChannelId, out var channelId)) return;
        if (_plugin.Discord?.Client == null) return;

        string key = $"{name}@{world}-{emoteName}";

        bool updateExisting = false;
        DiscordEmoteState state = null;

        if (_activeDiscordEmotes.TryGetValue(key, out state))
        {
            if (DateTime.Now - state.LastUpdate < _spamThreshold)
            {
                updateExisting = true;
                state.Count++;
                state.LastUpdate = DateTime.Now;
            }
            else
            {
                _activeDiscordEmotes.TryRemove(key, out _);
                state = null;
            }
        }

        if (state == null)
        {
            state = new DiscordEmoteState
            {
                User = name,
                World = world,
                EmoteName = emoteName,
                Command = command,
                Count = 1,
                LastUpdate = DateTime.Now,
                FirstSeen = DateTime.Now,
                EmotedBack = false,
                GameObjectId = gameObjectId
            };
            _activeDiscordEmotes[key] = state;
        }

        try
        {
            var avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(name, world);

            string description = $"**{name}@{world}** used **{emoteName}** on you!";
            if (state.Count > 1) description += $" (x{state.Count})";
            if (state.EmotedBack) description += "\n\n✅ *You emoted back!*";

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Emote Detected")
                .WithDescription(description)
                .WithColor(DiscordColor.Blurple)
                .WithThumbnail(avatarUrl)
                .WithFooter(state.EmotedBack ? "Interaction Complete" : "React with 🔙 to emote back")
                .WithTimestamp(DateTime.Now);

            if (updateExisting && state.MessageId != 0)
            {
                await _plugin.Discord.EditWebhookMessage(channelId, state.MessageId, embed.Build());
            }
            else
            {
                state.MessageId = await _plugin.Discord.SendWebhookMessage(channelId, embed.Build(), name, world);

                if (state.MessageId != 0)
                {
                    _messageIdCache[state.MessageId] = state;

                    if (_messageIdCache.Count > 100)
                    {
                        var oldest = _messageIdCache.Keys.OrderBy(x => x).Take(10);
                        foreach (var cacheKey in oldest) _messageIdCache.TryRemove(cacheKey, out _);
                    }

                    await _plugin.Discord.AddReaction(channelId, state.MessageId, DiscordEmoji.FromUnicode("🔙"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process Discord emote log.");
        }
    }

    public async Task PerformEmoteBack(string targetName, string targetWorld, string command, ulong targetId = 0)
    {
        float savedRotation = 0;
        bool targetFound = false;

        await CordiPlugin.Framework.RunOnFrameworkThread(() =>
        {
            var localPlayer = Service.ClientState.LocalPlayer;
            if (localPlayer == null) return;

            savedRotation = localPlayer.Rotation;

            IGameObject target = null;
            if (targetId != 0)
            {
                target = Service.ObjectTable.SearchById(targetId);
            }

            if (target == null)
            {
                target = Service.ObjectTable.FirstOrDefault(x =>
                    x is IPlayerCharacter pc &&
                    pc.Name.ToString() == targetName &&
                    (string.IsNullOrEmpty(targetWorld) || pc.HomeWorld.Value.Name.ToString() == targetWorld));
            }

            if (target != null)
            {
                Service.TargetManager.Target = target;
                _plugin._chat.SendMessage(command);
                targetFound = true;
            }
        });

        if (!targetFound) return;

        await Task.Delay(8000);

        await CordiPlugin.Framework.RunOnFrameworkThread(() =>
        {
            var localPlayer = Service.ClientState.LocalPlayer;
            if (localPlayer == null) return;

            var currentTarget = Service.TargetManager.Target;
            if (currentTarget != null)
            {
                if (currentTarget.Name.ToString() == targetName)
                {
                    Service.TargetManager.Target = null;
                }
            }

            unsafe
            {
                var go = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)localPlayer.Address;
                go->Rotation = savedRotation;
            }
        });
    }

    private async Task OnDiscordReactionAdded(MessageReactionAddEventArgs e)
    {
        if (e.User.IsBot) return;
        if (e.Emoji.Name != "🔙") return;

        if (!_messageIdCache.TryGetValue(e.Message.Id, out var state))
        {
            state = _activeDiscordEmotes.Values.FirstOrDefault(x => x.MessageId == e.Message.Id);
        }

        if (state == null) return;

        if (state.EmotedBack) return;

        state.EmotedBack = true;

        var cmd = state.Command;
        if (string.IsNullOrEmpty(cmd)) cmd = "/" + state.EmoteName.ToLower().Replace(" ", "");

        await PerformEmoteBack(state.User, state.World, cmd, state.GameObjectId);

        if (ulong.TryParse(_plugin.Config.EmoteLog.ChannelId, out var channelId))
        {
            try
            {
                var avatarUrl = await _plugin.Lodestone.GetAvatarUrlAsync(state.User, state.World);
                string description = $"**{state.User}@{state.World}** used **{state.EmoteName}** on you!";
                if (state.Count > 1) description += $" (x{state.Count})";
                description += "\n\n✅ *You emoted back!*";

                var embed = new DiscordEmbedBuilder()
                    .WithTitle("Emote Detected")
                    .WithDescription(description)
                    .WithColor(DiscordColor.Green)
                    .WithThumbnail(avatarUrl)
                    .WithFooter("Interaction Complete")
                    .WithTimestamp(DateTime.Now);

                await _plugin.Discord.EditWebhookMessage(channelId, state.MessageId, embed.Build());
                await _plugin.Discord.RemoveReaction(channelId, state.MessageId, DiscordEmoji.FromUnicode("🔙"));
            }
            catch (Exception ex) { _logger.Error(ex, "Failed to update embed after reaction."); }
        }
    }

    public void Dispose()
    {
        _hookEmote?.Dispose();
        if (_plugin.Discord != null)
        {
            _plugin.Discord.OnReactionAdded -= OnDiscordReactionAdded;
        }
    }
}
