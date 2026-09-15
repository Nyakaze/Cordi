using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Lingua;
using LinguaEngine = Lingua.LanguageDetector;

namespace Cordi.Services.Translation;

public sealed class LinguaDetector
{
    private const string LogSource = "Translation";
    private const double MinimumRelativeDistance = 0.2;
    private const int ChannelMemoryMs = 300_000;
    private const int ChannelHalfLifeMs = 150_000;
    private const double LengthSaturation = 20d;
    private const double EnglishTokenBoost = 0.10;

    private static readonly string[] NgramFiles =
        ["unigrams.json.br", "bigrams.json.br", "trigrams.json.br", "quadrigrams.json.br", "fivegrams.json.br"];

    private const string ModelBaseUrl =
        "https://raw.githubusercontent.com/searchpioneer/lingua-dotnet/1.0.5/src/Lingua/LanguageModels";

    private static readonly HashSet<string> ShippedIsos =
        new(StringComparer.OrdinalIgnoreCase) { "de", "en", "es", "fr", "ja", "ko", "zh" };

    private static readonly (string Iso, Language Language)[] Supported =
    [
        ("en", Language.English),
        ("ja", Language.Japanese),
        ("de", Language.German),
        ("fr", Language.French),
        ("zh", Language.Chinese),
        ("ko", Language.Korean),
        ("es", Language.Spanish),
        ("pt", Language.Portuguese),
        ("it", Language.Italian),
        ("nl", Language.Dutch),
        ("ru", Language.Russian),
        ("uk", Language.Ukrainian),
        ("pl", Language.Polish),
        ("tr", Language.Turkish),
        ("sv", Language.Swedish),
        ("da", Language.Danish),
        ("fi", Language.Finnish),
        ("nb", Language.Bokmal),
        ("cs", Language.Czech),
        ("el", Language.Greek),
        ("hu", Language.Hungarian),
        ("ro", Language.Romanian),
        ("bg", Language.Bulgarian),
        ("sk", Language.Slovak),
        ("sl", Language.Slovene),
        ("lv", Language.Latvian),
        ("lt", Language.Lithuanian),
        ("et", Language.Estonian),
        ("id", Language.Indonesian),
        ("ar", Language.Arabic),
        ("he", Language.Hebrew),
        ("hi", Language.Hindi),
        ("th", Language.Thai),
        ("vi", Language.Vietnamese),
    ];

    private static readonly Dictionary<string, Language> IsoToLanguage =
        Supported.ToDictionary(entry => entry.Iso, entry => entry.Language, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<Language, string> LanguageToIso =
        Supported.GroupBy(entry => entry.Language).ToDictionary(group => group.Key, group => group.First().Iso);

    private readonly CordiLogService _log;
    private readonly HttpClient _http;
    private readonly string _modelsDirectory;
    private readonly object _buildGate = new();
    private readonly Dictionary<XivChatType, (int Tick, string? Iso)> _channelCache = new();

    private volatile LinguaEngine? _engine;
    private bool _disposed;

    public LinguaDetector(CordiLogService log, HttpClient http, string modelsDirectory)
    {
        _log = log;
        _http = http;
        _modelsDirectory = modelsDirectory;
    }

    public bool IsReady => _engine != null;

    public static bool Supports(string? iso) => iso != null && IsoToLanguage.ContainsKey(iso);

    public void RecordChannel(XivChatType channel, string? iso)
    {
        lock (_channelCache)
        {
            _channelCache[channel] = (Environment.TickCount, iso);
        }
    }

    public (double Score, string? Iso) Classify(string text)
    {
        var engine = _engine;
        if (engine == null) return (0d, null);

        try
        {
            var top = engine.ComputeLanguageConfidenceValues(text).FirstOrDefault();
            if (top.Key == Language.Unknown) return (0d, null);

            return (top.Value, LanguageToIso.GetValueOrDefault(top.Key));
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Debug, "Lingua classification failed", ex);
            return (0d, null);
        }
    }

