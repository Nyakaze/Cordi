using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Packets.Attributes;
using Cordi.Services.Discord;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Cordi.Packets.Handler.Chat.ChatTypes;

[ChatType(XivChatType.Ls1)]
public class LS1PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.Ls1;
}

[ChatType(XivChatType.Ls2)]
public class LS2PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.Ls2;
}

[ChatType(XivChatType.Ls3)]
public class LS3PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.Ls3;
}

[ChatType(XivChatType.Ls4)]
public class LS4PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.Ls4;
}

[ChatType(XivChatType.Ls5)]
public class LS5PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.Ls5;
}

[ChatType(XivChatType.Ls6)]
public class LS6PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.Ls6;
}

[ChatType(XivChatType.Ls7)]
public class LS7PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.Ls7;
}

[ChatType(XivChatType.Ls8)]
public class LS8PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.Ls8;
}
