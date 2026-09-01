using System;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Connection;
using Crovus.Events;
using Crovus.Models;

namespace Cordi.Services.Discord.Presence;

public sealed class DiscordPresenceWatcher : IDisposable
{
    private const string LogSource = "Presence";
    private const string SubscriptionName = "Cordi.ActivityManager";

    private readonly CordiPlugin _plugin;
    private readonly DiscordConnection _connection;

    private PresenceTracker? _tracker;
    private IDisposable? _subscription;
    private ulong _watchedUserId;
    private volatile DiscordPresence? _lastDelivered;
    private bool _disposed;

    public DiscordPresenceWatcher(CordiPlugin plugin, DiscordConnection connection)
    {
        _plugin = plugin;
        _connection = connection;
    }

    public void Sync()
    {
        if (_disposed) return;

        var tracker = _connection.Presences;
        var targetId = _plugin.Config.ActivityConfig?.TargetUserId ?? 0;

        if (!ReferenceEquals(tracker, _tracker) || targetId != _watchedUserId)
            Rebind(tracker, targetId);

        if (_tracker is null || _watchedUserId == 0) return;

        if (_tracker.Get(_watchedUserId) is { } presence && !ReferenceEquals(presence, _lastDelivered))
            Deliver(presence, "PRESENCE_SEED");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unbind();

        _tracker = null;
        _watchedUserId = 0;
        _lastDelivered = null;
    }

    private void Rebind(PresenceTracker? tracker, ulong targetId)
    {
        Unbind();

        _tracker = tracker;
        _watchedUserId = targetId;
        _lastDelivered = null;

        _plugin.ActivityManager.ForgetPresence();

        if (tracker is null || targetId == 0) return;

        _subscription = tracker.OnUser(targetId, OnPresenceUpdatedAsync, SubscriptionName);

        Log.Info(LogSource, $"Watching presence of user {targetId}");
    }

    private Task OnPresenceUpdatedAsync(PresenceUpdatedEvent e, CancellationToken ct)
    {
        _lastDelivered = e.Presence;

        return _plugin.ActivityManager.OnPresenceUpdated(e);
    }

    private void Deliver(DiscordPresence presence, string name)
    {
        _lastDelivered = presence;

        Log.Debug(LogSource,
            $"Picked up cached presence for user {_watchedUserId} with {presence.Activities.Count} activities.");

        _ = _plugin.ActivityManager.OnPresenceUpdated(new PresenceUpdatedEvent(presence, null) { Name = name });
    }

    private void Unbind()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private CordiLogService Log => _plugin.LogService;
}
