using System.Globalization;
using System.Numerics;
namespace KiLupeDemo.Services;

public static class ArithmeticPrediction
{
    // True also means recognized but invalid: do not fall back to an LLM guess.
    public static bool TryComplete(string before, string after, out string? result)
    {
        result = null;
        var line = before[(Math.Max(before.LastIndexOf('\n'), before.LastIndexOf('\r')) + 1)..].Trim();
        if (!line.EndsWith('=') || !line.Any(character => char.IsAsciiDigit(character) || "+-−*/×xX÷()".Contains(character))) return false;
        if (!string.IsNullOrWhiteSpace(after.Split('\r', '\n')[0])) return true;
        var expression = line[..^1].Trim();
        var label = expression.LastIndexOf(':');
        if (label >= 0)
        {
            // Numeric colons can mean division, a ratio or a time, not a label.
            if (expression[..label].Any(char.IsAsciiDigit)) return true;
            expression = expression[(label + 1)..].Trim();
        }
        if (expression.Length is 0 or > 200) return true;
        try { result = new Parser(expression).Parse().Format(expression.Contains(',') ? ',' : '.'); }
        catch (Exception exception) when (exception is FormatException or DivideByZeroException or OverflowException)
        {
            // Unsupported, ambiguous and undefined expressions have no suggestion.
        }
        return true;
    }

    // Bounded recursive descent; no script evaluation or external calculator.
    private sealed class Parser(string text)
    {
        private int position;
        public ExactNumber Parse()
        {
            var value = Sum(0);
            SkipSpaces();
            if (position != text.Length) throw new FormatException();
            return value;
        }
        private ExactNumber Sum(int depth)
        {
            var value = Product(depth);
            while (true)
            {
                if (Take('+')) value += Product(depth);
                else if (Take('-') || Take('−')) value -= Product(depth);
                else return value;
            }
        }
        private ExactNumber Product(int depth)
        {
            var value = Atom(depth);
            while (true)
            {
                if (Take('*') || Take('x') || Take('X') || Take('×')) value *= Atom(depth);
                else if (Take('/') || Take('÷')) value /= Atom(depth);
                else return value;
            }
        }
        private ExactNumber Atom(int depth)
        {
            if (depth > 16) throw new FormatException();
            if (Take('+')) return Atom(depth + 1);
            if (Take('-') || Take('−')) return new ExactNumber(-1, 1) * Atom(depth + 1);
            if (Take('('))
            {
                var value = Sum(depth + 1);
                if (!Take(')')) throw new FormatException();
                return value;
            }
            SkipSpaces();
            var start = position;
            while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
            if (position == start) throw new FormatException();
            var whole = text[start..position];
            var decimals = "";
            if (position < text.Length && text[position] is '.' or ',')
            {
                position++;
                var fractionalStart = position;
                while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
                if (position == fractionalStart) throw new FormatException();
                decimals = text[fractionalStart..position];
            }
            if (whole.Length + decimals.Length > 30) throw new FormatException();
            return new ExactNumber(BigInteger.Parse(whole + decimals, CultureInfo.InvariantCulture), BigInteger.Pow(10, decimals.Length));
        }
        private bool Take(char character)
        {
            SkipSpaces();
            if (position >= text.Length || text[position] != character) return false;
            position++;
            return true;
        }
        private void SkipSpaces() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
    }
}
