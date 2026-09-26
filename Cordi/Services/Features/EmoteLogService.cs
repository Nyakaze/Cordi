using Cordi.Domain;
using Cordi.Services.Activity;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cordi.Services.Features;
using Cordi.UI.Windows;

using Cordi.Services;

namespace Cordi.Services.Features;

public class EmoteLogService : IDisposable
{
    private const string LogSource = "EmoteLog";

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService _log;
    private readonly EmoteBackAction _emoteBackAction;
    private readonly EmoteDiscordNotifier _discordNotifier;
    private readonly Lazy<EmoteLogWindow> _window;
    private readonly Lazy<CombinedWindow> _combined;

    private readonly List<EmoteEvent> _logs = new();

    private IReadOnlyList<EmoteEvent> _snapshot = Array.Empty<EmoteEvent>();

    private delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> _hookEmote;

    public event Action<ulong, ushort, ulong>? EmoteObserved;

    public EmoteLogService(
        Configuration.Configuration config,
        CordiLogService log,
        EmoteBackAction emoteBackAction,
        EmoteDiscordNotifier discordNotifier,
        Lazy<EmoteLogWindow> window,
        Lazy<CombinedWindow> combined)
    {
        _config = config;
        _log = log;
        _emoteBackAction = emoteBackAction;
        _discordNotifier = discordNotifier;
        _window = window;
        _combined = combined;

        _hookEmote = Service.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
        _hookEmote.Enable();
    }

    public EmoteDiscordNotifier DiscordNotifier => _discordNotifier;

    public IReadOnlyList<EmoteEvent> Logs => Volatile.Read(ref _snapshot);

    private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            EmoteObserved?.Invoke(instigatorAddr, emoteId, targetId);
        }
        catch (Exception ex)
        {
            _log.Error(LogSource, "Emote observer error", ex);
        }

        try
        {
            HandleEmote(instigatorAddr, emoteId, targetId);
        }
        catch (Exception ex)
        {
            _log.Error(LogSource, "Emote hook error", ex);
        }

        _hookEmote.Original(unk, instigatorAddr, emoteId, targetId, unk2);
    }

    private bool IsSurfaceOpen()
        => _window.Value.IsOpen || (_config.CombinedWindow.Enabled && _combined.Value.IsOpen);

    private void HandleEmote(ulong instigatorAddr, ushort emoteId, ulong targetId)
    {
        if (!_config.EmoteLog.Enabled) return;
        if (!IsSurfaceOpen() && !_config.EmoteLog.DetectWhenClosed) return;

        var localPlayer = Service.ObjectTable.LocalPlayer;
        if (localPlayer == null) return;

        if (targetId != localPlayer.GameObjectId) return;

        if (Service.ObjectTable.CreateObjectReference((nint)instigatorAddr) is not IPlayerCharacter player) return;

        if (player.GameObjectId == localPlayer.GameObjectId && !_config.EmoteLog.IncludeSelf) return;

        LogEmote(player, emoteId);
    }

    private void LogEmote(IPlayerCharacter player, ushort emoteId)
    {
        var emoteName = EmoteCatalog.NameFor(emoteId);
        var emoteCommand = EmoteCatalog.CommandFor(emoteId);
        var playerName = player.Name.ToString();
        var playerWorld = player.HomeWorld.Value.Name.ToString();
        var interned = Player.FromGameObject(player);

        EmoteEvent entry;
        bool collapsed;

        lock (_logs)
        {
            var index = _config.EmoteLog.CollapseDuplicates
                ? FindLast(playerName, playerWorld, emoteName)
                : -1;

            collapsed = index >= 0;

            if (collapsed)
            {
                entry = _logs[index];
                entry.Count++;
                entry.LastUpdate = DateTime.Now;
                entry.Command = emoteCommand;

                if (index != _logs.Count - 1)
                {
                    _logs.RemoveAt(index);
                    _logs.Add(entry);
                }
            }
            else
            {
                entry = new EmoteEvent(interned, emoteName, emoteCommand);
                _logs.Add(entry);

                var max = Math.Max(1, _config.EmoteLog.MaxEntries);
                if (_logs.Count > max) _logs.RemoveRange(0, _logs.Count - max);
            }

            Volatile.Write(ref _snapshot, _logs.ToArray());
        }

        if (collapsed)
        {
            _log.Debug(LogSource, $"Collapsed: {entry.User} used {entry.EmoteName} x{entry.Count}");
        }
        else
        {
            _log.Info(LogSource, $"{entry.User}@{entry.World} used {entry.EmoteName} ({entry.Command})");

            _config.Stats.IncrementEmotesTracked();
            _config.Stats.RecordEmote(playerName, playerWorld);

            var blacklistEntry = _config.EmoteLog.Blacklist.FirstOrDefault(x => x.Name == playerName && x.World == playerWorld);
            if (blacklistEntry?.DisableDiscord == true) return;
        }

        _log.FireAndForget(
            _discordNotifier.ProcessDiscordEmote(entry.Player, emoteName, emoteCommand),
            LogSource,
            $"Failed to relay the {emoteName} emote from {entry.User}@{entry.World} to Discord");
    }

    private int FindLast(string user, string world, string emoteName)
    {
        for (var i = _logs.Count - 1; i >= 0; i--)
        {
            var entry = _logs[i];
            if (entry.User == user && entry.World == world && entry.EmoteName == emoteName) return i;
        }

        return -1;
    }

    public void SimulateEmote(string name, string world, string emoteName, string command)
    {
        _log.FireAndForget(
            _discordNotifier.ProcessDiscordEmote(Player.FromNameWorld(name, world), emoteName, command),
            LogSource,
            $"Failed to relay the simulated {emoteName} emote to Discord");
    }

    public void PerformEmoteBack(string targetName, string targetWorld, string command, ulong targetId = 0, bool keepTarget = false, bool keepRotation = false)
    {
        _log.FireAndForget(
            _emoteBackAction.PerformAsync(targetName, targetWorld, command, targetId, keepTarget, keepRotation),
            LogSource,
            $"Failed to emote back at {targetName}@{targetWorld}");
    }

    public void Dispose()
    {
        _hookEmote?.Dispose();
    }
}
