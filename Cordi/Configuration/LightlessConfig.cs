using System;

namespace Cordi.Configuration;

[Serializable]
public class LightlessConfig
{
    public bool Enabled { get; set; } = false;

    // Channel that holds the persistent status embed and receives disconnect notifications.
    public string DiscordChannelId { get; set; } = string.Empty;

    // Persistent status embed — kept up to date in place. 0 if not posted yet.
    public ulong StatusMessageId { get; set; }

    // Separate disconnect alert message — created on transition into Disconnected,
    // deleted on transition back to Connected. 0 when no alert is currently posted.
    public ulong DisconnectMessageId { get; set; }

    // Automatically attempt to reconnect to the Lightless server when connection drops.
    public bool AutoReconnect { get; set; } = false;
}
