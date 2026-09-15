using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;
public class TextDocumentSpellingTests
{
    [Fact]
    public void FindsTyposWithoutModelAndKeepsOriginalOffsets()
    {
        var service = new TextDocumentSpellingService();
        const string text = "Ansicht\r\nEinstellugen\nDatei Einstellugen";
        var errors = service.Analyze(text);
        Assert.Equal(new[] { "Einstellugen", "Einstellugen" }, errors.Select(error => error.Label));
        Assert.Equal(new double[] { 9, 28 }, errors.Select(error => error.Bounds.X));
        Assert.All(errors, error => Assert.Contains("Einstellungen", error.Details));
    }

    [Fact]
    public void CorrectTextProducesNoSpellingFlags()
    {
        Assert.Empty(new TextDocumentSpellingService().Analyze("Ansicht Datei Einstellungen"));
    }
}
