using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services.Chatbox;
using Dalamud.Game.ClientState.Objects.SubKinds;

using Cordi.Services.Features;

namespace Cordi.Services.Activity;

public sealed class EmoteTitleService : IDisposable
{
    private const string LogSource = "Activity";

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService Log;
    private readonly ActivityManager _activity;
    private readonly EmoteLogService _emoteLog;
    private readonly Domain.LocalPlayerProvider _localPlayer;
    private readonly EmoteCounterStore _counters;

    private bool _disposed;

    public EmoteTitleService(
        Configuration.Configuration config,
        CordiLogService log,
        ActivityManager activity,
        EmoteLogService emoteLog,
        Domain.LocalPlayerProvider localPlayer,
        string configDirectory)
    {
        _config = config;
        Log = log;
        _activity = activity;
        _emoteLog = emoteLog;
        _localPlayer = localPlayer;
        _counters = new EmoteCounterStore(configDirectory, log);
        _counters.Load();

        _emoteLog.EmoteObserved += OnEmoteObserved;
    }

    public EmoteCounterStore Counters => _counters;

    private EmoteTitleSettings Settings => _config.ActivityConfig.EmoteTitles;

    public void OnFrameworkUpdate()
    {
        if (_disposed) return;

        if (_activity is { PriorityTitleExpired: true })
            _activity.ReleasePriorityTitle();

        _counters.FlushIfDirty();
    }

    public void OnLogout()
    {
        _counters.ResetSession();
        _counters.FlushIfDirty();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        _emoteLog.EmoteObserved -= OnEmoteObserved;

        _counters.FlushIfDirty();
    }

    private void OnEmoteObserved(ulong instigatorAddr, ushort emoteId, ulong targetId)
    {
        if (_disposed) return;

        var settings = Settings;
        if (!settings.Enabled || settings.Rules.Count == 0) return;

        var localPlayer = _localPlayer.Character;
        if (localPlayer is null) return;

        if (Service.ObjectTable.FirstOrDefault(x => (ulong)x.Address == instigatorAddr) is not IPlayerCharacter instigator)
            return;

        if (instigator.GameObjectId == targetId && !settings.TrackSelfEmotes) return;

        EmoteDirection direction;

        if (targetId == localPlayer.GameObjectId) direction = EmoteDirection.Receiving;
        else if (instigator.GameObjectId == localPlayer.GameObjectId) direction = EmoteDirection.Giving;
        else return;

        var characterId = ChatboxPlayerActions.LocalContentId();

        if (Match(settings.Rules, emoteId, direction, characterId) is not { } rule) return;

        var ids = rule.EmoteIds.Count > 0 ? rule.EmoteIds : new List<ushort> { emoteId };

        uint total = 0;
        uint session = 0;
        uint combo = 0;

        foreach (var id in ids)
        {
            var key = new EmoteCounterKey(characterId, direction, id);

            if (id == emoteId) _counters.Increment(key);

            total += _counters.GetTotal(key);
            session += _counters.GetSession(key);
            combo += _counters.GetCombo(key);
        }

        _counters.FlushIfDirty();

        var partner = direction == EmoteDirection.Receiving
            ? instigator.Name.ToString()
            : PartnerName(targetId);

        var title = Render(rule, total, session, combo, emoteId, partner);

        if (string.IsNullOrWhiteSpace(title)) return;

        var hold = TimeSpan.FromSeconds(Math.Clamp(
            settings.DisplaySeconds,
            EmoteTitleSettings.MinDisplaySeconds,
            EmoteTitleSettings.MaxDisplaySeconds));

        try
        {
            _activity.PushPriorityTitle(localPlayer, title, rule, hold);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, "Failed to push the emote title", ex);
        }
    }

    private static string PartnerName(ulong targetId)
    {
        if (Service.ObjectTable.FirstOrDefault(x => x.GameObjectId == targetId) is IPlayerCharacter target)
            return target.Name.ToString();

        return string.Empty;
    }

    internal static EmoteTitleRule? Match(IReadOnlyList<EmoteTitleRule> rules, ushort emoteId,
        EmoteDirection direction, ulong characterId)
    {
        EmoteTitleRule? best = null;
        var bestScoped = false;
        var bestBreadth = int.MaxValue;

        foreach (var rule in rules)
        {
            if (!rule.Enabled) continue;
            if (rule.Direction != direction) continue;
            if (rule.EmoteIds.Count > 0 && !rule.EmoteIds.Contains(emoteId)) continue;

            var scoped = rule.CharacterIds.Count > 0;
            if (scoped && !rule.CharacterIds.Contains(characterId)) continue;

            var breadth = rule.EmoteIds.Count == 0 ? int.MaxValue - 1 : rule.EmoteIds.Count;

            if (best is not null)
            {
                if (bestScoped && !scoped) continue;
                if (bestScoped == scoped && breadth >= bestBreadth) continue;
            }

            best = rule;
            bestScoped = scoped;
            bestBreadth = breadth;
        }

        return best;
    }

    internal string Render(EmoteTitleRule rule, uint total, uint session, uint combo, ushort emoteId, string partner)
    {
        var format = rule.Format;
        if (string.IsNullOrWhiteSpace(format)) return string.Empty;

        var builder = new StringBuilder(format);

        builder.Replace("{total}", total.ToString());
        builder.Replace("{session}", session.ToString());
        builder.Replace("{combo}", combo.ToString());
        builder.Replace("{emote}", EmoteCatalog.NameFor(emoteId));
        builder.Replace("{player}", partner);

        var rendered = ActivityText.ApplyReplacements(
            ActivityText.CollapseWhitespace(builder.ToString()),
            _config.ActivityConfig.Replacements);

        return ActivityText.Truncate(rendered, DiscordActivityConfig.MaxTitleLength);
    }

    public string Preview(EmoteTitleRule rule)
    {
        var emoteId = rule.EmoteIds.Count > 0 ? rule.EmoteIds[0] : (ushort)0;
        var characterId = ChatboxPlayerActions.LocalContentId();

        uint total = 0;

        foreach (var id in rule.EmoteIds)
            total += _counters.GetTotal(new EmoteCounterKey(characterId, rule.Direction, id));

        return Render(rule, total, 1, 1, emoteId, _localPlayer.Character?.Name.ToString() ?? "Someone");
    }
}
