using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace KiLupeDemo.Services;

public static class NumberSequencePrediction
{
    private static readonly Regex Number = new(@"^[+-]?[0-9]{1,30}(?:[.,][0-9]{1,16})?(?:/[+-]?[0-9]{1,30})?$", RegexOptions.CultureInvariant);

    // Recognized numeric lists never fall back to a language-model guess.
    public static bool TryComplete(string before, string after, out string? result)
    {
        result = null;
        var start = Math.Max(before.LastIndexOf('\n'), before.LastIndexOf('\r')) + 1;
        var body = before[start..].TrimStart();
        var label = body.IndexOf(':');
        if (label >= 0)
        {
            if (body[..label].Any(char.IsAsciiDigit)) return false;
            body = body[(label + 1)..].TrimStart();
        }
        if (!body.Any(char.IsAsciiDigit) || body.Any(character => !char.IsAsciiDigit(character)
            && !char.IsWhiteSpace(character) && !"+-−,;./".Contains(character))) return false;
        var separator = body.Contains(';') ? ';' : body.Contains(',') ? ',' : ' ';
        var trimmed = body.TrimEnd();
        var parts = separator == ' ' ? Regex.Split(trimmed, @"\s+") : trimmed.Split(separator);
        if (parts.Length < 2) return false;
        if (!string.IsNullOrWhiteSpace(after.Split('\r', '\n')[0])) return true;
        if (body.Length > 512 || parts.Length > 33) return true;
        var trailingSeparator = separator != ' ' && trimmed.EndsWith(separator);
        if (trailingSeparator) parts = parts[..^1];
        if (parts.Length < 3 || parts.Length > 32) return true;
        try
        {
            var values = parts.Select(Parse).ToArray();
            var predictions = Predict(values);
            if (predictions.Count != 1) return true;
            var formatted = predictions.Single().Format(separator == ';' && body.Contains(',') ? ',' : '.');
            if (formatted.Length > 100) return true;
            var hasSpace = char.IsWhiteSpace(body[^1]);
            var spacedList = body.Contains(separator + " ") || body.Contains(separator + "\t");
            var insertionPrefix = separator == ' ' ? (hasSpace ? "" : " ")
                : trailingSeparator ? (hasSpace || !spacedList ? "" : " ")
                : separator + (spacedList ? " " : "");
            result = insertionPrefix + formatted;
        }
        catch (Exception exception) when (exception is FormatException or DivideByZeroException or OverflowException)
        {
            // Bounded exact arithmetic: invalid lists and oversized numbers yield no suggestion.
        }
        return true;
    }

    private static HashSet<ExactNumber> Predict(ExactNumber[] values)
    {
        var predictions = new HashSet<ExactNumber>();
        var differences = values.Zip(values.Skip(1), (left, right) => right - left).ToArray();
        if (differences.All(difference => difference == differences[0]))
            predictions.Add(values[^1] + differences[0]);

        if (!values[0].Numerator.IsZero)
        {
            var ratio = values[1] / values[0];
            if (Enumerable.Range(1, values.Length - 1).All(index => values[index] == values[index - 1] * ratio))
                predictions.Add(values[^1] * ratio);
        }

        if (values.Length >= 5)
        {
            var secondDifference = differences[1] - differences[0];
            if (Enumerable.Range(1, differences.Length - 1).All(index => differences[index] - differences[index - 1] == secondDifference))
                predictions.Add(values[^1] + differences[^1] + secondDifference);
        }

        if (values.Length >= 6 && Enumerable.Range(2, values.Length - 2).All(index => values[index] == values[index - 1] + values[index - 2]))
            predictions.Add(values[^1] + values[^2]);
        return predictions;
    }

    private static ExactNumber Parse(string text)
    {
        text = text.Trim().Replace('−', '-');
        if (!Number.IsMatch(text)) throw new FormatException();
        var fraction = text.Split('/');
        var decimalParts = fraction[0].Split('.', ',');
        var scale = decimalParts.Length == 2 ? decimalParts[1].Length : 0;
        var numerator = BigInteger.Parse(string.Concat(decimalParts), CultureInfo.InvariantCulture);
        var denominator = BigInteger.Pow(10, scale);
        if (fraction.Length == 2) denominator *= BigInteger.Parse(fraction[1], CultureInfo.InvariantCulture);
        return new ExactNumber(numerator, denominator);
    }
}
