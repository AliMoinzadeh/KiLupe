using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public sealed class CorrectionCoordinator
{
    private readonly ITextCorrectionService correctionService;

    public CorrectionCoordinator(ITextCorrectionService correctionService)
    {
        this.correctionService = correctionService
            ?? throw new ArgumentNullException(nameof(correctionService));
    }

    public async Task<IReadOnlyList<CorrectionSuggestion>> CreateSuggestionsAsync(
        IEnumerable<AnalysisResult> results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(results);

        var allResults = results.ToArray();
        return await CorrectLinesAsync(
                CorrectionLineGrouper.Group(allResults).Select(line => line.Text),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<CorrectionSuggestion>> CreateSuggestionsAsync(
        IEnumerable<string> lines,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return CorrectLinesAsync(lines, cancellationToken);
    }

    private async Task<IReadOnlyList<CorrectionSuggestion>> CorrectLinesAsync(
        IEnumerable<string> lines,
        CancellationToken cancellationToken)
    {
        if (!correctionService.IsAvailable)
        {
            return Array.Empty<CorrectionSuggestion>();
        }

        var suggestions = new List<CorrectionSuggestion>();

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var suggestion = await correctionService
                .CorrectAsync(line, cancellationToken)
                .ConfigureAwait(false);
            if (suggestion is null
                || string.IsNullOrWhiteSpace(suggestion.CorrectedText)
                || string.Equals(
                    line,
                    suggestion.CorrectedText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            suggestions.Add(suggestion with { OriginalText = line });
        }

        return suggestions;
    }
}