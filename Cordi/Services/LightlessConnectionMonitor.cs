using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Crovus.Factory;
using Crovus.Models;

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
    /// <summary>
    /// Feature-level kill switch. Lightless has been retired from the UI, so the monitor
    /// loop is never started and no status/disconnect messages are posted, regardless of any
    /// user's saved config. Set to true (and restore the sidebar button in ConfigWindow) to
    /// bring the feature back.
    /// </summary>
    public const bool FeatureEnabled = false;

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
    private DateTime _lastStatusEditAt = DateTime.MinValue;

    // Auto-reconnect state
    private DateTime? _autoReconnectScheduledAt;
    private DateTime _lastAutoReconnectAttempt = DateTime.MinValue;
    private int _consecutiveReconnectFailures = 0;

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
        if (!FeatureEnabled) return;   // Feature retired from the UI; flip FeatureEnabled to restore.
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
        if (!cfg.Enabled && !cfg.AutoReconnect) return;

        var raw = _plugin.Lightless.ConnectionStateRaw() ?? "Unknown";
        var cls = Classify(raw);

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (cfg.Enabled && !string.IsNullOrEmpty(cfg.DiscordChannelId) && ulong.TryParse(cfg.DiscordChannelId, out var channelId))
            {
                await UpdateStatusEmbedAsync(channelId, raw, cls).ConfigureAwait(false);
                await HandleAlertTransitionsAsync(channelId, raw, cls).ConfigureAwait(false);
            }

            if (cfg.AutoReconnect)
            {
                await HandleAutoReconnectAsync(cls, raw).ConfigureAwait(false);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task HandleAutoReconnectAsync(StateClass cls, string raw)
    {
        // Safeguard 1: Must be logged in to the game to attempt reconnection.
        if (!Service.ClientState.IsLoggedIn)
        {
            _autoReconnectScheduledAt = null;
            _consecutiveReconnectFailures = 0;
            return;
        }

        // Safeguard 2: Only attempt reconnection if currently disconnected.
        if (cls != StateClass.Disconnected)
        {
            if (cls == StateClass.Connected)
            {
                if (_consecutiveReconnectFailures > 0)
                {
                    Service.Log.Information("[Lightless.Monitor] Auto-reconnect: Connected state restored. Resetting failure counter.");
                    _consecutiveReconnectFailures = 0;
                }
            }
            _autoReconnectScheduledAt = null;
            return;
        }

        // Safeguard 3: Flap protection / Initial delay.
        // Wait at least 15 seconds after detecting a disconnect to avoid acting on temporary network glitches.
        _autoReconnectScheduledAt ??= DateTime.UtcNow;
        var disconnectedDuration = DateTime.UtcNow - _autoReconnectScheduledAt.Value;
        if (disconnectedDuration < TimeSpan.FromSeconds(15))
        {
            return;
        }

        // Safeguard 4: Rate limiting & Heavy failure throttling.
        var timeSinceLastAttempt = DateTime.UtcNow - _lastAutoReconnectAttempt;
        var currentCooldown = _consecutiveReconnectFailures >= 5
            ? TimeSpan.FromMinutes(5) // Heavy backoff/throttle after 5 consecutive failures
            : TimeSpan.FromSeconds(30); // Normal cooldown between attempts

        if (timeSinceLastAttempt < currentCooldown)
        {
            return;
        }

        _lastAutoReconnectAttempt = DateTime.UtcNow;
        Service.Log.Information($"[Lightless.Monitor] Auto-reconnect: Initiating reconnect attempt {_consecutiveReconnectFailures + 1} (state={raw})");

        var success = _plugin.Lightless.TryReconnect();
        if (success)
        {
            _consecutiveReconnectFailures++;
        }
        else
        {
            Service.Log.Warning("[Lightless.Monitor] Auto-reconnect: TryReconnect returned false (reflection failed to resolve).");
            _consecutiveReconnectFailures++;
        }
    }

    private async Task UpdateStatusEmbedAsync(ulong channelId, string raw, StateClass cls)
    {
        var stateChanged = !string.Equals(raw, _lastRenderedState, StringComparison.Ordinal);
        var heartbeatDue = DateTime.UtcNow - _lastStatusEditAt >= StatusHeartbeat;
        var cfg = _plugin.Config.Lightless;
        var forceUpdate = cfg.AutoReconnect && cls != StateClass.Connected;

        // First-ever post must happen regardless of "changed" flags.
        if (!stateChanged && !heartbeatDue && !forceUpdate && cfg.StatusMessageId != 0) return;

        var embed = BuildStatusEmbed(raw, cls);

        if (cfg.StatusMessageId != 0)
        {
            var ok = await _plugin.Discord.EditEmbedInChannelAsync(channelId, cfg.StatusMessageId, embed)
                                           .ConfigureAwait(false);
            if (ok)
            {
                _lastRenderedState = raw;
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

                if (cfg.AutoReconnect)
                {
                    if (cfg.DisconnectMessageId != 0)
                    {
                        var msgId = cfg.DisconnectMessageId;
                        cfg.DisconnectMessageId = 0;
                        _plugin.Config.Save();
                        await _plugin.Discord.DeleteChannelMessageAsync(channelId, msgId).ConfigureAwait(false);
                        Service.Log.Information($"[Lightless.Monitor] Cleared lingering disconnect alert msg={msgId} because AutoReconnect is enabled.");
                    }
                    return;
                }

                if (cfg.DisconnectMessageId != 0) return; // alert already up

                await PostDisconnectAlertAsync(channelId, raw).ConfigureAwait(false);
                return;
        }
    }

    private async Task PostDisconnectAlertAsync(ulong channelId, string raw)
    {
        var autoReconnect = _plugin.Config.Lightless.AutoReconnect;
        var autoReconnectStatus = autoReconnect
            ? "\n*Auto-reconnect is active and will attempt to restore connection shortly.*"
            : $"\nReact with {ReconnectEmoji} to manually attempt a reconnect.";

        var embed = EmbedFactory.Create()
            .WithTitle("Lightless Sync Disconnected")
            .WithDescription($"### Connection Drop Detected\n" +
                             $"**State:** `{raw}`\n" +
                             $"{autoReconnectStatus}\n\n" +
                             $"*This notification will be automatically deleted once the bridge goes back online.*")
            .WithColor(0xF23F43)
            .WithFooter("Cordi Alert System")
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
            await _plugin.Discord.AddReaction(channelId, msgId, ReconnectEmoji)
                                 .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[Lightless.Monitor] AddReaction failed: {ex.Message}");
        }
    }

    private DiscordEmbed BuildStatusEmbed(string raw, StateClass cls)
    {
        var (color, dot, headline) = cls switch
        {
            StateClass.Connected    => (0x23A55A, "🟢", "Online"),
            StateClass.Transient    => (0xF0B232, "🟡", "Reconnecting"),
            StateClass.Disconnected => (0xF23F43, "🔴", "Offline"),
            _                        => (0x747F8D, "⚪", "Unknown"),
        };

        var builder = EmbedFactory.Create()
            .WithTitle("Lightless Sync Bridge")
            .WithColor(color)
            .WithFooter("Cordi Integration • Last updated")
            .WithTimestamp(DateTimeOffset.UtcNow);

        // Construct modern description layout
        var desc = $"**Status:** {dot} **{headline}**\n" +
                   $"**Raw State:** `{raw}`\n\n";

        var autoRecEnabled = _plugin.Config.Lightless.AutoReconnect;
        var autoRec = autoRecEnabled ? "Enabled" : "Disabled";
        desc += $"**Auto Reconnect:** `{autoRec}`\n";

        if (cls == StateClass.Connected)
        {
            // Online status
        }
        else
        {
            if (autoRecEnabled)
            {
                if (!Service.ClientState.IsLoggedIn)
                {
                    desc += "\n*Auto-reconnect: Paused (Not logged into the game).*";
                }
                else
                {
                    var isWaitingForFlap = false;
                    if (_autoReconnectScheduledAt.HasValue)
                    {
                        var disconnectedDuration = DateTime.UtcNow - _autoReconnectScheduledAt.Value;
                        if (disconnectedDuration < TimeSpan.FromSeconds(15))
                        {
                            isWaitingForFlap = true;
                            var remainingFlap = TimeSpan.FromSeconds(15) - disconnectedDuration;
                            desc += $"\n*Auto-reconnect: Initializing flap protection (retrying in {(int)remainingFlap.TotalSeconds}s)...*";
                        }
                    }

                    if (!isWaitingForFlap)
                    {
                        var timeSinceLastAttempt = DateTime.UtcNow - _lastAutoReconnectAttempt;
                        var currentCooldown = _consecutiveReconnectFailures >= 5
                            ? TimeSpan.FromMinutes(5)
                            : TimeSpan.FromSeconds(30);

                        var remaining = currentCooldown - timeSinceLastAttempt;
                        if (remaining > TimeSpan.Zero)
                        {
                            if (_consecutiveReconnectFailures >= 5)
                            {
                                desc += $"\n⚠️ *Auto-reconnect: Throttled (5+ failed attempts). Next attempt in {(int)remaining.TotalMinutes}m {(int)remaining.Seconds}s.*";
                            }
                            else
                            {
                                desc += $"\n*Auto-reconnect: Cooldown active. Next attempt in {(int)remaining.TotalSeconds}s.*";
                            }
                        }
                        else
                        {
                            desc += $"\n*Auto-reconnect: Attempt #{_consecutiveReconnectFailures + 1} pending shortly...*";
                        }
                    }
                }
            }
            else
            {
                desc += "\n*Bridge is offline. Check the game/plugin status.*";
            }
        }

        builder.WithDescription(desc);
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

        var embed = EmbedFactory.Create()
            .WithTitle("Lightless Sync Disconnected")
            .WithDescription($"### Connection Drop Detected\n" +
                             $"**State:** `{stateRaw}`\n\n" +
                             $"**Last Action:**\n" +
                             $"{note} (<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>)\n\n" +
                             $"*This notification will be automatically deleted once the bridge goes back online.*")
            .WithColor(ok ? 0xF0B232 : 0xF23F43)
            .WithFooter("Cordi Alert System")
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
