using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Cordi.Services.Discord;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Crovus.Models;
using Crovus.Events;
using Dalamud.Bindings.ImGui;
using Cordi.Core;
using Cordi.Core.Caching;
using Cordi.Domain;
using Cordi.Extensions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Cordi.Services.Features;

namespace Cordi.Services;

public class CordiPeepService : IDisposable
{
    private readonly Configuration.Configuration _config;
    private readonly CordiLogService Log;
    private readonly DiscordHandler _discord;
    private readonly DiscordEmbedFactory _embeds;
    private readonly LodestoneService _lodestone;
    private readonly AudioService _audio;
    private readonly Lazy<UI.Windows.CordiPeepWindow> _window;

    private readonly PeeperRegistry _registry = new();
    private readonly Cache<ulong, PeeperState> _messageIdCache;
    private readonly TimeSpan _minAlertInterval = TimeSpan.FromSeconds(5);
    private readonly List<PeeperState> _simulatedPeepers = new();
    private readonly HashSet<ulong> currentPeepers = new();

    private const string LogSource = "Peeper";

    public const double TickIntervalSeconds = 0.25;

    private long _tick;
    private bool configChanged;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private DateTime _lastSoundPlayTime = DateTime.MinValue;

    public CordiPeepService(
        Configuration.Configuration config,
        CordiLogService log,
        ICacheRegistry caches,
        DiscordHandler discord,
        DiscordEmbedFactory embeds,
        LodestoneService lodestone,
        AudioService audio,
        Lazy<UI.Windows.CordiPeepWindow> window)
    {
        _config = config;
        Log = log;
        _discord = discord;
        _embeds = embeds;
        _lodestone = lodestone;
        _audio = audio;
        _window = window;

        _messageIdCache = new Cache<ulong, PeeperState>(
            "peeper.messages", caches, maxSize: 100, ttl: TimeSpan.FromHours(2));

        Service.PluginInterface.UiBuilder.Draw += DrawTargetingDots;
    }

    public PeeperSnapshot Snapshot => _registry.Snapshot;

    public void AddSimulatedPeeper(string name, string world)
    {
        var id = (ulong)name.GetHashCode();

        lock (_simulatedPeepers)
        {
            if (_simulatedPeepers.Any(x => x.GameObjectId == id)) return;

            _simulatedPeepers.Add(new PeeperState(Player.FromNameWorld(name, world, id)));
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

    public unsafe void OnFrameworkUpdate(IFramework framework)
    {
        if (!_config.CordiPeep.Enabled) return;

        var window = _window.Value;
        if (!window.IsOpen && !_config.CordiPeep.DetectWhenClosed) return;

        var localPlayer = Service.ObjectTable.LocalPlayer;
        if (localPlayer == null) return;

        _tick++;
        currentPeepers.Clear();

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IPlayerCharacter player) continue;

            if (player.GameObjectId == localPlayer.GameObjectId)
            {
                if (!_config.CordiPeep.IncludeSelf) continue;

                var target = Service.TargetManager.Target;
                if (target != null && target.GameObjectId == localPlayer.GameObjectId)
                {
                    currentPeepers.Add(player.GameObjectId);
                    UpdatePeeperState(player);
                }

                continue;
            }

            UpdatePeeperData(player, localPlayer);

            bool inCombat = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) != 0;
            if (!_config.CordiPeep.LogCombat && inCombat) continue;

            bool isParty = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.PartyMember) != 0;
            bool isAlliance = (player.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.AllianceMember) != 0;

