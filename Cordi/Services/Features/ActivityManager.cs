using System;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Services.Discord;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using System.Reflection;
using Cordi.Core;
using Cordi.Configuration;

namespace Cordi.Services
{
    public class ActivityManager : IDisposable
    {
        private readonly CordiPlugin _plugin;
        private readonly DiscordHandler _discord;
        private readonly HonorificBridge _honorific;

        private ActivityType? _currentCyclingType;
        private DateTime _lastCycleSwap = DateTime.MinValue;
        private int _currentCycleIndex = -1;

        private DateTime _lastWatchLog = DateTime.MinValue;
        private DateTime _lastDebugLog = DateTime.MinValue;

        private DiscordPresence _cachedPresence;
        private bool _hasLoggedPresenceReceived = false;
        private string _lastLoggedTitle = string.Empty;

        // Periodic re-sync state. All ProcessPresence execution is marshalled onto the
        // Dalamud framework thread to keep HonorificBridge + cachedLocalPlayer +
        // _lastBroadcastTitle single-threaded. OnPresenceUpdated fires on a DSharpPlus
        // pool thread; instead of invoking ProcessPresence directly it writes
        // _cachedPresence and flips the volatile flags below. OnFrameworkUpdate then has
        // three branches:
        //   (1) reactive drain — Discord-originated field/state changes (song skip, pause,
        //       resume, metadata edits) picked up on the next frame (~16 ms at 60 fps),
        //       distinct from and much faster than the placeholder refresh cadence;
        //   (2) idle short-circuit — no active broadcast → loop halts entirely and waits
        //       to be re-armed by the next presence update (req 3);
        //   (3) time-placeholder refresh — 1 Hz tick so {elapsed}/{duration} advance and
        //       cycling formats rotate; dedup in the SetTitle path keeps static titles
        //       silent.
        // Writing _cachedPresence before the volatile _hasPendingPresenceUpdate
        // establishes a release barrier so the framework thread sees the new reference
        // whenever it observes the flag.
        private DateTime _lastRefreshTick = DateTime.MinValue;
        private string _lastBroadcastTitle = string.Empty;
        private volatile bool _hasPendingPresenceUpdate = false;
        private volatile bool _isIdle = true;
        private volatile bool _disposed = false;

        private const double RefreshIntervalSeconds = 1.0;

        private CordiLogService Log => _plugin.LogService;
        private const string LogSource = "Activity";

        public ActivityManager(CordiPlugin plugin, DiscordHandler discord, HonorificBridge honorific)
        {
            _plugin = plugin;
            _discord = discord;
            _honorific = honorific;

            _discord.OnPresenceUpdated += OnPresenceUpdated;
            Service.Framework.Update += OnFrameworkUpdate;

            Service.Log.Info("[ActivityManager] Initialized and listening for presence updates.");
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (_disposed) return;

            // (1) Reactive fast path — drain any Discord-originated presence update. This
            // covers song skip / pause / resume / field edits with ~1 frame of latency
            // (≈16 ms at 60 fps) instead of waiting up to a full second for the refresh
            // tick. Reading _hasPendingPresenceUpdate (volatile) also establishes an
            // acquire barrier so _cachedPresence is guaranteed visible.
            if (_hasPendingPresenceUpdate)
            {
                _hasPendingPresenceUpdate = false;
                _lastRefreshTick = DateTime.Now;
                ProcessPresence(_cachedPresence, isUpdateLoop: false);
                return;
            }

            // (2) Idle short-circuit (req 3) — nothing is being broadcast, so there is no
            // time placeholder to advance and no cycling to rotate. ClearTitle sets this
            // flag; OnPresenceUpdated clears it when a new presence arrives.
            if (_isIdle) return;

            // (3) Time-placeholder refresh — slow 1 Hz tick. Static titles cost one
            // RenderTitle pass per second but no IPC, because SetTitle dedupes on the
            // rendered string.
            if ((DateTime.Now - _lastRefreshTick).TotalSeconds < RefreshIntervalSeconds) return;
            _lastRefreshTick = DateTime.Now;
            ProcessPresence(_cachedPresence, isUpdateLoop: true);
        }

