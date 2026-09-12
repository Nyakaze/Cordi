using System;
using System.Collections.Generic;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public enum ChatboxSearchCategory
{
    Emotes,
    Announcements,
    Battle,
    Progress,
    System,
}

public static class ChatboxSearchCategories
{
    public static readonly ChatboxSearchCategory[] All =
    {
        ChatboxSearchCategory.Emotes,
        ChatboxSearchCategory.Announcements,
        ChatboxSearchCategory.Battle,
        ChatboxSearchCategory.Progress,
        ChatboxSearchCategory.System,
    };

    public static readonly ChatboxSearchCategory[] Default =
    {
        ChatboxSearchCategory.Battle,
        ChatboxSearchCategory.Progress,
        ChatboxSearchCategory.System,
    };

    private static readonly Dictionary<ChatboxSearchCategory, int[]> Types = new()
    {
        [ChatboxSearchCategory.Emotes] = Ints(
            XivChatType.CustomEmote,
            XivChatType.StandardEmote),

        [ChatboxSearchCategory.Announcements] = Ints(
            XivChatType.FreeCompanyAnnouncement,
            XivChatType.FreeCompanyLoginLogout,
            XivChatType.PvpTeamAnnouncement,
            XivChatType.PvpTeamLoginLogout,
            XivChatType.NoviceNetworkSystem,
            XivChatType.NPCDialogue,
            XivChatType.NPCDialogueAnnouncements,
            XivChatType.PeriodicRecruitmentNotification,
            XivChatType.MessageBook),

        [ChatboxSearchCategory.Battle] = Ints(
            XivChatType.Damage,
            XivChatType.Miss,
            XivChatType.Action,
            XivChatType.Item,
            XivChatType.Healing,
            XivChatType.GainBuff,
            XivChatType.LoseBuff,
            XivChatType.GainDebuff,
            XivChatType.LoseDebuff),

        [ChatboxSearchCategory.Progress] = Ints(
            XivChatType.LootNotice,
            XivChatType.LootRoll,
            XivChatType.Progress,
            XivChatType.Crafting,
            XivChatType.Gathering,
            XivChatType.GlamourNotifications,
            XivChatType.RetainerSale),

        [ChatboxSearchCategory.System] = Ints(
            XivChatType.Echo,
            XivChatType.SystemMessage,
            XivChatType.SystemError,
            XivChatType.GatheringSystemMessage,
            XivChatType.ErrorMessage,
            XivChatType.Notice,
            XivChatType.Urgent,
            XivChatType.Debug,
            XivChatType.Alarm,
            XivChatType.Orchestrion,
            XivChatType.Sign,
            XivChatType.RandomNumber),
    };

    public static string Label(ChatboxSearchCategory category) => category switch
    {
        ChatboxSearchCategory.Emotes => "Emotes",
        ChatboxSearchCategory.Announcements => "Announcements",
        ChatboxSearchCategory.Battle => "Battle log",
        ChatboxSearchCategory.Progress => "Loot and crafting",
        ChatboxSearchCategory.System => "System, errors and debug",
        _ => category.ToString(),
    };

    public static IReadOnlyList<int> TypesOf(ChatboxSearchCategory category) =>
        Types.TryGetValue(category, out var types) ? types : Array.Empty<int>();

    private static int[] Ints(params XivChatType[] types)
    {
        var values = new int[types.Length];

        for (var i = 0; i < types.Length; i++)
            values[i] = (int)types[i];

        return values;
    }
}

public enum ChatboxSearchField
{
    Everything,
    Message,
    Author,
}

public enum ChatboxSearchMatch
{
    Contains,
    WholeWord,
    StartsWith,
    Exact,
    Regex,
}

public enum ChatboxSearchFlag
{
    Any,
    Only,
    Exclude,
}

public enum ChatboxSearchSort
{
    Newest,
    Oldest,
    AuthorAscending,
    AuthorDescending,
    ChannelThenNewest,
    ChannelThenOldest,
    LongestFirst,
    ShortestFirst,
}

public enum ChatboxSearchRange
{
    Any,
    Today,
    Yesterday,
    LastSevenDays,
    LastThirtyDays,
    ThisYear,
    Custom,
}

public sealed class ChatboxSearchQuery
{
    public const int DefaultLimit = 500;
    public const int DefaultScanCap = 400000;

    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;

    public ChatboxSearchField Field { get; set; } = ChatboxSearchField.Everything;
    public ChatboxSearchMatch Match { get; set; } = ChatboxSearchMatch.Contains;
    public bool MatchCase { get; set; }

    public List<string> ChannelIds { get; set; } = new();
    public List<ChatboxSearchCategory> ExcludedCategories { get; set; } = new(ChatboxSearchCategories.Default);

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? TimeFromMinutes { get; set; }
    public int? TimeToMinutes { get; set; }

    public ChatboxSearchFlag Mentions { get; set; } = ChatboxSearchFlag.Any;
    public ChatboxSearchFlag FromMe { get; set; } = ChatboxSearchFlag.Any;
    public ChatboxSearchFlag Attachments { get; set; } = ChatboxSearchFlag.Any;
    public ChatboxSearchFlag Links { get; set; } = ChatboxSearchFlag.Any;
    public ChatboxSearchFlag FilteredAds { get; set; } = ChatboxSearchFlag.Exclude;

    public ChatboxSearchSort Sort { get; set; } = ChatboxSearchSort.Newest;
    public int Limit { get; set; } = DefaultLimit;
    public int ScanCap { get; set; } = DefaultScanCap;

    public bool HasTerms =>
        !string.IsNullOrWhiteSpace(Text)
        || !string.IsNullOrWhiteSpace(Author)
        || ChannelIds.Count > 0
        || From.HasValue
        || To.HasValue
        || TimeFromMinutes.HasValue
        || TimeToMinutes.HasValue
        || Mentions != ChatboxSearchFlag.Any
        || FromMe != ChatboxSearchFlag.Any
        || Attachments != ChatboxSearchFlag.Any
        || Links != ChatboxSearchFlag.Any;

    public ChatboxSearchQuery Clone() => new()
    {
        Text = Text,
        Author = Author,
        Field = Field,
        Match = Match,
        MatchCase = MatchCase,
        ChannelIds = new List<string>(ChannelIds),
        ExcludedCategories = new List<ChatboxSearchCategory>(ExcludedCategories),
        From = From,
        To = To,
        TimeFromMinutes = TimeFromMinutes,
        TimeToMinutes = TimeToMinutes,
        Mentions = Mentions,
        FromMe = FromMe,
        Attachments = Attachments,
        Links = Links,
        FilteredAds = FilteredAds,
        Sort = Sort,
        Limit = Limit,
        ScanCap = ScanCap,
    };
}

public sealed class ChatboxSearchResults
{
    public static readonly ChatboxSearchResults Empty = new();

    public IReadOnlyList<ChatboxMessage> Items { get; init; } = Array.Empty<ChatboxMessage>();
    public int Scanned { get; init; }
    public int Matched { get; init; }
    public bool LimitReached { get; init; }
    public bool ScanCapReached { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string Error { get; init; } = string.Empty;
}

public sealed class ChatboxChannelSummary
{
    public string Id { get; init; } = string.Empty;
    public int Count { get; init; }
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
    public XivChatType DominantType { get; init; }
}
