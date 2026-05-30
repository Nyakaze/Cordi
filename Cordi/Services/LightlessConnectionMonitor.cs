using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using DSharpPlus.Entities;

namespace Cordi.Services;

/// <summary>
/// Maintains a persistent "Lightless status" embed in the configured channel and posts
/// a separate, transient disconnect notification on connected→disconnected transitions.
/// On reconnect, the disconnect notification is deleted so the channel stays clean and
/// the status embed reflects the live state.
///
/// Why two messages?
///   - Edits don't generate Discord notifications, so a single edited embed would never
///     ping the user when sync drops. The status embed stays silent and current; the
///     transient alert (a fresh message) is what actually pings.
/// </summary>
public class LightlessConnectionMonitor : IDisposable
{
    public const string ReconnectEmoji = "🔄";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    // Suppress short flaps + the brief "Reconnecting" transient — only alert after this long.
    private static readonly TimeSpan DisconnectStableFor = TimeSpan.FromSeconds(20);
    // Heartbeat refresh of the status embed even when nothing changed.
    private static readonly TimeSpan StatusHeartbeat = TimeSpan.FromMinutes(5);

    private static readonly HashSet<string> ConnectedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connected",
    };
    private static readonly HashSet<string> TransientStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connecting", "Reconnecting", "Disconnecting",
    };
    private static readonly HashSet<string> DisconnectedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Offline", "Disconnected", "Unauthorized", "Failed", "RateLimited", "VersionMisMatch",
    };

    private enum StateClass { Unknown, Connected, Transient, Disconnected }

    private readonly CordiPlugin _plugin;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Task? _loop;

    private bool _wasConnectedOnce;
    private DateTime? _disconnectedSince;

    // Cached state used to skip no-op status edits.
    private string _lastRenderedState = string.Empty;
    private int _lastRenderedPairs = -1;
    private DateTime _lastStatusEditAt = DateTime.MinValue;

    // The ID of the disconnect-alert message, mirrored from config — exposed so the
    // reaction handler can match incoming reactions cheaply.
    public ulong ActiveNotifyChannelId
    {
        get
        {
            var cfg = _plugin.Config.Lightless;
            if (cfg.DisconnectMessageId == 0) return 0;
            return ulong.TryParse(cfg.DiscordChannelId, out var c) ? c : 0;
        }
    }
    public ulong ActiveNotifyMessageId => _plugin.Config.Lightless.DisconnectMessageId;

    public LightlessConnectionMonitor(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Start()
    {
        if (_loop != null) return;
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Service.Log.Debug($"[Lightless.Monitor] Tick failed: {ex.Message}");
            }

            try { await Task.Delay(PollInterval, ct).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }
        }
    }

    private async Task TickAsync()
    {
        var cfg = _plugin.Config.Lightless;
        if (!cfg.Enabled) return;
        if (string.IsNullOrEmpty(cfg.DiscordChannelId)) return;
        if (!ulong.TryParse(cfg.DiscordChannelId, out var channelId)) return;

        var raw = _plugin.Lightless.ConnectionStateRaw() ?? "Unknown";
        var cls = Classify(raw);
        var pairs = _plugin.Lightless.GetPairCount() ?? -1;

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await UpdateStatusEmbedAsync(channelId, raw, cls, pairs).ConfigureAwait(false);
            await HandleAlertTransitionsAsync(channelId, raw, cls).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task UpdateStatusEmbedAsync(ulong channelId, string raw, StateClass cls, int pairs)
    {
        var stateChanged = !string.Equals(raw, _lastRenderedState, StringComparison.Ordinal)
                           || pairs != _lastRenderedPairs;
        var heartbeatDue = DateTime.UtcNow - _lastStatusEditAt >= StatusHeartbeat;
        var cfg = _plugin.Config.Lightless;

        // First-ever post must happen regardless of "changed" flags.
        if (!stateChanged && !heartbeatDue && cfg.StatusMessageId != 0) return;

        var embed = BuildStatusEmbed(raw, cls, pairs);

        if (cfg.StatusMessageId != 0)
        {
            var ok = await _plugin.Discord.EditEmbedInChannelAsync(channelId, cfg.StatusMessageId, embed)
                                          .ConfigureAwait(false);
            if (ok)
            {
                _lastRenderedState = raw;
                _lastRenderedPairs = pairs;
                _lastStatusEditAt = DateTime.UtcNow;
                return;
            }
            // Message was deleted / inaccessible — fall through and recreate.
            cfg.StatusMessageId = 0;
            _plugin.Config.Save();
        }

        var newId = await _plugin.Discord.SendEmbedToChannelAsync(channelId, embed).ConfigureAwait(false);
        if (newId == 0)
        {
            Service.Log.Warning("[Lightless.Monitor] Failed to post status embed.");
            return;
        }
        cfg.StatusMessageId = newId;
        _plugin.Config.Save();
        _lastRenderedState = raw;
        _lastRenderedPairs = pairs;
        _lastStatusEditAt = DateTime.UtcNow;
        Service.Log.Information($"[Lightless.Monitor] Status embed posted: msg={newId}");
    }

    private async Task HandleAlertTransitionsAsync(ulong channelId, string raw, StateClass cls)
    {
        var cfg = _plugin.Config.Lightless;

        switch (cls)
        {
            case StateClass.Connected:
                _wasConnectedOnce = true;
                _disconnectedSince = null;

                // Reconnect recovery: drop any lingering disconnect alert so the channel is clean.
                if (cfg.DisconnectMessageId != 0)
                {
                    var msgId = cfg.DisconnectMessageId;
                    cfg.DisconnectMessageId = 0;
                    _plugin.Config.Save();
                    await _plugin.Discord.DeleteChannelMessageAsync(channelId, msgId).ConfigureAwait(false);
                    Service.Log.Information($"[Lightless.Monitor] Cleared disconnect alert msg={msgId} on reconnect.");
                }
                return;

            case StateClass.Transient:
                return;

            case StateClass.Disconnected:
                if (!_wasConnectedOnce) return;
                _disconnectedSince ??= DateTime.UtcNow;
                if (DateTime.UtcNow - _disconnectedSince.Value < DisconnectStableFor) return;
                if (cfg.DisconnectMessageId != 0) return; // alert already up

                await PostDisconnectAlertAsync(channelId, raw).ConfigureAwait(false);
                return;
        }
    }

    private async Task PostDisconnectAlertAsync(ulong channelId, string raw)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle("Lightless Sync disconnected")
            .WithDescription($"Connection state: **{raw}**\n" +
                             $"React with {ReconnectEmoji} to attempt a reconnect.\n" +
                             $"This message will be removed automatically once the connection is back.")
            .WithColor(new DiscordColor(0xE74C3C))
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        var msgId = await _plugin.Discord.SendEmbedToChannelAsync(channelId, embed).ConfigureAwait(false);
        if (msgId == 0)
        {
            Service.Log.Warning("[Lightless.Monitor] Disconnect alert send returned 0.");
            return;
        }

        _plugin.Config.Lightless.DisconnectMessageId = msgId;
        _plugin.Config.Save();
        Service.Log.Information($"[Lightless.Monitor] Disconnect alert posted: msg={msgId}");

        try
        {
            await _plugin.Discord.AddReaction(channelId, msgId, DiscordEmoji.FromUnicode(ReconnectEmoji))
                                 .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless.Monitor] AddReaction failed: {ex.Message}");
        }
    }

    private DiscordEmbed BuildStatusEmbed(string raw, StateClass cls, int pairs)
    {
        var (color, dot, headline) = cls switch
        {
            StateClass.Connected    => (new DiscordColor(0x2ECC71), "🟢", "Online"),
            StateClass.Transient    => (new DiscordColor(0xF1C40F), "🟡", "Reconnecting"),
            StateClass.Disconnected => (new DiscordColor(0xE74C3C), "🔴", "Offline"),
            _                        => (new DiscordColor(0x95A5A6), "⚪", "Unknown"),
        };

        var builder = new DiscordEmbedBuilder()
            .WithTitle("Lightless Sync")
            .WithDescription($"{dot}  **{headline}**")
            .WithColor(color)
            .AddField("State", $"`{raw}`", inline: true)
            .AddField("Pairs", pairs >= 0 ? pairs.ToString() : "—", inline: true)
            .WithFooter("Last updated")
            .WithTimestamp(DateTimeOffset.UtcNow);

        return builder.Build();
    }

    private static StateClass Classify(string raw)
    {
        if (ConnectedStates.Contains(raw)) return StateClass.Connected;
        if (TransientStates.Contains(raw)) return StateClass.Transient;
        if (DisconnectedStates.Contains(raw)) return StateClass.Disconnected;
        return StateClass.Disconnected;
    }

    /// <summary>
    /// Called by the reaction handler when our reconnect-emoji is added to the active alert.
    /// Edits the existing alert embed in place to reflect the dispatch result — no new
    /// message is posted, so the channel stays clean even on repeated reconnect attempts.
    /// </summary>
    public async Task HandleReconnectReactionAsync(ulong channelId, ulong messageId)
    {
        Service.Log.Information(
            $"[Lightless.Monitor] HandleReconnectReaction channel={channelId} msg={messageId} " +
            $"active=({ActiveNotifyChannelId}, {ActiveNotifyMessageId})");

        if (channelId != ActiveNotifyChannelId || messageId != ActiveNotifyMessageId)
            return;

        var ok = _plugin.Lightless.TryReconnect();
        Service.Log.Information($"[Lightless.Monitor] TryReconnect returned {ok}");

        // Reset the "stable disconnected" timer — if the reconnect fails fast, we re-arm cleanly.
        _disconnectedSince = null;

        var stateRaw = _plugin.Lightless.ConnectionStateRaw() ?? "Unknown";
        var note = ok
            ? "Reconnect dispatched — waiting for Lightless to report Connected."
            : "Reconnect failed — deep integration could not invoke Lightless reconnect.";

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Lightless Sync disconnected")
            .WithDescription($"Connection state: **{stateRaw}**\n" +
                             $"React with {ReconnectEmoji} to attempt a reconnect.\n" +
                             $"This message will be removed automatically once the connection is back.")
            .AddField("Last action", $"{note}\n<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>")
            .WithColor(new DiscordColor(ok ? 0xF1C40F : 0xE74C3C))
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        var edited = await _plugin.Discord.EditEmbedInChannelAsync(channelId, messageId, embed)
                                          .ConfigureAwait(false);
        if (!edited)
            Service.Log.Warning($"[Lightless.Monitor] Reconnect-result edit failed for msg={messageId}.");
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
        _writeLock.Dispose();
    }
}
