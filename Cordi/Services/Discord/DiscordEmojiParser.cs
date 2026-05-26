using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cordi.Services.Discord;

/// <summary>
/// Converts Discord message content into a form suitable for the in-game chat.
/// Discord auto-converts text smileys (":P", ":O", "&lt;3", ...) to Unicode emojis.
/// FFXIV cannot render those, and certain emoji bytes can cause the message to be
/// dropped silently. This parser reverses Discord's auto-conversion where possible,
/// strips custom emoji markup to its shortcode text, and falls back to a readable
/// ":shortcode:" form for other known emojis.
/// </summary>
public static class DiscordEmojiParser
{
    // <:name:123456789> or <a:name:123456789> -> :name:
    private static readonly Regex CustomEmojiRegex = new(
        @"<a?:([A-Za-z0-9_]+):\d+>",
        RegexOptions.Compiled);

    // [name](https://cdn.discordapp.com/emojis/123456789.png?...) -> :name:
    // Discord sends emotes as markdown links when posted via webhooks / certain clients.
    private static readonly Regex EmoteLinkRegex = new(
        @"\[([A-Za-z0-9_]+)\]\(https?://(?:cdn\.discordapp\.com|media\.discordapp\.net)/emojis/\d+\.[A-Za-z0-9]+(?:\?[^)]*)?\)",
        RegexOptions.Compiled);

    // Unicode emojis that Discord auto-creates from text shortcuts.
    // Reversed back to the original text the user most likely typed.
    private static readonly Dictionary<string, string> UnicodeToTextSmiley = new()
    {
        { "\U0001F642", ":)" },     // slight_smile
        { "\U0001F641", ":(" },     // slight_frown
        { "\U0001F603", ":D" },     // smiley
        { "\U0001F61B", ":P" },     // stuck_out_tongue
        { "\U0001F609", ";)" },     // wink
        { "\U0001F62E", ":O" },     // open_mouth
        { "\U0001F610", ":|" },     // neutral_face
        { "\U0001F615", ":/" },     // confused
        { "\U0001F617", ":*" },     // kissing
        { "\U0001F622", ":'(" },    // cry
        { "\U0001F626", "D:" },     // frowning
        { "\U0001F607", "O:)" },    // innocent
        { "\U0001F620", ">:(" },    // angry
        { "\U0001F60E", "8)" },     // sunglasses
        { "❤️", "<3" },   // heart (with VS16)
        { "❤", "<3" },          // heart (bare)
        { "\U0001F494", "</3" },    // broken_heart
    };

    // Other common Unicode emojis -> :shortcode:
    // Kept compact; unknown emojis are passed through unchanged.
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

    /// <summary>
    /// Parses Discord message content into in-game-chat-safe text.
    /// </summary>
    public static string Parse(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;

        // 1) Custom emojis: <:name:id> / <a:name:id>  ->  :name:
        var result = CustomEmojiRegex.Replace(content, m => $":{m.Groups[1].Value}:");

        // 2) Emote links: [name](https://cdn.discordapp.com/emojis/id.ext?...) -> :name:
        result = EmoteLinkRegex.Replace(result, m => $":{m.Groups[1].Value}:");

        // 3) Unicode -> original text smiley (reverses Discord auto-convert)
        foreach (var kv in UnicodeToTextSmiley)
            if (result.Contains(kv.Key))
                result = result.Replace(kv.Key, kv.Value);

        // 4) Other known Unicode emojis -> :shortcode:
        foreach (var kv in UnicodeToShortcode)
            if (result.Contains(kv.Key))
                result = result.Replace(kv.Key, kv.Value);

        return result;
    }
}
