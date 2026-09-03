using System;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Core;
using Crovus.Models;

namespace Cordi.Services.Discord;

public class DiscordMessageRouter
{
    private const string LogSource = "Relay";

    private readonly CordiPlugin _plugin;

    public DiscordMessageRouter(CordiPlugin plugin)
    {
        _plugin = plugin;
    }

    private CordiLogService Log => _plugin.LogService;

    private string ParseForGame(string? content, bool translateEmoji)
    {
        _plugin.Chatbox?.RegisterEmotes(content);

        return translateEmoji
            ? _plugin.Emoji.ToGame(content, _plugin.Config.Chatbox.RelayEmotesAsUrls)
            : content ?? string.Empty;
    }

    public async Task<bool> RouteExtraChatMessage(DiscordMessage message, ulong channelId)
    {
        var extraChatMapping = _plugin.Config.Chat.ExtraChatMappings.FirstOrDefault(x => x.Value.DiscordChannelId == channelId.ToString());

        if (string.IsNullOrEmpty(extraChatMapping.Key))
            return false;

        var label = extraChatMapping.Key;
        var connection = extraChatMapping.Value;

        if (connection.ExtraChatNumber <= 0)
        {
            Log.Warning(LogSource, $"ExtraChat mapping '{label}' has no channel number configured, message not sent.");
            return false;
        }

        var content = ParseForGame(message.Content, true);
        if (string.IsNullOrWhiteSpace(content)) return false;

        try
        {
            var command = $"/ecl{connection.ExtraChatNumber} {content}";

            await Service.Framework.RunOnFrameworkThread(() => _plugin._chat.SendMessage(command));

            Log.Info(LogSource, $"Forwarded to ExtraChat {connection.ExtraChatNumber} ({label}): {content}");

            await DeleteAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to forward message to ExtraChat {label}", ex);
            return false;
        }
    }

    public async Task<bool> RouteStandardMessage(DiscordMessage message, ulong channelId)
    {
        var mapping = _plugin.Config.Chat.Mappings.FirstOrDefault(m => m.DiscordChannelId == channelId.ToString());
        if (mapping == null) return false;

        var content = ParseForGame(message.Content, mapping.TranslateEmoji);
        if (string.IsNullOrWhiteSpace(content)) return false;

        try
        {
            await _plugin._chat.SendAsync(mapping.GameChatType, content);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to forward message to {mapping.GameChatType}", ex);
            return false;
        }

        Log.Info(LogSource, $"Forwarded to {mapping.GameChatType}: {content}");

        await DeleteAsync(message);
        return true;
    }

    public async Task<bool> RouteTellMessage(DiscordMessage message, ulong channelId)
    {
        var tellTarget = _plugin.Config.Chat.TellThreadMappings.FirstOrDefault(x => x.Value == channelId.ToString()).Key;
        if (string.IsNullOrEmpty(tellTarget)) return false;

        var mapping = _plugin.Config.Chat.Mappings
            .FirstOrDefault(m => m.GameChatType == Dalamud.Game.Text.XivChatType.TellIncoming);

        var content = ParseForGame(message.Content, mapping?.TranslateEmoji ?? true);
        if (string.IsNullOrWhiteSpace(content)) return false;

        try
        {
            await _plugin._chat.SendTellAsync(tellTarget, content);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, $"Failed to forward tell reply to {tellTarget}", ex);
            return false;
        }

        Log.Info(LogSource, $"Forwarded tell reply to {tellTarget}: {content}");

        await DeleteAsync(message);
        return true;
    }

    private async Task DeleteAsync(DiscordMessage message)
    {
        try
        {
            await message.DeleteAsync();
        }
        catch (Exception ex)
        {
            Log.Debug(LogSource, $"Could not delete relayed message {message.Id}: {ex.Message}");
        }
    }
}
