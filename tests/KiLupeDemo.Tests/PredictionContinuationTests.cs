using System.Windows;
using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;

public sealed class PredictionContinuationTests
{
    [Theory]
    [InlineData("Ich freue mich ", "Ich freue mich auf unser Treffen.", "auf unser Treffen.")]
    [InlineData("Ich freue mich", "Ich freue mich auf unser Treffen.", " auf unser Treffen.")]
    [InlineData("Ich freu", "Ich freue mich auf unser Treffen.", "e mich auf unser Treffen.")]
    [InlineData("Ein anderer Satz. Ich freue mich ", "Ich freue mich auf unser Treffen.", "auf unser Treffen.")]
    [InlineData("Ein anderer Absatz.\r\nIch freue mich ", "Ich freue mich auf unser Treffen.", "auf unser Treffen.")]
    [InlineData("Ich freue mich ", "  ich freue mich auf unser Treffen.", "auf unser Treffen.")]
    [InlineData("Ich  freue mich ", "Ich freue mich auf unser Treffen.", "auf unser Treffen.")]
    [InlineData("Ein Absatz.\nIch freue mich ", "Ein Absatz.\nIch freue mich auf morgen.", "auf morgen.")]
    [InlineData("Ich mag ", "Ich magische Dinge.", "Ich magische Dinge.")]
    [InlineData("Hallo ", "Hallo Welt", "Welt")]
    [InlineData("Ich freue mich ", "auf unser Treffen.", "auf unser Treffen.")]
    [InlineData("Das ist sehr ", "sehr gut.", "sehr gut.")]
    [InlineData("Er sagt das", "schoene Wort.", "schoene Wort.")]
    public void DisplaysAndInsertsOnlyMissingContinuation(string before, string response, string expected)
    {
        var snapshot = new CaretSnapshot(new IntPtr(1), "editor", "notepad", before, "", new Rect(0, 0, 1, 20));
        var session = new PredictionSession();
        session.Observe(snapshot, 0, 300);
        var generation = session.Observe(snapshot, 300, 300)!.Value;
        Assert.True(session.Complete(generation, response));
        Assert.Equal(expected, session.Suggestion);
        Assert.Equal(expected, session.Take(snapshot, true));
    }

    [Fact]
    public void RemovesLongEchoBeforeApplyingInsertionLengthLimit()
    {
        var before = string.Concat(Enumerable.Repeat("Ein langes Beispiel ", 20));
        Assert.Equal("endet hier.", PredictionText.GetInsertion(before + "endet hier.", before));
    }
    [Fact]
    public void EchoWithoutNewTextDoesNotProduceSuggestion()
    {
        var snapshot = new CaretSnapshot(new IntPtr(1), "editor", "notepad", "Ich freue mich ", "", new Rect(0, 0, 1, 20));
        var session = new PredictionSession();
        session.Observe(snapshot, 0, 300);
        Assert.False(session.Complete(session.Observe(snapshot, 300, 300)!.Value, "Ich freue mich"));
        Assert.Null(session.Suggestion);
    }
}
