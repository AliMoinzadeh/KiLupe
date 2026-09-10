using System.Windows;
using KiLupeDemo.Models;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class AnalysisResultTests
{
    [Fact]
    public void ResultKeepsDetectionDetails()
    {
        var bounds = new Rect(10, 20, 30, 40);

        var result = new AnalysisResult(AnalysisKind.Spelling, "Katze", 1, bounds, "Vorschlag: Katze");

        Assert.Equal(AnalysisKind.Spelling, result.Kind);
        Assert.Equal("Katze", result.Label);
        Assert.Equal(1, result.Confidence);
        Assert.Equal(bounds, result.Bounds);
        Assert.Equal("Vorschlag: Katze", result.Details);
    }
}