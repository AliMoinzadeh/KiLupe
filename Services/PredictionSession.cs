using System.Windows;
namespace KiLupeDemo.Services;

public sealed record CaretSnapshot(IntPtr Window, string ElementId, string ProcessName,
    string Before, string After, Rect Bounds)
{
    public Rect EditorBounds { get; init; } = Rect.Empty;
    public bool IsLineEnd => string.IsNullOrWhiteSpace(After.Split('\n', '\r')[0]);
}

// Owned by the UI dispatcher. Each context change invalidates in-flight inference.
public sealed class PredictionSession
{
    public CaretSnapshot? Context { get; private set; }
    public string? Suggestion { get; private set; }
    public long Generation { get; private set; }
    private long changedAt;
    private bool requested;

    public long? Observe(CaretSnapshot? snapshot, long now, int pauseMilliseconds)
    {
        if (Context != snapshot)
        {
            Clear();
            Context = snapshot;
            changedAt = now;
        }
        if (snapshot is null || requested || now - changedAt < pauseMilliseconds) return null;
        requested = true;
        return Generation;
    }

    public bool Complete(long generation, string text)
    {
        if (generation != Generation || Context is null) return false;
        var insertion = PredictionText.GetInsertion(text, Context.Before);
        Suggestion = string.IsNullOrWhiteSpace(insertion) ? null : insertion;
        return Suggestion is not null;
    }

    public string? Take(CaretSnapshot? current, bool allowed)
    {
        if (!allowed || current is null || current != Context) return null;
        var result = Suggestion;
        Suggestion = null;
        Generation++;
        return result;
    }

    public void Clear()
    {
        Context = null;
        Suggestion = null;
        requested = false;
        Generation++;
    }
}

public static class PredictionText
{
    public static string GetInsertion(string response, string before)
    {
        // The model sees at most this suffix of the document. Match whole sentence/
        // paragraph prefixes, not arbitrary character overlaps ("das" / "schoen").
        var context = before.Length > 1200 ? before[^1200..] : before;
        var candidateResponse = response.TrimStart();
        for (var start = 0; start < context.Length; start++)
        {
            if (start > 0 && context[start - 1] is not ('.' or '!' or '?' or '\r' or '\n')) continue;
            var prefix = context[start..].Trim();
            if (prefix.Length == 0 || !prefix.Any(char.IsLetterOrDigit)) continue;
            var matchedLength = MatchPrefix(prefix, candidateResponse);
            if (matchedLength < 0) continue;
            var insertion = candidateResponse[matchedLength..];
            if (char.IsWhiteSpace(before[^1]))
            {
                // A completed word must not match the beginning of a different word.
                if (insertion.Length > 0 && char.IsLetterOrDigit(prefix[^1]) && char.IsLetterOrDigit(insertion[0])) continue;
                insertion = insertion.TrimStart();
            }
            return Normalize(insertion);
        }
        return Normalize(response);
    }

    private static int MatchPrefix(string prefix, string response)
    {
        var source = 0;
        var target = 0;
        while (source < prefix.Length)
        {
            if (target >= response.Length) return -1;
            if (char.IsWhiteSpace(prefix[source]))
            {
                if (!char.IsWhiteSpace(response[target])) return -1;
                while (source < prefix.Length && char.IsWhiteSpace(prefix[source])) source++;
                while (target < response.Length && char.IsWhiteSpace(response[target])) target++;
            }
            else
            {
                if (char.ToUpperInvariant(prefix[source]) != char.ToUpperInvariant(response[target])) return -1;
                source++;
                target++;
            }
        }
        return target;
    }
    public static string Normalize(string response)
    {
        foreach (var marker in new[] { "<|im_end|>", "<|endoftext|>" })
        {
            var index = response.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0) response = response[..index];
        }
        if (response.Contains("<|", StringComparison.Ordinal) || response.Contains("```", StringComparison.Ordinal)) return "";
        response = response.TrimStart('\r', '\n').Split('\r', '\n')[0].TrimEnd();
        if (response.Any(char.IsControl) || response.Length > 240) return "";
        return response;
    }
}