        private Task OnPresenceUpdated(DiscordClient sender, PresenceUpdateEventArgs e)
        {
            var targetId = _plugin.Config.ActivityConfig.TargetUserId;

            if (e.User.Id != targetId)
            {
                if ((DateTime.Now - _lastDebugLog).TotalSeconds >= 30)
                {
                    Service.Log.Debug($"[ActivityManager] Presence update from user {e.User.Id} ('{e.User.Username}') ignored — does not match TargetUserId {targetId}.");
                    _lastDebugLog = DateTime.Now;
                }
                return Task.CompletedTask;
            }

            if (!_hasLoggedPresenceReceived)
            {
                Service.Log.Info($"[ActivityManager] First presence update received for target user {e.User.Id} ('{e.User.Username}').");
                Log.Info(LogSource, $"First presence update from {e.User.Username}");
                _hasLoggedPresenceReceived = true;
            }

            var activityCount = e.PresenceAfter?.Activities?.Count ?? 0;
            Service.Log.Debug($"[ActivityManager] Presence update for target user {e.User.Id}: Status={e.PresenceAfter?.Status}, Activities={activityCount}");

            if (e.PresenceAfter?.Activities != null)
            {
                foreach (var a in e.PresenceAfter.Activities)
                {
                    Service.Log.Debug($"[ActivityManager]   -> Activity: Type={a.ActivityType}, Name='{a.Name}'");
                }
            }

            // Hand off to the framework-thread tick. ORDER MATTERS: _cachedPresence must
            // be written before the volatile _hasPendingPresenceUpdate flag — the latter
            // acts as a release barrier, so any framework-thread reader that sees the
            // flag is guaranteed to see the matching presence reference.
            _cachedPresence = e.PresenceAfter;
            _isIdle = false;
            _hasPendingPresenceUpdate = true;
            return Task.CompletedTask;
        }