    public async Task<(double Reliability, string? Iso)> ComputeReliabilityAsync(
        string text,
        XivChatType channel,
        bool hasEnglishToken)
    {
        var (confidence, iso) = await Task.Run(() => Classify(text)).ConfigureAwait(false);

        if (hasEnglishToken && string.Equals(iso, "en", StringComparison.Ordinal))
            confidence = Math.Min(confidence + EnglishTokenBoost, 1d);

        var lengthFactor = Math.Clamp((text.Length + CountCjk(text) * 2) / LengthSaturation, 0d, 1d);
        var channelBoost = ChannelBoost(channel, iso, lengthFactor);
        var reliability = Math.Clamp((confidence * lengthFactor) + (channelBoost * (1d - lengthFactor) * 0.5d), 0d, 1d);

        return (reliability, iso);
    }

    private double ChannelBoost(XivChatType channel, string? iso, double lengthFactor)
    {
        if (iso == null) return 0d;

        (int Tick, string? Iso) cached;

        lock (_channelCache)
        {
            if (!_channelCache.TryGetValue(channel, out cached)) return 0d;
        }

        if (cached.Iso == null) return 0d;

        var elapsed = Environment.TickCount - cached.Tick;
        if (elapsed is < 0 or >= ChannelMemoryMs) return 0d;

        var decay = Math.Exp(-elapsed * Math.Log(2) / ChannelHalfLifeMs);

        return string.Equals(cached.Iso, iso, StringComparison.Ordinal)
            ? decay * Math.Max(1d - lengthFactor, 0.15d) * 0.7d
            : -decay * (1d - lengthFactor) * 0.15d;
    }

    private static int CountCjk(string text)
    {
        var count = 0;

        foreach (var c in text)
        {
            if (c is >= '⺀' and <= '鿿' or >= '가' and <= '힯' or >= '･' and <= 'ﾟ') count++;
        }

        return count;
    }

    public async Task RebuildAsync(IEnumerable<string> requestedIsos)
    {
        if (_disposed) return;

        try
        {
            var isos = new HashSet<string>(ShippedIsos, StringComparer.OrdinalIgnoreCase);

            foreach (var iso in requestedIsos)
            {
                var normalized = TranslationLanguages.Normalize(iso);
                if (IsoToLanguage.ContainsKey(normalized)) isos.Add(normalized);
            }

            await DownloadMissingModelsAsync(isos).ConfigureAwait(false);

            var languages = new HashSet<Language>();

            foreach (var iso in isos)
            {
                if (IsoToLanguage.TryGetValue(iso, out var language) && HasModel(iso)) languages.Add(language);
            }

            if (languages.Count < 2)
            {
                _log.Log(LogSource, CordiLogLevel.Warning, "Lingua has fewer than two usable language models - local detection stays off");
                return;
            }

            lock (_buildGate)
            {
                if (_disposed) return;

                var previous = _engine;

                _engine = LanguageDetectorBuilder
                    .FromLanguages([.. languages])
                    .WithMinimumRelativeDistance(MinimumRelativeDistance)
                    .WithLanguageModelsDirectory(_modelsDirectory)
                    .WithPreloadedLanguageModels()
                    .Build();

                previous?.UnloadLanguageModels();
            }

            _log.Debug(LogSource, $"Lingua detector built with {languages.Count} languages");
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, "Failed to build the Lingua detector", ex);
        }
    }

    private bool HasModel(string iso)
    {
        var directory = Path.Combine(_modelsDirectory, iso);

        return Directory.Exists(directory) && Directory.EnumerateFiles(directory).Any();
    }

    private async Task DownloadMissingModelsAsync(IEnumerable<string> isos)
    {
        foreach (var iso in isos)
        {
            if (ShippedIsos.Contains(iso) || HasModel(iso)) continue;

            await DownloadModelAsync(iso).ConfigureAwait(false);
        }
    }

    private async Task DownloadModelAsync(string iso)
    {
        var directory = Path.Combine(_modelsDirectory, iso);

        try
        {
            Directory.CreateDirectory(directory);

            foreach (var file in NgramFiles)
            {
                using var response = await _http.GetAsync($"{ModelBaseUrl}/{iso}/{file}").ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(directory, file), bytes).ConfigureAwait(false);
            }

            _log.Debug(LogSource, $"Downloaded the Lingua language model for '{iso}'");
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, $"Failed to download the Lingua language model for '{iso}'", ex);
        }
    }

    public void Dispose()
    {
        lock (_buildGate)
        {
            if (_disposed) return;
            _disposed = true;

            _engine?.UnloadLanguageModels();
            _engine = null;
        }
    }
}
