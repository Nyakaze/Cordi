using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Packets.Attributes;
using Cordi.Services.Discord;
using Cordi.Core;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Cordi.Packets.Handler.Chat.ChatTypes;

[ChatType(XivChatType.TellIncoming)]
public class TellIncommingPacketHandler : IChatHandler
{
    static readonly IPluginLog Logger = Service.Log;
    private static System.Collections.Generic.Dictionary<string, System.DateTime> LastNotificationTime = new();

    public XivChatType ChatType => XivChatType.TellIncoming;

    public async Task<Task> HandleAsync(ChatMessage msg, DiscordHandler discord, CancellationToken ct = default)
    {
        string? correspondent = null;
        PlayerPayload? playerLink = null;

        if (msg.Sender.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) is PlayerPayload pp)
        {
            playerLink = pp;
            correspondent = $"{playerLink.PlayerName}@{playerLink.World.Value.Name.ExtractText()}";
        }
        else
        {
            if (msg.Sender.TextValue.EndsWith(CordiPlugin.Plugin.cachedLocalPlayer.Name.TextValue))
            {
                Logger.Info($"hm? {CordiPlugin.Plugin.cachedLocalPlayer.Name} {msg.Message}");
            }
            playerLink = msg.Message.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) as PlayerPayload;

            if (playerLink != null)
            {
                correspondent = $"{playerLink.PlayerName}@{playerLink.World.Value.Name.ExtractText()}";
            }
        }

        if (playerLink != null && !string.IsNullOrEmpty(correspondent))
        {
            await discord.SendMessage(null, msg.Message, playerLink.PlayerName, playerLink.World.Value.Name.ExtractText(), ChatType, correspondent);

            if (CordiPlugin.Plugin.Config.Chat.EnableTellNotification
                && ulong.TryParse(CordiPlugin.Plugin.Config.Chat.TellNotificationChannelId, out var notifChannelId)
                && notifChannelId > 0)
            {
                var now = System.DateTime.UtcNow;
                if (!LastNotificationTime.TryGetValue(correspondent, out var lastTime)
                    || (now - lastTime).TotalSeconds > CordiPlugin.Plugin.Config.Chat.TellNotificationCooldownSeconds)
                {
                    LastNotificationTime[correspondent] = now;

                    string link = "";
                    if (CordiPlugin.Plugin.Config.Chat.TellThreadMappings.TryGetValue(correspondent, out var threadId))
                    {
                        link = $"<#{threadId}>";
                    }

                    string notifMsg = $"U got a Tell from {correspondent} {link}";
                    Logger.Info($"Sending Tell Notification to {notifChannelId}: {notifMsg} | correspondent: {correspondent} | link: {link}");
                    _ = discord.SendWebhookMessage(notifChannelId, notifMsg, playerLink.PlayerName, playerLink.World.Value.Name.ExtractText());
                }
            }
        }

        return Task.CompletedTask;

    }
}