        private void ProcessPresence(DiscordPresence presence, bool isUpdateLoop)
        {
            var config = _plugin.Config.ActivityConfig;
            if (config == null)
            {
                if (!isUpdateLoop) Service.Log.Warning("[ActivityManager] ActivityConfig is null, clearing title.");
                ClearTitle();
                return;
            }

            if (!config.Enabled)
            {
                if (!isUpdateLoop) Service.Log.Debug("[ActivityManager] Activity integration is disabled, clearing title.");
                ClearTitle();
                return;
            }

            if (config.TargetUserId == 0)
            {
                if (!isUpdateLoop) Service.Log.Warning("[ActivityManager] TargetUserId is not set (0). No presence will be tracked.");
                ClearTitle();
                return;
            }

            if (presence == null)
            {
                if (!isUpdateLoop) Service.Log.Debug("[ActivityManager] Cached presence is null — no presence data received yet.");
                ClearTitle();
                return;
            }

            if (presence.Activities == null || !presence.Activities.Any())
            {
                if (!isUpdateLoop) Service.Log.Debug("[ActivityManager] Presence has no activities.");
            }

            var candidates = new List<(DiscordActivity Activity, ActivityTypeConfig Config)>();

            if (presence != null && presence.Activities != null)
            {
                foreach (var a in presence.Activities)
                {
                    ActivityTypeConfig conf = null;
                    // Check for Game Override first (Only for Playing activities)
                    if (a.ActivityType == ActivityType.Playing && !string.IsNullOrEmpty(a.Name) && config.GameConfigs.TryGetValue(a.Name, out var gameConf))
                    {
                        if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Activity '{a.Name}' ({a.ActivityType}) matched game override config.");
                        conf = gameConf;
                    }
                    else
                    {
                        conf = GetConfigForType(a.ActivityType, config);
                        if (conf == null)
                        {
                            if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Activity '{a.Name}' ({a.ActivityType}) has no matching type config — skipped.");
                        }
                    }

                    if (conf != null && conf.Enabled)
                    {
                        if (IsFilteredOut(a, conf, isUpdateLoop))
                        {
                            if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Activity '{a.Name}' ({a.ActivityType}) matched a blacklist filter — skipped.");
                            continue;
                        }
                        if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Activity '{a.Name}' ({a.ActivityType}) added as candidate (Priority={conf.Priority}).");
                        candidates.Add((a, conf));
                    }
                    else if (conf != null && !conf.Enabled)
                    {
                        if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Activity '{a.Name}' ({a.ActivityType}) has config but it is disabled — skipped.");
                    }
                }
            }

            if (config.TypeConfigs.TryGetValue(ActivityType.Custom, out var customConf) && customConf.Enabled)
            {
                if (!candidates.Any(c => c.Activity != null && c.Activity.ActivityType == ActivityType.Custom))
                {
                    if (!isUpdateLoop) Service.Log.Debug("[ActivityManager] Adding fallback Custom activity candidate.");
                    candidates.Add((null, customConf));
                }
            }

            if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Total candidates: {candidates.Count}");

            var best = candidates.OrderByDescending(x => x.Config.Priority).FirstOrDefault();

            if (best.Config == null)
            {
                if (!isUpdateLoop) Service.Log.Debug("[ActivityManager] No valid candidate found — clearing title.");
                ClearTitle();
                return;
            }

            var bestActivityType = best.Activity?.ActivityType ?? ActivityType.Custom;
            if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Best candidate: Type={bestActivityType}, Name='{best.Activity?.Name ?? "(custom)"}', Priority={best.Config.Priority}");

            if (best.Config.EnableCycling && best.Config.CycleFormats != null && best.Config.CycleFormats.Any())
            {
                if (_currentCyclingType != bestActivityType)
                {
                    _currentCyclingType = bestActivityType;
                    _lastCycleSwap = DateTime.Now;
                    _currentCycleIndex = -1;
                    if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Cycling reset for new activity type {bestActivityType}.");
                }

                double seconds = (DateTime.Now - _lastCycleSwap).TotalSeconds;
                if (seconds >= best.Config.CycleIntervalSeconds)
                {
                    int totalSteps = best.Config.CycleFormats.Count + 1;
                    _currentCycleIndex++;
                    if (_currentCycleIndex >= best.Config.CycleFormats.Count)
                    {
                        _currentCycleIndex = -1;
                    }

                    _lastCycleSwap = DateTime.Now;
                    Service.Log.Debug($"[ActivityManager] Cycle advanced to index {_currentCycleIndex}.");
                }
            }
            else
            {
                _currentCyclingType = null;
                _currentCycleIndex = -1;
            }

            string title = RenderTitle(best.Activity, best.Config, config.Replacements, isUpdateLoop);

            if (title.Length > 32)
            {
                if (!isUpdateLoop) Service.Log.Debug($"[ActivityManager] Title truncated from {title.Length} to 32 chars.");
                title = title.Substring(0, 32);
            }

            var player = _plugin.cachedLocalPlayer;
            if (player == null)
            {
                if (!isUpdateLoop) Service.Log.Warning("[ActivityManager] cachedLocalPlayer is null — cannot set title. Is the player logged in?");
                return;
            }

            if (!isUpdateLoop)
            {
                Service.Log.Info($"[ActivityManager] Setting title: \"{title}\" (Prefix={config.PrefixTitle})");
            }

            if (title != _lastLoggedTitle)
            {
                Log.Info(LogSource, $"Setting title: \"{title}\"");
                _lastLoggedTitle = title;
            }
            try
            {
                var typeConf = best.Config;
                // Only push when the rendered title actually changed — this is what keeps
                // static titles from re-broadcasting every second while still allowing
                // time-placeholder titles to tick.
                if (title != _lastBroadcastTitle)
                {
                    _honorific.SetTitle(player, title, config.PrefixTitle, typeConf.Color, typeConf.Glow, typeConf.GradientColourSet, typeConf.GradientAnimationStyle);
                    _lastBroadcastTitle = title;
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error($"[ActivityManager] Failed to set title via HonorificBridge: {ex.Message}\n{ex.StackTrace}");
                Log.Error(LogSource, $"Failed to set title: {ex.Message}");
            }
        }

        private ActivityTypeConfig GetConfigForType(ActivityType type, DiscordActivityConfig fullConfig)
        {
            if (fullConfig.TypeConfigs.TryGetValue(type, out var conf)) return conf;
            if (type == ActivityType.ListeningTo && fullConfig.TypeConfigs.TryGetValue(ActivityType.ListeningTo, out var conf2)) return conf2;
            if (type == ActivityType.ListeningTo && fullConfig.TypeConfigs.ContainsKey(ActivityType.ListeningTo)) return fullConfig.TypeConfigs[ActivityType.ListeningTo];
            return null;
        }

        private string RenderTitle(DiscordActivity act, ActivityTypeConfig config, Dictionary<string, string> replacements, bool silent)
        {
            if (config == null)
            {
                if (!silent) Service.Log.Debug("[ActivityManager] RenderTitle called with null config — returning empty.");
                return "";
            }

            string format = config.Format;
            if (config.EnableCycling && config.CycleFormats != null && _currentCycleIndex >= 0 && _currentCycleIndex < config.CycleFormats.Count)
            {
                format = config.CycleFormats[_currentCycleIndex];
                if (!silent) Service.Log.Debug($"[ActivityManager] Using cycle format [{_currentCycleIndex}]: \"{format}\"");
            }

            if (string.IsNullOrEmpty(format))
            {
                if (!silent) Service.Log.Debug("[ActivityManager] Format string is empty — returning empty title.");
                return "";
            }

            string name = act?.Name ?? "";
            string details = "";
            string state = "";
            string album = "";

            string elapsed = "";
            string duration = "";
            string timeStart = "";
            string timeEnd = "";

            if (act != null)
            {
                try
                {
                    dynamic dAct = act;

                    try { details = dAct.Details ?? ""; } catch { }
                    try { state = dAct.State ?? ""; } catch { }

                    dynamic rp = null;
                    try { rp = dAct.RichPresence; } catch { }

                    if (rp != null)
                    {
                        try { if (string.IsNullOrEmpty(details)) details = rp.Details ?? ""; } catch { }
                        try { if (string.IsNullOrEmpty(state)) state = rp.State ?? ""; } catch { }
                        try { album = rp.LargeImageText ?? ""; } catch { }

                        DateTimeOffset? start = null;
                        DateTimeOffset? end = null;

                        try { start = rp.StartTimestamp; } catch { }
                        try { end = rp.EndTimestamp; } catch { }

                        if (start != null)
                        {
                            TimeSpan diff = DateTimeOffset.UtcNow - start.Value;
                            elapsed = $"{(int)diff.TotalMinutes:D2}:{diff.Seconds:D2}";
                            timeStart = start.Value.ToLocalTime().ToString("HH:mm");
                        }

                        if (end != null)
                        {
                            TimeSpan diff = end.Value - DateTimeOffset.UtcNow;
                            if (diff.TotalSeconds > 0)
                            {
                                if (start != null)
                                {
                                    TimeSpan total = end.Value - start.Value;
                                    duration = $"{(int)total.TotalMinutes:D2}:{total.Seconds:D2}";
                                }
                                timeEnd = end.Value.ToLocalTime().ToString("HH:mm");
                            }
                        }
                    }

                    if (!silent) Service.Log.Debug($"[ActivityManager] RenderTitle extracted — name='{name}', details='{details}', state='{state}', album='{album}', elapsed='{elapsed}', duration='{duration}'");
                }
                catch (Exception ex)
                {
                    Service.Log.Error($"[ActivityManager] Extraction Error: {ex.Message}");
                }
            }
            else
            {
                if (!silent) Service.Log.Debug("[ActivityManager] RenderTitle called with null activity (custom/fallback).");
            }

            if (config != null)
            {
                if (config.TrackLimit > 0 && details.Length > config.TrackLimit)
                    details = details.Substring(0, config.TrackLimit);

                if (config.ArtistLimit > 0 && state.Length > config.ArtistLimit)
                    state = state.Substring(0, config.ArtistLimit);
            }

            string result = format
                .Replace("{name}", name)
                .Replace("{details}", details)
                .Replace("{state}", state);

            result = result.Replace("{track}", details).Replace("{artist}", state);
            result = result.Replace("{album}", album);

            result = result.Replace("{elapsed}", elapsed)
                           .Replace("{duration}", duration)
                           .Replace("{time_start}", timeStart)
                           .Replace("{time_end}", timeEnd);

            result = ApplyReplacements(result, replacements);

            result = Regex.Replace(result, @"\s+", " ").Trim();

            if (!silent) Service.Log.Debug($"[ActivityManager] RenderTitle result: \"{result}\"");
            return result;
        }

        private string ApplyReplacements(string input, Dictionary<string, string> replacements)
        {
            if (string.IsNullOrEmpty(input)) return "";
            foreach (var kvp in replacements)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;
                input = input.Replace(kvp.Key, kvp.Value);
            }
            return input;
        }

