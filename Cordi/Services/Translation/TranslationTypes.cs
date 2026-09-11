using System.Threading;
using System.Threading.Tasks;

namespace Cordi.Services.Translation;

public enum TranslationState
{
    None,
    Pending,
    Translated,
    Failed,
}

public sealed class TranslationRequest
{
    public required string Text { get; init; }
    public required string TargetIso { get; init; }
    public string? SourceIso { get; init; }
    public string? Context { get; init; }
}

public readonly record struct TranslationResult(
    bool Success,
    string Text,
    string? DetectedIso,
    string Provider)
{
    public static TranslationResult Failure() => new(false, string.Empty, null, string.Empty);
}

public interface ITranslationProvider
{
    string Name { get; }

    bool IsConfigured { get; }

    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken token);
}
