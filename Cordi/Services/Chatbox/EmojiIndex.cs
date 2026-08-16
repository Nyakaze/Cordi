using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cordi.Services.Chatbox;

public static class EmojiIndex
{
    private const int Zwj = 0x200D;
    private const int VariationSelector16 = 0xFE0F;
    private const int Keycap = 0x20E3;

    private static readonly Dictionary<string, string> ShortcodeToUnicode;

    static EmojiIndex()
    {
        ShortcodeToUnicode = BuildShortcodeMap();
    }

    private static Dictionary<string, string> BuildShortcodeMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in ExtraShortcodes)
            map[pair.Key] = pair.Value;

        foreach (var pair in UnicodeShortcodes)
            map.TryAdd(pair.Key, pair.Value);

        return map;
    }

    private static readonly Dictionary<string, string> UnicodeShortcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "grinning", "\U0001F600" },
        { "smile", "\U0001F604" },
        { "grin", "\U0001F601" },
        { "laughing", "\U0001F606" },
        { "sweat_smile", "\U0001F605" },
        { "rofl", "\U0001F923" },
        { "joy", "\U0001F602" },
        { "upside_down", "\U0001F643" },
        { "blush", "\U0001F60A" },
        { "smiling_face_with_hearts", "\U0001F970" },
        { "heart_eyes", "\U0001F60D" },
        { "star_struck", "\U0001F929" },
        { "kissing_heart", "\U0001F618" },
        { "kissing_closed_eyes", "\U0001F61A" },
        { "kissing_smiling_eyes", "\U0001F619" },
        { "yum", "\U0001F60B" },
        { "stuck_out_tongue_winking_eye", "\U0001F61C" },
        { "zany_face", "\U0001F92A" },
        { "stuck_out_tongue_closed_eyes", "\U0001F61D" },
        { "money_mouth", "\U0001F911" },
        { "hugging", "\U0001F917" },
        { "hand_over_mouth", "\U0001F92D" },
        { "shushing", "\U0001F92B" },
        { "thinking", "\U0001F914" },
        { "zipper_mouth", "\U0001F910" },
        { "raised_eyebrow", "\U0001F928" },
        { "expressionless", "\U0001F611" },
        { "no_mouth", "\U0001F636" },
        { "smirk", "\U0001F60F" },
        { "unamused", "\U0001F612" },
        { "roll_eyes", "\U0001F644" },
        { "grimacing", "\U0001F62C" },
        { "lying_face", "\U0001F925" },
        { "relieved", "\U0001F60C" },
        { "pensive", "\U0001F614" },
        { "sleepy", "\U0001F62A" },
        { "drooling", "\U0001F924" },
        { "sleeping", "\U0001F634" },
        { "mask", "\U0001F637" },
        { "thermometer_face", "\U0001F912" },
        { "head_bandage", "\U0001F915" },
        { "nauseated", "\U0001F922" },
        { "vomiting", "\U0001F92E" },
        { "sneezing", "\U0001F927" },
        { "hot_face", "\U0001F975" },
        { "cold_face", "\U0001F976" },
        { "woozy", "\U0001F974" },
        { "dizzy_face", "\U0001F635" },
        { "exploding_head", "\U0001F92F" },
        { "cowboy", "\U0001F920" },
        { "partying", "\U0001F973" },
        { "nerd", "\U0001F913" },
        { "monocle", "\U0001F9D0" },
        { "worried", "\U0001F61F" },
        { "frowning2", "☹️" },
        { "hushed", "\U0001F62F" },
        { "astonished", "\U0001F632" },
        { "flushed", "\U0001F633" },
        { "pleading", "\U0001F97A" },
        { "anguished", "\U0001F627" },
        { "fearful", "\U0001F628" },
        { "cold_sweat", "\U0001F630" },
        { "disappointed_relieved", "\U0001F625" },
        { "sob", "\U0001F62D" },
        { "scream", "\U0001F631" },
        { "confounded", "\U0001F616" },
        { "persevere", "\U0001F623" },
        { "disappointed", "\U0001F61E" },
        { "sweat", "\U0001F613" },
        { "weary", "\U0001F629" },
        { "tired_face", "\U0001F62B" },
        { "triumph", "\U0001F624" },
        { "rage", "\U0001F621" },
        { "cursing", "\U0001F92C" },
        { "smiling_imp", "\U0001F608" },
        { "imp", "\U0001F47F" },
        { "skull", "\U0001F480" },
        { "skull_crossbones", "☠️" },
        { "poop", "\U0001F4A9" },
        { "clown", "\U0001F921" },
        { "ogre", "\U0001F479" },
        { "goblin", "\U0001F47A" },
        { "ghost", "\U0001F47B" },
        { "alien", "\U0001F47D" },
        { "space_invader", "\U0001F47E" },
        { "robot", "\U0001F916" },
        { "orange_heart", "\U0001F9E1" },
        { "yellow_heart", "\U0001F49B" },
        { "green_heart", "\U0001F49A" },
        { "blue_heart", "\U0001F499" },
        { "purple_heart", "\U0001F49C" },
        { "black_heart", "\U0001F5A4" },
        { "white_heart", "\U0001F90D" },
        { "brown_heart", "\U0001F90E" },
        { "two_hearts", "\U0001F495" },
        { "revolving_hearts", "\U0001F49E" },
        { "heartbeat", "\U0001F493" },
        { "heartpulse", "\U0001F497" },
        { "sparkling_heart", "\U0001F496" },
        { "cupid", "\U0001F498" },
        { "gift_heart", "\U0001F49D" },
        { "thumbsup", "\U0001F44D" },
        { "thumbsdown", "\U0001F44E" },
        { "ok_hand", "\U0001F44C" },
        { "v", "✌️" },
        { "crossed_fingers", "\U0001F91E" },
        { "love_you_gesture", "\U0001F91F" },
        { "metal", "\U0001F918" },
        { "call_me", "\U0001F919" },
        { "point_left", "\U0001F448" },
        { "point_right", "\U0001F449" },
        { "point_up", "\U0001F446" },
        { "point_down", "\U0001F447" },
        { "middle_finger", "\U0001F595" },
        { "hand", "✋" },
        { "hand_splayed", "\U0001F590" },
        { "vulcan", "\U0001F596" },
        { "wave", "\U0001F44B" },
        { "raised_back_of_hand", "\U0001F91A" },
        { "clap", "\U0001F44F" },
        { "raised_hands", "\U0001F64C" },
        { "handshake", "\U0001F91D" },
        { "pray", "\U0001F64F" },
        { "fire", "\U0001F525" },
        { "star", "⭐" },
        { "sparkles", "✨" },
        { "100", "\U0001F4AF" },
        { "check", "✅" },
        { "x", "❌" },
        { "question", "❓" },
        { "exclamation", "❗" },
        { "tada", "\U0001F389" },
        { "confetti_ball", "\U0001F38A" },
    };

    private static readonly Dictionary<string, string> ExtraShortcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "slight_smile", "\U0001F642" },
        { "slight_frown", "\U0001F641" },
        { "smiley", "\U0001F603" },
        { "stuck_out_tongue", "\U0001F61B" },
        { "wink", "\U0001F609" },
        { "open_mouth", "\U0001F62E" },
        { "neutral_face", "\U0001F610" },
        { "confused", "\U0001F615" },
        { "kissing", "\U0001F617" },
        { "cry", "\U0001F622" },
        { "frowning", "\U0001F626" },
        { "innocent", "\U0001F607" },
        { "angry", "\U0001F620" },
        { "sunglasses", "\U0001F60E" },
        { "heart", "❤️" },
        { "broken_heart", "\U0001F494" },
        { "eyes", "\U0001F440" },
        { "wave", "\U0001F44B" },
        { "sob", "\U0001F62D" },
        { "smile", "\U0001F604" },
        { "grin", "\U0001F601" },
        { "thumbsup", "\U0001F44D" },
        { "thumbsdown", "\U0001F44E" },
        { "+1", "\U0001F44D" },
        { "-1", "\U0001F44E" },
        { "fire", "\U0001F525" },
        { "star", "⭐" },
        { "sparkles", "✨" },
        { "tada", "\U0001F389" },
        { "cat", "\U0001F431" },
        { "dog", "\U0001F436" },
        { "sleeping", "\U0001F634" },
        { "zzz", "\U0001F4A4" },
        { "sweat_drops", "\U0001F4A6" },
        { "musical_note", "\U0001F3B5" },
        { "notes", "\U0001F3B6" },
        { "crown", "\U0001F451" },
        { "gem", "\U0001F48E" },
        { "coffee", "☕" },
        { "beer", "\U0001F37A" },
        { "cake", "\U0001F370" },
        { "pizza", "\U0001F355" },
        { "moon", "\U0001F319" },
        { "sun", "☀️" },
        { "rainbow", "\U0001F308" },
        { "snowflake", "❄️" },
        { "warning", "⚠️" },
        { "no_entry", "⛔" },
        { "white_check_mark", "✅" },
        { "heavy_check_mark", "✔️" },
        { "x", "❌" },
        { "question", "❓" },
        { "exclamation", "❗" },
        { "zap", "⚡" },
        { "boom", "\U0001F4A5" },
        { "100", "\U0001F4AF" },
        { "pray", "\U0001F64F" },
        { "clap", "\U0001F44F" },
        { "muscle", "\U0001F4AA" },
        { "ok_hand", "\U0001F44C" },
        { "point_right", "\U0001F449" },
        { "point_left", "\U0001F448" },
        { "raised_hands", "\U0001F64C" },
        { "skull", "\U0001F480" },
        { "ghost", "\U0001F47B" },
        { "alien", "\U0001F47D" },
        { "robot", "\U0001F916" },
        { "joy", "\U0001F602" },
        { "rofl", "\U0001F923" },
        { "sparkling_heart", "\U0001F496" },
        { "gift", "\U0001F381" },
        { "bell", "\U0001F514" },
        { "loudspeaker", "\U0001F4E2" },
        { "speech_balloon", "\U0001F4AC" },
        { "crossed_swords", "⚔️" },
        { "shield", "\U0001F6E1️" },
        { "hourglass", "⌛" },
        { "watch", "⌚" },
        { "pleading", "\U0001F97A" },
        { "pleading_face", "\U0001F97A" },
        { "salute", "\U0001FAE1" },
        { "melting_face", "\U0001FAE0" },
        { "melt", "\U0001FAE0" },
        { "heart_eyes", "\U0001F60D" },
        { "smiling_face_with_3_hearts", "\U0001F970" },
        { "face_with_raised_eyebrow", "\U0001F928" },
        { "clown", "\U0001F921" },
        { "clown_face", "\U0001F921" },
        { "rolling_on_the_floor_laughing", "\U0001F923" },
        { "hugs", "\U0001F917" },
        { "face_with_hand_over_mouth", "\U0001F92D" },
        { "shush", "\U0001F92B" },
        { "thinking_face", "\U0001F914" },
        { "thinking", "\U0001F914" },
        { "exploding_head", "\U0001F92F" },
        { "party_face", "\U0001F973" },
        { "partying_face", "\U0001F973" },
        { "partying", "\U0001F973" },
        { "drooling_face", "\U0001F924" },
        { "nauseated_face", "\U0001F922" },
        { "vomiting_face", "\U0001F92E" },
        { "hot_face", "\U0001F975" },
        { "cold_face", "\U0001F976" },
        { "woozy_face", "\U0001F974" },
        { "dizzy_face", "\U0001F635" },
        { "money_mouth_face", "\U0001F911" },
        { "cowboy_hat_face", "\U0001F920" },
        { "clapping", "\U0001F44F" },
        { "praying", "\U0001F64F" },
        { "skull_and_crossbones", "☠️" },
        { "poop", "\U0001F4A9" },
        { "poo", "\U0001F4A9" },
    };

    public static bool TryGetShortcode(string name, out string unicode) =>
        ShortcodeToUnicode.TryGetValue(name, out unicode!);

    private static bool TryCodepointAt(string text, int index, out int codepoint, out int length)
    {
        codepoint = 0;
        length = 0;
        if (index < 0 || index >= text.Length) return false;

        var c = text[index];
        if (char.IsLowSurrogate(c)) return false;
        if (char.IsHighSurrogate(c))
        {
            if (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1])) return false;
            codepoint = char.ConvertToUtf32(c, text[index + 1]);
            length = 2;
            return true;
        }

        codepoint = c;
        length = 1;
        return true;
    }

    public static bool IsEmojiStart(string text, int index)
    {
        if (!TryCodepointAt(text, index, out var cp, out var length)) return false;

        if (cp is >= 0x1F000 and <= 0x1FAFF) return true;
        if (cp is >= 0x2600 and <= 0x27BF) return true;
        if (cp is >= 0x2B00 and <= 0x2BFF) return true;
        if (cp is 0x231A or 0x231B or 0x24C2 or 0x25B6 or 0x25C0 or 0x2934 or 0x2935 or 0x3030 or 0x303D or 0x3297 or 0x3299) return true;
        if (cp is >= 0x23E9 and <= 0x23FA) return true;
        if (cp is >= 0x25AA and <= 0x25AB) return true;
        if (cp is >= 0x25FB and <= 0x25FE) return true;

        bool followedByVs16 = index + length < text.Length && text[index + length] == (char)VariationSelector16;
        if (followedByVs16 &&
            cp is 0x00A9 or 0x00AE or 0x2122 or 0x2139 or 0x203C or 0x2049 or 0x21A9 or 0x21AA
                or 0x2194 or 0x2195 or 0x2196 or 0x2197 or 0x2198 or 0x2199)
            return true;

        return IsKeycapBase(cp) && HasKeycapSuffix(text, index);
    }

    private static bool IsKeycapBase(int cp) =>
        cp is >= '0' and <= '9' or '#' or '*';

    private static bool HasKeycapSuffix(string text, int index)
    {
        var cursor = index + 1;
        if (cursor < text.Length && text[cursor] == (char)VariationSelector16) cursor++;
        return cursor < text.Length && text[cursor] == (char)Keycap;
    }

    private static bool IsModifier(int cp) =>
        cp == VariationSelector16
        || cp == Keycap
        || cp is >= 0x1F3FB and <= 0x1F3FF
        || cp is >= 0xE0020 and <= 0xE007F
        || cp is >= 0x1F1E6 and <= 0x1F1FF;

    public static int MeasureCluster(string text, int index)
    {
        if (!TryCodepointAt(text, index, out _, out var length)) return 1;

        var cursor = index + length;

        while (TryCodepointAt(text, cursor, out var cp, out var size))
        {
            if (IsModifier(cp))
            {
                cursor += size;
                continue;
            }

            if (cp == Zwj
                && TryCodepointAt(text, cursor + size, out _, out var joinedSize)
                && IsEmojiStart(text, cursor + size))
            {
                cursor += size + joinedSize;
                continue;
            }

            break;
        }

        return cursor - index;
    }

    public static string ToCodePointName(string cluster)
    {
        bool hasZwj = cluster.IndexOf((char)Zwj) >= 0;
        var builder = new StringBuilder();

        for (int i = 0; i < cluster.Length;)
        {
            var cp = char.ConvertToUtf32(cluster, i);
            i += char.ConvertFromUtf32(cp).Length;

            if (cp == VariationSelector16 && !hasZwj) continue;

            if (builder.Length > 0) builder.Append('-');
            builder.Append(cp.ToString("x", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
