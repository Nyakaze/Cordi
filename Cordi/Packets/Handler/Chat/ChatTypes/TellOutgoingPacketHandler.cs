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

[ChatType(XivChatType.TellOutgoing)]
public class TellOutgoingPacketHandler : IChatHandler
{
    static readonly IPluginLog Logger = Service.Log;

    public XivChatType ChatType => XivChatType.TellOutgoing;

    public async Task<Task> HandleAsync(ChatMessage msg, DiscordHandler discord, CancellationToken ct = default)
    {


        string correspondentName = msg.SenderName;
        string correspondentWorld = msg.SenderWorld;


        if (msg.Message.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) is PlayerPayload playerLink)
        {
            correspondentName = playerLink.PlayerName;
            correspondentWorld = playerLink.World.Value.Name.ExtractText();
        }
        else if (msg.Sender.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) is PlayerPayload senderLink)
        {

            correspondentName = senderLink.PlayerName;
            correspondentWorld = senderLink.World.Value.Name.ExtractText();
        }

        var correspondent = $"{correspondentName}@{correspondentWorld}";
        var localName = CordiPlugin.Plugin.cachedLocalPlayer?.Name.TextValue ?? "Unknown";
        var localWorld = CordiPlugin.Plugin.cachedLocalPlayer?.HomeWorld.Value.Name.ExtractText() ?? "Unknown";

        await discord.SendMessage(null, msg.Message, localName, localWorld, ChatType, correspondent);

        return Task.CompletedTask;



    }
}
