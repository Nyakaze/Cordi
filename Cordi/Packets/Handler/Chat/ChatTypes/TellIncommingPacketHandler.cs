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

    public XivChatType ChatType => XivChatType.TellIncoming;

    public async Task<Task> HandleAsync(ChatMessage msg, DiscordHandler discord, CancellationToken ct = default)
    {
        if (msg.Sender.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) is not PlayerPayload playerLink)
        {
            if (msg.Sender.TextValue.EndsWith(CordiPlugin.Plugin.cachedLocalPlayer.Name.TextValue))
            {
                Logger.Info($"hm? {CordiPlugin.Plugin.cachedLocalPlayer.Name} {msg.Message}");
            }
            playerLink = msg.Message.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) as PlayerPayload;

            await discord.SendMessage(null, msg.Message, playerLink.PlayerName, playerLink.World.Value.Name.ExtractText(), ChatType, $"{playerLink.PlayerName}@{playerLink.World.Value.Name.ExtractText()}");
            return Task.CompletedTask;
        }
        else
        {
            var correspondent = $"{playerLink.PlayerName}@{playerLink.World.Value.Name.ExtractText()}";
            await discord.SendMessage(null, msg.Message, playerLink.PlayerName, playerLink.World.Value.Name.ExtractText(), ChatType, correspondent);
        }


        return Task.CompletedTask;

    }
}
