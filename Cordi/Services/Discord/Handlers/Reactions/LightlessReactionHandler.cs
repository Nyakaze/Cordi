using System;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Core;
using Cordi.Services.Discord.Dispatch;
using DSharpPlus.EventArgs;

namespace Cordi.Services.Discord.Handlers.Reactions;

[DiscordHandler]
public class LightlessReactionHandler : IDiscordReactionHandler
{
    private readonly CordiPlugin _plugin;

    public LightlessReactionHandler(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task HandleAsync(MessageReactionAddEventArgs e, CancellationToken ct)
    {
        var monitor = _plugin.LightlessMonitor;
        if (monitor == null) return;

        Service.Log.Information(
            $"[LightlessReaction] got reaction emoji='{e.Emoji.Name}' " +
            $"channel={e.Channel?.Id} msg={e.Message?.Id} user={e.User?.Id} bot={e.User?.IsBot} " +
            $"active=({monitor.ActiveNotifyChannelId}, {monitor.ActiveNotifyMessageId})");

        if (e.User.IsBot) return;
        if (e.Emoji.Name != LightlessConnectionMonitor.ReconnectEmoji) return;
        if (e.Channel.Id != monitor.ActiveNotifyChannelId) return;
        if (e.Message.Id != monitor.ActiveNotifyMessageId) return;

        try
        {
            await monitor.HandleReconnectReactionAsync(e.Channel.Id, e.Message.Id);
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"[LightlessReactionHandler] handler failed: {ex.Message}");
        }
    }
}
