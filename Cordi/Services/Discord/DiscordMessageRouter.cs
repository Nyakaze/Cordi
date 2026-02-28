using System;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using Dalamud.Plugin.Services;
using DSharpPlus.Entities;

namespace Cordi.Services.Discord;

public class DiscordMessageRouter
{
    private static readonly IPluginLog Logger = Service.Log;
    private readonly CordiPlugin _plugin;

    public DiscordMessageRouter(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    public async Task<bool> RouteExtraChatMessage(DiscordMessage message, ulong channelId)
    {
        var extraChatMapping = _plugin.Config.Chat.ExtraChatMappings.FirstOrDefault(x => x.Value.DiscordChannelId == channelId.ToString());

        if (string.IsNullOrEmpty(extraChatMapping.Key))
            return false;

        var label = extraChatMapping.Key;
        var connection = extraChatMapping.Value;

        if (connection.ExtraChatNumber > 0)
        {
            string contentToSend = message.Content;

            try
            {
                string command = $"/ecl{connection.ExtraChatNumber} {contentToSend}";
                Logger.Info($"[DiscordMessageRouter] Routing to ExtraChat (Key: {label}, Channel: {connection.ExtraChatNumber}) for Msg {message.Id}: {command}");

                await Service.Framework.RunOnFrameworkThread(() =>
                {
                    _plugin._chat.SendMessage(command);
                });

                try { await message.DeleteAsync(); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to send ExtraChat command for {label}");
            }
        }
        else
        {
            Logger.Warning($"[DiscordMessageRouter] ExtraChat mapping found for {label} but 'Channel #' is not configured (0). Msg not sent.");
        }

        return false;
    }

    public Task<bool> RouteStandardMessage(DiscordMessage message, ulong channelId)
    {
        var mapping = _plugin.Config.Chat.Mappings.FirstOrDefault(m => m.DiscordChannelId == channelId.ToString());
        if (mapping != null)
        {
            _ = _plugin._chat.SendAsync(mapping.GameChatType, message.Content);
            Logger.Info($"Forwarding message: {message.Content} to {mapping.GameChatType}");
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> RouteTellMessage(DiscordMessage message, ulong channelId)
    {
        var tellTarget = _plugin.Config.Chat.TellThreadMappings.FirstOrDefault(x => x.Value == channelId.ToString()).Key;
        if (!string.IsNullOrEmpty(tellTarget))
        {
            _ = _plugin._chat.SendTellAsync(tellTarget, message.Content);
            Logger.Info($"Forwarding Tell reply: {message.Content} to {tellTarget}");
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