        private bool IsFilteredOut(DiscordActivity act, ActivityTypeConfig conf, bool silent)
        {
            if (conf.Filters == null || conf.Filters.Count == 0 || act == null)
                return false;

            var values = ExtractPlaceholderValues(act);

            foreach (var filter in conf.Filters)
            {
                if (string.IsNullOrEmpty(filter.Value))
                    continue;

                string key = filter.TargetPlaceholder;
                // Support {track} -> {details}, {artist} -> {state} aliases
                if (key == "{track}") key = "{details}";
                if (key == "{artist}") key = "{state}";

                if (!values.TryGetValue(key, out var fieldValue))
                    fieldValue = "";

                bool matched = filter.Mode switch
                {
                    FilterMode.Contains => fieldValue.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
                    FilterMode.Equals => fieldValue.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
                    FilterMode.StartsWith => fieldValue.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                    FilterMode.EndsWith => fieldValue.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                    FilterMode.Regex => Regex.IsMatch(fieldValue, filter.Value, RegexOptions.IgnoreCase),
                    _ => false,
                };

                if (matched)
                {
                    if (!silent) Service.Log.Debug($"[ActivityManager] Filter matched: {filter.TargetPlaceholder} {filter.Mode} '{filter.Value}' against '{fieldValue}'");
                    return true;
                }
            }

            return false;
        }

