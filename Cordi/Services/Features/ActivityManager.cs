using System;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Activity;
using Crovus.Events;
using Crovus.Models;
using Dalamud.Plugin.Services;

namespace Cordi.Services
{
    public class ActivityManager : IDisposable
    {
        private const string LogSource = "Activity";
        private const double RefreshIntervalSeconds = 1.0;

        private readonly CordiPlugin _plugin;
        private readonly ActivityCycler _cycler = new();
        private readonly ActivityTitleBroadcaster _broadcaster;

        private volatile DiscordPresence? _cachedPresence;
        private volatile bool _hasPendingPresenceUpdate;
        private volatile bool _isIdle = true;
        private volatile bool _disposed;

        private bool _hasLoggedPresenceReceived;
        private string _lastLoggedTitle = string.Empty;
        private DateTime _lastRefreshTick = DateTime.MinValue;

        public ActivityManager(CordiPlugin plugin, HonorificBridge honorific)
        {
            _plugin = plugin;
            _broadcaster = new ActivityTitleBroadcaster(honorific);

            Log.Info(LogSource, "Initialized and listening for presence updates.");
        }

        public void OnFrameworkUpdate(IFramework framework)
        {
            if (_disposed) return;

            if (_hasPendingPresenceUpdate)
            {
                _hasPendingPresenceUpdate = false;
                _lastRefreshTick = DateTime.Now;
                ProcessPresence(quiet: false);
                return;
            }

            if (_isIdle && !IsCustomAlwaysOn() && string.IsNullOrEmpty(_broadcaster.LastTitle)) return;

            if ((DateTime.Now - _lastRefreshTick).TotalSeconds < RefreshIntervalSeconds) return;

            _lastRefreshTick = DateTime.Now;
            ProcessPresence(quiet: true);
        }

        public Task OnPresenceUpdated(PresenceUpdatedEvent e)
        {
            if (!_hasLoggedPresenceReceived)
            {
                Log.Info(LogSource, $"First presence update for target user {e.UserId} ('{e.User.DisplayName}').");
                _hasLoggedPresenceReceived = true;
            }

            Log.Debug(LogSource, $"Presence update for target user {e.UserId}: Status={e.Status}, Activities={e.Activities.Count}");

            foreach (var activity in e.Activities)
                Log.Debug(LogSource, $"  -> Activity: Type={activity.Type}, Name='{activity.Name}'");

            _cachedPresence = e.Presence;
            _isIdle = false;
            _hasPendingPresenceUpdate = true;

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Log.Info(LogSource, "Disposing — unsubscribing from events.");

            _disposed = true;
            _hasPendingPresenceUpdate = false;
            _isIdle = true;
        }

        private void ProcessPresence(bool quiet)
        {
            var config = _plugin.Config.ActivityConfig;

            if (config is null)
            {
                Log.Warning(LogSource, "ActivityConfig is null, clearing title.", mute: quiet);
                ClearTitle(quiet);
                return;
            }

            if (!config.Enabled)
            {
                Log.Debug(LogSource, "Activity integration is disabled, clearing title.", mute: quiet);
                ClearTitle(quiet);
                return;
            }

            if (config.TargetUserId == 0 && !IsCustomAlwaysOn())
            {
                Log.Warning(LogSource, "TargetUserId is not set (0). No presence will be tracked.", mute: quiet);
                ClearTitle(quiet);
                return;
            }

            var detail = quiet ? null : Log;

            if (ActivitySelector.SelectBest(_cachedPresence, config, detail) is not { } best)
            {
                Log.Debug(LogSource, "No valid candidate found — clearing title.", mute: quiet);
                ClearTitle(quiet);
                return;
            }

            _cycler.Advance(best, detail);

            var title = ActivityTitleRenderer.Render(best.Activity, best.Config, _cycler.Index,
                config.Replacements, detail);

            if (title.Length > DiscordActivityConfig.MaxTitleLength)
            {
                Log.Debug(LogSource, $"Title truncated from {title.Length} to {DiscordActivityConfig.MaxTitleLength} chars.", mute: quiet);
                title = ActivityText.Truncate(title, DiscordActivityConfig.MaxTitleLength);
            }

            var player = _plugin.cachedLocalPlayer;

            if (player is null)
            {
                Log.Warning(LogSource, "cachedLocalPlayer is null — cannot set title. Is the player logged in?", mute: quiet);
                return;
            }

            if (title != _lastLoggedTitle)
            {
                _lastLoggedTitle = title;
                Log.Info(LogSource, $"Setting title: \"{title}\" (Prefix={config.PrefixTitle})");
            }

            try
            {
                _broadcaster.Broadcast(player, title, config, best.Config);
            }
            catch (Exception ex)
            {
                Log.Error(LogSource, "Failed to set title via HonorificBridge", ex);
            }
        }

        private void ClearTitle(bool quiet)
        {
            _isIdle = true;

            _cycler.Reset();
            _broadcaster.Clear(_plugin.cachedLocalPlayer);

            Log.Debug(LogSource, "Title cleared.", mute: quiet);
        }

        private bool IsCustomAlwaysOn()
        {
            var config = _plugin.Config.ActivityConfig;

            if (config is null || !config.Enabled) return false;

            return config.TypeConfigs.TryGetValue(ActivityType.Custom, out var custom) && custom.Enabled;
        }

        private CordiLogService Log => _plugin.LogService;
    }
}
