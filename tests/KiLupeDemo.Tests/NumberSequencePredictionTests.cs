using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;

public sealed class NumberSequencePredictionTests
{
    [Theory]
    [InlineData("2, 4, 6, 8, ", "10")]
    [InlineData("1, 2, 3,", " 4")]
    [InlineData("2,4,6,8,", "10")]
    [InlineData("2, 4, 6, 8", ", 10")]
    [InlineData("2 4 6 8 ", "10")]
    [InlineData("2 4 6 8", " 10")]
    [InlineData("1, 2, 4, 8, ", "16")]
    [InlineData("1, 4, 9, 16, 25, ", "36")]
    [InlineData("1, 1, 2, 3, 5, 8, ", "13")]
    [InlineData("10, 7, 4, 1, ", "-2")]
    [InlineData("-2, -4, -8, -16, ", "-32")]
    [InlineData("0, 0, 0, ", "0")]
    [InlineData("8, 4, 2, 1, ", "0.5")]
    [InlineData("1, 1/3, 1/9, ", "1/27")]
    [InlineData("0.1, 0.2, 0.3, ", "0.4")]
    [InlineData("0,5; 1,0; 1,5; ", "2")]
    [InlineData("Ein Absatz.\rFolge: 2, 4, 6, 8, ", "10")]
    public void RecognizesSupportedPatternsAndPreservesListSeparators(string before, string expected)
    {
        Assert.True(NumberSequencePrediction.TryComplete(before, "", out var result));
        Assert.Equal(expected, result);
    }
    [Theory]
    [InlineData("1, 2, ")]
    [InlineData("2, 5, 9, 16, ")]
    [InlineData("1, 4, 9, ")]
    [InlineData("2, 4,, 6, ")]
    [InlineData("1, 1/0, 3, ")]
    public void InsufficientOrInvalidPatternDoesNotProduceAGuess(string before)
    {
        Assert.True(NumberSequencePrediction.TryComplete(before, "", out var result));
        Assert.Null(result);
    }
    [Fact]
    public void DoesNotInsertBeforeAnExistingNextValue()
    {
        Assert.True(NumberSequencePrediction.TryComplete("2, 4, 6, 8, ", "10", out var result));
        Assert.Null(result);
    }
    [Fact]
    public void FollowingParagraphDoesNotBlockCurrentSequence()
    {
        Assert.True(NumberSequencePrediction.TryComplete("2, 4, 6, 8, ", "\nWeitere Aufgabe", out var result));
        Assert.Equal("10", result);
    }
    [Theory]
    [InlineData("Wir brauchen 2, 4 und 6 Exemplare.")]
    [InlineData("2 x 3 = ")]
    [InlineData("192.168.0.1")]
    public void DoesNotMisclassifyProseCalculationsOrAddresses(string before)
        => Assert.False(NumberSequencePrediction.TryComplete(before, "", out _));
    [Fact]
    public async Task SequenceUsesLocalRulesWithoutLanguageModel()
        => Assert.Equal("10", await PredictionGenerator.GenerateAsync("2, 4, 6, 8, ", "", new PredictionSettings(),
            (_, _, _, _) => throw new Exception("Model must not run"), CancellationToken.None));
    [Fact]
    public async Task UnknownSequenceDoesNotFallBackToModelGuess()
        => Assert.Empty(await PredictionGenerator.GenerateAsync("2, 5, 9, 16, ", "", new PredictionSettings(),
            (_, _, _, _) => throw new Exception("Model must not run"), CancellationToken.None));
    [Fact]
    public async Task ExistingCalculationPreferenceAlsoControlsSequences()
        => Assert.Equal("model", await PredictionGenerator.GenerateAsync("2, 4, 6, 8, ", "",
            new PredictionSettings { CompleteCalculations = false }, (_, _, _, _) => Task.FromResult("model"), CancellationToken.None));
}
