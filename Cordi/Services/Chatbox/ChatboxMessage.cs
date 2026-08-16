using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public enum ChatboxOrigin
{
    Game,
    Discord,
    System,
}

public enum SegmentKind
{
    Text,
    Emote,
    Mention,
    ChannelRef,
    Link,
    LineBreak,
}

public sealed class ContentSegment
{
    public SegmentKind Kind { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? Url { get; init; }
    public bool TargetsMe { get; init; }

    public static ContentSegment PlainText(string text) => new() { Kind = SegmentKind.Text, Text = text };
    public static ContentSegment Break() => new() { Kind = SegmentKind.LineBreak };
}

public sealed class ChatboxReplyRef
{
    public long Seq { get; init; }
    public ulong DiscordMessageId { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string Excerpt { get; init; } = string.Empty;
}

public sealed class ChatboxMessage
{
    public long Seq { get; set; }
    public string ChannelId { get; init; } = string.Empty;
    public ChatboxOrigin Origin { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string AuthorKey { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorWorld { get; init; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public Vector4? AuthorColor { get; init; }

    public XivChatType GameChatType { get; init; } = XivChatType.None;
    public ulong DiscordMessageId { get; init; }
    public ulong DiscordChannelId { get; init; }

    public string RawContent { get; init; } = string.Empty;
    public IReadOnlyList<ContentSegment> Segments { get; set; } = Array.Empty<ContentSegment>();
    public IReadOnlyList<string> Attachments { get; init; } = Array.Empty<string>();

    public bool MentionsMe { get; set; }
    public bool IsSelf { get; init; }
    public bool IsSystem => Origin == ChatboxOrigin.System;
    public ChatboxReplyRef? Reply { get; init; }

    public bool OnlyEmotes { get; set; }
    public bool SegmentsReady { get; set; }
    public bool FilteredAsAd { get; set; }

    public string DisplayName(Configuration.ChatboxNameStyle style)
    {
        if (string.IsNullOrEmpty(AuthorWorld))
            return AuthorName;

        return style switch
        {
            Configuration.ChatboxNameStyle.NameOnly => AuthorName,
            Configuration.ChatboxNameStyle.NameAndWorld => $"{AuthorName}@{AuthorWorld}",
            _ => AuthorName,
        };
    }
}
