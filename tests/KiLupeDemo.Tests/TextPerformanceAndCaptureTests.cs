using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public class TextPerformanceAndCaptureTests
{
    [Theory]
    [InlineData(1000, 500, 0, 0, 1920, 1080, 520, 340, 960, 320)]
    [InlineData(-1900, -100, -1920, -200, 1920, 1080, -1920, -200, 960, 320)]
    [InlineData(200, 100, 0, 0, 640, 200, 0, 0, 640, 200)]
    public void WideCaptureClampsAtScreenEdges(int x, int y, int left, int top, int sw, int sh,
        int expectedLeft, int expectedTop, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(new CaptureRegion(expectedLeft, expectedTop, expectedWidth, expectedHeight),
            ScreenCaptureService.CalculateCursorRegion(x, y, left, top, sw, sh, 960, 320));
    }

    [Fact]
    public void RepeatedWordsKeepEveryOccurrenceAndCancellationStopsCheck()
    {
        var service = new TextDocumentSpellingService();
        var text = string.Join(" ", Enumerable.Repeat("Einstellugen", 1000));
        var errors = service.Analyze(text);
        Assert.Equal(1000, errors.Count);
        Assert.Equal(999 * 13, errors[^1].Bounds.X);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => service.Analyze(text, cancellation.Token));
    }
}
