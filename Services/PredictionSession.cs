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
        var insertion = PredictionText.GetInsertion(text, Context.Before, Context.After);
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
    public static string GetInsertion(string response, string before, string after = "")
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
            return NormalizeInsertion(insertion, after);
        }
        return NormalizeInsertion(response, after);
    }

    private static string NormalizeInsertion(string response, string after)
    {
        var normalized = Normalize(response, preserveTrailingSpace: after.Length > 0);
        if (after.Length == 0) return normalized;
        var following = after.TrimStart();
        var candidate = normalized.TrimEnd();
        var existingLine = following.Split('\r', '\n')[0];
        var repeatedAt = existingLine.Length >= 3 ? candidate.IndexOf(existingLine, StringComparison.OrdinalIgnoreCase) : -1;
        var repeatedEnd = repeatedAt + existingLine.Length;
        if (repeatedAt >= 0
            && (repeatedAt == 0 || !char.IsLetterOrDigit(candidate[repeatedAt - 1]))
            && (repeatedEnd == candidate.Length || !char.IsLetterOrDigit(existingLine[^1]) || !char.IsLetterOrDigit(candidate[repeatedEnd])))
        {
            var missing = candidate[..repeatedAt];
            return char.IsWhiteSpace(after[0]) ? missing.TrimEnd() : missing;
        }
        for (var length = Math.Min(candidate.Length, following.Length); length > 0; length--)
        {
            var start = candidate.Length - length;
            if (!candidate.AsSpan(start).Equals(following.AsSpan(0, length), StringComparison.OrdinalIgnoreCase)) continue;
            // Only remove complete words/punctuation, never shared word endings.
            if (start > 0 && char.IsLetterOrDigit(candidate[start - 1]) && char.IsLetterOrDigit(following[0])) continue;
            if (length < following.Length && char.IsLetterOrDigit(following[length - 1]) && char.IsLetterOrDigit(following[length])) continue;
            if (length < 3 && following[..length].Any(char.IsLetterOrDigit)) continue;
            normalized = candidate[..start];
            break;
        }
        if (char.IsWhiteSpace(after[0])) normalized = normalized.TrimEnd();
        return normalized;
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
    public static string Normalize(string response, bool preserveTrailingSpace = false)
    {
        foreach (var marker in new[] { "<|im_end|>", "<|endoftext|>" })
        {
            var index = response.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0) response = response[..index];
        }
        if (response.Contains("<|", StringComparison.Ordinal) || response.Contains("```", StringComparison.Ordinal)) return "";
        response = response.TrimStart('\r', '\n').Split('\r', '\n')[0];
        if (!preserveTrailingSpace) response = response.TrimEnd();
        if (response.Any(char.IsControl) || response.Length > 240) return "";
        return response;
    }
}
