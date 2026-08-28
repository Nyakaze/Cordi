using System;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Connection;
using Crovus.Events;

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

        if (ReferenceEquals(tracker, _tracker) && targetId == _watchedUserId) return;

        Unbind();

        _tracker = tracker;
        _watchedUserId = targetId;

        if (tracker is null || targetId == 0) return;

        _subscription = tracker.OnUser(targetId, OnPresenceUpdatedAsync, SubscriptionName);

        Log.Info(LogSource, $"Watching presence of user {targetId}");

        if (tracker.Get(targetId) is { } presence)
            _ = _plugin.ActivityManager.OnPresenceUpdated(
                new PresenceUpdatedEvent(presence, null) { Name = "PRESENCE_SEED" });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unbind();

        _tracker = null;
        _watchedUserId = 0;
    }

    private Task OnPresenceUpdatedAsync(PresenceUpdatedEvent e, CancellationToken ct) =>
        _plugin.ActivityManager.OnPresenceUpdated(e);

    private void Unbind()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private CordiLogService Log => _plugin.LogService;
}
