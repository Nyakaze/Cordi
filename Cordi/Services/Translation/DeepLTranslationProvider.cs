using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cordi.Services.Translation;

public sealed class DeepLTranslationProvider : ITranslationProvider
{
    private const string FreeEndpoint = "https://api-free.deepl.com/v2/translate";
    private const string ProEndpoint = "https://api.deepl.com/v2/translate";
    private const string GameContext = "FFXIV, MMORPG";

    private readonly HttpClient _http;
    private readonly Func<string> _apiKey;
    private readonly Func<bool> _pro;

    public DeepLTranslationProvider(HttpClient http, Func<string> apiKey, Func<bool> pro)
    {
        _http = http;
        _apiKey = apiKey;
        _pro = pro;
    }

    public string Name => "DeepL";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey());

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken token)
    {
        var target = TranslationLanguages.DeepLCode(request.TargetIso);
        if (target.Length == 0)
            throw new NotSupportedException($"DeepL does not support {TranslationLanguages.NameOf(request.TargetIso)}.");

        var body = JsonSerializer.Serialize(new
        {
            text = new[] { request.Text },
            target_lang = target,
            context = GameContext,
        });

        using var message = new HttpRequestMessage(HttpMethod.Post, _pro() ? ProEndpoint : FreeEndpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        message.Headers.TryAddWithoutValidation("Authorization", $"DeepL-Auth-Key {_apiKey()}");

        using var response = await _http.SendAsync(message, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));

        if (!document.RootElement.TryGetProperty("translations", out var translations)
            || translations.ValueKind != JsonValueKind.Array
            || translations.GetArrayLength() == 0)
            return TranslationResult.Failure();

        var first = translations[0];
        var text = first.TryGetProperty("text", out var value) ? value.GetString()?.Trim() : null;

        if (string.IsNullOrEmpty(text)) return TranslationResult.Failure();

        var detected = first.TryGetProperty("detected_source_language", out var source)
            ? TranslationLanguages.Normalize(source.GetString())
            : null;

        return new TranslationResult(true, text, string.IsNullOrEmpty(detected) ? null : detected, Name);
    }
}
