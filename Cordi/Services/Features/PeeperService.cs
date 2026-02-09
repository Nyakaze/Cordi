using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Services.Discord;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Lumina.Excel.Sheets;
using Cordi.Core;
using Cordi.Configuration;

namespace Cordi.Services;

public class CordiPeepService : IDisposable
{
    private readonly CordiPlugin plugin;
    public readonly ConcurrentDictionary<ulong, PeeperState> ActivePeepers = new();
    public readonly List<PeeperState> History = new();
    private readonly ConcurrentDictionary<ulong, PeeperState> _messageIdCache = new();
    private readonly TimeSpan _minAlertInterval = TimeSpan.FromSeconds(5);
    private readonly List<PeeperState> _simulatedPeepers = new();

    public void AddSimulatedPeeper(string name, string world)
    {
        var id = (ulong)name.GetHashCode();
        lock (_simulatedPeepers)
        {
            if (!_simulatedPeepers.Any(x => x.GameObjectId == id))
            {
                _simulatedPeepers.Add(new PeeperState
                {
                    GameObjectId = id,
                    Name = name,
                    World = world,
                    AvatarUrl = ""
                });
            }
        }
    }

    public void RemoveSimulatedPeeper(string name)
    {
        var id = (ulong)name.GetHashCode();
        lock (_simulatedPeepers)
        {
            _simulatedPeepers.RemoveAll(x => x.GameObjectId == id);
        }
    }

    public class PeeperState
    {
        public ulong GameObjectId;
        public string Name = string.Empty;
        public string World = string.Empty;
        public DateTime StartTime;
        public DateTime LastSeen;
        public DateTime? EndTime;
        public ulong DiscordMessageId;
        public bool IsLooking;
        public string AvatarUrl = string.Empty;
    }

    public CordiPeepService(CordiPlugin plugin)
    {
        this.plugin = plugin;
        Service.Framework.Update += OnFrameworkUpdate;


        if (plugin.Discord != null)
            plugin.Discord.OnReactionAdded += OnDiscordReactionAdded;
    }


