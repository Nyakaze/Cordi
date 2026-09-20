using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cordi.Core;
using Crovus.Models;

namespace Cordi.Services.Discord;

public class DiscordMessageRouter
{
    private const string LogSource = "Relay";
    private const int MaxRelayLength = 400;

    private readonly Configuration.Configuration _config;
    private readonly CordiLogService Log;
    private readonly Emojis.EmojiTranslator _emoji;
    private readonly Packets.Handler.Chat.ChatMessenger _chat;
    private readonly Lazy<Chatbox.ChatboxService> _chatbox;

    public DiscordMessageRouter(
        Configuration.Configuration config,
        CordiLogService log,
        Emojis.EmojiTranslator emoji,
        Packets.Handler.Chat.ChatMessenger chat,
        Lazy<Chatbox.ChatboxService> chatbox)
    {
        _config = config;
        Log = log;
        _emoji = emoji;
        _chat = chat;
        _chatbox = chatbox;
    }

    private string ParseForGame(DiscordMessage message, bool translateEmoji)
    {
        var content = message.Content;
        _chatbox.Value.RegisterEmotes(content);

        var parsed = translateEmoji
            ? _emoji.ToGame(content, _config.Chatbox.RelayEmotesAsUrls)
            : content ?? string.Empty;

        return AppendAttachments(parsed, message.Attachments);
    }

    private static string AppendAttachments(string content, IReadOnlyList<DiscordAttachment>? attachments)
    {
        if (attachments == null || attachments.Count == 0) return content;

        var builder = new StringBuilder(content.TrimEnd());

        foreach (var attachment in attachments)
        {
            var url = attachment.Url;
            if (string.IsNullOrWhiteSpace(url)) continue;

            var extra = builder.Length > 0 ? url.Length + 1 : url.Length;
            if (builder.Length + extra > MaxRelayLength) break;

            if (builder.Length > 0) builder.Append(' ');
            builder.Append(url);
        }

        return builder.ToString();
    }

    public Task<bool> RouteExtraChatMessage(DiscordMessage message, ulong channelId)
    {
        var extraChatMapping = _config.Chat.ExtraChatMappings.FirstOrDefault(x => x.Value.DiscordChannelId == channelId.ToString());

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
                () => _chat.SendMessage($"/ecl{connection.ExtraChatNumber} {content}")),
            content => $"Forwarded to ExtraChat {connection.ExtraChatNumber} ({label}): {content}",
            () => $"Failed to forward message to ExtraChat {label}");
    }

    public Task<bool> RouteStandardMessage(DiscordMessage message, ulong channelId)
    {
        var mapping = _config.Chat.Mappings.FirstOrDefault(m => m.DiscordChannelId == channelId.ToString());
        if (mapping == null) return Task.FromResult(false);

        return RelayAsync(message, mapping.TranslateEmoji,
            content => _chat.SendAsync(mapping.GameChatType, content),
            content => $"Forwarded to {mapping.GameChatType}: {content}",
            () => $"Failed to forward message to {mapping.GameChatType}");
    }

    public Task<bool> RouteTellMessage(DiscordMessage message, ulong channelId)
    {
        var tellTarget = _config.Chat.TellThreadMappings.FirstOrDefault(x => x.Value == channelId.ToString()).Key;
        if (string.IsNullOrEmpty(tellTarget)) return Task.FromResult(false);

        var mapping = _config.Chat.Mappings
            .FirstOrDefault(m => m.GameChatType == Dalamud.Game.Text.XivChatType.TellIncoming);

        return RelayAsync(message, mapping?.TranslateEmoji ?? true,
            content => _chat.SendTellAsync(tellTarget, content),
            content => $"Forwarded tell reply to {tellTarget}: {content}",
            () => $"Failed to forward tell reply to {tellTarget}");
    }

    private async Task<bool> RelayAsync(DiscordMessage message, bool translateEmoji, Func<string, Task> send,
        Func<string, string> succeeded, Func<string> failed)
    {
        var content = ParseForGame(message, translateEmoji);
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
