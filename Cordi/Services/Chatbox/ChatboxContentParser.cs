using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Cordi.Configuration;

namespace Cordi.Services.Chatbox;

public sealed class ParsedContent
{
    public IReadOnlyList<ContentSegment> Segments { get; init; } = Array.Empty<ContentSegment>();
    public bool MentionsMe { get; init; }
    public bool MentionsEveryone { get; init; }
    public bool OnlyEmotes { get; init; }
}

public sealed class ChatboxContentParser
{
    private readonly Func<ChatboxConfig> _config;

    public ChatboxContentParser(Func<ChatboxConfig> config)
    {
        _config = config;
    }

    public ParsedContent Parse(string? content, MentionResolver resolver)
    {
        var segments = new List<ContentSegment>();
        if (string.IsNullOrEmpty(content))
            return new ParsedContent { Segments = segments };

        var cfg = _config();
        var buffer = new StringBuilder();
        bool mentionsMe = false;
        bool mentionsEveryone = false;
        int emoteCount = 0;
        bool hasVisibleText = false;

        void Flush()
        {
            if (buffer.Length == 0) return;
            var text = buffer.ToString();
            buffer.Clear();
            if (!string.IsNullOrWhiteSpace(text)) hasVisibleText = true;
            segments.Add(ContentSegment.PlainText(text));
        }

        void AddMention(string label, bool targetsMe)
        {
            Flush();
            hasVisibleText = true;
            if (targetsMe) mentionsMe = true;
            segments.Add(new ContentSegment { Kind = SegmentKind.Mention, Text = label, TargetsMe = targetsMe });
        }

        void AddEmote(string label, string url)
        {
            Flush();
            emoteCount++;
            segments.Add(new ContentSegment { Kind = SegmentKind.Emote, Text = label, ImageUrl = url });
        }

        int i = 0;
        while (i < content.Length)
        {
            var c = content[i];

            if (c == '\r')
            {
                i++;
                continue;
            }

            if (c == '\n')
            {
                Flush();
                segments.Add(ContentSegment.Break());
                i++;
                continue;
            }

            if (c == '<' && TryReadAngleToken(content, i, cfg, resolver, out var consumed, out var segment, out var segmentMentionsMe))
            {
                Flush();
                if (segment.Kind == SegmentKind.Emote) emoteCount++;
                else hasVisibleText = true;
                if (segmentMentionsMe) mentionsMe = true;
                segments.Add(segment);
                i += consumed;
                continue;
            }

            if (c == '@' && cfg.EnableMentions)
            {
                if (cfg.MentionEveryone && Matches(content, i, "@everyone"))
                {
                    mentionsEveryone = true;
                    AddMention("@everyone", true);
                    i += "@everyone".Length;
                    continue;
                }

                if (cfg.MentionHere && Matches(content, i, "@here"))
                {
                    mentionsEveryone = true;
                    AddMention("@here", true);
                    i += "@here".Length;
                    continue;
                }

                if (resolver.TryMatchPlainName(content, i + 1, out var nameLength, out var name, out var targetsMe))
                {
                    AddMention("@" + name, targetsMe);
                    i += nameLength + 1;
                    continue;
                }
            }

            if (c == '[')
            {
                if (TryReadMarkdownEmote(content, i, out var mdLength, out var mdLabel, out var mdUrl))
                {
                    AddEmote(mdLabel, mdUrl);
                    i += mdLength;
                    continue;
                }

                if (TryReadItemLink(content, i, out var itemLength, out var itemSegment))
                {
                    Flush();
                    hasVisibleText = true;
                    segments.Add(itemSegment);
                    i += itemLength;
                    continue;
                }
            }

            if ((c == 'h' || c == 'H') && TryReadUrl(content, i, out var urlLength, out var url))
            {
                if (cfg.RenderCustomEmotes && TryReadEmoteUrl(url, resolver, out var emoteLabel, out var emoteUrl))
                {
                    AddEmote(emoteLabel, emoteUrl);
                    i += urlLength;
                    continue;
                }

                Flush();
                hasVisibleText = true;
                segments.Add(new ContentSegment { Kind = SegmentKind.Link, Text = url, Url = url });
                i += urlLength;
                continue;
            }

            if (c == ':' && cfg.RenderShortcodes && TryReadShortcode(content, i, cfg, resolver, out var codeLength, out var label, out var imageUrl))
            {
                AddEmote(label, imageUrl);
                i += codeLength;
                continue;
            }

            if (cfg.RenderUnicodeEmoji && EmojiIndex.IsEmojiStart(content, i))
            {
                var clusterLength = EmojiIndex.MeasureCluster(content, i);
                var cluster = content.Substring(i, clusterLength);
                var codepoints = EmojiIndex.ToCodePointName(cluster);
                if (codepoints.Length > 0)
                {
                    AddEmote(cluster, $"{cfg.TwemojiBaseUrl.TrimEnd('/')}/{codepoints}.png");
                    i += clusterLength;
                    continue;
                }
            }

            buffer.Append(c);
            i++;
        }

        Flush();

        return new ParsedContent
        {
            Segments = segments,
            MentionsMe = mentionsMe,
            MentionsEveryone = mentionsEveryone,
            OnlyEmotes = emoteCount > 0 && !hasVisibleText,
        };
    }

