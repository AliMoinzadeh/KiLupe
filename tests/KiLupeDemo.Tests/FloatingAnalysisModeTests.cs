using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class FloatingAnalysisModeTests
{
    [Theory]
    [InlineData(FloatingAnalysisMode.ObjectsCursor, FloatingAnalysisMode.TextCursor)]
    [InlineData(FloatingAnalysisMode.TextCursor, FloatingAnalysisMode.ObjectsScreen)]
    [InlineData(FloatingAnalysisMode.ObjectsScreen, FloatingAnalysisMode.TextScreen)]
    [InlineData(FloatingAnalysisMode.TextScreen, FloatingAnalysisMode.ObjectsCursor)]
    public void CyclesThroughObjectAndTextModes(
        FloatingAnalysisMode current,
        FloatingAnalysisMode expected)
    {
        Assert.Equal(expected, FloatingAnalysisModeCatalog.GetNext(current));
    }

    [Theory]
    [InlineData(FloatingAnalysisMode.ObjectsCursor, false, false, "Objekte / Cursor")]
    [InlineData(FloatingAnalysisMode.TextCursor, true, false, "Text / Cursor")]
    [InlineData(FloatingAnalysisMode.ObjectsScreen, false, true, "Objekte / Bildschirm")]
    [InlineData(FloatingAnalysisMode.TextScreen, true, true, "Text / Bildschirm")]
    public void DescribesAnalysisMode(
        FloatingAnalysisMode mode,
        bool usesTextAnalysis,
        bool usesFullScreen,
        string label)
    {
        Assert.Equal(usesTextAnalysis, FloatingAnalysisModeCatalog.UsesTextAnalysis(mode));
        Assert.Equal(usesFullScreen, FloatingAnalysisModeCatalog.IsFullScreen(mode));
        Assert.Equal(label, FloatingAnalysisModeCatalog.GetLabel(mode));
    }
}
