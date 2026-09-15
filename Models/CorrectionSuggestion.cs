namespace KiLupeDemo.Models;

public sealed record CorrectionSuggestion(
    string OriginalText,
    string CorrectedText,
    string ModelName,
    string ProviderName);