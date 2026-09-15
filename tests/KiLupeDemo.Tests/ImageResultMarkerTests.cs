using System.Windows;
using System.Windows.Media;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public class ImageResultMarkerTests
{
    [Fact]
    public void ErrorUsesTransparentYellowAtActualWordSizeWithoutLabel()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var result = new AnalysisResult(AnalysisKind.Spelling, "Huas", .9, new Rect(10, 20, 22, 12), "Haus");
                var marker = ImageResultMarkerFactory.Create(result, result.Bounds, false);
                Assert.Equal(22, marker.Width);
                Assert.Equal(12, marker.Height);
                Assert.Null(marker.Child);
                Assert.Equal(new Thickness(0), marker.BorderThickness);
                Assert.Equal(Color.FromArgb(100, 255, 255, 0), Assert.IsType<SolidColorBrush>(marker.Background).Color);
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void OnlyErrorsAndObjectsAreDrawnWithoutRawOcrBoxes()
    {
        var word = new AnalysisResult(AnalysisKind.Text, "Huas", .9, new Rect(10, 20, 22, 12), "OCR");
        var error = word with { Kind = AnalysisKind.Spelling, Details = "Haus" };
        var other = word with { Label = "richtig", Bounds = new Rect(50, 20, 40, 12) };
        var detectedObject = other with { Kind = AnalysisKind.Object, Label = "Objekt" };
        Assert.Equal(new[] { error, detectedObject }, ImageResultMarkerFactory.VisibleResults(new[] { word, error, other, detectedObject }));
    }
}
