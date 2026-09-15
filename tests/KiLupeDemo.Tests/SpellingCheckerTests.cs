using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class SpellingCheckerTests
{
    [Theory]
    [InlineData("Ansicht")]
    [InlineData("Datei")]
    [InlineData("Einstellungen")]
    [InlineData("Ansicht:")]
    public void CorrectGermanMenuWordsAreNotSpellingErrors(string text)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var dictionary = WeCantSpell.Hunspell.WordList.CreateFromFiles(
            System.IO.Path.Combine(AppContext.BaseDirectory, "dictionaries", "de_DE.dic"));
        var checker = new SpellingChecker(dictionary.Check, null);
        Assert.False(checker.IsMisspelled(text));
        Assert.True(checker.IsMisspelled("Einstellugen"));
    }
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