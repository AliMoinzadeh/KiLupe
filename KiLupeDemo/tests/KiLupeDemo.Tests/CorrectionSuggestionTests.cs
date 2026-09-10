using KiLupeDemo.Models;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class CorrectionSuggestionTests
{
    [Fact]
    public void StoresOriginalCorrectedTextAndRuntimeMetadata()
    {
        var suggestion = new CorrectionSuggestion(
            "Das ist falsch.",
            "Das ist ein falscher Satz.",
            "German spelling correction",
            "CPU");

        Assert.Equal("Das ist falsch.", suggestion.OriginalText);
        Assert.Equal("Das ist ein falscher Satz.", suggestion.CorrectedText);
        Assert.Equal("German spelling correction", suggestion.ModelName);
        Assert.Equal("CPU", suggestion.ProviderName);
    }
}