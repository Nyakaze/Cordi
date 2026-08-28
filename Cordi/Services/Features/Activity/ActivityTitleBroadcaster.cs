using System;
using Cordi.Configuration;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace Cordi.Services.Activity;

public sealed class ActivityTitleBroadcaster
{
    private const double ForcedRebroadcastSeconds = 5.0;

    private readonly HonorificBridge _honorific;

    private DateTime _lastBroadcast = DateTime.MinValue;
    private string _lastTitle = string.Empty;

    public ActivityTitleBroadcaster(HonorificBridge honorific)
    {
        _honorific = honorific;
    }

    public string LastTitle => _lastTitle;

    public void Broadcast(IPlayerCharacter player, string title, DiscordActivityConfig config,
        ActivityTypeConfig typeConfig)
    {
        var textChanged = title != _lastTitle;
        var reassertDue = (DateTime.Now - _lastBroadcast).TotalSeconds >= ForcedRebroadcastSeconds;

        if (!textChanged && !reassertDue) return;

        _honorific.SetTitle(player, title, config.PrefixTitle, typeConfig.Color, typeConfig.Glow,
            typeConfig.GradientColourSet, typeConfig.GradientAnimationStyle);

        _lastTitle = title;
        _lastBroadcast = DateTime.Now;
    }

    public void Clear(IPlayerCharacter? player)
    {
        if (player is not null) _honorific.ClearTitle(player);

        _lastTitle = string.Empty;
    }
}
