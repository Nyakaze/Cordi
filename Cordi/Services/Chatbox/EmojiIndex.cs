using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Cordi.Services.Discord;

namespace Cordi.Services.Chatbox;

public static class EmojiIndex
{
    private const int Zwj = 0x200D;
    private const int VariationSelector16 = 0xFE0F;
    private const int Keycap = 0x20E3;

    private static readonly Dictionary<string, string> ShortcodeToUnicode;
    private static readonly Dictionary<string, string> TextSmileyToUnicode;

    static EmojiIndex()
    {
        ShortcodeToUnicode = BuildShortcodeMap();
        TextSmileyToUnicode = BuildTextSmileyMap();
    }

    private static Dictionary<string, string> BuildShortcodeMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in ExtraShortcodes)
            map[pair.Key] = pair.Value;

        if (DiscordEmojiParser.UnicodeShortcodeMap != null)
        {
            foreach (var pair in DiscordEmojiParser.UnicodeShortcodeMap)
            {
                var name = pair.Value.Trim(':');
                if (name.Length == 0) continue;
                if (!map.ContainsKey(name))
                    map[name] = pair.Key;
            }
        }

        return map;
    }

    private static Dictionary<string, string> BuildTextSmileyMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (DiscordEmojiParser.UnicodeTextSmileyMap != null)
        {
            foreach (var pair in DiscordEmojiParser.UnicodeTextSmileyMap)
            {
                if (!map.ContainsKey(pair.Value))
                    map[pair.Value] = pair.Key;
            }
        }
        return map;
    }

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

    public static bool TryGetTextSmiley(string smiley, out string unicode) =>
        TextSmileyToUnicode.TryGetValue(smiley, out unicode!);

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
