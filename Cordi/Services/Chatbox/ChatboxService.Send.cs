using System;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Domain;
using Cordi.Services.Discord;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    public bool CanSend(ChatboxChannelState? channel) =>
        channel != null
        && channel.Id != CombinedChannelId
        && (SupportsGameSend(channel) || SupportsDiscordSend(channel));

    public static bool SupportsGameSend(ChatboxChannelState channel) =>
        channel.Config.SendToGame
        && (channel.Config.SendGameChatType != XivChatType.None || IsTellChannel(channel));

    public static bool SupportsDiscordSend(ChatboxChannelState channel) =>
        !SupportsGameSend(channel) && ulong.TryParse(channel.Config.DiscordChannelId, out _);

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
        var sentToGame = false;

        if (SupportsGameSend(channel))
            sentToGame = SendToGame(channel, text, useReply ? reply : null);

        if (SupportsDiscordSend(channel))
            SendToDiscord(channel, text);

        if (!sentToGame)
            EchoLocally(channel, text, useReply ? reply : null);
    }

    private bool SendToGame(ChatboxChannelState channel, string text, ChatboxReplyRef? reply)
    {
        text = EncodeEmojiForGame(StripEmoteTokens(text));

        var body = reply != null ? FormatGameReply(reply, text) : text;

        if (IsTellChannel(channel) && channel.Config.SendGameChatType == XivChatType.None)
        {
            var target = ResolveTellTarget(channel, reply);
            if (string.IsNullOrEmpty(target))
            {
                PostSystemMessage(channel.Id, "Reply to a tell to choose a recipient.");
                return true;
            }

            _ = _plugin._chat.SendTellAsync(target!, text);
            return true;
        }

        if (channel.Config.SendGameChatType == XivChatType.None) return false;

        _ = _plugin._chat.SendAsync(channel.Config.SendGameChatType, body);
        return true;
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

    private void SendToDiscord(ChatboxChannelState channel, string text)
    {
        if (!ulong.TryParse(channel.Config.DiscordChannelId, out var discordChannelId)) return;

        var content = ConvertShortcodesForDiscord(text);
        var local = _plugin.cachedLocalPlayer;
        var sender = (Player?)_plugin.LocalPlayer.Current
                     ?? Player.FromNameWorld(
                         local?.Name.TextValue ?? "Cordi",
                         local?.HomeWorld.Value.Name.ExtractText() ?? string.Empty);

        _ = _plugin.Discord.SendWebhookMessage(discordChannelId, content, sender);
    }

    private void EchoLocally(ChatboxChannelState channel, string text, ChatboxReplyRef? reply)
    {
        var local = _plugin.cachedLocalPlayer;
        var resolver = BuildResolver(channel);
        var parsed = _parser.Parse(text, resolver);

        var entry = new ChatboxMessage
        {
            ChannelId = channel.Id,
            Origin = SupportsDiscordSend(channel) ? ChatboxOrigin.Discord : ChatboxOrigin.Game,
            AuthorName = local?.Name.TextValue ?? "You",
            AuthorWorld = local?.HomeWorld.Value.Name.ExtractText() ?? string.Empty,
            AuthorKey = "self",
            RawContent = text,
            Segments = parsed.Segments,
            IsSelf = true,
            AuthorColor = channel.Config.Color,
            OnlyEmotes = parsed.OnlyEmotes,
            Reply = reply,
            GameChatType = channel.Config.SendGameChatType,
        };

        Publish(channel, entry, notify: false);
        RequestGameAvatar(entry);
    }
}
