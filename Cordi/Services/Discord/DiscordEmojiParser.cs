using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cordi.Services.Discord;

public readonly record struct DiscordCustomEmote(ulong Id, string Name, bool Animated);

public static class DiscordEmojiParser
{
    private static readonly Regex CustomEmojiRegex = new(
        @"<(?<a>a?):(?<name>[A-Za-z0-9_~]{2,32}):(?<id>\d{5,25})>",
        RegexOptions.Compiled);

    private static readonly Regex EmoteLinkRegex = new(
        @"\[(?<name>[^\]]+)\]\(https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/(?<id>\d{5,25})\.(?<ext>[A-Za-z0-9]+)(?:\?[^)]*)?\)",
        RegexOptions.Compiled);

    private static readonly Regex BareEmoteUrlRegex = new(
        @"https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/(?<id>\d{5,25})\.(?<ext>png|gif|webp|jpe?g)(?:\?(?<query>\S*))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UrlNameRegex = new(
        @"(?:^|&)name=(?<name>[A-Za-z0-9_~%\-]{1,64})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmoteSuffixRegex = new(
        @"[^A-Za-z0-9]\d+$",
        RegexOptions.Compiled);

    private static readonly Regex EmoteWordRegex = new(
        @"[A-Za-z0-9_]+",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> UnicodeToTextSmiley = new()
    {
        { "\U0001F642", ":)" },
        { "\U0001F641", ":(" },
        { "\U0001F603", ":D" },
        { "\U0001F61B", ":P" },
        { "\U0001F609", ";)" },
        { "\U0001F62E", ":O" },
        { "\U0001F610", ":|" },
        { "\U0001F615", ":/" },
        { "\U0001F617", ":*" },
        { "\U0001F622", ":'(" },
        { "\U0001F626", "D:" },
        { "\U0001F607", "O:)" },
        { "\U0001F620", ">:(" },
        { "\U0001F60E", "8)" },
        { "❤️", "<3" },
        { "❤", "<3" },
        { "\U0001F494", "</3" },
    };

    private static readonly Dictionary<string, string> UnicodeToShortcode = new()
    {
        { "\U0001F600", ":grinning:" },
        { "\U0001F604", ":smile:" },
        { "\U0001F601", ":grin:" },
        { "\U0001F606", ":laughing:" },
        { "\U0001F605", ":sweat_smile:" },
        { "\U0001F923", ":rofl:" },
        { "\U0001F602", ":joy:" },
        { "\U0001F643", ":upside_down:" },
        { "\U0001F60A", ":blush:" },
        { "\U0001F970", ":smiling_face_with_hearts:" },
        { "\U0001F60D", ":heart_eyes:" },
        { "\U0001F929", ":star_struck:" },
        { "\U0001F618", ":kissing_heart:" },
        { "\U0001F61A", ":kissing_closed_eyes:" },
        { "\U0001F619", ":kissing_smiling_eyes:" },
        { "\U0001F60B", ":yum:" },
        { "\U0001F61C", ":stuck_out_tongue_winking_eye:" },
        { "\U0001F92A", ":zany_face:" },
        { "\U0001F61D", ":stuck_out_tongue_closed_eyes:" },
        { "\U0001F911", ":money_mouth:" },
        { "\U0001F917", ":hugging:" },
        { "\U0001F92D", ":hand_over_mouth:" },
        { "\U0001F92B", ":shushing:" },
        { "\U0001F914", ":thinking:" },
        { "\U0001F910", ":zipper_mouth:" },
        { "\U0001F928", ":raised_eyebrow:" },
        { "\U0001F611", ":expressionless:" },
        { "\U0001F636", ":no_mouth:" },
        { "\U0001F60F", ":smirk:" },
        { "\U0001F612", ":unamused:" },
        { "\U0001F644", ":roll_eyes:" },
        { "\U0001F62C", ":grimacing:" },
        { "\U0001F925", ":lying_face:" },
        { "\U0001F60C", ":relieved:" },
        { "\U0001F614", ":pensive:" },
        { "\U0001F62A", ":sleepy:" },
        { "\U0001F924", ":drooling:" },
        { "\U0001F634", ":sleeping:" },
        { "\U0001F637", ":mask:" },
        { "\U0001F912", ":thermometer_face:" },
        { "\U0001F915", ":head_bandage:" },
        { "\U0001F922", ":nauseated:" },
        { "\U0001F92E", ":vomiting:" },
        { "\U0001F927", ":sneezing:" },
        { "\U0001F975", ":hot_face:" },
        { "\U0001F976", ":cold_face:" },
        { "\U0001F974", ":woozy:" },
        { "\U0001F635", ":dizzy_face:" },
        { "\U0001F92F", ":exploding_head:" },
        { "\U0001F920", ":cowboy:" },
        { "\U0001F973", ":partying:" },
        { "\U0001F913", ":nerd:" },
        { "\U0001F9D0", ":monocle:" },
        { "\U0001F61F", ":worried:" },
        { "☹️", ":frowning2:" },
        { "☹", ":frowning2:" },
        { "\U0001F62F", ":hushed:" },
        { "\U0001F632", ":astonished:" },
        { "\U0001F633", ":flushed:" },
        { "\U0001F97A", ":pleading:" },
        { "\U0001F627", ":anguished:" },
        { "\U0001F628", ":fearful:" },
        { "\U0001F630", ":cold_sweat:" },
        { "\U0001F625", ":disappointed_relieved:" },
        { "\U0001F62D", ":sob:" },
        { "\U0001F631", ":scream:" },
        { "\U0001F616", ":confounded:" },
        { "\U0001F623", ":persevere:" },
        { "\U0001F61E", ":disappointed:" },
        { "\U0001F613", ":sweat:" },
        { "\U0001F629", ":weary:" },
        { "\U0001F62B", ":tired_face:" },
        { "\U0001F624", ":triumph:" },
        { "\U0001F621", ":rage:" },
        { "\U0001F92C", ":cursing:" },
        { "\U0001F608", ":smiling_imp:" },
        { "\U0001F47F", ":imp:" },
        { "\U0001F480", ":skull:" },
        { "☠️", ":skull_crossbones:" },
        { "☠", ":skull_crossbones:" },
        { "\U0001F4A9", ":poop:" },
        { "\U0001F921", ":clown:" },
        { "\U0001F479", ":ogre:" },
        { "\U0001F47A", ":goblin:" },
        { "\U0001F47B", ":ghost:" },
        { "\U0001F47D", ":alien:" },
        { "\U0001F47E", ":space_invader:" },
        { "\U0001F916", ":robot:" },
        { "\U0001F9E1", ":orange_heart:" },
        { "\U0001F49B", ":yellow_heart:" },
        { "\U0001F49A", ":green_heart:" },
        { "\U0001F499", ":blue_heart:" },
        { "\U0001F49C", ":purple_heart:" },
        { "\U0001F5A4", ":black_heart:" },
        { "\U0001F90D", ":white_heart:" },
        { "\U0001F90E", ":brown_heart:" },
        { "\U0001F495", ":two_hearts:" },
        { "\U0001F49E", ":revolving_hearts:" },
        { "\U0001F493", ":heartbeat:" },
        { "\U0001F497", ":heartpulse:" },
        { "\U0001F496", ":sparkling_heart:" },
        { "\U0001F498", ":cupid:" },
        { "\U0001F49D", ":gift_heart:" },
        { "\U0001F44D", ":thumbsup:" },
        { "\U0001F44E", ":thumbsdown:" },
        { "\U0001F44C", ":ok_hand:" },
        { "✌️", ":v:" },
        { "✌", ":v:" },
        { "\U0001F91E", ":crossed_fingers:" },
        { "\U0001F91F", ":love_you_gesture:" },
        { "\U0001F918", ":metal:" },
        { "\U0001F919", ":call_me:" },
        { "\U0001F448", ":point_left:" },
        { "\U0001F449", ":point_right:" },
        { "\U0001F446", ":point_up:" },
        { "\U0001F447", ":point_down:" },
        { "\U0001F595", ":middle_finger:" },
        { "✋", ":hand:" },
        { "\U0001F590", ":hand_splayed:" },
        { "\U0001F596", ":vulcan:" },
        { "\U0001F44B", ":wave:" },
        { "\U0001F91A", ":raised_back_of_hand:" },
        { "\U0001F44F", ":clap:" },
        { "\U0001F64C", ":raised_hands:" },
        { "\U0001F91D", ":handshake:" },
        { "\U0001F64F", ":pray:" },
        { "\U0001F525", ":fire:" },
        { "⭐", ":star:" },
        { "✨", ":sparkles:" },
        { "\U0001F4AF", ":100:" },
        { "✅", ":check:" },
        { "❌", ":x:" },
        { "❓", ":question:" },
        { "❗", ":exclamation:" },
        { "\U0001F389", ":tada:" },
        { "\U0001F38A", ":confetti_ball:" },
    };

    public static IReadOnlyDictionary<string, string> UnicodeShortcodeMap => UnicodeToShortcode;

    public static IReadOnlyDictionary<string, string> UnicodeTextSmileyMap => UnicodeToTextSmiley;

    public static string Parse(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;

        var result = CustomEmojiRegex.Replace(content, m => $":{m.Groups["name"].Value}:");

        result = EmoteLinkRegex.Replace(result, m => $":{CleanEmoteName(m.Groups["name"].Value)}:");

        foreach (var kv in UnicodeToTextSmiley)
            if (result.Contains(kv.Key))
                result = result.Replace(kv.Key, kv.Value);

        foreach (var kv in UnicodeToShortcode)
            if (result.Contains(kv.Key))
                result = result.Replace(kv.Key, kv.Value);

        return result;
    }

    public static string ParseToUrls(string? content)
    {
        if (string.IsNullOrEmpty(content)) return content ?? string.Empty;

        var result = CustomEmojiRegex.Replace(content, m =>
            ulong.TryParse(m.Groups["id"].Value, out var id)
                ? $" {EmoteUrl(id, m.Groups["a"].Value.Length > 0, m.Groups["name"].Value)} "
                : m.Value);

        result = EmoteLinkRegex.Replace(result, m =>
            ulong.TryParse(m.Groups["id"].Value, out var id)
                ? $" {EmoteUrl(id, IsAnimated(m.Groups["ext"].Value), CleanEmoteName(m.Groups["name"].Value))} "
                : m.Value);

        return result.Trim();
    }

    public static List<DiscordCustomEmote> Extract(string? content)
    {
        var found = new List<DiscordCustomEmote>();
        if (string.IsNullOrEmpty(content)) return found;

        foreach (Match match in CustomEmojiRegex.Matches(content))
            Add(found, match.Groups["id"].Value, match.Groups["name"].Value, match.Groups["a"].Value.Length > 0);

        foreach (Match match in EmoteLinkRegex.Matches(content))
            Add(found, match.Groups["id"].Value, CleanEmoteName(match.Groups["name"].Value), IsAnimated(match.Groups["ext"].Value));

        foreach (Match match in BareEmoteUrlRegex.Matches(content))
            Add(found, match.Groups["id"].Value, NameFromQuery(match.Groups["query"]), IsAnimated(match.Groups["ext"].Value));

        return found;
    }

    public static string EmoteUrl(ulong id, bool animated, string name)
    {
        var url = $"https://cdn.discordapp.com/emojis/{id}.{(animated ? "gif" : "png")}?size=48&quality=lossless";

        return string.IsNullOrEmpty(name) ? url : $"{url}&name={name}";
    }

    private static string NameFromQuery(Group query)
    {
        if (!query.Success) return string.Empty;

        var match = UrlNameRegex.Match(query.Value);
        if (!match.Success) return string.Empty;

        var decoded = System.Net.WebUtility.UrlDecode(match.Groups["name"].Value);

        return string.IsNullOrWhiteSpace(decoded) ? string.Empty : CleanEmoteName(decoded);
    }

    private static bool IsAnimated(string extension) =>
        string.Equals(extension, "gif", StringComparison.OrdinalIgnoreCase);

    private static void Add(List<DiscordCustomEmote> found, string rawId, string name, bool animated)
    {
        if (!ulong.TryParse(rawId, out var id)) return;
        if (string.IsNullOrEmpty(name)) return;

        foreach (var existing in found)
            if (existing.Id == id) return;

        found.Add(new DiscordCustomEmote(id, name, animated));
    }

    private static string CleanEmoteName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        name = System.Net.WebUtility.UrlDecode(name);

        name = EmoteSuffixRegex.Replace(name, "");

        var matches = EmoteWordRegex.Matches(name);
        string longest = "";
        foreach (Match match in matches)
        {
            if (match.Value.Length > longest.Length)
            {
                longest = match.Value;
            }
        }

        return longest;
    }
}
