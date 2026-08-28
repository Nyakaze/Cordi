using System;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using Crovus.Events;

namespace Cordi.Services.Discord.Handlers.Reactions;

[DiscordHandler]
public class LightlessReactionHandler : IDiscordReactionHandler
{
    private readonly CordiPlugin _plugin;

    public LightlessReactionHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task HandleAsync(ReactionAddedEvent e, CancellationToken ct)
    {
        if (!LightlessConnectionMonitor.FeatureEnabled) return;

        var monitor = _plugin.LightlessMonitor;
        if (monitor == null) return;

        Service.Log.Information(
            $"[LightlessReaction] got reaction emoji='{e.Emoji.Name}' " +
            $"channel={e.ChannelId} msg={e.MessageId} user={e.UserId} bot={e.User.IsBot} " +
            $"active=({monitor.ActiveNotifyChannelId}, {monitor.ActiveNotifyMessageId})");

        if (e.Emoji.Name != LightlessConnectionMonitor.ReconnectEmoji) return;
        if (e.ChannelId != monitor.ActiveNotifyChannelId) return;
        if (e.MessageId != monitor.ActiveNotifyMessageId) return;

        try
        {
            await monitor.HandleReconnectReactionAsync(e.ChannelId, e.MessageId);
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[LightlessReactionHandler] handler failed: {ex.Message}");
        }
    }
}
