using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Dalamud.Game.ClientState.Conditions;

namespace Cordi.Services.Translation;

public sealed class TranslationService : IDisposable
{
    private const string LogSource = "Translation";
    private const string CacheProvider = "Cache";
    private const int MaxInFlight = 32;
    private const int ContextLines = 8;
    private const int KeylessSpacingMs = 1200;
    private const int MaxQueueWaitMs = 15000;
    private const double CooldownBaseSeconds = 30d;
    private const double CooldownCapSeconds = 480d;
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
    private int _cooldownStep;

    private readonly GoogleTranslationProvider _google;
    private readonly DeepLTranslationProvider _deepl;
    private readonly LlmTranslationProvider _llm;

    private bool _disposed;

    public TranslationService(string configDirectory, Func<TranslationConfig> config, CordiLogService log)
    {
        _config = config;
        _log = log;

        _http = CreateClient(config().RequestTimeoutSeconds);
        _cache = new TranslationCache(configDirectory, () => config().CacheSize, log);
        _cache.Load();

        _slots = new SemaphoreSlim(Math.Clamp(config().MaxParallelRequests, 1, 8));

        _google = new GoogleTranslationProvider(_http);
        _deepl = new DeepLTranslationProvider(_http, () => config().DeepLApiKey, () => config().DeepLPro);
        _llm = new LlmTranslationProvider(
            _http,
            () => config().LlmEndpoint,
            () => config().LlmApiKey,
            () => config().LlmModel,
            () => config().LlmPrompt);
    }

    public event Action<ChatboxMessage>? Translated;

    public TranslationStats Stats { get; } = new();

