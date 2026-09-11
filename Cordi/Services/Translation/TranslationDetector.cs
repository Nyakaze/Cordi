using System;
using System.Collections.Generic;

namespace Cordi.Services.Translation;

public static class TranslationDetector
{
    private const int MinimumTokenHits = 2;
    private const double DominanceRatio = 1.35;

    private static readonly Dictionary<string, string[]> Stopwords = new(StringComparer.Ordinal)
    {
        ["en"] =
        [
            "the", "and", "you", "are", "for", "that", "this", "with", "have", "what", "was", "not", "but", "can",
            "all", "your", "just", "they", "from", "please", "thanks", "thank", "hello", "help", "need", "want",
            "good", "yes", "sorry", "who", "how", "when", "will", "does", "any", "one", "get", "got", "let", "make",
            "my", "is", "it", "to", "of", "at", "do", "don't", "dont", "doesn't", "didn't", "can't", "cant",
            "i'm", "im", "he", "she", "him", "her", "his", "hers", "be", "so", "now", "here", "there", "also",
            "like", "know", "think", "really", "still", "back", "going", "gonna", "wanna", "too", "then", "some",
            "much", "many", "more", "most", "only", "its", "it's", "has", "had", "were", "been", "being", "see",
            "look", "come", "came", "take", "time", "well", "why", "where", "which", "about", "would", "could",
            "should", "because", "after", "before", "right", "thing", "things", "people", "someone", "everyone",
            "everything", "something", "nothing", "anyone", "anything", "said", "say", "says", "went", "doing",
            "done", "made", "give", "tell", "told", "put", "keep", "feel", "love", "hate", "wait", "again",
            "maybe", "sure", "okay", "guys", "yeah", "yep", "nope", "nah", "actually", "probably", "though",
            "tho", "even", "over", "into", "than", "them", "their", "there's", "theres", "we're", "weve",
            "you're", "youre", "won't", "wont", "always", "never", "already", "every", "other", "another",
        ],
        ["de"] =
        [
            "der", "die", "das", "und", "ist", "nicht", "ich", "wir", "ihr", "sie", "mit", "auf", "für", "ein",
            "eine", "kein", "sich", "noch", "auch", "wie", "danke", "bitte", "hallo", "aber", "schon", "haben",
            "kann", "wenn", "oder", "gibt", "hier", "mehr", "sehr", "nach", "dann",
            "du", "mir", "mich", "dir", "dich", "nichts", "etwas", "jemand", "niemand", "jetzt", "immer", "wieder",
            "nur", "mal", "halt", "doch", "denn", "weil", "dass", "vielleicht", "wirklich", "gerade", "gleich",
            "bisschen", "leute", "machen", "gemacht", "gehen", "geht", "kommt", "kommen", "sagen", "gesagt",
            "weiß", "weiss", "glaube", "denke", "brauche", "brauchen", "muss", "müssen", "soll", "sollen",
            "wollen", "würde", "könnte", "hätte", "warte", "warten", "tschüss", "gute", "guten", "viel", "viele",
            "alle", "alles", "keine", "diese", "dieser", "dieses", "meine", "mein", "deine", "dein", "seine",
            "unser", "euch", "uns", "ihnen", "beim", "vom", "zum", "zur", "über", "unter", "gegen", "ohne",
            "durch", "seit", "dort", "heute", "morgen", "gestern", "eigentlich", "einfach", "richtig", "besser",
        ],
        ["fr"] =
        [
            "les", "des", "une", "est", "pas", "que", "qui", "pour", "avec", "dans", "vous", "nous", "elle",
            "merci", "bonjour", "salut", "oui", "non", "mais", "tout", "plus", "bien", "sur", "aussi", "être",
            "comme", "faire", "quoi", "peut",
            "ils", "moi", "toi", "mon", "mes", "ton", "ses", "son", "cette", "ça", "ca", "c'est", "j'ai",
            "tu", "il", "tous", "toutes", "fait", "peux", "veux", "veut", "dois", "doit", "sais", "sait",
            "alors", "encore", "jamais", "toujours", "vraiment", "beaucoup", "demain", "quand", "pourquoi",
            "comment", "où", "ici", "gens", "monde", "temps", "chose", "truc", "ouais", "trop", "avoir",
            "était", "sont", "êtes", "sommes", "puis", "donc", "parce", "chez", "leur", "nos", "vos",
        ],
        ["es"] =
        [
            "los", "las", "que", "por", "para", "con", "como", "hola", "gracias", "muy", "pero", "más", "tengo",
            "está", "esta", "una", "del", "también", "ahora", "puede", "hacer", "todo", "bien", "aquí", "sí",
            "mas", "tienes", "tiene", "están", "estan", "puedo", "todos", "eres", "soy", "somos", "estoy",
            "estás", "quiero", "quieres", "necesito", "gente", "cosa", "cosas", "cuando", "dónde", "porque",
            "entonces", "siempre", "nunca", "mucho", "mucha", "algo", "alguien", "nada", "nadie", "vamos",
            "vale", "claro", "bueno", "buenas", "buenos", "oye", "jaja", "ustedes", "nosotros", "ellos",
            "hay", "ser", "estar", "sobre", "desde", "hasta", "porqué", "quién", "cuál", "otro", "otra",
        ],
        ["it"] =
        [
            "che", "gli", "non", "per", "con", "come", "ciao", "grazie", "molto", "più", "sono", "questo",
            "anche", "della", "delle", "però", "adesso", "fare", "bene", "qui", "sì", "una", "del",
            "piu", "pero", "questa", "dei", "degli", "ti", "ci", "vi", "io", "lui", "lei", "noi", "voi",
            "devo", "posso", "voglio", "sai", "siamo", "siete", "cosa", "quando", "dove", "perché", "perche",
            "sempre", "mai", "allora", "ancora", "ragazzi", "ecco", "essere", "avere", "fatto", "detto",
            "tutto", "tutti", "niente", "qualcosa", "qualcuno", "grazie", "prego", "scusa", "davvero", "quindi",
        ],
        ["pt"] =
        [
            "que", "não", "por", "para", "com", "como", "olá", "obrigado", "muito", "mais", "está", "você",
            "uma", "dos", "das", "também", "agora", "fazer", "bem", "aqui", "sim", "isso",
            "nao", "ola", "obrigada", "voce", "tambem", "eu", "ele", "ela", "nós", "vocês", "voces", "tenho",
            "tem", "quero", "preciso", "gente", "coisa", "quando", "onde", "porque", "então", "entao",
            "sempre", "nunca", "algo", "alguém", "alguem", "nada", "vamos", "né", "cara", "valeu", "tudo",
            "todos", "ainda", "depois", "antes", "sobre", "pode", "posso", "fazendo", "feito", "dizer",
        ],
        ["nl"] =
        [
            "het", "een", "niet", "ik", "je", "met", "voor", "dat", "wat", "hoe", "dank", "hallo", "maar",
            "ook", "naar", "zijn", "heb", "kan", "even", "goed", "wel",
            "jij", "wij", "zij", "hij", "ze", "van", "op", "te", "om", "als", "dan", "nog", "daar", "waar",
            "wanneer", "waarom", "echt", "altijd", "nooit", "veel", "weinig", "iets", "iemand", "niets",
            "niemand", "mensen", "ding", "doen", "gaan", "komen", "zeggen", "weet", "denk", "moet", "wil",
            "kunnen", "graag", "alsjeblieft", "bedankt", "doei", "hoi", "misschien", "gewoon", "helemaal",
        ],
    };

