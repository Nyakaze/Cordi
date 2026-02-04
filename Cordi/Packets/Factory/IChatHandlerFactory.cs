using Cordi.Packets.Handler.Chat;
using Cordi.Packets.Handler;
using Dalamud.Game.Text;

namespace Cordi.Packets.Factory;

public interface IChatHandlerFactory
{
    IChatHandler Get(XivChatType type);
}
