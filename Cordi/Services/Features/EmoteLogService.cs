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
using Cordi.Services.Features;

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
    private readonly EmoteBackAction _emoteBackAction;
    private readonly EmoteDiscordNotifier _discordNotifier;

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
        _emoteBackAction = new EmoteBackAction(plugin);
        _discordNotifier = new EmoteDiscordNotifier(plugin, _messageIdCache, _activeDiscordEmotes, _spamThreshold, _emoteBackAction);


        _hookEmote = Service.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
        _hookEmote.Enable();
    }

    public void Initialize()
    {
        if (_plugin.Discord != null)
        {
            _plugin.Discord.OnReactionAdded += _discordNotifier.OnDiscordReactionAdded;
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
            var playerName = player.Name.ToString();
            var playerWorld = player.HomeWorld.Value.Name.ToString();

            if (_plugin.Config.EmoteLog.CollapseDuplicates)
            {
                var existing = _logs.LastOrDefault(x => x.User == playerName && x.World == playerWorld && x.Emote == emoteName);
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
                User = playerName,
                World = playerWorld,
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
            _plugin.Config.Stats.RecordEmote(playerName, playerWorld);

            var blacklistEntry = _plugin.Config.EmoteLog.Blacklist.FirstOrDefault(x => x.Name == playerName && x.World == playerWorld);
            if (blacklistEntry?.DisableDiscord == true) return;

            _ = ProcessDiscordEmote(playerName, playerWorld, player.GameObjectId, emoteName, emoteCommand, 1);
        }
    }

    public void SimulateEmote(string name, string world, string emoteName, string command)
    {
        ulong fakeId = (ulong)name.GetHashCode();
        _ = ProcessDiscordEmote(name, world, fakeId, emoteName, command, 1);
    }

    private async Task ProcessDiscordEmote(string name, string world, ulong gameObjectId, string emoteName, string command, int uiCount)
    {
        await _discordNotifier.ProcessDiscordEmote(name, world, gameObjectId, emoteName, command, uiCount);
    }

    public async Task PerformEmoteBack(string targetName, string targetWorld, string command, ulong targetId = 0, bool keepTarget = false, bool keepRotation = false)
    {
        await _emoteBackAction.PerformAsync(targetName, targetWorld, command, targetId, keepTarget, keepRotation);
    }




    public void Dispose()
    {
        _hookEmote?.Dispose();
        if (_plugin.Discord != null)
        {
            _plugin.Discord.OnReactionAdded -= _discordNotifier.OnDiscordReactionAdded;
        }
    }
}
