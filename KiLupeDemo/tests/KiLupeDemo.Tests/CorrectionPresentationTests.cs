using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class CorrectionPresentationTests
{
    [Fact]
    public void EmptySuggestionHidesTheCorrectionSurface()
    {
        var state = CorrectionPresentationState.From(Array.Empty<CorrectionSuggestion>());

        Assert.False(state.IsVisible);
        Assert.Equal(string.Empty, state.SuggestionText);
    }

    [Fact]
    public void LatestSuggestionWinsOverOlderFloatingResults()
    {
        var state = new CorrectionPresentationState();

        state.Apply(10, new CorrectionSuggestion("alt", "neu", "test", "CPU"));
        state.Apply(9, new CorrectionSuggestion("alt", "veraltet", "test", "CPU"));

        Assert.Equal("neu", state.SuggestionText);
    }
}