            if (!_config.CordiPeep.LogParty && isParty) continue;
            if (!_config.CordiPeep.LogAlliance && isAlliance) continue;

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
                UpdatePeeperState(sim.Player);
            }
        }

        foreach (var lost in _registry.TakeLost(currentPeepers))
            UpdatePeeperStateLoss(lost);

        _registry.EndTick(_tick);

        if (configChanged && (DateTime.Now - _lastSaveTime).TotalSeconds > 10)
        {
            _config.Save();
            configChanged = false;
            _lastSaveTime = DateTime.Now;
        }
    }

    private void UpdatePeeperState(IPlayerCharacter player)
    {
        if (_config.CordiPeep.UnhideFromVisibilityEffective)
        {
            VisibilityBridge.UnhidePlayer(player, _config.CordiPeep.UnhideVoidedPlayers, isEmote: false);
        }

        if (_registry.TryGetActive(player.GameObjectId, out var state))
        {
            Refresh(state);
            return;
        }

        Track(Player.FromGameObject(player));
    }

    private void UpdatePeeperState(Player player)
    {
        if (_registry.TryGetActive(player.GameObjectId ?? 0, out var state))
        {
            Refresh(state);
            return;
        }

        Track(player);
    }

    private void Refresh(PeeperState state)
    {
        var now = DateTime.Now;
        state.LastSeen = now;

        if (state.IsLooking) return;

        state.IsLooking = true;
        state.StartTime = now;
        state.EndTime = null;
        state.TargetedBack = false;

        Log.FireAndForget(
            SendAlert(state, GetLocalPlayerTargetName()),
            LogSource,
            $"Failed to send the peeper alert for {state.Player.FullName}");
    }

    private void Track(Player player)
    {
        var now = DateTime.Now;
        var id = player.GameObjectId ?? 0;

        var previous = _registry.TakeFromHistory(id, player.Name, player.World);

        var skipNotifications = false;
        if (previous != null && _config.CordiPeep.SkipRepeatedNotifications)
        {
            var timeSinceLost = now - (previous.EndTime ?? DateTime.MinValue);
            if (timeSinceLost.TotalSeconds < _config.CordiPeep.RepeatedNotificationsCooldown)
            {
                skipNotifications = true;
                Log.Info(LogSource, $"Skipping notifications for {player.FullName} (cooldown: {timeSinceLost.TotalSeconds:F1}s < {_config.CordiPeep.RepeatedNotificationsCooldown}s)");
            }
        }

        var state = new PeeperState(player)
        {
            StartTime = now,
            LastSeen = now,
            IsLooking = true,
            TargetedBack = false,
        };

        if (previous != null && skipNotifications) state.DiscordMessageId = previous.DiscordMessageId;

        _registry.Add(state);
        Log.Info(LogSource, $"Peeper detected: {player.FullName}");

        Log.FireAndForget(
            SendAlert(state, GetLocalPlayerTargetName(), skipNotifications),
            LogSource,
            $"Failed to send the peeper alert for {player.FullName}");

        _config.Stats.IncrementPeepsTracked();
        _config.Stats.RecordPeep(player.Name, player.World);
        configChanged = true;
    }

    private void UpdatePeeperStateLoss(PeeperState state)
    {
        var duration = (DateTime.Now - state.StartTime).TotalSeconds;
        Log.Info(LogSource, $"Peeper left: {state.Player.FullName} (duration: {duration:F1}s)");

        state.IsLooking = false;
        state.EndTime = DateTime.Now;

        _registry.Retire(state);

        Log.FireAndForget(
            UpdateAlertStopped(state, GetLocalPlayerTargetName()),
            LogSource,
            $"Failed to update the peeper alert for {state.Player.FullName}");
    }

    private DiscordEmbed BuildPeeperEmbed(PeeperState state, string myTargetName, string? lodestoneId)
    {
        var nameLink = !string.IsNullOrEmpty(lodestoneId)
            ? $"[{state.Player.FullName}](https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/)"
            : state.Player.FullName;

        var color = state.IsLooking ? 0xE74C3C : 0x2ECC71;
        var title = state.IsLooking ? "Peeper Detected!" : "Peeper Left!";
        var description = state.IsLooking
            ? $"**{nameLink}** is looking at you!"
            : $"**{nameLink}** was looking at you.";

        var footerText = state.IsLooking
            ? $"Started looking at {state.StartTime:HH:mm:ss}"
            : $"Looked away at {DateTime.Now:HH:mm:ss} (Duration: {((state.EndTime ?? DateTime.Now) - state.StartTime).TotalSeconds:F1}s)";

        var embedBuilder = _embeds.CreateEmbedBuilder(
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

        if (!ulong.TryParse(_config.CordiPeep.DiscordChannelId, out var channelId)) return;

        string finalTargetName = "None";
        await Service.Framework.RunOnFrameworkThread(() =>
        {
            finalTargetName = GetLocalPlayerTargetName();
        });

        var lodestoneId = await _lodestone.ResolveLodestoneIdAsync(state.Player);
        var embed = BuildPeeperEmbed(state, finalTargetName, lodestoneId);

        await _discord.EditWebhookMessage(channelId, state.DiscordMessageId, embed);
    }

    private async Task SendAlert(PeeperState state, string myTargetName, bool skipNotifications = false)
    {
        var blacklistEntry = _config.CordiPeep.Blacklist.FirstOrDefault(x => x.Name == state.Name && x.World == state.World);

        if (_config.CordiPeep.SoundEnabled && !skipNotifications && (blacklistEntry == null || !blacklistEntry.DisableSound))
        {
            PlaySound();
        }

        if (!_config.CordiPeep.DiscordEnabled) return;

        if (!ulong.TryParse(_config.CordiPeep.DiscordChannelId, out var channelId)) return;

        if (skipNotifications)
        {
            if (state.DiscordMessageId != 0 && (blacklistEntry == null || !blacklistEntry.DisableDiscord))
            {
                _messageIdCache.Set(state.DiscordMessageId, state);

                var skipLodestoneId = await _lodestone.ResolveLodestoneIdAsync(state.Player);
                var skipEmbed = BuildPeeperEmbed(state, myTargetName, skipLodestoneId);

                await _discord.EditWebhookMessage(channelId, state.DiscordMessageId, skipEmbed);
            }

            return;
        }

        if (_config.CordiPeep.DisableDiscordInCombat &&
            Service.ObjectTable.LocalPlayer != null &&
            (Service.ObjectTable.LocalPlayer.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) != 0)
            return;
        if (_config.CordiPeep.DisableDiscordInPvP && Service.ClientState.IsPvP)
            return;
        if (blacklistEntry?.DisableDiscord == true) return;

        state.AvatarUrl = await _lodestone.GetAvatarUrlAsync(state.Player);

        var lodestoneId = await _lodestone.ResolveLodestoneIdAsync(state.Player);
        var embed = BuildPeeperEmbed(state, myTargetName, lodestoneId);

        if (state.DiscordMessageId == 0)
        {
            state.DiscordMessageId = await _discord.SendWebhookMessage(channelId, embed, state.Player);
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

        if (state.DiscordMessageId == 0) return;

        await _discord.AddReaction(channelId, state.DiscordMessageId, "👀");

        if (state.IsLooking) return;

        string finalTargetName = "None";
        await Service.Framework.RunOnFrameworkThread(() =>
        {
            finalTargetName = GetLocalPlayerTargetName();
        });

        await UpdateAlertStopped(state, finalTargetName);
    }

    private async Task UpdateAlertStopped(PeeperState state, string myTargetName)
    {
        if (state.DiscordMessageId == 0) return;
        if (!ulong.TryParse(_config.CordiPeep.DiscordChannelId, out var channelId)) return;

        var lodestoneId = await _lodestone.ResolveLodestoneIdAsync(state.Player);
        var embed = BuildPeeperEmbed(state, myTargetName, lodestoneId);

        await _discord.EditWebhookMessage(channelId, state.DiscordMessageId, embed);
    }

    private void PlaySound()
    {
        if (!_config.CordiPeep.SoundEnabled) return;

        if (_config.CordiPeep.DisableSoundInCombat &&
            Service.ObjectTable.LocalPlayer != null &&
            (Service.ObjectTable.LocalPlayer.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) != 0)
            return;
        if (_config.CordiPeep.DisableSoundInPvP && Service.ClientState.IsPvP)
            return;
        if ((DateTime.Now - _lastSoundPlayTime) < _minAlertInterval) return;

        _audio.Play(_config.CordiPeep.SoundPath, _config.CordiPeep.SoundVolume);
        _lastSoundPlayTime = DateTime.Now;
    }

    public Task OnDiscordReactionAdded(ReactionAddedEvent e)
    {
        if (e.Emoji.Name != "👀")
        {
            Log.Debug(LogSource, $"Ignored reaction '{e.Emoji.Name}' on msg {e.MessageId}");
            return Task.CompletedTask;
        }

        var peeper = _messageIdCache.TryGet(e.MessageId, out var cachedPeeper)
            ? cachedPeeper
            : _registry.FindByMessageId(e.MessageId);

        if (peeper == null)
        {
            Log.Warning(LogSource, $"No peeper state found for message {e.MessageId} (cache size: {_messageIdCache.Count})");
            return Task.CompletedTask;
        }

        return Service.Framework.RunOnFrameworkThread(() =>
        {
            var target = peeper.Live
                         ?? Service.ObjectTable.SearchById(peeper.GameObjectId)
                         ?? Service.ObjectTable.FindPlayerByName(peeper.Name, peeper.World);

            if (target == null)
            {
                Log.Warning(LogSource, $"Target not found in object table: {peeper.Player.FullName}");
                return;
            }

            if (target is IPlayerCharacter pc)
            {
                VisibilityBridge.UnhidePlayer(pc, allowVoided: true, isEmote: false);
            }

            Service.TargetManager.Target = target;
            Log.Info(LogSource, $"Targeted via reaction: {target.Name} (ID: {target.GameObjectId:X})");

            Log.FireAndForget(
                UpdateEmbedTargetedBack(peeper),
                LogSource,
                $"Failed to update the peeper embed for {peeper.Player.FullName}");
        });
    }

    private unsafe void UpdatePeeperData(IPlayerCharacter player, IPlayerCharacter localPlayer)
    {
        var state = _registry.FindTracked(
            player.GameObjectId,
            player.Name.TextValue,
            player.HomeWorld.Value.Name.ExtractText());

        if (state == null) return;

        state.SeenTick = _tick;
        state.Resolved = player;

        state.Distance = Vector3.Distance(localPlayer.Position, player.Position);

        var dx = player.Position.X - localPlayer.Position.X;
        var dz = player.Position.Z - localPlayer.Position.Z;
        var camRot = GetCameraRotation();
        var worldAngle = MathF.Atan2(dx, dz);
        var relative = worldAngle - (camRot + MathF.PI);
        while (relative > MathF.PI) relative -= 2 * MathF.PI;
        while (relative < -MathF.PI) relative += 2 * MathF.PI;
        state.DirectionAngle = -relative;

        var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address;
        var targetId = character != null ? (ulong)character->TargetId : player.TargetObjectId;

        if (targetId != 0)
        {
            state.CurrentTargetId = targetId;
            state.CurrentTargetName = Service.ObjectTable.SearchById(targetId)?.Name.TextValue;
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

    public Task<bool> TargetPlayer(string name, string? world = null)
        => Service.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                var target = Service.ObjectTable.FindPlayerByName(name, world);

                if (target == null)
                {
                    Log.Warning(LogSource, $"Could not find {name}{(string.IsNullOrEmpty(world) ? "" : "@" + world)}");
                    return false;
                }

                VisibilityBridge.UnhidePlayer(target, allowVoided: true, isEmote: false);
                Service.TargetManager.Target = target;
                Log.Info(LogSource, $"Targeted: {target.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(LogSource, "Error while targeting player", ex);
                return false;
            }
        });

    private unsafe void DrawTargetingDots()
    {
        var config = _config.CordiPeep;
        if (!config.ShowTargetingDot || !config.Enabled) return;

        var active = _registry.Snapshot.Active;
        if (active.Count == 0) return;

        var drawList = ImGui.GetForegroundDrawList();
        var dotColor = ImGui.GetColorU32(config.TargetingDotColor);
        var radius = config.TargetingDotSize;
        var yOffset = config.TargetingDotYOffset;

        foreach (var peeper in active)
        {
            if (peeper.Live is not { } obj) continue;

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
            _config.Save();
        }

        Service.PluginInterface.UiBuilder.Draw -= DrawTargetingDots;
    }
}
