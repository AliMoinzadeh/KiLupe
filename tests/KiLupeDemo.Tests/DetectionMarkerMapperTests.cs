using System.Windows;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class DetectionMarkerMapperTests
{
    [Fact]
    public void MapsImageBoundsIntoCaptureScreenBounds()
    {
        var marker = DetectionMarkerMapper.Map(
            new AnalysisResult(AnalysisKind.Object, "cat", 0.94, new Rect(80, 40, 160, 120), "YOLO"),
            new CaptureRegion(800, 400, 320, 320),
            new Size(320, 320));

        Assert.Equal(new Rect(880, 440, 160, 120), marker.ScreenBounds);
        Assert.Equal(DetectionMarkerShape.Circle, marker.Shape);
    }

    [Fact]
    public void MapsTextAndSpellingToDistinctStyles()
    {
        var region = new CaptureRegion(0, 0, 100, 100);
        var size = new Size(100, 100);

        var text = DetectionMarkerMapper.Map(
            new AnalysisResult(AnalysisKind.Text, "Katze", 1, new Rect(10, 20, 40, 12), "OCR"),
            region,
            size);
        var spelling = DetectionMarkerMapper.Map(
            new AnalysisResult(AnalysisKind.Spelling, "Katze", 1, new Rect(10, 20, 40, 12), "Hunspell"),
            region,
            size);

        Assert.Equal(DetectionMarkerShape.Rectangle, text.Shape);
        Assert.Equal(DetectionMarkerShape.Spelling, spelling.Shape);
        Assert.NotEqual(text.Color, spelling.Color);
    }
}