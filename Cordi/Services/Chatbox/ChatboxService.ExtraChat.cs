using System;
using System.Collections.Generic;
using System.Linq;
using Cordi.Configuration;
using Cordi.Packets.Handler.Chat;
using Cordi.Services.Features;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    private sealed record ExtraChatLine(string Label, string Name, string World, ChatMessage Message);

    public static bool HasExtraChat(ChatboxChannelConfig config) => config.ExtraChatChannel.Length > 0;

    private bool IsExtraChatLabel(string label) =>
        _config.Chat.ExtraChatMappings.ContainsKey(label)
        || Channels.Any(c => string.Equals(c.Config.ExtraChatChannel, label, StringComparison.Ordinal));

    private ExtraChatLine? ParseExtraChat(ChatMessage message)
    {
        var content = message.Message;
        var text = content?.TextValue ?? string.Empty;
        if (content == null || text.Length < 4 || text[0] != '[') return null;

        var close = text.IndexOf(']');
        if (close < 2) return null;

        var open = text.IndexOf('<', close);
        var end = open < 0 ? -1 : text.IndexOf('>', open);
        if (end < 0) return null;

        var label = text[1..close];
        if (!IsExtraChatLabel(label)) return null;

        var headerLength = end + 1;
        while (headerLength < text.Length && text[headerLength] == ' ') headerLength++;

        var body = new List<Payload>();
        PlayerPayload? sender = null;
        var consumed = 0;

        foreach (var payload in content.Payloads)
        {
            if (consumed >= headerLength)
            {
                body.Add(payload);
                continue;
            }

            if (payload is PlayerPayload player) sender ??= player;

            var piece = new SeString(payload).TextValue;
            var remaining = headerLength - consumed;
            consumed += piece.Length;

            if (piece.Length > remaining) body.Add(new TextPayload(piece[remaining..].TrimStart(' ')));
        }

        string name;
        string world;

        if (sender != null)
        {
            name = sender.PlayerName;
            world = PayloadWorldName(sender);
        }
        else
        {
            var author = text[(open + 1)..end].Split('@', 2);
            name = author[0].Trim();
            world = author.Length > 1 ? author[1].Trim() : string.Empty;
        }

        if (world.Length == 0) world = ResolveWorldForName(name);

        var stripped = new ChatMessage
        {
            ChatType = message.ChatType,
            Message = new SeString(body),
            Sender = new SeString(new TextPayload(name)),
            SenderName = name,
            SenderWorld = world,
        };

        return new ExtraChatLine(label, name, world, stripped);
    }

    private void SendToExtraChat(ChatboxChannelState channel, string text)
    {
        var label = channel.Config.ExtraChatChannel;

        if (!ExtraChatService.IsLoaded())
        {
            PostSystemMessage(channel.Id, "ExtraChat isn't loaded, so the message wasn't sent.");
            return;
        }

        if (!_config.Chat.ExtraChatMappings.TryGetValue(label, out var connection) || connection.ExtraChatNumber <= 0)
        {
            PostSystemMessage(channel.Id, $"ExtraChat channel {label} has no channel number. Set it under Chats, ExtraChat Mappings.");
            return;
        }

        _ = _chat.SendCommandAsync($"/ecl{connection.ExtraChatNumber} {text}");
    }
}
