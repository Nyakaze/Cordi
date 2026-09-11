using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cordi.Services.Translation;

public sealed class LlmTranslationProvider : ITranslationProvider
{
    public const string DefaultPrompt =
        """
        You are a precise translator for Final Fantasy XIV chat.

        RULES:
        1. Keep FFXIV terms, job abbreviations, item names and player names intact.
        2. Preserve tone, punctuation and formatting.
        3. If the text is already written in the target language, repeat it unchanged.
        4. Never explain, never add notes, never use quotation marks.

        Reply with the translated text and nothing else.
        """;

    private readonly HttpClient _http;
    private readonly Func<string> _endpoint;
    private readonly Func<string> _apiKey;
    private readonly Func<string> _model;
    private readonly Func<string> _prompt;

    public LlmTranslationProvider(
        HttpClient http,
        Func<string> endpoint,
        Func<string> apiKey,
        Func<string> model,
        Func<string> prompt)
    {
        _http = http;
        _endpoint = endpoint;
        _apiKey = apiKey;
        _model = model;
        _prompt = prompt;
    }

    public string Name => "LLM";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_endpoint())
        && !string.IsNullOrWhiteSpace(_apiKey())
        && !string.IsNullOrWhiteSpace(_model());

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken token)
    {
        var system = BuildPrompt(request.Context);
        var user = $"Translate to: {TranslationLanguages.NameOf(request.TargetIso)}\n{request.Text}";

        var body = JsonSerializer.Serialize(new
        {
            model = _model(),
            temperature = 0.3,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
        });

        using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint())
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey()}");

        using var response = await _http.SendAsync(message, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));

        if (!document.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
            return TranslationResult.Failure();

        if (!choices[0].TryGetProperty("message", out var reply)
            || !reply.TryGetProperty("content", out var content))
            return TranslationResult.Failure();

        var text = Tidy(content.GetString());

        return text.Length == 0
            ? TranslationResult.Failure()
            : new TranslationResult(true, text, request.SourceIso, Name);
    }

    private string BuildPrompt(string? context)
    {
        var configured = _prompt();
        var prompt = string.IsNullOrWhiteSpace(configured) ? DefaultPrompt : configured;

        if (string.IsNullOrWhiteSpace(context)) return prompt;

        return prompt + $"\n\nRecent chat for context, do not translate it:\n<context>\n{context}\n</context>";
    }

    private static string Tidy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var trimmed = text.Trim();
        var lines = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length > 1) trimmed = lines[0].Trim();

        if (trimmed.Length > 1 && trimmed[0] == '"' && trimmed[^1] == '"')
            trimmed = trimmed[1..^1].Trim();

        return trimmed;
    }
}