    private readonly HashSet<ulong> currentPeepers = new();

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!plugin.Config.CordiPeep.Enabled) return;

        if (plugin.CordiPeepWindow == null) return;

        bool windowOpen = plugin.CordiPeepWindow.IsOpen;
        bool detectClosed = plugin.Config.CordiPeep.DetectWhenClosed;
        if (!windowOpen && !detectClosed) return;


        var localPlayer = Service.ObjectTable.LocalPlayer;
        if (localPlayer == null) return;


        currentPeepers.Clear();

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IPlayerCharacter player) continue;


            if (player.GameObjectId == localPlayer.GameObjectId)
            {
                if (!plugin.Config.CordiPeep.IncludeSelf) continue;


                var target = Service.TargetManager.Target;
                if (target != null && target.GameObjectId == localPlayer.GameObjectId)
                {
                    currentPeepers.Add(player.GameObjectId);
                    UpdatePeeperState(player.GameObjectId, player.Name.ToString(), player.HomeWorld.Value.Name.ToString());
                }
                continue;
            }


            bool inCombat = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) != 0;
            if (!plugin.Config.CordiPeep.LogCombat && inCombat) continue;


            bool isParty = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.PartyMember) != 0;
            bool isAlliance = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.AllianceMember) != 0;

            if (!plugin.Config.CordiPeep.LogParty && isParty) continue;
            if (!plugin.Config.CordiPeep.LogAlliance && isAlliance) continue;


            if (player.TargetObjectId == localPlayer.GameObjectId)
            {
                currentPeepers.Add(player.GameObjectId);
                UpdatePeeperState(player.GameObjectId, player.Name.ToString(), player.HomeWorld.Value.Name.ToString());
            }
        }


        lock (_simulatedPeepers)
        {
            foreach (var sim in _simulatedPeepers)
            {
                currentPeepers.Add(sim.GameObjectId);
                UpdatePeeperState(sim.GameObjectId, sim.Name, sim.World);
            }
        }


        foreach (var id in ActivePeepers.Keys)
        {
            if (!currentPeepers.Contains(id))
            {
                if (ActivePeepers.TryGetValue(id, out var state) && state.IsLooking)
                {
                    UpdatePeeperStateLoss(state);
                }
            }
        }


        if (configChanged && (DateTime.Now - _lastSaveTime).TotalSeconds > 10)
        {
            plugin.Config.Save();
            configChanged = false;
            _lastSaveTime = DateTime.Now;
        }
    }

    private bool configChanged = false;
    private DateTime _lastSaveTime = DateTime.MinValue;

    private void UpdatePeeperState(ulong id, string name, string world)
    {
        var now = DateTime.Now;

        if (ActivePeepers.TryGetValue(id, out var state))
        {
            state.LastSeen = now;

            if (!state.IsLooking)
            {
                state.IsLooking = true;
                state.StartTime = now;
                state.EndTime = null;
                _ = SendAlert(state, GetLocalPlayerTargetName());
            }
        }
        else
        {
            lock (History)
            {
                History.RemoveAll(x => x.GameObjectId == id || (x.Name == name && x.World == world));
            }

            var newState = new PeeperState
            {
                GameObjectId = id,
                Name = name,
                World = world,
                StartTime = now,
                LastSeen = now,
                IsLooking = true
            };
            ActivePeepers[id] = newState;
            _ = SendAlert(newState, GetLocalPlayerTargetName());

            plugin.Config.Stats.IncrementPeepsTracked();
            plugin.Config.Stats.RecordPeep(name, world);
            configChanged = true;
        }
    }

    private void UpdatePeeperStateLoss(PeeperState state)
    {
        state.IsLooking = false;
        state.EndTime = DateTime.Now;

        lock (History)
        {
            History.Insert(0, state);
            if (History.Count > 50) History.RemoveAt(History.Count - 1);
        }


        _ = UpdateAlertStopped(state, GetLocalPlayerTargetName());

        ActivePeepers.TryRemove(state.GameObjectId, out _);
    }

    private async Task SendAlert(PeeperState state, string myTargetName)
    {
        var blacklistEntry = plugin.Config.CordiPeep.Blacklist.FirstOrDefault(x => x.Name == state.Name && x.World == state.World);

        if (plugin.Config.CordiPeep.SoundEnabled && (blacklistEntry == null || !blacklistEntry.DisableSound))
        {
            PlaySound();
        }

        if (!plugin.Config.CordiPeep.DiscordEnabled) return;
        if (blacklistEntry?.DisableDiscord == true) return;

        var channelIdStr = plugin.Config.CordiPeep.DiscordChannelId;
        if (ulong.TryParse(channelIdStr, out var channelId))
        {
            var avatarUrl = await plugin.Lodestone.GetAvatarUrlAsync(state.Name, state.World);
            state.AvatarUrl = avatarUrl;

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Peeper Detected!")
                .WithDescription($"**{state.Name}@{state.World}** is looking at you!")
                .WithColor(DiscordColor.Red)
                .WithThumbnail(avatarUrl)
                .AddField("Your Target", myTargetName, true)
                .WithFooter($"Started looking at {state.StartTime:HH:mm:ss}")
                .Build();

            if (state.DiscordMessageId == 0)
            {
                state.DiscordMessageId = await plugin.Discord.SendWebhookMessage(channelId, embed, state.Name, state.World);
                if (state.DiscordMessageId != 0)
                {
                    _messageIdCache[state.DiscordMessageId] = state;
                    configChanged = true;
                }
            }
            else
            {
                _messageIdCache[state.DiscordMessageId] = state;

                if (_messageIdCache.Count > 100)
                {
                    var oldest = _messageIdCache.Keys.OrderBy(x => x).Take(10);
                    foreach (var key in oldest) _messageIdCache.TryRemove(key, out _);
                }
            }

            if (state.DiscordMessageId != 0)
            {
                await plugin.Discord.AddReaction(channelId, state.DiscordMessageId, DiscordEmoji.FromUnicode("👀"));

                if (!state.IsLooking)
                {
                    string finalTargetName = "None";
                    await Service.Framework.RunOnFrameworkThread(() =>
                    {
                        finalTargetName = GetLocalPlayerTargetName();
                    });

                    await UpdateAlertStopped(state, finalTargetName);
                }
            }
        }
    }

    private async Task UpdateAlertStopped(PeeperState state, string myTargetName)
    {
        if (state.DiscordMessageId == 0) return;
        var channelIdStr = plugin.Config.CordiPeep.DiscordChannelId;
        if (ulong.TryParse(channelIdStr, out var channelId))
        {
            var duration = (state.EndTime ?? DateTime.Now) - state.StartTime;
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Peeper Left!")
                .WithDescription($"**{state.Name}@{state.World}** was looking at you.")
                .WithColor(DiscordColor.Green)
                .WithThumbnail(state.AvatarUrl)
                .AddField("Your Target", myTargetName, true)
                .WithFooter($"Looked away at {DateTime.Now:HH:mm:ss} (Duration: {duration.TotalSeconds:F1}s)")
                .Build();

            await plugin.Discord.EditWebhookMessage(channelId, state.DiscordMessageId, embed);
        }
    }

    private DateTime _lastSoundPlayTime = DateTime.MinValue;

    public IEnumerable<NAudio.Wave.DirectSoundDeviceInfo> GetOutputDevices()
    {
        return NAudio.Wave.DirectSoundOut.Devices;
    }

    private void PlaySound()
    {
        if (!plugin.Config.CordiPeep.SoundEnabled) return;
        if ((DateTime.Now - _lastSoundPlayTime) < _minAlertInterval) return;

        bool windowOpen = plugin.CordiPeepWindow != null && plugin.CordiPeepWindow.IsOpen;

        var path = plugin.Config.CordiPeep.SoundPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.Join(Service.PluginInterface.AssemblyLocation.Directory!.FullName, "target.wav");
        }

        if (!File.Exists(path)) return;

        try
        {
            float volume = plugin.Config.CordiPeep.SoundVolume;
            var deviceId = plugin.Config.CordiPeep.SoundDevice;

            Task.Run(() =>
            {
                try
                {
                    using var audioFile = new NAudio.Wave.AudioFileReader(path);
                    audioFile.Volume = volume;

                    using var outputDevice = new NAudio.Wave.DirectSoundOut(deviceId);
                    outputDevice.Init(audioFile);
                    outputDevice.Play();

                    while (outputDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    Service.Log.Error(ex, "Error playing sound via NAudio");
                }
            });
            _lastSoundPlayTime = DateTime.Now;
        }
        catch (Exception ex) { Service.Log.Error(ex, "Failed to initiate sound playback"); }
    }

    private Task OnDiscordReactionAdded(MessageReactionAddEventArgs e)
    {
        if (e.User.IsBot) return Task.CompletedTask;

        if (e.User.IsBot) return Task.CompletedTask;

        bool isEyes = e.Emoji.Name == "👀" ||
                      e.Emoji.Name == "\ud83d\udc40" ||
                      e.Emoji.GetDiscordName() == ":eyes:";

        if (!isEyes)
        {
            Service.Log.Debug($"[CordiPeep] Ignored reaction '{e.Emoji.Name}' ({e.Emoji.GetDiscordName()}) on msg {e.Message.Id}");
            return Task.CompletedTask;
        }

        Service.Log.Info($"[CordiPeep] \ud83d\udc40 Processing reaction target for MsgID {e.Message.Id}...");

        PeeperState peeper = null;
        if (_messageIdCache.TryGetValue(e.Message.Id, out var cachedPeeper))
        {
            peeper = cachedPeeper;
        }
        else
        {
            peeper = ActivePeepers.Values.FirstOrDefault(p => p.DiscordMessageId == e.Message.Id);
        }

        if (peeper == null)
        {
            Service.Log.Warning($"[CordiPeep] \u274c FAILED: No peeper state found for MsgID {e.Message.Id}. Cache size: {_messageIdCache.Count}");
            return Task.CompletedTask;
        }

        Service.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                var target = Service.ObjectTable.SearchById(peeper.GameObjectId);

                if (target == null)
                {
                    target = Service.ObjectTable.FirstOrDefault(x =>
                        x is IPlayerCharacter pc &&
                        pc.Name.ToString() == peeper.Name &&
                        pc.HomeWorld.Value.Name.ToString() == peeper.World);
                }

                if (target != null)
                {
                    Service.TargetManager.Target = target;
                    Service.Log.Info($"[CordiPeep] \u2705 TARGETED: {target.Name} (ID: {target.GameObjectId:X})");
                }
                else
                {
                    Service.Log.Warning($"[CordiPeep] \u26a0\ufe0f Could not find entity {peeper.Name}@{peeper.World} in object table.");
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "[CordiPeep] Error while attempting to target peeper.");
            }
        });

        return Task.CompletedTask;
    }

    private string GetLocalPlayerTargetName()
    {
        var target = Service.TargetManager.Target;
        if (target == null) return "None";

        if (target is IPlayerCharacter pc)
        {
            return $"{pc.Name}@{pc.HomeWorld.Value.Name}";
        }
        return target.Name.ToString();
    }

    public async Task<bool> TargetPlayer(string name, string? world = null)
    {
        var tcs = new TaskCompletionSource<bool>();

        await Service.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                IGameObject? target = null;

                if (string.IsNullOrEmpty(world))
                {
                    target = Service.ObjectTable.FirstOrDefault(x =>
                        x is IPlayerCharacter pc &&
                        pc.Name.ToString() == name);
                }
                else
                {
                    target = Service.ObjectTable.FirstOrDefault(x =>
                        x is IPlayerCharacter pc &&
                        pc.Name.ToString() == name &&
                        pc.HomeWorld.Value.Name.ToString() == world);
                }

                if (target != null)
                {
                    Service.TargetManager.Target = target;
                    Service.Log.Info($"[CordiPeep] TARGETED: {target.Name}");
                    tcs.SetResult(true);
                }
                else
                {
                    Service.Log.Warning($"[CordiPeep] Could not find {name}{(string.IsNullOrEmpty(world) ? "" : "@" + world)}");
                    tcs.SetResult(false);
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "[CordiPeep] Error while targeting player.");
                tcs.SetResult(false);
            }
        });

        return await tcs.Task;
    }

    public void Dispose()
    {
        if (configChanged)
        {
            plugin.Config.Save();
        }
        Service.Framework.Update -= OnFrameworkUpdate;
        if (plugin.Discord != null)
            plugin.Discord.OnReactionAdded -= OnDiscordReactionAdded;
    }

}