    private static bool Matches(string content, int index, string token) =>
        string.Compare(content, index, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private static bool TryReadAngleToken(
        string content,
        int index,
        ChatboxConfig cfg,
        MentionResolver resolver,
        out int consumed,
        out ContentSegment segment,
        out bool mentionsMe)
    {
        consumed = 0;
        segment = ContentSegment.PlainText(string.Empty);
        mentionsMe = false;

        var close = content.IndexOf('>', index + 1);
        if (close < 0 || close - index > 64) return false;

        var body = content.Substring(index + 1, close - index - 1);
        consumed = close - index + 1;

        if (body.StartsWith(':') || body.StartsWith("a:"))
        {
            var animated = body.StartsWith("a:");
            var parts = body[(animated ? 2 : 1)..].Split(':');
            if (parts.Length != 2 || !ulong.TryParse(parts[1], out var emoteId)) return false;
            if (!cfg.RenderCustomEmotes)
            {
                segment = ContentSegment.PlainText($":{parts[0]}:");
                return true;
            }

            segment = new ContentSegment
            {
                Kind = SegmentKind.Emote,
                Text = $":{parts[0]}:",
                ImageUrl = CustomEmoteUrl(emoteId, animated),
            };
            return true;
        }

        if (body.StartsWith('@'))
        {
            var isRole = body.Length > 1 && body[1] == '&';
            var raw = body[1..].TrimStart('&', '!');
            if (!ulong.TryParse(raw, out var id)) return false;

            var name = isRole
                ? resolver.ResolveRole?.Invoke(id)
                : resolver.ResolveUser?.Invoke(id);

            mentionsMe = !isRole && resolver.IsSelfUser(id);
            segment = new ContentSegment
            {
                Kind = SegmentKind.Mention,
                Text = "@" + (name ?? (isRole ? "role" : "user")),
                TargetsMe = mentionsMe,
            };
            return true;
        }

        if (body.StartsWith('#'))
        {
            if (!ulong.TryParse(body[1..], out var channelId)) return false;
            var name = resolver.ResolveChannel?.Invoke(channelId);
            segment = new ContentSegment
            {
                Kind = SegmentKind.ChannelRef,
                Text = "#" + (name ?? "channel"),
            };
            return true;
        }

        return false;
    }

    private static bool TryReadShortcode(
        string content,
        int index,
        ChatboxConfig cfg,
        MentionResolver resolver,
        out int consumed,
        out string label,
        out string imageUrl)
    {
        consumed = 0;
        label = string.Empty;
        imageUrl = string.Empty;

        var close = content.IndexOf(':', index + 1);
        if (close < 0 || close == index + 1 || close - index > 48) return false;

        var name = content.Substring(index + 1, close - index - 1);
        foreach (var ch in name)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '+' && ch != '-')
                return false;
        }

        consumed = close - index + 1;
        label = $":{name}:";

        if (cfg.RenderGuildEmotesByName)
        {
            var guildUrl = resolver.ResolveEmoteByName?.Invoke(name);
            if (!string.IsNullOrEmpty(guildUrl))
            {
                imageUrl = guildUrl!;
                return true;
            }
        }

        if (!cfg.RenderUnicodeEmoji || !EmojiIndex.TryGetShortcode(name, out var unicode))
            return false;

        var codepoints = EmojiIndex.ToCodePointName(unicode);
        if (codepoints.Length == 0) return false;

