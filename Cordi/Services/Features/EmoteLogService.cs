using Cordi.Domain;
using Cordi.Services.Activity;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Services.Features;
using Cordi.UI.Windows;

namespace Cordi.Services;

public class EmoteLogService : IDisposable
{
    private const string LogSource = "EmoteLog";

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService _log;
    private readonly EmoteBackAction _emoteBackAction;
    private readonly EmoteDiscordNotifier _discordNotifier;
    private readonly Lazy<EmoteLogWindow> _window;

    private readonly List<EmoteEvent> _logs = new();

    private delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate> _hookEmote;

    public event Action<ulong, ushort, ulong>? EmoteObserved;

    public EmoteLogService(
        Configuration.Configuration config,
        CordiLogService log,
        EmoteBackAction emoteBackAction,
        EmoteDiscordNotifier discordNotifier,
        Lazy<EmoteLogWindow> window)
    {
        _config = config;
        _log = log;
        _emoteBackAction = emoteBackAction;
        _discordNotifier = discordNotifier;
        _window = window;

        _hookEmote = Service.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
        _hookEmote.Enable();
    }

    public EmoteDiscordNotifier DiscordNotifier => _discordNotifier;

    public IReadOnlyList<EmoteEvent> Logs => _logs;

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

    private void HandleEmote(ulong instigatorAddr, ushort emoteId, ulong targetId)
    {
        if (!_config.EmoteLog.Enabled) return;
        if (!_window.Value.IsOpen && !_config.EmoteLog.DetectWhenClosed) return;

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

        lock (_logs)
        {
            var playerName = player.Name.ToString();
            var playerWorld = player.HomeWorld.Value.Name.ToString();

            if (_config.EmoteLog.CollapseDuplicates)
            {
                var index = FindLast(playerName, playerWorld, emoteName);

                if (index >= 0)
                {
                    var existing = _logs[index];
                    existing.Count++;
                    existing.LastUpdate = DateTime.Now;
                    existing.Command = emoteCommand;

                    if (index != _logs.Count - 1)
                    {
                        _logs.RemoveAt(index);
                        _logs.Add(existing);
                    }

                    _log.Debug(LogSource, $"Collapsed: {existing.User} used {existing.EmoteName} x{existing.Count}");

                    _ = _discordNotifier.ProcessDiscordEmote(existing.Player, emoteName, emoteCommand);
                    return;
                }
            }

            var interned = Player.FromGameObject(player);
            var entry = new EmoteEvent(interned, emoteName, emoteCommand);

            _logs.Add(entry);

            var max = Math.Max(1, _config.EmoteLog.MaxEntries);
            if (_logs.Count > max) _logs.RemoveRange(0, _logs.Count - max);

            _log.Info(LogSource, $"{entry.User}@{entry.World} used {entry.EmoteName} ({entry.Command})");
            _config.Stats.IncrementEmotesTracked();
            _config.Stats.RecordEmote(playerName, playerWorld);

            var blacklistEntry = _config.EmoteLog.Blacklist.FirstOrDefault(x => x.Name == playerName && x.World == playerWorld);
            if (blacklistEntry?.DisableDiscord == true) return;

            _ = _discordNotifier.ProcessDiscordEmote(interned, emoteName, emoteCommand);
        }
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
        _ = _discordNotifier.ProcessDiscordEmote(Player.FromNameWorld(name, world), emoteName, command);
    }

    public Task PerformEmoteBack(string targetName, string targetWorld, string command, ulong targetId = 0, bool keepTarget = false, bool keepRotation = false)
        => _emoteBackAction.PerformAsync(targetName, targetWorld, command, targetId, keepTarget, keepRotation);

    public void Dispose()
    {
        _hookEmote?.Dispose();
    }
}
