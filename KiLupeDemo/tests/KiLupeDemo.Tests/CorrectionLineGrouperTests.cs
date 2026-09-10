using System.Windows;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class CorrectionLineGrouperTests
{
    [Fact]
    public void GroupsWordsWithNearbyVerticalCentersIntoOneLine()
    {
        var results = new[]
        {
            new AnalysisResult(AnalysisKind.Text, "Das", 1, new Rect(0, 0, 25, 12), "OCR"),
            new AnalysisResult(AnalysisKind.Text, "ist", 1, new Rect(30, 1, 18, 12), "OCR"),
            new AnalysisResult(AnalysisKind.Text, "falsch", 1, new Rect(0, 30, 35, 12), "OCR")
        };

        var lines = CorrectionLineGrouper.Group(results);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Das ist", lines[0].Text);
    }
}