        imageUrl = $"{cfg.TwemojiBaseUrl.TrimEnd('/')}/{codepoints}.png";
        return true;
    }

    private static bool TryReadUrl(string content, int index, out int consumed, out string url)
    {
        consumed = 0;
        url = string.Empty;

        var isHttps = Matches(content, index, "https://");
        if (!isHttps && !Matches(content, index, "http://")) return false;

        var end = index;
        while (end < content.Length && !char.IsWhiteSpace(content[end]) && content[end] != '<' && content[end] != '>')
            end++;

        while (end > index && ".,!?;:)".IndexOf(content[end - 1]) >= 0)
            end--;

        consumed = end - index;
        if (consumed <= (isHttps ? 8 : 7)) return false;

        url = content.Substring(index, consumed);
        return true;
    }

    private static readonly Regex EmoteUrlRegex = new(
        @"^https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/(?<id>\d{5,25})\.(?<ext>png|gif|webp|jpe?g)(?:\?(?<query>\S*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmoteUrlNameRegex = new(
        @"(?:^|&)name=(?<name>[A-Za-z0-9_~%\-]{1,64})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmoteUrlAnimatedRegex = new(
        @"(?:^|&)animated=true",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryReadEmoteUrl(string url, out string label, out string imageUrl) =>
        TryReadEmoteUrl(url, null, out label, out imageUrl);

    public static bool TryReadEmoteUrl(string url, MentionResolver? resolver, out string label, out string imageUrl)
    {
        label = string.Empty;
        imageUrl = string.Empty;

        if (string.IsNullOrEmpty(url)) return false;

        var match = EmoteUrlRegex.Match(url.TrimEnd('.', ',', ')', ']', '!', '?'));
        if (!match.Success) return false;
        if (!ulong.TryParse(match.Groups["id"].Value, out var emoteId)) return false;

        var query = match.Groups["query"].Success ? match.Groups["query"].Value : string.Empty;

        var animated = string.Equals(match.Groups["ext"].Value, "gif", StringComparison.OrdinalIgnoreCase)
                       || EmoteUrlAnimatedRegex.IsMatch(query);

        var name = string.Empty;

        var nameMatch = EmoteUrlNameRegex.Match(query);
        if (nameMatch.Success)
        {
            var decoded = System.Net.WebUtility.UrlDecode(nameMatch.Groups["name"].Value);
            if (!string.IsNullOrWhiteSpace(decoded)) name = decoded;
        }

        if (name.Length == 0) name = resolver?.ResolveEmoteNameById?.Invoke(emoteId) ?? string.Empty;
        if (name.Length == 0) name = "emote";

        label = $":{name}:";
        imageUrl = CustomEmoteUrl(emoteId, animated);
        return true;
    }

    private static readonly Regex MarkdownEmoteRegex = new(
        @"^\[([^\]]+)\]\(https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/(\d+)\.([A-Za-z0-9]+)(?:\?[^)]*)?\)",
        RegexOptions.Compiled);

    private static bool TryReadMarkdownEmote(string content, int index, out int consumed, out string label, out string imageUrl)
    {
        consumed = 0;
        label = string.Empty;
        imageUrl = string.Empty;

        var slice = content.Substring(index);
        var match = MarkdownEmoteRegex.Match(slice);
        if (!match.Success) return false;

        consumed = match.Length;
        var name = match.Groups[1].Value;
        var idStr = match.Groups[2].Value;
        var ext = match.Groups[3].Value;
        var animated = string.Equals(ext, "gif", StringComparison.OrdinalIgnoreCase);

        label = $":{name}:";
        if (ulong.TryParse(idStr, out var emoteId))
        {
            imageUrl = CustomEmoteUrl(emoteId, animated);
            return true;
        }

        imageUrl = match.Value;
        return true;
    }

    public static string CustomEmoteUrl(ulong emoteId, bool animated = false) =>
        $"https://cdn.discordapp.com/emojis/{emoteId}.{(animated ? "gif" : "png")}?size=48&quality=lossless";

    public static string CustomEmoteLink(ulong emoteId, bool animated = false) =>
        animated
            ? $"https://cdn.discordapp.com/emojis/{emoteId}.webp?size=96&animated=true"
            : $"https://cdn.discordapp.com/emojis/{emoteId}.webp?size=96";

    private static readonly Dictionary<string, uint> _itemNameCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _itemCacheInitialized;

    public static uint FindItemIdByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        lock (_itemNameCache)
        {
            if (!_itemCacheInitialized)
            {
                var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
                if (sheet != null)
                {
                    foreach (var item in sheet)
                    {
                        var n = item.Name.ExtractText();
                        if (!string.IsNullOrEmpty(n) && !_itemNameCache.ContainsKey(n))
                            _itemNameCache[n] = item.RowId;
                    }
                }
                _itemCacheInitialized = true;
            }

            return _itemNameCache.TryGetValue(name, out var id) ? id : 0;
        }
    }

    private static bool TryReadItemLink(string content, int index, out int consumed, out ContentSegment segment)
    {
        consumed = 0;
        segment = ContentSegment.PlainText(string.Empty);

        if (index >= content.Length || content[index] != '[') return false;
        var close = content.IndexOf(']', index + 1);
        if (close < 0 || close - index > 120) return false;

        var body = content.Substring(index + 1, close - index - 1).Trim();
        if (!body.StartsWith('')) return false;

        var isHq = body.EndsWith("") || body.EndsWith("(HQ)", StringComparison.OrdinalIgnoreCase);
        var name = body.TrimStart('').TrimEnd('').Replace("(HQ)", "", StringComparison.OrdinalIgnoreCase).Trim();

        var itemId = FindItemIdByName(name);
        var fullText = content.Substring(index, close - index + 1);
        consumed = close - index + 1;

        segment = new ContentSegment
        {
            Kind = SegmentKind.ItemLink,
            Text = fullText,
            ItemId = itemId,
            IsHq = isHq,
        };
        return true;
    }
}
