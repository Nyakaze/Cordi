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

        private DiscordPresence _cachedPresence;

        public ActivityManager(CordiPlugin plugin, DiscordHandler discord, HonorificBridge honorific)
        {
            _plugin = plugin;
            _discord = discord;
            _honorific = honorific;

            _discord.OnPresenceUpdated += OnPresenceUpdated;
            Service.Framework.Update += OnFrameworkUpdate;
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if ((DateTime.Now - _lastCycleSwap).TotalSeconds < 1) return;

            ProcessPresence(_cachedPresence, isUpdateLoop: true);
        }

        private Task OnPresenceUpdated(DiscordClient sender, PresenceUpdateEventArgs e)
        {
            if (e.User.Id != _plugin.Config.ActivityConfig.TargetUserId)
            {
                return Task.CompletedTask;
            }

            _cachedPresence = e.PresenceAfter;

            ProcessPresence(e.PresenceAfter, isUpdateLoop: false);
            return Task.CompletedTask;
        }

        private void ProcessPresence(DiscordPresence presence, bool isUpdateLoop)
        {
            if (presence == null || presence.Activities == null || !presence.Activities.Any())
            {
                ClearTitle();
                return;
            }

            var config = _plugin.Config.ActivityConfig;
            if (!config.Enabled)
            {
                ClearTitle();
                return;
            }

            var candidates = presence.Activities
                .Select(a =>
                {
                    ActivityTypeConfig conf = null;
                    // Check for Game Override first (Only for Playing activities)
                    if (a.ActivityType == ActivityType.Playing && !string.IsNullOrEmpty(a.Name) && config.GameConfigs.TryGetValue(a.Name, out var gameConf))
                    {
                        conf = gameConf;
                    }
                    else
                    {
                        conf = GetConfigForType(a.ActivityType, config);
                    }
                    return new { Activity = a, Config = conf };
                })
                .Where(x => x.Config != null && x.Config.Enabled)
                .OrderByDescending(x => x.Config.Priority)
                .ToList();

            var best = candidates.FirstOrDefault();

            if (best == null)
            {
                ClearTitle();
                return;
            }

            if (best.Config.EnableCycling && best.Config.CycleFormats != null && best.Config.CycleFormats.Any())
            {
                if (_currentCyclingType != best.Activity.ActivityType)
                {
                    _currentCyclingType = best.Activity.ActivityType;
                    _lastCycleSwap = DateTime.Now;
                    _currentCycleIndex = -1;
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
                title = title.Substring(0, 32);
            }

            var player = _plugin.cachedLocalPlayer;
            if (player != null)
            {
                var typeConf = best.Config;
                _honorific.SetTitle(player, title, config.PrefixTitle, typeConf.Color, typeConf.Glow);
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
            if (config == null) return "";

            string format = config.Format;
            if (config.EnableCycling && config.CycleFormats != null && _currentCycleIndex >= 0 && _currentCycleIndex < config.CycleFormats.Count)
            {
                format = config.CycleFormats[_currentCycleIndex];
            }

            if (string.IsNullOrEmpty(format)) return "";

            string name = act.Name ?? "";
            string details = "";
            string state = "";
            string album = "";

            string elapsed = "";
            string duration = "";
            string timeStart = "";
            string timeEnd = "";

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
            }
            catch (Exception ex)
            {
                if (!silent) Service.Log.Error($"[ActivityManager] Extraction Error: {ex.Message}");
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

        private void ClearTitle()
        {
            var local = _plugin.cachedLocalPlayer;
            if (local != null) _honorific.ClearTitle(local);
        }

        public void Dispose()
        {
            _discord.OnPresenceUpdated -= OnPresenceUpdated;
            Service.Framework.Update -= OnFrameworkUpdate;
        }
    }
}
