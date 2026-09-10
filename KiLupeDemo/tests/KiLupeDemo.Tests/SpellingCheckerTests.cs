using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class SpellingCheckerTests
{
    [Fact]
    public void GermanDictionaryCanValidateWordsWithoutEnglishDictionary()
    {
        var checker = new SpellingChecker(
            germanCheck: word => word == "katze",
            englishCheck: null);

        Assert.False(checker.IsMisspelled("Katze"));
        Assert.True(checker.IsMisspelled("qzxwort"));
    }

    [Fact]
    public void EnglishDictionaryCanValidateWordsWithoutGermanDictionary()
    {
        var checker = new SpellingChecker(
            germanCheck: null,
            englishCheck: word => word == "screen");

        Assert.False(checker.IsMisspelled("Screen"));
        Assert.True(checker.IsMisspelled("qzxword"));
    }

    [Fact]
    public void MissingDictionariesDoNotFlagWords()
    {
        var checker = new SpellingChecker(
            germanCheck: null,
            englishCheck: null);

        Assert.False(checker.IsMisspelled("qzxwort"));
    }
}