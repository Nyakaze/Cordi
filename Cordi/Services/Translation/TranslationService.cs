using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;

namespace Cordi.Services.Translation;

public sealed class TranslationService : IDisposable
{
    private const string LogSource = "Translation";
    private const string CacheProvider = "Cache";
    private const string PhraseProvider = "Phrases";
    private const int MaxInFlight = 32;
    private const int ContextLines = 8;
    private const int MachineSpacingMs = 200;
    private const int MaxQueueWaitMs = 15000;
    private const double CooldownBaseSeconds = 30d;
    private const double CooldownCapSeconds = 480d;
    private const double CooldownDecaySeconds = 600d;
    private const int MaxCooldownStep = 5;

    private readonly CordiLogService _log;
    private readonly Func<TranslationConfig> _config;
    private readonly HttpClient _http;
    private readonly TranslationCache _cache;
    private readonly TranslationFilter.MacroGuard _macroGuard = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _slots;

    private readonly Dictionary<string, List<ChatboxMessage>> _inFlight = new(StringComparer.Ordinal);
    private readonly object _inFlightGate = new();

    private readonly Queue<string> _context = new();
    private readonly object _contextGate = new();

    private readonly object _throttleGate = new();
    private DateTime _nextRequestAt = DateTime.MinValue;
    private DateTime _cooldownUntil = DateTime.MinValue;
    private DateTime _lastLimitAt = DateTime.MinValue;
    private int _cooldownStep;

    private readonly MachineTranslationProvider _machine;
    private readonly DeepLTranslationProvider _deepl;
    private readonly LlmTranslationProvider _llm;
    private readonly LinguaDetector _lingua;

    private bool _disposed;

    public TranslationService(string configDirectory, Func<TranslationConfig> config, CordiLogService log)
    {
        _config = config;
        _log = log;

        _http = CreateClient(config().RequestTimeoutSeconds);
        _cache = new TranslationCache(configDirectory, () => config().CacheSize, log);
        _cache.Load();

        _slots = new SemaphoreSlim(Math.Clamp(config().MaxParallelRequests, 1, 8));

        _machine = new MachineTranslationProvider(_http, log);
        _deepl = new DeepLTranslationProvider(_http, () => config().DeepLApiKey, () => config().DeepLPro);
        _llm = new LlmTranslationProvider(
            _http,
            () => config().LlmEndpoint,
            () => config().LlmApiKey,
            () => config().LlmModel,
            () => config().LlmPrompt);

        _lingua = new LinguaDetector(log, _http, ResolveModelsDirectory());

        RebuildDetector();
    }

    public event Action<ChatboxMessage>? Translated;

    public TranslationStats Stats { get; } = new();

    public int CachedEntries => _cache.Count;

    public int KnownPhrases => TranslationPhrases.Count;

    public bool IsDetectorReady => _lingua.IsReady;

    public bool IsProviderReady => ActiveProvider.IsConfigured;

    public string ActiveProviderName => ActiveProvider.Name;

