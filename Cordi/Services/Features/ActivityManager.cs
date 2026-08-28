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
        private const double RefreshIntervalSeconds = 1.0;
        private const string LogSource = "Activity";

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

            Service.Log.Info("[ActivityManager] Initialized and listening for presence updates.");
        }

        public void OnFrameworkUpdate(IFramework framework)
        {
            if (_disposed) return;

            if (_hasPendingPresenceUpdate)
            {
                _hasPendingPresenceUpdate = false;
                _lastRefreshTick = DateTime.Now;
                ProcessPresence(ActivityTrace.Verbose);
                return;
            }

            if (_isIdle && !IsCustomAlwaysOn() && string.IsNullOrEmpty(_broadcaster.LastTitle)) return;

            if ((DateTime.Now - _lastRefreshTick).TotalSeconds < RefreshIntervalSeconds) return;

            _lastRefreshTick = DateTime.Now;
            ProcessPresence(ActivityTrace.Quiet);
        }

        public Task OnPresenceUpdated(PresenceUpdatedEvent e)
        {
            if (!_hasLoggedPresenceReceived)
            {
                Service.Log.Info($"[ActivityManager] First presence update received for target user {e.UserId} ('{e.User.DisplayName}').");
                Log.Info(LogSource, $"First presence update from {e.User.DisplayName}");
                _hasLoggedPresenceReceived = true;
            }

            Service.Log.Debug($"[ActivityManager] Presence update for target user {e.UserId}: Status={e.Status}, Activities={e.Activities.Count}");

            foreach (var activity in e.Activities)
                Service.Log.Debug($"[ActivityManager]   -> Activity: Type={activity.Type}, Name='{activity.Name}'");

            _cachedPresence = e.Presence;
            _isIdle = false;
            _hasPendingPresenceUpdate = true;

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Service.Log.Info("[ActivityManager] Disposing — unsubscribing from events.");

            _disposed = true;
            _hasPendingPresenceUpdate = false;
            _isIdle = true;
        }

        private void ProcessPresence(ActivityTrace trace)
        {
            var config = _plugin.Config.ActivityConfig;

            if (config is null)
            {
                trace.Warning("ActivityConfig is null, clearing title.");
                ClearTitle(trace);
                return;
            }

            if (!config.Enabled)
            {
                trace.Debug("Activity integration is disabled, clearing title.");
                ClearTitle(trace);
                return;
            }

            if (config.TargetUserId == 0 && !IsCustomAlwaysOn())
            {
                trace.Warning("TargetUserId is not set (0). No presence will be tracked.");
                ClearTitle(trace);
                return;
            }

            if (ActivitySelector.SelectBest(_cachedPresence, config, trace) is not { } best)
            {
                trace.Debug("No valid candidate found — clearing title.");
                ClearTitle(trace);
                return;
            }

            _cycler.Advance(best, trace);

            var title = ActivityTitleRenderer.Render(best.Activity, best.Config, _cycler.Index,
                config.Replacements, trace);

            if (title.Length > DiscordActivityConfig.MaxTitleLength)
            {
                trace.Debug($"Title truncated from {title.Length} to {DiscordActivityConfig.MaxTitleLength} chars.");
                title = ActivityText.Truncate(title, DiscordActivityConfig.MaxTitleLength);
            }

            var player = _plugin.cachedLocalPlayer;

            if (player is null)
            {
                trace.Warning("cachedLocalPlayer is null — cannot set title. Is the player logged in?");
                return;
            }

            trace.Info($"Setting title: \"{title}\" (Prefix={config.PrefixTitle})");

            if (title != _lastLoggedTitle)
            {
                Log.Info(LogSource, $"Setting title: \"{title}\"");
                _lastLoggedTitle = title;
            }

            try
            {
                _broadcaster.Broadcast(player, title, config, best.Config);
            }
            catch (Exception ex)
            {
                Service.Log.Error($"[ActivityManager] Failed to set title via HonorificBridge: {ex.Message}\n{ex.StackTrace}");
                Log.Error(LogSource, $"Failed to set title: {ex.Message}");
            }
        }

        private void ClearTitle(ActivityTrace trace)
        {
            _isIdle = true;

            _cycler.Reset();
            _broadcaster.Clear(_plugin.cachedLocalPlayer);

            trace.Debug("Title cleared.");
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
