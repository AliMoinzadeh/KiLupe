using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;
public class TextErrorAdornerTests
{
    [Fact]
    public void HighlightsMultipleErrorsWithoutSelectingOrChangingText()
    {
        OnUiThread(() =>
        {
            var box = new TextBox { Text = "Huas und Feler", FontSize = 16 };
            var host = new AdornerDecorator { Child = box };
            host.Measure(new Size(300, 100)); host.Arrange(new Rect(0, 0, 300, 100)); host.UpdateLayout();
            var adorner = new TextErrorAdorner(box);
            adorner.SetResults(new[] { Error(0, 4), Error(9, 5) });
            Assert.Equal(2, adorner.GetHighlightBounds().Count);
            Assert.Equal(0, box.SelectionLength);
            Assert.Equal("Huas und Feler", box.Text);
            adorner.SetResults(Array.Empty<AnalysisResult>());
            Assert.Empty(adorner.GetHighlightBounds());
        });
    }

    [Fact]
    public void WrappedErrorsUseSeparateVisibleLines()
    {
        OnUiThread(() =>
        {
            var box = new TextBox { Text = "eins zwei drei vier fuenf sechs sieben", FontSize = 16, TextWrapping = TextWrapping.Wrap };
            var host = new AdornerDecorator { Child = box };
            host.Measure(new Size(90, 200)); host.Arrange(new Rect(0, 0, 90, 200)); host.UpdateLayout();
            var adorner = new TextErrorAdorner(box);
            adorner.SetResults(new[] { Error(0, box.Text.Length) });
            var rectangles = adorner.GetHighlightBounds();
            Assert.True(rectangles.Count > 1);
            Assert.All(rectangles, rect => { Assert.True(rect.Width > 0); Assert.True(rect.Right <= box.ActualWidth); });
        });
    }

    [Fact]
    public void ScrollingClipsOffscreenErrorsAndRepositionsVisibleOnes()
    {
        OnUiThread(() =>
        {
            var box = new TextBox { Text = string.Join("\n", Enumerable.Repeat("Fehler", 30)), FontSize = 16, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var host = new AdornerDecorator { Child = box };
            host.Measure(new Size(200, 100)); host.Arrange(new Rect(0, 0, 200, 100)); host.UpdateLayout();
            var adorner = new TextErrorAdorner(box);
            adorner.SetResults(new[] { Error(0, 6) });
            Assert.NotEmpty(adorner.GetHighlightBounds());
            box.ScrollToEnd(); host.UpdateLayout();
            Assert.Empty(adorner.GetHighlightBounds());
            adorner.SetResults(new[] { Error(box.Text.Length - 6, 6) });
            Assert.NotEmpty(adorner.GetHighlightBounds());
            Assert.All(adorner.GetHighlightBounds(), rect => Assert.True(rect.Bottom <= box.ActualHeight));
        });
    }
    private static AnalysisResult Error(int start, int length) => new(AnalysisKind.Spelling, "", 1, new Rect(start, 0, length, 1), "");
    private static void OnUiThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
