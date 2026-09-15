namespace Cordi.Services.Translation;

public static class TranslationDetector
{
    public static string? DetectScript(string text)
    {
        var kana = false;
        var han = false;
        var hangul = false;
        var cyrillic = false;
        var ukrainian = false;
        var greek = false;
        var arabic = false;
        var hebrew = false;
        var thai = false;
        var devanagari = false;

        foreach (var c in text)
        {
            if (c is >= '぀' and <= 'ヿ') kana = true;
            else if (c is >= '一' and <= '鿿' or >= '㐀' and <= '䶿') han = true;
            else if (c is >= '가' and <= '힯' or >= 'ᄀ' and <= 'ᇿ'
                     or >= '㄰' and <= '㆏') hangul = true;
            else if (c is >= 'Ѐ' and <= 'ӿ')
            {
                cyrillic = true;
                if (c is 'і' or 'ї' or 'є' or 'ґ'
                    or 'І' or 'Ї' or 'Є' or 'Ґ') ukrainian = true;
            }
            else if (c is >= 'Ͱ' and <= 'Ͽ') greek = true;
            else if (c is >= '؀' and <= 'ۿ') arabic = true;
            else if (c is >= '֐' and <= '׿') hebrew = true;
            else if (c is >= '฀' and <= '๿') thai = true;
            else if (c is >= 'ऀ' and <= 'ॿ') devanagari = true;
        }

        if (kana) return "ja";
        if (hangul) return "ko";
        if (han) return "zh";
        if (cyrillic) return ukrainian ? "uk" : "ru";
        if (greek) return "el";
        if (arabic) return "ar";
        if (hebrew) return "he";
        if (thai) return "th";
        if (devanagari) return "hi";

        return null;
    }
}
