using System;
using System.Threading.Tasks;
using Dalamud.Game.Text;

namespace Cordi.Packets.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ChatTypeAttribute : Attribute
{
    public XivChatType ChatType { get; }
    public ChatTypeAttribute(XivChatType chatType) => ChatType = chatType;
}
