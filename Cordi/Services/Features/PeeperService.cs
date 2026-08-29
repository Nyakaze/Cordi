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
using Crovus.Events;
using Lumina.Excel.Sheets;
using Dalamud.Bindings.ImGui;
using Cordi.Core;
using Cordi.Configuration;
using Cordi.Domain;
using Cordi.Domain.Observations;
using Cordi.Domain.Tracking;
using Cordi.Extensions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Cordi.Services.Features;

namespace Cordi.Services;

public class CordiPeepService : IDisposable
{
    private readonly CordiPlugin plugin;
    public readonly ConcurrentDictionary<ulong, PeeperState> ActivePeepers = new();
    public readonly List<PeeperState> History = new();
    private readonly Cordi.Core.Caching.Cache<ulong, PeeperState> _messageIdCache;
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
        public float Distance;
        public float DirectionAngle;
        public string? CurrentTargetName;
        public ulong CurrentTargetId;
        public bool IsPresent;
        public bool TargetedBack;
    }

    private CordiLogService Log => plugin.LogService;
    private const string LogSource = "Peeper";

    public CordiPeepService(CordiPlugin plugin)
    {
        this.plugin = plugin;
        _messageIdCache = new Cordi.Core.Caching.Cache<ulong, PeeperState>(
            "peeper.messages", plugin.CacheRegistry,
            maxSize: 100, ttl: TimeSpan.FromHours(2));
        Service.PluginInterface.UiBuilder.Draw += DrawTargetingDots;
    }


    private readonly HashSet<ulong> currentPeepers = new();

    public const double TickIntervalSeconds = 0.25;

    public unsafe void OnFrameworkUpdate(IFramework framework)
    {
        if (!plugin.Config.CordiPeep.Enabled) return;

        if (plugin.CordiPeepWindow == null) return;

        bool windowOpen = plugin.CordiPeepWindow.IsOpen;
        bool detectClosed = plugin.Config.CordiPeep.DetectWhenClosed;
        if (!windowOpen && !detectClosed) return;


        var localPlayer = Service.ObjectTable.LocalPlayer;
        if (localPlayer == null) return;


        currentPeepers.Clear();

        foreach (var state in ActivePeepers.Values) state.IsPresent = false;
        lock (History)
        {
            foreach (var state in History) state.IsPresent = false;
        }

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
                    UpdatePeeperState(player);
                }
                continue;
            }

            // Update distance, direction, and current target for any tracked peeper (Active or History)
            UpdatePeeperData(player, localPlayer);

            bool inCombat = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) != 0;
            if (!plugin.Config.CordiPeep.LogCombat && inCombat) continue;


            bool isParty = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.PartyMember) != 0;
            bool isAlliance = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.AllianceMember) != 0;

            if (!plugin.Config.CordiPeep.LogParty && isParty) continue;
            if (!plugin.Config.CordiPeep.LogAlliance && isAlliance) continue;


            var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address;
            var targetId = character != null ? (ulong)character->TargetId : player.TargetObjectId;

            if (targetId == localPlayer.GameObjectId)
            {
                currentPeepers.Add(player.GameObjectId);
                UpdatePeeperState(player);
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

        foreach (var state in ActivePeepers.Values)
        {
            if (!state.IsPresent)
            {
                state.CurrentTargetId = 0;
                state.CurrentTargetName = null;
            }
        }
        lock (History)
        {
            foreach (var state in History)
            {
                if (!state.IsPresent)
                {
                    state.CurrentTargetId = 0;
                    state.CurrentTargetName = null;
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

    private void UpdatePeeperState(IPlayerCharacter player)
    {
        if (plugin.Config.CordiPeep.UnhideFromVisibilityEffective)
        {
            VisibilityBridge.UnhidePlayer(player, plugin.Config.CordiPeep.UnhideVoidedPlayers, isEmote: false);
        }

        var id = player.GameObjectId;
        var now = DateTime.Now;

        if (ActivePeepers.TryGetValue(id, out var state))
        {
            state.LastSeen = now;

            if (!state.IsLooking)
            {
                state.IsLooking = true;
                state.StartTime = now;
                state.EndTime = null;
                state.TargetedBack = false;
                _ = SendAlert(state, GetLocalPlayerTargetName());
            }
        }
        else
        {
            UpdatePeeperState(id, player.Name.ToString(), player.HomeWorld.Value.Name.ToString());

            _ = plugin.PlayerObservations.FireAsync(new PlayerObservation(
                Player.FromGameObject(player),
                new ObservationContext(
                    Source: ObservationSource.Peeper,
                    TerritoryId: (uint)Service.ClientState.TerritoryType,
                    Position: player.Position,
                    At: DateTime.UtcNow)));
        }
    }

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
                state.TargetedBack = false;
                _ = SendAlert(state, GetLocalPlayerTargetName());
            }
        }
        else
        {
            PeeperState? existingHistoryEntry = null;
            lock (History)
            {
                existingHistoryEntry = History.FirstOrDefault(x => x.GameObjectId == id || (x.Name == name && x.World == world));
                if (existingHistoryEntry != null)
                {
                    History.Remove(existingHistoryEntry);
                }
            }

            var skipNotifications = false;
            if (existingHistoryEntry != null && plugin.Config.CordiPeep.SkipRepeatedNotifications)
            {
                var timeSinceLost = DateTime.Now - (existingHistoryEntry.EndTime ?? DateTime.MinValue);
                if (timeSinceLost.TotalSeconds < plugin.Config.CordiPeep.RepeatedNotificationsCooldown)
                {
                    skipNotifications = true;
                    Log.Info(LogSource, $"Skipping notifications for {name}@{world} (cooldown: {timeSinceLost.TotalSeconds:F1}s < {plugin.Config.CordiPeep.RepeatedNotificationsCooldown}s)");
                }
            }

            var newState = new PeeperState
            {
                GameObjectId = id,
                Name = name,
                World = world,
                StartTime = now,
                LastSeen = now,
                IsLooking = true,
                TargetedBack = false
            };
            if (existingHistoryEntry != null)
            {
                newState.AvatarUrl = existingHistoryEntry.AvatarUrl;
                if (skipNotifications)
                {
                    newState.DiscordMessageId = existingHistoryEntry.DiscordMessageId;
                }
            }
            ActivePeepers[id] = newState;
            Log.Info(LogSource, $"Peeper detected: {name}@{world}");
            _ = SendAlert(newState, GetLocalPlayerTargetName(), skipNotifications);

            plugin.Config.Stats.IncrementPeepsTracked();
            plugin.Config.Stats.RecordPeep(name, world);
            configChanged = true;
        }
    }

    private void UpdatePeeperStateLoss(PeeperState state)
    {
        var duration = (DateTime.Now - state.StartTime).TotalSeconds;
        Log.Info(LogSource, $"Peeper left: {state.Name}@{state.World} (duration: {duration:F1}s)");
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

    private DiscordEmbed BuildPeeperEmbed(PeeperState state, string myTargetName, string? lodestoneId)
    {
        var nameLink = !string.IsNullOrEmpty(lodestoneId)
            ? $"[{state.Name}@{state.World}](https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/)"
            : $"{state.Name}@{state.World}";

        var color = state.IsLooking ? new DiscordColor(0xE74C3C) : new DiscordColor(0x2ECC71);
        var title = state.IsLooking ? "Peeper Detected!" : "Peeper Left!";
        var description = state.IsLooking
            ? $"**{nameLink}** is looking at you!"
            : $"**{nameLink}** was looking at you.";

        var footerText = state.IsLooking
            ? $"Started looking at {state.StartTime:HH:mm:ss}"
            : $"Looked away at {DateTime.Now:HH:mm:ss} (Duration: {((state.EndTime ?? DateTime.Now) - state.StartTime).TotalSeconds:F1}s)";

        var embedBuilder = plugin.EmbedFactory.CreateEmbedBuilder(
            title,
            description,
            color,
            state.AvatarUrl,
            footerText
        );

        if (state.IsLooking)
        {
            embedBuilder.AddField("Distance", $"{state.Distance:F1}m", true);
        }
        else
        {
            var duration = (state.EndTime ?? DateTime.Now) - state.StartTime;
            embedBuilder.AddField("Duration", $"{duration.TotalSeconds:F1}s", true);
        }
        embedBuilder.AddField("Your Target", myTargetName, true);

        if (state.TargetedBack)
        {
            embedBuilder.AddField("Status", "You targeted them back!", false);
        }

        return embedBuilder.Build();
    }

    private async Task UpdateEmbedTargetedBack(PeeperState state)
    {
        if (state.DiscordMessageId == 0) return;
        state.TargetedBack = true;
        var channelIdStr = plugin.Config.CordiPeep.DiscordChannelId;
        if (ulong.TryParse(channelIdStr, out var channelId))
        {
            string finalTargetName = "None";
            await Service.Framework.RunOnFrameworkThread(() =>
            {
                finalTargetName = GetLocalPlayerTargetName();
            });

            var lodestoneId = await plugin.Lodestone.ResolveLodestoneIdAsync(state.Name, state.World);
            var embed = BuildPeeperEmbed(state, finalTargetName, lodestoneId);

            await plugin.Discord.EditWebhookMessage(channelId, state.DiscordMessageId, embed);
        }
    }

    private async Task SendAlert(PeeperState state, string myTargetName, bool skipNotifications = false)
    {
        var blacklistEntry = plugin.Config.CordiPeep.Blacklist.FirstOrDefault(x => x.Name == state.Name && x.World == state.World);

        if (plugin.Config.CordiPeep.SoundEnabled && !skipNotifications && (blacklistEntry == null || !blacklistEntry.DisableSound))
        {
            PlaySound();
        }

        if (!plugin.Config.CordiPeep.DiscordEnabled) return;

        var channelIdStr = plugin.Config.CordiPeep.DiscordChannelId;
        if (!ulong.TryParse(channelIdStr, out var channelId)) return;

        if (skipNotifications)
        {
            if (state.DiscordMessageId != 0 && (blacklistEntry == null || !blacklistEntry.DisableDiscord))
            {
                _messageIdCache.Set(state.DiscordMessageId, state);
                var skipLodestoneId = await plugin.Lodestone.ResolveLodestoneIdAsync(state.Name, state.World);
                var skipEmbed = BuildPeeperEmbed(state, myTargetName, skipLodestoneId);
                await plugin.Discord.EditWebhookMessage(channelId, state.DiscordMessageId, skipEmbed);
            }
            return;
        }

        if (plugin.Config.CordiPeep.DisableDiscordInCombat &&
            Service.ObjectTable.LocalPlayer != null &&
            (Service.ObjectTable.LocalPlayer.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) != 0)
            return;
        if (plugin.Config.CordiPeep.DisableDiscordInPvP && Service.ClientState.IsPvP)
            return;
        if (blacklistEntry?.DisableDiscord == true) return;

        var avatarUrl = await plugin.Lodestone.GetAvatarUrlAsync(state.Name, state.World);
        state.AvatarUrl = avatarUrl;

        var lodestoneId = await plugin.Lodestone.ResolveLodestoneIdAsync(state.Name, state.World);
        var embed = BuildPeeperEmbed(state, myTargetName, lodestoneId);

        if (state.DiscordMessageId == 0)
        {
            state.DiscordMessageId = await plugin.Discord.SendWebhookMessage(channelId, embed, state.Name, state.World);
            if (state.DiscordMessageId != 0)
            {
                _messageIdCache.Set(state.DiscordMessageId, state);
                configChanged = true;
            }
        }
        else
        {
            _messageIdCache.Set(state.DiscordMessageId, state);
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

    private async Task UpdateAlertStopped(PeeperState state, string myTargetName)
    {
        if (state.DiscordMessageId == 0) return;
        var channelIdStr = plugin.Config.CordiPeep.DiscordChannelId;
        if (ulong.TryParse(channelIdStr, out var channelId))
        {
            var lodestoneId = await plugin.Lodestone.ResolveLodestoneIdAsync(state.Name, state.World);
            var embed = BuildPeeperEmbed(state, myTargetName, lodestoneId);

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

        if (plugin.Config.CordiPeep.DisableSoundInCombat &&
            Service.ObjectTable.LocalPlayer != null &&
            (Service.ObjectTable.LocalPlayer.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) != 0)
            return;
        if (plugin.Config.CordiPeep.DisableSoundInPvP && Service.ClientState.IsPvP)
            return;
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
                    Log.Error(LogSource, "Sound playback error", ex);
                }
            });
            _lastSoundPlayTime = DateTime.Now;
        }
        catch (Exception ex) { Service.Log.Error(ex, "Failed to initiate sound playback"); }
    }

    public Task OnDiscordReactionAdded(ReactionAddedEvent e)
    {
        bool isEyes = e.Emoji.Name == "👀" ||
                      e.Emoji.Name == "\ud83d\udc40";

        if (!isEyes)
        {
            Service.Log.Debug($"[CordiPeep] Ignored reaction '{e.Emoji.Name}' on msg {e.MessageId}");
            return Task.CompletedTask;
        }

        Service.Log.Info($"[CordiPeep] \ud83d\udc40 Processing reaction target for MsgID {e.MessageId}...");

        PeeperState peeper = null;
        if (_messageIdCache.TryGet(e.MessageId, out var cachedPeeper))
        {
            peeper = cachedPeeper;
        }
        else
        {
            peeper = ActivePeepers.Values.FirstOrDefault(p => p.DiscordMessageId == e.MessageId);
        }

        if (peeper == null)
        {
            Service.Log.Warning($"[CordiPeep] \u274c FAILED: No peeper state found for MsgID {e.MessageId}. Cache size: {_messageIdCache.Count}");
            return Task.CompletedTask;
        }

        Service.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                var target = Service.ObjectTable.SearchById(peeper.GameObjectId);

                if (target == null)
                {
                    target = Service.ObjectTable.FindPlayerByName(peeper.Name, peeper.World);
                }

                if (target != null)
                {
                    if (target is IPlayerCharacter pc)
                    {
                        VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                    }
                    Service.TargetManager.Target = target;
                    Log.Info(LogSource, $"Targeted via reaction: {target.Name} (ID: {target.GameObjectId:X})");
                    _ = UpdateEmbedTargetedBack(peeper);
                }
                else
                {
                    Log.Warning(LogSource, $"Target not found in object table: {peeper.Name}@{peeper.World}");
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "[CordiPeep] Error while attempting to target peeper.");
            }
        });

        return Task.CompletedTask;
    }

    private unsafe void UpdatePeeperData(IPlayerCharacter player, IPlayerCharacter localPlayer)
    {
        PeeperState? state = null;
        if (ActivePeepers.TryGetValue(player.GameObjectId, out state))
        {
            // found in active
        }
        else
        {
            lock (History)
            {
                state = History.FirstOrDefault(h =>
                    h.GameObjectId == player.GameObjectId ||
                    (h.Name == player.Name.TextValue && h.World == player.HomeWorld.Value.Name.ExtractText()));
            }
        }

        if (state == null) return;

        state.IsPresent = true;

        // Direction and distance updated for anyone in active or history list currently present
        state.Distance = Vector3.Distance(localPlayer.Position, player.Position);

        var dx = player.Position.X - localPlayer.Position.X;
        var dz = player.Position.Z - localPlayer.Position.Z;
        var camRot = GetCameraRotation();
        var worldAngle = MathF.Atan2(dx, dz);
        var relative = worldAngle - (camRot + MathF.PI);
        while (relative > MathF.PI) relative -= 2 * MathF.PI;
        while (relative < -MathF.PI) relative += 2 * MathF.PI;
        state.DirectionAngle = -relative;

        // Current target of the peeper — always updated
        var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address;
        var targetId = character != null ? (ulong)character->TargetId : player.TargetObjectId;
        if (targetId != 0)
        {
            state.CurrentTargetId = targetId;
            var pTarget = Service.ObjectTable.SearchById(targetId);
            state.CurrentTargetName = pTarget?.Name.TextValue;
        }
        else
        {
            state.CurrentTargetId = 0;
            state.CurrentTargetName = null;
        }
    }

    private static unsafe float GetCameraRotation()
    {
        var cm = CameraManager.Instance();
        if (cm != null && cm->Camera != null)
            return cm->Camera->DirH;
        return 0f;
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

                target = Service.ObjectTable.FindPlayerByName(name, world);

                if (target != null)
                {
                    if (target is IPlayerCharacter pc)
                    {
                        VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
                    }
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

    private unsafe void DrawTargetingDots()
    {
        var config = plugin.Config.CordiPeep;
        if (!config.ShowTargetingDot || !config.Enabled) return;
        if (ActivePeepers.IsEmpty) return;

        var drawList = ImGui.GetForegroundDrawList();
        var dotColor = ImGui.GetColorU32(config.TargetingDotColor);
        var radius = config.TargetingDotSize;
        var yOffset = config.TargetingDotYOffset;

        foreach (var peeper in ActivePeepers.Values)
        {
            var obj = Service.ObjectTable.SearchById(peeper.GameObjectId);
            if (obj == null) continue;

            var worldPos = obj.Position;
            var gameObj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address;
            worldPos.Y += gameObj->NameplateOffset.Y + yOffset;

            if (!Service.GameGui.WorldToScreen(worldPos, out var screenPos)) continue;

            drawList.AddCircleFilled(screenPos, radius, dotColor);
        }
    }

    public void Dispose()
    {
        if (configChanged)
        {
            plugin.Config.Save();
        }
        Service.PluginInterface.UiBuilder.Draw -= DrawTargetingDots;
    }

}
