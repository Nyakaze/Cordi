using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cordi.Services.Chatbox;

public static class ChatboxRtlText
{
    public static readonly ushort[] GlyphRanges =
    {
        0x0590, 0x05FF,
        0x0600, 0x06FF,
        0x0750, 0x077F,
        0xFB1D, 0xFDFF,
        0xFE70, 0xFEFF,
        0,
    };

    private static readonly Dictionary<char, (char Isolated, int Forms)> Shapes = BuildShapes();

    private static Dictionary<char, (char Isolated, int Forms)> BuildShapes()
    {
        var shapes = new Dictionary<char, (char, int)>();

        var forms = new[]
        {
            1, 2, 2, 2, 2, 4, 2, 4, 2, 4, 4, 4, 4, 4, 2, 2, 2, 2, 4, 4, 4, 4, 4, 4, 4, 4,
        };

        var isolated = 0xFE80;
        for (var i = 0; i < forms.Length; i++)
        {
            shapes[(char)(0x0621 + i)] = ((char)isolated, forms[i]);
            isolated += forms[i];
        }

        var tail = new[] { 4, 4, 4, 4, 4, 4, 4, 2, 2, 4 };
        for (var i = 0; i < tail.Length; i++)
        {
            shapes[(char)(0x0641 + i)] = ((char)isolated, tail[i]);
            isolated += tail[i];
        }

        shapes['پ'] = ('ﭖ', 4);
        shapes['چ'] = ('ﭺ', 4);
        shapes['ژ'] = ('ﮊ', 2);
        shapes['ک'] = ('ﮎ', 4);
        shapes['گ'] = ('ﮒ', 4);
        shapes['ی'] = ('ﯼ', 4);

        return shapes;
    }

    private const char Tatweel = 'ـ';
    private const char Lam = 'ل';

    public static bool HasRtl(string text)
    {
        foreach (var c in text)
            if (IsRtl(c)) return true;

        return false;
    }

    private static bool IsRtl(char c) =>
        c is >= '֐' and <= '׿'
            or >= '؀' and <= 'ٟ'
            or >= '٪' and <= 'ۯ'
            or >= 'ۺ' and <= 'ۿ'
            or >= 'ݐ' and <= 'ݿ'
            or >= 'יִ' and <= '﷿'
            or >= 'ﹰ' and <= '﻿';

    private static bool IsStrongLtr(char c) => char.IsLetterOrDigit(c) && !IsRtl(c);

    private static bool IsMark(char c) =>
        CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark;

    public static bool IsRtlParagraph(string text)
    {
        foreach (var c in text)
        {
            if (IsRtl(c)) return true;
            if (IsStrongLtr(c)) return false;
        }

        return false;
    }

    public static string Shape(string text)
    {
        var builder = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (!Joins(c, out var shape))
            {
                builder.Append(c);
                continue;
            }

            var previous = Neighbor(text, i, -1);
            var joinsPrevious = shape.Forms >= 2 && Joins(previous, out var before) && before.Forms == 4;

            if (c == Lam && i + 1 < text.Length && LamAlef(text[i + 1]) is var ligature && ligature != 0)
            {
                builder.Append((char)(ligature + (joinsPrevious ? 1 : 0)));
                i++;
                continue;
            }

            var next = Neighbor(text, i, 1);
            var joinsNext = shape.Forms == 4 && Joins(next, out var after) && after.Forms >= 2;

            if (c == Tatweel)
            {
                builder.Append(c);
                continue;
            }

            var offset = (joinsPrevious, joinsNext) switch
            {
                (true, true) => 3,
                (false, true) => 2,
                (true, false) => 1,
                _ => 0,
            };

            builder.Append((char)(shape.Isolated + offset));
        }

        return builder.ToString();
    }

    private static bool Joins(char c, out (char Isolated, int Forms) shape)
    {
        if (c == Tatweel)
        {
            shape = (Tatweel, 4);
            return true;
        }

        return Shapes.TryGetValue(c, out shape);
    }

    private static char Neighbor(string text, int index, int step)
    {
        for (var i = index + step; i >= 0 && i < text.Length; i += step)
        {
            if (!IsMark(text[i])) return text[i];
        }

        return '\0';
    }

    private static int LamAlef(char c) => c switch
    {
        'آ' => 0xFEF5,
        'أ' => 0xFEF7,
        'إ' => 0xFEF9,
        'ا' => 0xFEFB,
        _ => 0,
    };

    public static string Visual(string line, bool rtlParagraph)
    {
        var units = Units(line);
        var builder = new StringBuilder(line.Length);

        if (rtlParagraph)
        {
            var end = units.Count;
            while (end > 0)
            {
                var start = end - 1;

                if (IsStrongLtr(units[start][0]))
                {
                    while (start > 0 && !IsRtl(units[start - 1][0])) start--;
                    while (start < end - 1 && !IsStrongLtr(units[start][0])) start++;

                    for (var i = start; i < end; i++) builder.Append(units[i]);
                }
                else
                {
                    builder.Append(Mirror(units[start]));
                }

                end = start;
            }

            return builder.ToString();
        }

        var index = 0;
        while (index < units.Count)
        {
            if (!IsRtl(units[index][0]))
            {
                builder.Append(units[index++]);
                continue;
            }

            var last = index;
            for (var i = index; i < units.Count && !IsStrongLtr(units[i][0]); i++)
                if (IsRtl(units[i][0])) last = i;

            for (var i = last; i >= index; i--) builder.Append(Mirror(units[i]));

            index = last + 1;
        }

        return builder.ToString();
    }

    private static List<string> Units(string text)
    {
        var units = new List<string>(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            var start = i++;
            if (char.IsHighSurrogate(text[start]) && i < text.Length && char.IsLowSurrogate(text[i])) i++;
            while (i < text.Length && IsMark(text[i])) i++;

            units.Add(text[start..i]);
        }

        return units;
    }

    private static string Mirror(string unit) => unit switch
    {
        "(" => ")",
        ")" => "(",
        "[" => "]",
        "]" => "[",
        "{" => "}",
        "}" => "{",
        "<" => ">",
        ">" => "<",
        _ => unit,
    };
}