    public int CachedEntries => _cache.Count;

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
        TranslationProviderKind.DeepL => _deepl,
        TranslationProviderKind.Llm => _llm,
        _ => _google,
    };

    public void ClearCache() => _cache.Clear();

    public void Consider(ChatboxMessage message)
    {
        if (_disposed || !Config.TranslatesAutomatically) return;
        if (message.TranslationState != TranslationState.None) return;
        if (!PassesGate(message)) return;

        Begin(message, false);
    }

    public void Request(ChatboxMessage message)
    {
        if (_disposed || !Config.Enabled) return;
        if (message.TranslationState is TranslationState.Pending or TranslationState.Translated) return;

        message.TranslationState = TranslationState.None;

        Begin(message, true);
    }

    private void Begin(ChatboxMessage message, bool forced)
    {
        var text = TranslationFilter.CleanText(message);
        if (!TranslationFilter.IsTranslatable(text, forced ? 1 : Config.MinimumLength)) return;

        var target = TranslationLanguages.Normalize(Config.TargetLanguage);
        if (target.Length == 0) return;

        RecordContext(message.AuthorName, text);

        var key = TranslationCache.KeyFor(target, text);

        if (Config.CacheEnabled && _cache.TryGet(key, out var cached))
        {
            Stats.CacheHits++;
            Apply(message, cached, null, CacheProvider);
            return;
        }

        if (IsRateLimited) return;

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

        _ = Task.Run(() => RunAsync(key, text, target, forced), _cancellation.Token);
    }

    public async Task<TranslationResult> TranslateTextAsync(string text, string targetIso)
    {
        var target = TranslationLanguages.Normalize(targetIso);
        if (target.Length == 0 || string.IsNullOrWhiteSpace(text)) return TranslationResult.Failure();

        var request = new TranslationRequest { Text = text.Trim(), TargetIso = target };

        return await RequestAsync(request, _cancellation.Token).ConfigureAwait(false);
    }

    private bool PassesGate(ChatboxMessage message)
    {
        if (message.IsSystemLine) return false;
        if (message.IsSelf && !Config.TranslateOwnMessages) return false;
        if (message.FilteredAsAd && Config.SkipFilteredMessages) return false;

        if (message.Origin == ChatboxOrigin.Game)
        {
            if (!Config.ChatTypes.Contains(message.GameChatType)) return false;
            if (!Config.TranslateInDuty && Service.Condition[ConditionFlag.BoundByDuty]) return false;
        }

        return true;
    }

    private async Task RunAsync(string key, string text, string target, bool forced)
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

                var source = await ResolveSourceAsync(text).ConfigureAwait(false);
                if (forced) source = (source.Iso, false);

                if (!source.Skip && await ReserveRequestAsync(_cancellation.Token).ConfigureAwait(false))
                {
                    var request = new TranslationRequest
                    {
                        Text = text,
                        TargetIso = target,
                        SourceIso = source.Iso,
                        Context = context,
                    };

                    var result = await RequestAsync(request, _cancellation.Token).ConfigureAwait(false);

                    if (!result.Success)
                    {
                        state = TranslationState.Failed;
                        Stats.Failed++;
                    }
                    else
                    {
                        detected = result.DetectedIso ?? source.Iso;

                        if (forced || !ShouldDiscard(detected, result.Text, text))
                        {
                            state = TranslationState.Translated;
                            translated = result.Text;
                            provider = result.Provider;

                            if (Config.CacheEnabled) _cache.Store(key, result.Text);
                            Stats.Translated++;
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

    private async Task<(string? Iso, bool Skip)> ResolveSourceAsync(string text)
    {
        var local = TranslationDetector.DetectLocal(text);

        if (local != null && ShouldSkipSource(local)) return (local, true);

        var deferred = Config.Provider == TranslationProviderKind.Google
                       && Config.DetectionSource == TranslationDetectionSource.Online;

        if (deferred) return (null, false);

        var iso = local;

        if (iso == null && Config.DetectionSource == TranslationDetectionSource.Online)
            iso = await DetectOnlineAsync(text).ConfigureAwait(false);

        return (iso, ShouldSkipSource(iso));
    }

    private async Task<string?> DetectOnlineAsync(string text)
    {
        if (!await ReserveRequestAsync(_cancellation.Token).ConfigureAwait(false)) return null;

        try
        {
            return await _google.DetectAsync(text, _cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            EnterCooldown(_google.Name);
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
        _ => TimeSpan.FromMilliseconds(KeylessSpacingMs),
    };

    private async Task<bool> ReserveRequestAsync(CancellationToken token)
    {
        TimeSpan wait;

        lock (_throttleGate)
        {
            var now = DateTime.UtcNow;
            var earliest = _nextRequestAt > _cooldownUntil ? _nextRequestAt : _cooldownUntil;
            if (earliest < now) earliest = now;

            wait = earliest - now;
            if (wait.TotalMilliseconds > MaxQueueWaitMs) return false;

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

            _cooldownStep = Math.Min(_cooldownStep + 1, MaxCooldownStep);

            var seconds = Math.Min(CooldownBaseSeconds * Math.Pow(2, _cooldownStep - 1), CooldownCapSeconds);
            wait = TimeSpan.FromSeconds(seconds);

            _cooldownUntil = now + wait;
            _nextRequestAt = _cooldownUntil;
        }

        _log.Warning(
            LogSource,
            $"{provider} rate limited (429) - pausing translation requests for {wait.TotalSeconds:0}s");
    }

    private void ResetCooldown()
    {
        lock (_throttleGate)
        {
            _cooldownStep = 0;
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

    private async Task<TranslationResult> RequestAsync(TranslationRequest request, CancellationToken token)
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

        if (provider == _google) return TranslationResult.Failure();

        if (!await ReserveRequestAsync(token).ConfigureAwait(false)) return TranslationResult.Failure();

        try
        {
            var fallback = await _google.TranslateAsync(request, token).ConfigureAwait(false);
            if (fallback.Success) ResetCooldown();
            return fallback;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            EnterCooldown(_google.Name);
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

    public void Reset()
    {
        Translated = 0;
        Failed = 0;
        CacheHits = 0;
    }
}