    public static string? DetectLocal(string text)
    {
        var script = DetectScript(text);
        if (script != null) return script;

        return DetectLatin(text);
    }

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

    private static string? DetectLatin(string text)
    {
        var tokens = Tokenize(text);
        if (tokens.Count == 0) return null;

        string? best = null;
        var bestScore = 0d;
        var runnerUp = 0d;

        foreach (var (iso, words) in Stopwords)
        {
            var hits = 0;

            foreach (var token in tokens)
            {
                foreach (var word in words)
                {
                    if (!string.Equals(token, word, StringComparison.Ordinal)) continue;
                    hits++;
                    break;
                }
            }

            var score = hits + DiacriticBonus(iso, text);

            if (score > bestScore)
            {
                runnerUp = bestScore;
                bestScore = score;
                best = iso;
            }
            else if (score > runnerUp)
            {
                runnerUp = score;
            }
        }

        if (best == null || bestScore < MinimumTokenHits) return null;
        if (runnerUp > 0 && bestScore < runnerUp * DominanceRatio) return null;

        return best;
    }

    private static double DiacriticBonus(string iso, string text)
    {
        var bonus = 0d;

        foreach (var c in text)
        {
            bonus += iso switch
            {
                "de" when c is 'ä' or 'ö' or 'ü' or 'ß' => 0.5,
                "fr" when c is 'é' or 'è' or 'ê' or 'à' or 'ç' or 'ù' => 0.5,
                "es" when c is 'ñ' or '¿' or '¡' => 0.75,
                "pt" when c is 'ã' or 'õ' or 'ç' => 0.5,
                "it" when c is 'è' or 'ò' or 'à' => 0.25,
                _ => 0d,
            };
        }

        return Math.Min(bonus, 2d);
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var start = -1;

        for (var i = 0; i <= text.Length; i++)
        {
            var isLetter = i < text.Length && (char.IsLetter(text[i]) || text[i] == '\'');

            if (isLetter)
            {
                if (start < 0) start = i;
                continue;
            }

            if (start < 0) continue;

            tokens.Add(text[start..i].ToLowerInvariant());
            start = -1;
        }

        return tokens;
    }
}
