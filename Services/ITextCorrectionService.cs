using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public interface ITextCorrectionService : IDisposable
{
    string Name { get; }

    string ModelId { get; }

    bool IsAvailable { get; }

    string StatusText { get; }

    Task<CorrectionSuggestion?> CorrectAsync(
        string text,
        CancellationToken cancellationToken);
}