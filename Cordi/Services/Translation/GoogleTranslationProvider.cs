using System;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cordi.Services.Translation;

public sealed class GoogleTranslationProvider : ITranslationProvider
{
    private const string Endpoint = "https://translate.googleapis.com/translate_a/single";

    private readonly HttpClient _http;

    public GoogleTranslationProvider(HttpClient http)
    {
        _http = http;
    }

    public string Name => "Google";

    public bool IsConfigured => true;

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken token)
    {
        var target = TranslationLanguages.GoogleCode(request.TargetIso);
        var source = request.SourceIso == null ? "auto" : TranslationLanguages.GoogleCode(request.SourceIso);
        var url = $"{Endpoint}?client=gtx&sl={Uri.EscapeDataString(source)}&tl={Uri.EscapeDataString(target)}" +
                  $"&dt=t&q={Uri.EscapeDataString(request.Text)}";

        using var response = await _http.GetAsync(url, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

        return Parse(payload);
    }

    public async Task<string?> DetectAsync(string text, CancellationToken token)
    {
        var url = $"{Endpoint}?client=gtx&sl=auto&tl=en&dt=t&q={Uri.EscapeDataString(text)}";

        using var response = await _http.GetAsync(url, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

        return Parse(payload).DetectedIso;
    }

    private TranslationResult Parse(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            return TranslationResult.Failure();

        var builder = new StringBuilder();
        var chunks = root[0];

        if (chunks.ValueKind == JsonValueKind.Array)
        {
            foreach (var chunk in chunks.EnumerateArray())
            {
                if (chunk.ValueKind != JsonValueKind.Array || chunk.GetArrayLength() == 0) continue;

                var piece = chunk[0];
                if (piece.ValueKind == JsonValueKind.String) builder.Append(piece.GetString());
            }
        }

        var detected = root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String
            ? TranslationLanguages.Normalize(root[2].GetString())
            : null;

        var text = builder.ToString().Trim();

        return text.Length == 0
            ? TranslationResult.Failure()
            : new TranslationResult(true, text, string.IsNullOrEmpty(detected) ? null : detected, Name);
    }
}
