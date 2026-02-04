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

[ChatType(XivChatType.CrossLinkShell1)]
public class CWLS1PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.CrossLinkShell1;
}

[ChatType(XivChatType.CrossLinkShell2)]
public class CWLS2PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.CrossLinkShell2;
}

[ChatType(XivChatType.CrossLinkShell3)]
public class CWLS3PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.CrossLinkShell3;
}

[ChatType(XivChatType.CrossLinkShell4)]
public class CWLS4PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.CrossLinkShell4;
}

[ChatType(XivChatType.CrossLinkShell5)]
public class CWLS5PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.CrossLinkShell5;
}

[ChatType(XivChatType.CrossLinkShell6)]
public class CWLS6PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.CrossLinkShell6;
}

[ChatType(XivChatType.CrossLinkShell7)]
public class CWLS7PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.CrossLinkShell7;
}

[ChatType(XivChatType.CrossLinkShell8)]
public class CWLS8PacketHandler : ChatHandlerBase
{
    public override XivChatType ChatType => XivChatType.CrossLinkShell8;
}
