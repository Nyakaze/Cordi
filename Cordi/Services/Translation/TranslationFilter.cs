using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Cordi.Services.Chatbox;

namespace Cordi.Services.Translation;

public static partial class TranslationFilter
{
    private const int MacroWindowMs = 650;
    private const int MacroTrackLimit = 24;
    private const int MacroPruneMs = 3000;

    [GeneratedRegex(@"^<se\.\d+>$")]
    private static partial Regex SoundMacroRegex();

    [GeneratedRegex(@"^\s*https?://\S+(\s+https?://\S+)*\s*$")]
    private static partial Regex PureLinkRegex();

    [GeneratedRegex(@"[㐀-䶿一-鿿぀-ヿ가-힯]|\p{L}{2,}")]
    private static partial Regex TranslatableRegex();

    [GeneratedRegex(@"[-]+")]
    private static partial Regex PrivateUseRegex();

    public static string CleanText(ChatboxMessage message)
    {
        var builder = new StringBuilder();

        foreach (var segment in message.Segments)
        {
            switch (segment.Kind)
            {
                case SegmentKind.Text:
                    builder.Append(segment.Text);
                    break;

                case SegmentKind.LineBreak:
                    builder.Append(' ');
                    break;
            }
        }

        var text = builder.Length > 0 ? builder.ToString() : message.RawContent;

        return PrivateUseRegex().Replace(text, string.Empty).Trim();
    }

    public static bool IsTranslatable(string text, int minimumLength)
    {
        if (text.Length < Math.Max(1, minimumLength)) return false;
        if (!TranslatableRegex().IsMatch(text)) return false;
        if (PureLinkRegex().IsMatch(text)) return false;
        if (SoundMacroRegex().IsMatch(text)) return false;

        return true;
    }

    public static bool SameMeaning(string original, string translated) =>
        string.Equals(Normalize(original), Normalize(translated), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    public sealed class MacroGuard
    {
        private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        public bool IsSpam(string authorKey)
        {
            var now = Environment.TickCount;

            lock (_gate)
            {
                if (_seen.Count > MacroTrackLimit) Prune(now);

                var spam = _seen.TryGetValue(authorKey, out var last) && now - last < MacroWindowMs;
                _seen[authorKey] = now;

                return spam;
            }
        }

        private void Prune(int now)
        {
            var stale = new List<string>();

            foreach (var (key, stamp) in _seen)
            {
                if (now - stamp > MacroPruneMs) stale.Add(key);
            }

            foreach (var key in stale)
                _seen.Remove(key);
        }
    }
}