    public TimeSpan CooldownRemaining
    {
        get
        {
            lock (_throttleGate)
            {
                var remaining = _cooldownUntil - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
    }

    public bool IsRateLimited => CooldownRemaining > TimeSpan.Zero;

    private TranslationConfig Config => _config();

    private ITranslationProvider ActiveProvider => Config.Provider switch
    {
        TranslationProviderKind.DeepL when _deepl.IsConfigured => _deepl,
        TranslationProviderKind.Llm when _llm.IsConfigured => _llm,
        _ => _machine,
    };

    public void ClearCache() => _cache.Clear();

    public void RebuildDetector()
    {
        var wanted = new List<string>(Config.KnownLanguages);
        wanted.AddRange(Config.SourceLanguages);
        wanted.Add(Config.TargetLanguage);

        _ = Task.Run(() => _lingua.RebuildAsync(wanted), _cancellation.Token);
    }

    public void Consider(ChatboxMessage message)
    {
        if (_disposed || !Config.TranslatesAutomatically) return;
        if (message.TranslationState != TranslationState.None) return;
        if (!PassesGate(message)) return;

        Begin(message, false);
    }

    public void Request(ChatboxMessage message) => Request(message, null);

    public void Request(ChatboxMessage message, string? sourceIso)
    {
        if (_disposed || !Config.Enabled) return;
        if (message.TranslationState == TranslationState.Pending) return;
        if (sourceIso == null && message.TranslationState == TranslationState.Translated) return;

        message.TranslationState = TranslationState.None;

        Begin(message, true, sourceIso);
    }

    private void Begin(ChatboxMessage message, bool forced, string? sourceIso = null)
    {
        var text = TranslationFilter.CleanText(message);
        if (!TranslationFilter.IsTranslatable(text, forced ? 1 : Config.MinimumLength)) return;

        var target = TranslationLanguages.Normalize(Config.TargetLanguage);
        if (target.Length == 0) return;

        var source = sourceIso == null ? null : TranslationLanguages.Normalize(sourceIso);
        if (source is { Length: 0 }) source = null;

        var channel = message.GameChatType;

        if (source == null && Config.SkipChatNoise && !ResolvePhrase(message, text, target, channel, forced, out source))
            return;

        RecordContext(message.AuthorName, text);

        var key = TranslationCache.KeyFor(source, target, text);

        if (Config.CacheEnabled && _cache.TryGet(key, out var cached, out var cachedSource))
        {
            Stats.CacheHits++;
            Apply(message, cached, cachedSource ?? source, CacheProvider);
            return;
        }

        if (!forced && IsRateLimited) return;

        lock (_inFlightGate)
        {
            if (_inFlight.TryGetValue(key, out var waiters))
            {
                waiters.Add(message);
                message.TranslationState = TranslationState.Pending;
                return;
            }

            if (_inFlight.Count >= MaxInFlight) return;
            if (!forced && Config.SkipMacroSpam && !message.IsSelf && _macroGuard.IsSpam(message.AuthorKey)) return;

            _inFlight[key] = [message];
        }

        message.TranslationState = TranslationState.Pending;

        _ = Task.Run(() => RunAsync(key, text, target, channel, forced, source), _cancellation.Token);
    }

    private bool ResolvePhrase(
        ChatboxMessage message,
        string text,
        string target,
        XivChatType channel,
        bool forced,
        out string? source)
    {
        source = null;

        var phrase = TranslationPhrases.Resolve(text, target);

        switch (phrase.Outcome)
        {
            case PhraseOutcome.Swallow:
                if (forced) return true;

                Stats.Skipped++;
                return false;

            case PhraseOutcome.Identified:
                _lingua.RecordChannel(channel, phrase.Iso);

                var skip = ShouldSkipSource(phrase.Iso);

                if (phrase.Translation != null && (forced || !skip))
                {
                    Stats.PhraseHits++;
                    Apply(message, phrase.Translation, phrase.Iso, PhraseProvider);
                    return false;
                }

                if (!forced && skip)
                {
                    Stats.Skipped++;
                    return false;
                }

                source = phrase.Iso;
                return true;

            default:
                return true;
        }
    }

    public async Task<TranslationResult> TranslateTextAsync(string text, string targetIso)
    {
        var target = TranslationLanguages.Normalize(targetIso);
        if (target.Length == 0 || string.IsNullOrWhiteSpace(text)) return TranslationResult.Failure();

        var request = new TranslationRequest { Text = text.Trim(), TargetIso = target };

        return await RequestAsync(request, true, _cancellation.Token).ConfigureAwait(false);
    }

    private bool PassesGate(ChatboxMessage message)
    {
        if (message.IsSystemLine) return false;
        if (message.IsSelf && !Config.TranslateOwnMessages) return false;
        if (message.FilteredAsAd && Config.SkipFilteredMessages) return false;

        if (message.Origin == ChatboxOrigin.Discord && !Config.TranslateDiscordMessages) return false;

        if (message.Origin == ChatboxOrigin.Game)
        {
            if (!Config.ChatTypes.Contains(message.GameChatType)) return false;
            if (!Config.TranslateInDuty && Service.Condition[ConditionFlag.BoundByDuty]) return false;
        }

        return true;
    }

    private async Task RunAsync(
        string key,
        string text,
        string target,
        XivChatType channel,
        bool forced,
        string? sourceIso)
    {
        var state = TranslationState.None;
        var translated = string.Empty;
        string? detected = null;
        var provider = string.Empty;

        try
        {
            await _slots.WaitAsync(_cancellation.Token).ConfigureAwait(false);

            try
            {
                var context = Config.LlmSendContext && Config.Provider == TranslationProviderKind.Llm
                    ? SnapshotContext()
                    : null;

                var source = sourceIso != null
                    ? (Iso: (string?)sourceIso, Skip: false)
                    : await ResolveSourceAsync(text, channel).ConfigureAwait(false);

                if (forced) source = (source.Iso, false);

                if (source.Skip)
                {
                    Stats.Skipped++;
                }
                else if (!await ReserveRequestAsync(forced, _cancellation.Token).ConfigureAwait(false))
                {
                    if (forced) state = TranslationState.Failed;
                }
                else
                {
                    var request = new TranslationRequest
                    {
                        Text = text,
                        TargetIso = target,
                        SourceIso = source.Iso,
                        Context = context,
                    };

                    var result = await RequestAsync(request, forced, _cancellation.Token).ConfigureAwait(false);

                    if (!result.Success)
                    {
                        state = TranslationState.Failed;
                        Stats.Failed++;
                    }
                    else
                    {
                        detected = result.DetectedIso ?? source.Iso;
                        _lingua.RecordChannel(channel, detected);

                        if (forced || !ShouldDiscard(detected, result.Text, text))
                        {
                            state = TranslationState.Translated;
                            translated = result.Text;
                            provider = result.Provider;

                            if (Config.CacheEnabled) _cache.Store(key, result.Text, detected);
                            Stats.Translated++;
                        }
                        else
                        {
                            Stats.Skipped++;
                        }
                    }
                }
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException)
        {
            state = TranslationState.None;
        }
        catch (Exception ex)
        {
            state = TranslationState.Failed;
            Stats.Failed++;
            _log.Log(LogSource, CordiLogLevel.Warning, "Failed to translate a message", ex);
        }
        finally
        {
            Complete(key, state, translated, detected, provider);
        }
    }

    private void Complete(string key, TranslationState state, string translated, string? detected, string provider)
    {
        List<ChatboxMessage> waiters;

        lock (_inFlightGate)
        {
            if (!_inFlight.Remove(key, out var pending)) return;
            waiters = pending;
        }

        foreach (var message in waiters)
        {
            if (state == TranslationState.Translated)
            {
                Apply(message, translated, detected, provider);
                continue;
            }

            message.TranslationState = state;
            if (state == TranslationState.Failed) Translated?.Invoke(message);
        }
    }

    private async Task<(string? Iso, bool Skip)> ResolveSourceAsync(string text, XivChatType channel)
    {
        var script = TranslationDetector.DetectScript(text);

        if (script != null)
        {
            _lingua.RecordChannel(channel, script);
            return (script, ShouldSkipSource(script));
        }

        var hasEnglishToken = TranslationPhrases.HasEnglishToken(text);

        if (Config.DetectionSource == TranslationDetectionSource.Online)
        {
            var online = await DetectOnlineAsync(text).ConfigureAwait(false);

            if (online == null)
            {
                var (_, guess) = await _lingua.ComputeReliabilityAsync(text, channel, hasEnglishToken).ConfigureAwait(false);
                online = guess;
            }

            _lingua.RecordChannel(channel, online);
            return (online, ShouldSkipSource(online));
        }

        var (reliability, iso) = await _lingua.ComputeReliabilityAsync(text, channel, hasEnglishToken).ConfigureAwait(false);
        var threshold = Math.Clamp(Config.DetectionConfidence, 0, 100) / 100d;

        if (reliability >= threshold)
        {
            _lingua.RecordChannel(channel, iso);
            return (iso, ShouldSkipSource(iso));
        }

        var detected = await DetectOnlineAsync(text).ConfigureAwait(false) ?? iso;

        _lingua.RecordChannel(channel, detected);
        return (detected, ShouldSkipSource(detected));
    }

    private async Task<string?> DetectOnlineAsync(string text)
    {
        if (!await ReserveRequestAsync(false, _cancellation.Token).ConfigureAwait(false)) return null;

        try
        {
            return await _machine.DetectAsync(text, _cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            EnterCooldown(_machine.Name);
            return null;
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Debug, "Online language detection failed", ex);
            return null;
        }
    }

    private TimeSpan RequestSpacing => Config.Provider switch
    {
        TranslationProviderKind.DeepL when _deepl.IsConfigured => TimeSpan.Zero,
        TranslationProviderKind.Llm when _llm.IsConfigured => TimeSpan.Zero,
        _ => TimeSpan.FromMilliseconds(MachineSpacingMs),
    };

    private async Task<bool> ReserveRequestAsync(bool priority, CancellationToken token)
    {
        TimeSpan wait;

        lock (_throttleGate)
        {
            var now = DateTime.UtcNow;
            var earliest = _nextRequestAt;

            if (!priority && _cooldownUntil > earliest) earliest = _cooldownUntil;
            if (earliest < now) earliest = now;

            wait = earliest - now;
            if (!priority && wait.TotalMilliseconds > MaxQueueWaitMs) return false;

            _nextRequestAt = earliest + RequestSpacing;
        }

        if (wait > TimeSpan.Zero) await Task.Delay(wait, token).ConfigureAwait(false);

        return true;
    }

    private void EnterCooldown(string provider)
    {
        TimeSpan wait;

        lock (_throttleGate)
        {
            var now = DateTime.UtcNow;
            if (_cooldownUntil > now) return;

            if (_lastLimitAt != DateTime.MinValue && (now - _lastLimitAt).TotalSeconds > CooldownDecaySeconds)
                _cooldownStep = 0;

            _lastLimitAt = now;
            _cooldownStep = Math.Min(_cooldownStep + 1, MaxCooldownStep);

            var seconds = Math.Min(CooldownBaseSeconds * Math.Pow(2, _cooldownStep - 1), CooldownCapSeconds);
            wait = TimeSpan.FromSeconds(seconds);

            _cooldownUntil = now + wait;
            _nextRequestAt = _cooldownUntil;
        }

        _log.Warning(
            LogSource,
            $"{provider} rate limited (429) - pausing automatic translation for {wait.TotalSeconds:0}s");
    }

    private void ResetCooldown()
    {
        lock (_throttleGate)
        {
            _cooldownStep = 0;
            _cooldownUntil = DateTime.MinValue;
        }
    }

    private bool ShouldSkipSource(string? iso)
    {
        if (iso == null) return Config.SourceMode == TranslationSourceMode.Selected;
        if (!TranslationLanguages.IsSupported(iso)) return true;
        if (Config.IsTarget(iso)) return true;

        return Config.SourceMode == TranslationSourceMode.Selected
            ? !Config.IsSelectedSource(iso)
            : Config.IsKnown(iso);
    }

    private bool ShouldDiscard(string? detected, string translated, string original)
    {
        if (ShouldSkipSource(detected)) return true;

        return TranslationFilter.SameMeaning(original, translated);
    }

    private async Task<TranslationResult> RequestAsync(TranslationRequest request, bool priority, CancellationToken token)
    {
        var provider = ActiveProvider;

        if (provider.IsConfigured)
        {
            try
            {
                var result = await provider.TranslateAsync(request, token).ConfigureAwait(false);

                if (result.Success)
                {
                    ResetCooldown();
                    return result;
                }

                _log.Debug(LogSource, $"{provider.Name} returned no translation");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                EnterCooldown(provider.Name);
                return TranslationResult.Failure();
            }
            catch (Exception ex)
            {
                _log.Log(LogSource, CordiLogLevel.Warning, $"{provider.Name} translation failed", ex);
            }
        }

        if (ReferenceEquals(provider, _machine)) return TranslationResult.Failure();

        if (!await ReserveRequestAsync(priority, token).ConfigureAwait(false)) return TranslationResult.Failure();

        try
        {
            var fallback = await _machine.TranslateAsync(request, token).ConfigureAwait(false);
            if (fallback.Success) ResetCooldown();
            return fallback;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            EnterCooldown(_machine.Name);
            return TranslationResult.Failure();
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Warning, "Fallback translation failed", ex);
            return TranslationResult.Failure();
        }
    }

    private void Apply(ChatboxMessage message, string translated, string? detected, string provider)
    {
        message.TranslatedText = translated;
        message.TranslationSource = detected;
        message.TranslationProvider = provider;
        message.TranslationState = TranslationState.Translated;

        Translated?.Invoke(message);
    }

    private void RecordContext(string author, string text)
    {
        var line = $"{author}: {text}";

        lock (_contextGate)
        {
            foreach (var existing in _context)
            {
                if (string.Equals(existing, line, StringComparison.Ordinal)) return;
            }

            _context.Enqueue(line);
            while (_context.Count > ContextLines) _context.Dequeue();
        }
    }

    private string SnapshotContext()
    {
        lock (_contextGate)
        {
            return string.Join('\n', _context);
        }
    }

    private static string ResolveModelsDirectory() =>
        Path.Combine(Service.PluginInterface.AssemblyLocation.DirectoryName!, "Lingua", "LanguageModels");

    private static HttpClient CreateClient(int timeoutSeconds)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 60)),
        };

        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Cordi");

        return client;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cancellation.Cancel();
        _cache.Save();
        _lingua.Dispose();
        _machine.Dispose();
        _cancellation.Dispose();
        _slots.Dispose();
        _http.Dispose();
    }
}

public sealed class TranslationStats
{
    public int Translated;
    public int Failed;
    public int CacheHits;
    public int PhraseHits;
    public int Skipped;

    public void Reset()
    {
        Translated = 0;
        Failed = 0;
        CacheHits = 0;
        PhraseHits = 0;
        Skipped = 0;
    }
}
