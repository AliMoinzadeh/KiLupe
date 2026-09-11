using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public sealed class CorrectionPresentationState
{
    private long latestRequestId = long.MinValue;
    private IReadOnlyList<CorrectionSuggestion> suggestions = Array.Empty<CorrectionSuggestion>();

    public bool IsVisible => suggestions.Count > 0;

    public IReadOnlyList<CorrectionSuggestion> Suggestions => suggestions;

    public string OriginalText => string.Join(
        Environment.NewLine,
        suggestions.Select(suggestion => suggestion.OriginalText));

    public string SuggestionText => string.Join(
        Environment.NewLine,
        suggestions.Select(suggestion => suggestion.CorrectedText));

    public string StatusText => string.Join(
        " | ",
        suggestions
            .Select(suggestion => $"{suggestion.ModelName} ({suggestion.ProviderName})")
            .Distinct(StringComparer.Ordinal));

    public static CorrectionPresentationState From(
        IEnumerable<CorrectionSuggestion> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        var state = new CorrectionPresentationState();
        state.Apply(0, suggestions);
        return state;
    }

    public void Apply(long requestId, CorrectionSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        Apply(requestId, new[] { suggestion });
    }

    public void Apply(
        long requestId,
        IEnumerable<CorrectionSuggestion> newSuggestions)
    {
        ArgumentNullException.ThrowIfNull(newSuggestions);
        if (requestId < latestRequestId)
        {
            return;
        }

        latestRequestId = requestId;
        suggestions = newSuggestions
            .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion.CorrectedText))
            .ToArray();
    }

    public void Clear(long requestId)
    {
        if (requestId < latestRequestId)
        {
            return;
        }

        latestRequestId = requestId;
        suggestions = Array.Empty<CorrectionSuggestion>();
    }
}