using System.Threading;
using System.Threading.Tasks;
using Cordi.Packets.Attributes;
using Cordi.Services.Discord;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace Cordi.Packets.Handler.Chat.ChatTypes;

[ChatType(XivChatType.FreeCompany)]
public class FCPacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.FreeCompany;
}