        private Dictionary<string, string> ExtractPlaceholderValues(DiscordActivity act)
        {
            var values = new Dictionary<string, string>
            {
                { "{name}", act.Name ?? "" },
                { "{details}", "" },
                { "{state}", "" },
                { "{album}", "" },
            };

            try
            {
                dynamic dAct = act;
                try { values["{details}"] = dAct.Details ?? ""; } catch { }
                try { values["{state}"] = dAct.State ?? ""; } catch { }

                dynamic rp = null;
                try { rp = dAct.RichPresence; } catch { }
                if (rp != null)
                {
                    try { if (string.IsNullOrEmpty(values["{details}"])) values["{details}"] = rp.Details ?? ""; } catch { }
                    try { if (string.IsNullOrEmpty(values["{state}"])) values["{state}"] = rp.State ?? ""; } catch { }
                    try { values["{album}"] = rp.LargeImageText ?? ""; } catch { }
                }
            }
            catch { }

            return values;
        }

        private void ClearTitle()
        {
            // Mark idle so the 1 Hz refresh loop halts until OnPresenceUpdated arms it
            // again (req 3). Always forward the clear to Honorific — do NOT dedup on
            // _lastBroadcastTitle here, so a transition from an active title (Spotify
            // paused, activity removed, config disabled) always propagates to peers via
            // Lightless Sync.
            _isIdle = true;
            var local = _plugin.cachedLocalPlayer;
            if (local != null)
            {
                _honorific.ClearTitle(local);
            }
            _lastBroadcastTitle = string.Empty;
        }

        public void Dispose()
        {
            Service.Log.Info("[ActivityManager] Disposing — unsubscribing from events.");
            // Set _disposed before unsubscribing: OnFrameworkUpdate's first check is the
            // _disposed flag, so any in-flight tick exits without touching state.
            _disposed = true;
            _hasPendingPresenceUpdate = false;
            _isIdle = true;
            _discord.OnPresenceUpdated -= OnPresenceUpdated;
            Service.Framework.Update -= OnFrameworkUpdate;
        }
    }
}
