using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public sealed record CorrectionLine(
    string Text,
    IReadOnlyList<AnalysisResult> Words);

public static class CorrectionLineGrouper
{
    public static IReadOnlyList<CorrectionLine> Group(
        IEnumerable<AnalysisResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var words = results
            .Where(result => result.Kind == AnalysisKind.Text)
            .Where(result => !string.IsNullOrWhiteSpace(result.Label))
            .Where(result => result.Bounds.Width > 0 && result.Bounds.Height > 0)
            .Select(result => new Word(result, GetCenterY(result)))
            .OrderBy(word => word.CenterY)
            .ThenBy(word => word.Result.Bounds.Left)
            .ToArray();

        if (words.Length == 0)
        {
            return Array.Empty<CorrectionLine>();
        }

        var medianHeight = GetMedian(words.Select(word => word.Result.Bounds.Height));
        var verticalTolerance = Math.Max(1, medianHeight * 0.75);
        var groups = new List<LineGroup>();

        foreach (var word in words)
        {
            var group = groups
                .OrderBy(candidate => Math.Abs(candidate.CenterY - word.CenterY))
                .FirstOrDefault(candidate =>
                    Math.Abs(candidate.CenterY - word.CenterY) <= verticalTolerance);

            if (group is null)
            {
                groups.Add(new LineGroup(word));
            }
            else
            {
                group.Add(word);
            }
        }

        return groups
            .OrderBy(group => group.CenterY)
            .Select(group =>
            {
                var sortedWords = group.Words
                    .OrderBy(word => word.Result.Bounds.Left)
                    .ThenBy(word => word.Result.Bounds.Top)
                    .Select(word => word.Result)
                    .ToArray();
                return new CorrectionLine(
                    string.Join(" ", sortedWords.Select(word => word.Label.Trim())),
                    sortedWords);
            })
            .ToArray();
    }

    private static double GetCenterY(AnalysisResult result)
    {
        return result.Bounds.Top + result.Bounds.Height / 2;
    }

    private static double GetMedian(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }

    private sealed class LineGroup
    {
        public LineGroup(Word word)
        {
            Words = new List<Word> { word };
            CenterY = word.CenterY;
        }

        public List<Word> Words { get; }

        public double CenterY { get; private set; }

        public void Add(Word word)
        {
            Words.Add(word);
            CenterY = Words.Average(item => item.CenterY);
        }
    }

    private sealed record Word(AnalysisResult Result, double CenterY);
}