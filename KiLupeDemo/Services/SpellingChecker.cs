namespace KiLupeDemo.Services;

public sealed class SpellingChecker
{
    private readonly Func<string, bool>? germanCheck;
    private readonly Func<string, bool>? englishCheck;

    public SpellingChecker(
        Func<string, bool>? germanCheck,
        Func<string, bool>? englishCheck)
    {
        this.germanCheck = germanCheck;
        this.englishCheck = englishCheck;
    }

    public bool IsMisspelled(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        var normalized = new string(word
            .Where(character => char.IsLetter(character) || character is '\'' or '-')
            .ToArray())
            .ToLowerInvariant();
        if (normalized.Length < 2 || germanCheck is null && englishCheck is null)
        {
            return false;
        }

        var germanKnown = germanCheck?.Invoke(normalized) == true;
        var englishKnown = englishCheck?.Invoke(normalized) == true;
        return !germanKnown && !englishKnown;
    }
}