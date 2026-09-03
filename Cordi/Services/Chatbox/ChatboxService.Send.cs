using System.Collections.Generic;
using Cordi.Configuration;
using Cordi.Domain;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    public bool CanSend(ChatboxChannelState? channel) =>
        channel != null
        && channel.Id != CombinedChannelId
        && (ResolveSendType(channel.Config) != XivChatType.None || IsTellChannel(channel));

    public XivChatType ResolveSendType(ChatboxChannelConfig config)
    {
        if (config.SendGameChatType != XivChatType.None)
            return config.SendGameChatType;

        var fallback = Config.LastSendChatType;
        return IsSendTargetAvailable(fallback) ? fallback : XivChatType.None;
    }

    public bool IsSendTypePinned(ChatboxChannelConfig config) =>
        config.SendGameChatType != XivChatType.None;

    private static bool IsTellChannel(ChatboxChannelState channel) =>
        channel.Config.GameChatTypes.Contains(XivChatType.TellIncoming)
        || channel.Config.GameChatTypes.Contains(XivChatType.TellOutgoing);

    public void Send(string channelId, string text, ChatboxReplyRef? reply)
    {
        if (_disposed) return;

        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0) return;

        var channel = GetChannel(channelId);
        if (channel == null || !CanSend(channel)) return;

        var useReply = reply != null && Config.EnableReplies;

        foreach (var part in SplitForSending(text))
        {
            SendToGame(channel, part, useReply ? reply : null);
            useReply = false;
        }
    }

    private List<string> SplitForSending(string text)
    {
        const int limit = ChatboxConfig.MaxMessageLength;

        if (text.Length <= limit) return new List<string> { text };

        if (!Config.SplitLongMessages)
            return new List<string> { text[..limit].TrimEnd() };

        var parts = new List<string>();
        var remaining = text;

        while (remaining.Length > limit)
        {
            var take = remaining.LastIndexOfAny(new[] { ' ', '\n' }, limit - 1);
            if (take < limit / 2) take = limit;

            var part = remaining[..take].Trim();
            if (part.Length != 0) parts.Add(part);

            remaining = remaining[take..].TrimStart();
        }

        var tail = remaining.Trim();
        if (tail.Length != 0) parts.Add(tail);

        return parts;
    }

    private void SendToGame(ChatboxChannelState channel, string text, ChatboxReplyRef? reply)
    {
        text = EncodeEmojiForGame(StripEmoteTokens(text));

        var body = reply != null ? FormatGameReply(reply, text) : text;

        if (reply != null && IsTellChannel(channel) && !IsSendTypePinned(channel.Config))
        {
            var target = ResolveTellTarget(channel, reply);
            if (string.IsNullOrEmpty(target))
            {
                PostSystemMessage(channel.Id, "Reply to a tell to choose a recipient.");
                return;
            }

            _ = _plugin._chat.SendTellAsync(target!, text);
            return;
        }

        var type = ResolveSendType(channel.Config);
        if (type == XivChatType.None)
        {
            PostSystemMessage(channel.Id, "Pick a chat channel next to the input field first.");
            return;
        }

        _ = _plugin._chat.SendAsync(type, body);
    }

    private string? ResolveTellTarget(ChatboxChannelState channel, ChatboxReplyRef? reply)
    {
        if (reply == null) return null;

        var source = reply.Seq != 0 ? channel.FindBySeq(reply.Seq) : null;
        if (source != null && !string.IsNullOrEmpty(source.AuthorName))
        {
            return string.IsNullOrEmpty(source.AuthorWorld)
                ? source.AuthorName
                : $"{source.AuthorName}@{source.AuthorWorld}";
        }

        return string.IsNullOrEmpty(reply.AuthorName) ? null : reply.AuthorName;
    }

    private string FormatGameReply(ChatboxReplyRef reply, string text)
    {
        var format = string.IsNullOrWhiteSpace(Config.GameReplyFormat)
            ? "@{name} {message}"
            : Config.GameReplyFormat;

        return format
            .Replace("{name}", reply.AuthorName)
            .Replace("{excerpt}", reply.Excerpt)
            .Replace("{message}", text);
    }

}
