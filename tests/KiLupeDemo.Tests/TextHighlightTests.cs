using System.Windows;
using System.Windows.Media;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;
public class TextHighlightTests
{
    [Fact]
    public void SpellingMarkerIsYellowAndShowsSuggestion()
    {
        var marker = DetectionMarkerMapper.Map(new AnalysisResult(AnalysisKind.Spelling, "Huas", 1, new Rect(10, 20, 30, 12), "Haus"), new CaptureRegion(0,0,100,100), new Size(100,100));
        Assert.Equal(Colors.Yellow, marker.Color);
        Assert.Equal("Haus", marker.Label);
    }
}
