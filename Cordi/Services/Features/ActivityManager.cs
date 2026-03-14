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
            if ((DateTime.Now - _lastCycleSwap).TotalSeconds < 1) return;

            ProcessPresence(_cachedPresence, isUpdateLoop: true);
        }

        private Task OnPresenceUpdated(DiscordClient sender, PresenceUpdateEventArgs e)
        {
            var targetId = _plugin.Config.ActivityConfig.TargetUserId;

            if (e.User.Id != targetId)
            {
                // Log mismatched presence updates periodically to avoid spam
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

            _cachedPresence = e.PresenceAfter;

            ProcessPresence(e.PresenceAfter, isUpdateLoop: false);
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

            if (!isUpdateLoop) Service.Log.Info($"[ActivityManager] Setting title: \"{title}\" (Prefix={config.PrefixTitle})");

            try
            {
                var typeConf = best.Config;
                _honorific.SetTitle(player, title, config.PrefixTitle, typeConf.Color, typeConf.Glow, typeConf.GradientColourSet, typeConf.GradientAnimationStyle);
            }
            catch (Exception ex)
            {
                Service.Log.Error($"[ActivityManager] Failed to set title via HonorificBridge: {ex.Message}\n{ex.StackTrace}");
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
            var local = _plugin.cachedLocalPlayer;
            if (local != null)
            {
                _honorific.ClearTitle(local);
            }
        }

        public void Dispose()
        {
            Service.Log.Info("[ActivityManager] Disposing — unsubscribing from events.");
            _discord.OnPresenceUpdated -= OnPresenceUpdated;
            Service.Framework.Update -= OnFrameworkUpdate;
        }
    }
}
