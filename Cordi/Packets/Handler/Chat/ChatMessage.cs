using System;
using Cordi.Model;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace Cordi.Packets.Handler.Chat;

public class ChatMessage : QueuedXivEvent
{
    public XivChatType ChatType { get; set; }
    public string SenderName { get; set; }
    public string SenderWorld { get; set; }
    public SeString Sender { get; set; }
    public SeString Message { get; set; }
    public DateTime Timestamp { get; }
    
    
}
