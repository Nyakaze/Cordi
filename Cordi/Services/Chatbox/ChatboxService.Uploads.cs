using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cordi.Configuration;
using Crovus.Models;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public sealed record ChatboxUpload(string FileName, byte[] Bytes);

public sealed partial class ChatboxService
{
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    public const int MaxUploads = 4;
    public const int UploadLinkLength = 185;

    private const string UploadUnavailable =
        "Images can only be sent in a chat that is mapped to a Discord channel, while Discord is connected.";

    private static readonly string[] UploadExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    private readonly record struct UploadTarget(ulong? ChannelId, XivChatType Type, string? Correspondent);

    public static bool IsUploadFile(string fileName) =>
        UploadExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    public bool UploadsConnected => _connection.Context is not null;

    public bool CanUpload(ChatboxChannelState? channel) =>
        channel != null
        && ResolveUploadTarget(channel, null) is { } target
        && _sender.CanSendFiles(target.ChannelId, target.Type);

    private UploadTarget? ResolveUploadTarget(ChatboxChannelState channel, ChatboxReplyRef? reply)
    {
        if (IsConversationId(channel.Id))
            return TellUploadTarget(ConversationTargetFor(channel));

        if (HasExtraChat(channel.Config))
        {
            return _config.Chat.ExtraChatMappings.TryGetValue(channel.Config.ExtraChatChannel, out var connection)
                   && ulong.TryParse(connection.DiscordChannelId, out var id)
                ? new UploadTarget(id, XivChatType.Debug, null)
                : null;
        }

        if (reply != null && IsTellChannel(channel) && !IsSendTypePinned(channel.Config))
            return TellUploadTarget(ResolveTellTarget(channel, reply));

        var type = ResolveSendType(channel.Config);

        return type == XivChatType.None ? null : new UploadTarget(null, type, null);
    }

    private static UploadTarget? TellUploadTarget(string? recipient) =>
        !string.IsNullOrEmpty(recipient) && recipient.Contains('@')
            ? new UploadTarget(null, XivChatType.TellOutgoing, recipient)
            : null;

    public int OutgoingLength(ChatboxChannelState channel, string text, ChatboxReplyRef? reply)
    {
        text = text.Trim();
        if (text.Length == 0) return 0;

        return text[0] == '/'
            ? _emoji.ToGame(text, Config.RelayEmotesAsUrls).Length
            : GameBody(channel, text, reply).Length;
    }

    private string GameBody(ChatboxChannelState channel, string text, ChatboxReplyRef? reply)
    {
        var game = _emoji.ToGame(text, Config.RelayEmotesAsUrls);

        if (reply == null || !Config.EnableReplies || game.Length == 0) return game;
        if (IsConversationId(channel.Id)) return game;
        if (!HasExtraChat(channel.Config) && IsTellChannel(channel) && !IsSendTypePinned(channel.Config)) return game;

        return FormatGameReply(reply, game);
    }

    private async Task SendUploadsAsync(
        ChatboxChannelState channel, string text, ChatboxReplyRef? reply, IReadOnlyList<ChatboxUpload> uploads)
    {
        try
        {
            var sender = _localPlayer.Current;

            if (ResolveUploadTarget(channel, reply) is not { } target
                || sender == null
                || !_sender.CanSendFiles(target.ChannelId, target.Type))
            {
                PostSystemMessage(channel.Id, UploadUnavailable);
                return;
            }

            var caption = text.Length == 0 ? string.Empty : GameBody(channel, text, reply);
            var joined = caption.Length > 0
                         && caption.Length + 1 + UploadLinkLength <= ChatboxConfig.MaxMessageLength;

            if (caption.Length > 0 && !joined)
            {
                var useReply = reply;

                foreach (var part in SplitForSending(_emoji.ToGame(text, Config.RelayEmotesAsUrls)))
                {
                    SendToGame(channel, part, useReply);
                    useReply = null;
                }
            }

            var files = uploads
                .Select((upload, index) => DiscordFile.FromBytes(UploadName(upload.FileName, index), upload.Bytes))
                .ToList();

            var links = await _sender.SendFiles(
                target.ChannelId, joined ? caption : string.Empty, sender, files, target.Type, target.Correspondent);

            for (var i = 0; i < links.Count && i < uploads.Count; i++)
                ImageCache.Seed(links[i], uploads[i].Bytes);

            await Service.Framework.RunOnFrameworkThread(() =>
            {
                if (links.Count == 0)
                {
                    PostSystemMessage(channel.Id, "The upload to Discord failed, so the image wasn't sent.");
                    return;
                }

                SendUploadLinks(channel, joined ? text : string.Empty, joined ? caption.Length : 0, joined ? reply : null, links);
            });
        }
        catch (Exception ex)
        {
            _log.Error("Chatbox", "Sending images failed", ex);
        }
    }

    private void SendUploadLinks(
        ChatboxChannelState channel, string line, int length, ChatboxReplyRef? reply, IReadOnlyList<string> links)
    {
        foreach (var link in links)
        {
            if (length == 0)
            {
                line = link;
                length = link.Length;
                continue;
            }

            if (length + 1 + link.Length <= ChatboxConfig.MaxMessageLength)
            {
                line = $"{line} {link}";
                length += 1 + link.Length;
                continue;
            }

            SendToGame(channel, line, reply);
            reply = null;
            line = link;
            length = link.Length;
        }

        SendToGame(channel, line, reply);
    }

    private static string UploadName(string fileName, int index)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return index == 0 ? $"image{extension}" : $"image{index + 1}{extension}";
    }
}
