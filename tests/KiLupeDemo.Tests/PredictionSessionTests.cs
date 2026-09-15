using System.Windows;
using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;
public class PredictionSessionTests
{
    private static CaretSnapshot Snapshot() => new(new IntPtr(10), "element", "notepad", "Hallo ", "", new Rect(10, 20, 1, 18));
    [Fact]
    public void WaitsForPauseAndRequestsOnlyOnce()
    {
        var session = new PredictionSession();
        Assert.Null(session.Observe(Snapshot(), 0, 600));
        Assert.Null(session.Observe(Snapshot(), 599, 600));
        Assert.NotNull(session.Observe(Snapshot(), 600, 600));
        Assert.Null(session.Observe(Snapshot(), 1200, 600));
    }
    [Fact]
    public void DiscardsLateResponseAfterCursorMoves()
    {
        var session = new PredictionSession();
        session.Observe(Snapshot(), 0, 600);
        var generation = session.Observe(Snapshot(), 600, 600)!.Value;
        session.Observe(Snapshot() with { Before = "Hal", After = "lo " }, 700, 600);
        Assert.False(session.Complete(generation, "Welt"));
        Assert.Null(session.Suggestion);
    }
    [Fact]
    public void AcceptanceRequiresPermissionAndIdenticalCurrentContextAndIsSingleUse()
    {
        var session = new PredictionSession();
        session.Observe(Snapshot(), 0, 600);
        var generation = session.Observe(Snapshot(), 600, 600)!.Value;
        Assert.True(session.Complete(generation, "Welt"));
        Assert.Null(session.Take(Snapshot(), false));
        Assert.Null(session.Take(Snapshot() with { Window = new IntPtr(11) }, true));
        Assert.Equal("Welt", session.Take(Snapshot(), true));
        Assert.Null(session.Take(Snapshot(), true));
    }
    [Fact]
    public void UnsupportedFieldClearsSuggestion()
    {
        var session = new PredictionSession();
        session.Observe(Snapshot(), 0, 1);
        session.Complete(session.Observe(Snapshot(), 1, 1)!.Value, "Welt");
        session.Observe(null, 2, 1);
        Assert.Null(session.Suggestion);
        Assert.Null(session.Take(Snapshot(), true));
    }
    [Theory]
    [InlineData(" Welt<|im_end|>", " Welt")]
    [InlineData("\nErste Zeile\nZweite Zeile", "Erste Zeile")]
    [InlineData("<|im_start|>assistant", "")]
    [InlineData("", "")]
    public void CompletionPreservesLeadingSpaceAndRejectsRoleOutput(string input, string expected)
        => Assert.Equal(expected, PredictionText.Normalize(input));
}
