using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

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
    GameLink,
    AutoTranslate,
    GameIcon,
}

public enum GameLinkKind
{
    None,
    Item,
    Status,
    Map,
    Quest,
    Player,
    Plugin,
    PartyFinder,
    PartyFinderNotification,
    Achievement,
}

public sealed class ContentSegment
{
    public SegmentKind Kind { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? Url { get; init; }
    public bool TargetsMe { get; init; }

    public Vector4? Color { get; set; }

    public GameLinkKind LinkKind { get; init; }
    public uint LinkId { get; init; }
    public uint IconId { get; init; }
    public Payload? Link { get; init; }

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
    public const string SystemSender = "System";

    public long Seq { get; set; }
    public string ChannelId { get; init; } = string.Empty;
    public ChatboxOrigin Origin { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string AuthorKey { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorWorld { get; init; } = string.Empty;
    public string AuthorPrefix { get; init; } = string.Empty;
    public Vector4? AuthorPrefixColor { get; init; }
    public ulong SenderContentId { get; set; }
    public ulong SenderAccountId { get; set; }
    public ushort SenderWorldId { get; set; }
    public string? AvatarUrl { get; set; }
    public Vector4? AuthorColor { get; init; }

    public XivChatType GameChatType { get; init; } = XivChatType.None;
    public ulong DiscordMessageId { get; init; }
    public ulong DiscordChannelId { get; init; }

    public string RawContent { get; init; } = string.Empty;
    public byte[]? SourcePayload { get; set; }
    public SeString? Source { get; set; }
    public IReadOnlyList<ContentSegment> Segments { get; set; } = Array.Empty<ContentSegment>();
    public IReadOnlyList<string> Attachments { get; init; } = Array.Empty<string>();

    public bool MentionsMe { get; set; }
    public bool IsSelf { get; init; }
    public bool IsSystem => Origin == ChatboxOrigin.System;
    public ChatboxReplyRef? Reply { get; init; }

    public bool IsSystemLine =>
        IsSystem
        || (Origin == ChatboxOrigin.Game
            && string.IsNullOrEmpty(AuthorWorld)
            && string.Equals(AuthorName, SystemSender, StringComparison.Ordinal));

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
