using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GTranslate.Translators;

namespace Cordi.Services.Translation;

public sealed class MachineTranslationProvider : ITranslationProvider, IDisposable
{
    private const string LogSource = "Translation";

    private readonly CordiLogService _log;
    private readonly MicrosoftTranslator _microsoft;
    private readonly GoogleTranslator2 _google;
    private readonly YandexTranslator _yandex;

    public MachineTranslationProvider(HttpClient http, CordiLogService log)
    {
        _log = log;
        _microsoft = new MicrosoftTranslator(http);
        _google = new GoogleTranslator2(http);
        _yandex = new YandexTranslator(http);
    }

    public string Name => "Bing + Google";

    public bool IsConfigured => true;

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken token)
    {
        var target = TranslationLanguages.Normalize(request.TargetIso);
        var source = request.SourceIso == null ? null : TranslationLanguages.Normalize(request.SourceIso);

        var microsoft = await TryMicrosoftAsync(request.Text, target, source, token).ConfigureAwait(false);
        if (microsoft.Success) return microsoft;

        return await TryGoogleAsync(request.Text, target, source, token).ConfigureAwait(false);
    }

    private async Task<TranslationResult> TryMicrosoftAsync(string text, string target, string? source, CancellationToken token)
    {
        try
        {
            var result = await _microsoft.TranslateAsync(text, target, source).WaitAsync(token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.Translation) || string.Equals(result.Translation, text, StringComparison.Ordinal))
                return TranslationResult.Failure();

            return new TranslationResult(
                true,
                result.Translation,
                TranslationLanguages.Normalize(result.SourceLanguage?.ISO6391) is { Length: > 0 } iso ? iso : source,
                "Bing");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Debug, "Bing translation failed", ex);
            return TranslationResult.Failure();
        }
    }

    private async Task<TranslationResult> TryGoogleAsync(string text, string target, string? source, CancellationToken token)
    {
        try
        {
            var result = await _google.TranslateAsync(text, target, source).WaitAsync(token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.Translation) || string.Equals(result.Translation, text, StringComparison.Ordinal))
                return TranslationResult.Failure();

            return new TranslationResult(
                true,
                result.Translation,
                TranslationLanguages.Normalize(result.SourceLanguage?.ISO6391) is { Length: > 0 } iso ? iso : source,
                "Google");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Log(LogSource, CordiLogLevel.Debug, "Google translation failed", ex);
            return TranslationResult.Failure();
        }
    }

    public async Task<string?> DetectAsync(string text, CancellationToken token)
    {
        var detectors = new Func<Task<GTranslate.Language>>[]
        {
            () => _yandex.DetectLanguageAsync(text),
            () => _google.DetectLanguageAsync(text),
            () => _microsoft.DetectLanguageAsync(text),
        };

        foreach (var detect in detectors)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var language = await detect().WaitAsync(token).ConfigureAwait(false);
                var iso = TranslationLanguages.Normalize(language?.ISO6391);

                if (iso.Length > 0) return iso;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Log(LogSource, CordiLogLevel.Debug, "Online language detection failed", ex);
            }
        }

        return null;
    }

    public void Dispose()
    {
        _microsoft.Dispose();
        _google.Dispose();
        _yandex.Dispose();
    }
}
