using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;

namespace Cordi.Configuration;

public enum ChatboxLayout
{
    Cozy,
    Compact,
}

public enum ChatboxNavStyle
{
    ServerRail,
    ChannelList,
    Tabs,
    Hidden,
}

public enum ChatboxNavSide
{
    Left,
    Right,
}

public enum ChatboxTabSide
{
    Top,
    Bottom,
}

public enum ChatboxTimestampStyle
{
    None,
    Time,
    TimeWithSeconds,
    DateAndTime,
    Relative,
}

public enum ChatboxNameStyle
{
    NameOnly,
    NameAndWorld,
}

[Serializable]
public class ChatboxChannelConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Channel";
    public string ShortLabel { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public Vector4 Color { get; set; } = new(0.45f, 0.55f, 0.95f, 1f);
    public bool Enabled { get; set; } = true;
    public bool ShowInNav { get; set; } = true;
    public int Order { get; set; }

    public List<XivChatType> GameChatTypes { get; set; } = new();
    public string DiscordChannelId { get; set; } = string.Empty;

    public bool SendToGame { get; set; } = true;
    public XivChatType SendGameChatType { get; set; } = XivChatType.None;

    public bool MuteNotifications { get; set; }
    public bool TreatAllAsMention { get; set; }
    public bool IncludeInCombined { get; set; } = true;

    public int MaxMessages { get; set; } = 5000;
    public bool PersistHistory { get; set; } = true;
    public bool FilterAdvertisements { get; set; } = true;
}

[Serializable]
public class ChatboxConfig
{
    public bool Enabled { get; set; }
    public bool OpenOnLogin { get; set; }

    public bool HideTitleBar { get; set; }
    public bool WindowLockPosition { get; set; }
    public bool WindowLockSize { get; set; }
    public bool IgnoreEsc { get; set; }
    public bool ClickThroughWhenUnfocused { get; set; }
    public float BackgroundOpacity { get; set; } = 1.0f;
    public bool HideWhenNotLoggedIn { get; set; } = true;
    public bool HideGameChat { get; set; }
    public bool CaptureEnterKey { get; set; }

    public ChatboxLayout Layout { get; set; } = ChatboxLayout.Cozy;
    public ChatboxNavStyle NavStyle { get; set; } = ChatboxNavStyle.ServerRail;
    public ChatboxNavSide NavSide { get; set; } = ChatboxNavSide.Left;
    public ChatboxTabSide TabSide { get; set; } = ChatboxTabSide.Top;
    public float NavWidth { get; set; } = 170f;
    public float RailWidth { get; set; } = 68f;
    public float TabWidth { get; set; }
    public float RailIconSize { get; set; } = 44f;
    public bool ResizableNav { get; set; } = true;

    public bool ShowAvatars { get; set; } = true;
    public float AvatarSize { get; set; } = 40f;
    public bool RoundAvatars { get; set; } = true;
    public bool ShowLodestoneAvatars { get; set; } = true;

    public bool GroupConsecutive { get; set; } = true;
    public int GroupWindowSeconds { get; set; } = 420;
    public float MessageSpacing { get; set; } = 6f;
    public float LineSpacing { get; set; } = 2f;

    public ChatboxTimestampStyle Timestamps { get; set; } = ChatboxTimestampStyle.Time;
    public ChatboxNameStyle NameStyle { get; set; } = ChatboxNameStyle.NameAndWorld;
    public bool ColorNamesByChannel { get; set; } = true;
    public bool ShowNewMessageDivider { get; set; } = true;
    public bool ShowHoverToolbar { get; set; } = true;
    public bool AutoScroll { get; set; } = true;

    public bool ShowUnreadDot { get; set; } = true;
    public bool ShowMentionBadge { get; set; } = true;
    public bool ShowCombinedChannel { get; set; } = true;
    public string CombinedChannelName { get; set; } = "All";

    public bool RenderCustomEmotes { get; set; } = true;
    public bool RenderUnicodeEmoji { get; set; } = true;
    public bool RenderShortcodes { get; set; } = true;
    public bool RenderGuildEmotesByName { get; set; } = true;
    public float EmoteScale { get; set; } = 1.35f;
    public bool JumboLoneEmotes { get; set; } = true;
    public float JumboEmoteScale { get; set; } = 3.0f;
    public string TwemojiBaseUrl { get; set; } = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/72x72";

    public bool EnableMentions { get; set; } = true;
    public bool MentionEveryone { get; set; } = true;
    public bool MentionHere { get; set; } = true;
    public bool MentionOwnName { get; set; } = true;
    public bool MentionOwnNameParts { get; set; } = true;
    public bool MentionOnTell { get; set; } = true;
    public List<string> MentionKeywords { get; set; } = new();
    public string DiscordUserId { get; set; } = string.Empty;
    public Vector4 MentionColor { get; set; } = new(0.34f, 0.39f, 0.85f, 1f);
    public Vector4 MentionHighlightColor { get; set; } = new(0.98f, 0.75f, 0.18f, 0.10f);
    public Vector4 UnreadBadgeColor { get; set; } = new(0.93f, 0.27f, 0.27f, 1f);
    public Vector4 LinkColor { get; set; } = new(0.29f, 0.78f, 1f, 1f);
    public bool EnableLinkEmbeds { get; set; } = true;
    public bool EmbedImages { get; set; } = true;
    public int MaxEmbedsPerMessage { get; set; } = 2;
    public float EmbedMaxWidth { get; set; } = 420f;
    public float EmbedImageMaxHeight { get; set; } = 220f;
    public int EmbedCacheDays { get; set; } = 14;
    public bool EmbedFilteredMessages { get; set; }
    public bool HideMediaLinks { get; set; } = true;
    public bool OutlineLinks { get; set; } = true;

    public bool NotifyOnMention { get; set; } = true;
    public bool NotifyOnUnread { get; set; }
    public bool PlaySoundOnMention { get; set; }
    public string MentionSoundPath { get; set; } = string.Empty;
    public float MentionSoundVolume { get; set; } = 0.6f;
    public bool FlashTitleOnMention { get; set; } = true;

    public bool EnableReplies { get; set; } = true;
    public bool ShowReplyPreview { get; set; } = true;
    public int ReplyExcerptLength { get; set; } = 64;
    public string GameReplyFormat { get; set; } = "@{name} {message}";
    public bool PingOnDiscordReply { get; set; }

    public bool ShowInputBar { get; set; } = true;
    public bool ClearInputAfterSend { get; set; } = true;
    public bool KeepFocusAfterSend { get; set; } = true;
    public int MaxMessagesPerChannel { get; set; } = 5000;
    public int MaxInputLength { get; set; } = 500;

    public bool ImageCacheEnabled { get; set; } = true;
    public int ImageCacheMaxEntries { get; set; } = 600;

    public bool PersistHistory { get; set; } = true;

    public bool FilterAdvertisements { get; set; } = true;

    public string ActiveChannelId { get; set; } = string.Empty;
    public List<ChatboxChannelConfig> Channels { get; set; } = new();
}
