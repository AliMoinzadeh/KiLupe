using System.Globalization;
using System.Numerics;
namespace KiLupeDemo.Services;

internal readonly record struct ExactNumber
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }
    public ExactNumber(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero) throw new DivideByZeroException();
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
        if (Numerator.GetBitLength() > 2048 || Denominator.GetBitLength() > 2048) throw new OverflowException();
    }
    public static ExactNumber operator +(ExactNumber a, ExactNumber b) => new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
    public static ExactNumber operator -(ExactNumber a, ExactNumber b) => new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);
    public static ExactNumber operator *(ExactNumber a, ExactNumber b) => new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
    public static ExactNumber operator /(ExactNumber a, ExactNumber b) => new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
    public string Format(char separator)
    {
        var numerator = BigInteger.Abs(Numerator);
        var integer = BigInteger.DivRem(numerator, Denominator, out var remainder);
        var sign = Numerator.Sign < 0 ? "-" : "";
        var digits = new System.Text.StringBuilder();
        for (var index = 0; !remainder.IsZero && index < 16; index++)
        {
            var digit = BigInteger.DivRem(remainder * 10, Denominator, out remainder);
            digits.Append(digit.ToString(CultureInfo.InvariantCulture));
        }
        // Exact fractions instead of falsely exact rounded decimals after '='.
        if (!remainder.IsZero) return Numerator.ToString(CultureInfo.InvariantCulture) + "/" + Denominator.ToString(CultureInfo.InvariantCulture);
        return sign + integer.ToString(CultureInfo.InvariantCulture) + (digits.Length == 0 ? "" : separator + digits.ToString());
    }
}

