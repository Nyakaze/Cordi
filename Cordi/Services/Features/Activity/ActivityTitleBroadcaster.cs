using System;
using System.Numerics;
using Cordi.Configuration;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace Cordi.Services.Activity;

public sealed class ActivityTitleBroadcaster
{
    private const double ForcedRebroadcastSeconds = 5.0;

    private readonly HonorificBridge _honorific;

    private DateTime _lastBroadcast = DateTime.MinValue;
    private DateTime _priorityUntil = DateTime.MinValue;
    private string _lastTitle = string.Empty;

    public ActivityTitleBroadcaster(HonorificBridge honorific)
    {
        _honorific = honorific;
    }

    public string LastTitle => _lastTitle;

    public bool PriorityHeld => _priorityUntil != DateTime.MinValue && DateTime.Now < _priorityUntil;

    public bool PriorityExpired => _priorityUntil != DateTime.MinValue && DateTime.Now >= _priorityUntil;

    public void Broadcast(IPlayerCharacter player, string title, DiscordActivityConfig config,
        ActivityTypeConfig typeConfig)
    {
        if (PriorityHeld) return;

        var textChanged = title != _lastTitle;
        var reassertDue = (DateTime.Now - _lastBroadcast).TotalSeconds >= ForcedRebroadcastSeconds;

        if (!textChanged && !reassertDue) return;

        _honorific.SetTitle(player, title, config.PrefixTitle, typeConfig.Color, typeConfig.Glow,
            typeConfig.GradientColourSet, typeConfig.GradientAnimationStyle);

        _lastTitle = title;
        _lastBroadcast = DateTime.Now;
    }

    public void BroadcastPriority(IPlayerCharacter player, string title, bool prefix, Vector3? color, Vector3? glow,
        int? gradientColourSet, int? gradientAnimationStyle, TimeSpan hold)
    {
        _honorific.SetTitle(player, title, prefix, color, glow, gradientColourSet, gradientAnimationStyle);

        _lastTitle = title;
        _lastBroadcast = DateTime.Now;
        _priorityUntil = DateTime.Now + hold;
    }

    public void ReleasePriority(IPlayerCharacter? player)
    {
        if (_priorityUntil == DateTime.MinValue) return;

        _priorityUntil = DateTime.MinValue;
        _lastTitle = string.Empty;
        _lastBroadcast = DateTime.MinValue;

        if (player is not null) _honorific.ClearTitle(player);
    }

    public void Clear(IPlayerCharacter? player)
    {
        if (PriorityHeld) return;

        if (player is not null) _honorific.ClearTitle(player);

        _lastTitle = string.Empty;
    }
}
