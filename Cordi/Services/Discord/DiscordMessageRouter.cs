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

    public Task<bool> RouteExtraChatMessage(DiscordMessage message, ulong channelId)
    {
        var extraChatMapping = _plugin.Config.Chat.ExtraChatMappings.FirstOrDefault(x => x.Value.DiscordChannelId == channelId.ToString());

        if (string.IsNullOrEmpty(extraChatMapping.Key))
            return Task.FromResult(false);

        var label = extraChatMapping.Key;
        var connection = extraChatMapping.Value;

        if (connection.ExtraChatNumber <= 0)
        {
            Log.Warning(LogSource, $"ExtraChat mapping '{label}' has no channel number configured, message not sent.");
            return Task.FromResult(false);
        }

        return RelayAsync(message, true,
            content => Service.Framework.RunOnFrameworkThread(
                () => _plugin._chat.SendMessage($"/ecl{connection.ExtraChatNumber} {content}")),
            content => $"Forwarded to ExtraChat {connection.ExtraChatNumber} ({label}): {content}",
            () => $"Failed to forward message to ExtraChat {label}");
    }

    public Task<bool> RouteStandardMessage(DiscordMessage message, ulong channelId)
    {
        var mapping = _plugin.Config.Chat.Mappings.FirstOrDefault(m => m.DiscordChannelId == channelId.ToString());
        if (mapping == null) return Task.FromResult(false);

        return RelayAsync(message, mapping.TranslateEmoji,
            content => _plugin._chat.SendAsync(mapping.GameChatType, content),
            content => $"Forwarded to {mapping.GameChatType}: {content}",
            () => $"Failed to forward message to {mapping.GameChatType}");
    }

    public Task<bool> RouteTellMessage(DiscordMessage message, ulong channelId)
    {
        var tellTarget = _plugin.Config.Chat.TellThreadMappings.FirstOrDefault(x => x.Value == channelId.ToString()).Key;
        if (string.IsNullOrEmpty(tellTarget)) return Task.FromResult(false);

        var mapping = _plugin.Config.Chat.Mappings
            .FirstOrDefault(m => m.GameChatType == Dalamud.Game.Text.XivChatType.TellIncoming);

        return RelayAsync(message, mapping?.TranslateEmoji ?? true,
            content => _plugin._chat.SendTellAsync(tellTarget, content),
            content => $"Forwarded tell reply to {tellTarget}: {content}",
            () => $"Failed to forward tell reply to {tellTarget}");
    }

    private async Task<bool> RelayAsync(DiscordMessage message, bool translateEmoji, Func<string, Task> send,
        Func<string, string> succeeded, Func<string> failed)
    {
        var content = ParseForGame(message.Content, translateEmoji);
        if (string.IsNullOrWhiteSpace(content)) return false;

        try
        {
            await send(content);
        }
        catch (Exception ex)
        {
            Log.Error(LogSource, failed(), ex);
            return false;
        }

        Log.Info(LogSource, succeeded(content));

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
