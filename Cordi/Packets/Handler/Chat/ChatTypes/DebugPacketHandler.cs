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
using Cordi.Configuration;

namespace Cordi.Packets.Handler.Chat.ChatTypes;

[ChatType(XivChatType.Debug)]
public class DebugPacketHandler : IChatHandler
{
    static readonly IPluginLog Logger = Service.Log;
    public XivChatType ChatType => XivChatType.Debug;

    public async Task<Task> HandleAsync(ChatMessage msg, DiscordHandler discord, CancellationToken ct = default)
    {
        var (label, senderName, content) = ParseDebugMessage(msg.Message.TextValue);

        if (string.IsNullOrEmpty(label)) return Task.CompletedTask;

        var config = CordiPlugin.Plugin.Config.Chat;

        if (!config.ExtraChatMappings.ContainsKey(label))
        {
            config.ExtraChatMappings[label] = new Configuration.ExtraChatConnection();
            CordiPlugin.Plugin.Config.Save();
            CordiPlugin.Plugin.NotificationManager.Add("New Channel Discovered!", $"Found ExtraChat channel: {label}. Go to Chat settings to map it.", Cordi.Services.CordiNotificationType.Info);
        }
        if (config.ExtraChatMappings.TryGetValue(label, out var connection) && !string.IsNullOrEmpty(connection.DiscordChannelId))
        {
            string world = "";
            string name = senderName;
            if (name.Contains("@"))
            {
                var parts = name.Split('@', 2);
                name = parts[0];
                world = parts[1];
            }

            string? avatarUrl = null;
            if (config.CustomAvatars.TryGetValue($"{name}@{world}", out var url)) avatarUrl = url;
            else if (config.CustomAvatars.TryGetValue($"{name}@", out url)) avatarUrl = url;
            else if (config.CustomAvatars.TryGetValue(name, out url)) avatarUrl = url;
            if (string.IsNullOrEmpty(avatarUrl) && string.IsNullOrEmpty(world))
            {
                var fuzzyKey = config.CustomAvatars.Keys.FirstOrDefault(k => k.StartsWith($"{name}@"));
                if (fuzzyKey != null)
                {
                    avatarUrl = config.CustomAvatars[fuzzyKey];
                    var parts = fuzzyKey.Split('@', 2);
                    if (parts.Length > 1) world = parts[1];
                }
                if (string.IsNullOrEmpty(avatarUrl))
                {
                    var lodestone = CordiPlugin.Plugin.Lodestone;
                    var cachedKey = lodestone.AvatarCache.Keys.FirstOrDefault(k => k.StartsWith($"{name}@"));
                    if (cachedKey != null)
                    {
                        avatarUrl = lodestone.AvatarCache[cachedKey];
                        var parts = cachedKey.Split('@', 2);
                        if (parts.Length > 1) world = parts[1];
                    }
                }
                if (string.IsNullOrEmpty(avatarUrl))
                {
                    var idCache = CordiPlugin.Plugin.Config.Lodestone.CharacterIdCache;
                    var cachedKey = idCache.Keys.FirstOrDefault(k => k.StartsWith($"{name}@"));

                    if (cachedKey != null)
                    {
                        var parts = cachedKey.Split('@', 2);
                        if (parts.Length > 1)
                        {
                            world = parts[1];
                            avatarUrl = await CordiPlugin.Plugin.Lodestone.GetAvatarUrlAsync(name, world);
                        }
                    }
                }
            }

            if (ulong.TryParse(connection.DiscordChannelId, out var channelId))
            {
                try
                {
                    var channel = await discord.Client.GetChannelAsync(channelId);
                    await discord.SendMessage(channel, content, name, world, XivChatType.Debug, avatarUrl: avatarUrl);
                }
                catch (System.Exception ex)
                {
                    Logger.Error(ex, $"Failed to send ExtraChat message to Discord channel {channelId}");
                }
            }
        }

        return Task.CompletedTask;
    }

    private (string label, string senderName, string content) ParseDebugMessage(string input)
    {

        int endBracket = input.IndexOf(']');
        int startAngle = input.IndexOf('<');
        int endAngle = input.IndexOf('>');

        if (endBracket == -1 || startAngle == -1 || endAngle == -1)
            return ("", "", "");

        string type = input.Substring(1, endBracket - 1);

        string name = input.Substring(startAngle + 1, endAngle - startAngle - 1);

        string msg = input.Substring(endAngle + 1).TrimStart();

        return (type, name, msg);
    }
}
