using System;
using Dalamud.Game.Text;

namespace Cordi.Configuration;

[Serializable]
public class ChannelMapping
{
    public string DiscordChannelId { get; set; } = string.Empty;
    public XivChatType GameChatType { get; set; } = XivChatType.None;
    public bool EnableAdvertisementFilter { get; set; } = false;
}
