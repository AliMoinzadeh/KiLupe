using System.Text.Json;
using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;

public sealed class ArithmeticPredictionTests
{
    [Theory]
    [InlineData("2 x 3 = ", "6")]
    [InlineData("2 × 3 =", "6")]
    [InlineData("Ein Absatz.\r2 x 3 =", "6")]
    [InlineData("2 + 3 * 4 = ", "14")]
    [InlineData("(2 + 3) * 4 = ", "20")]
    [InlineData("-2 * (-3) = ", "6")]
    [InlineData("1,5 + 2,25 = ", "3,75")]
    [InlineData("0.1 + 0.2 = ", "0.3")]
    [InlineData("1 / 8 = ", "0.125")]
    [InlineData("1 / 3 = ", "1/3")]
    [InlineData("6 ÷ 2 = ", "3")]
    [InlineData("Ein Absatz.\nErgebnis: 12 - 7 = ", "5")]
    public void CalculatesExactResult(string before, string expected)
    {
        Assert.True(ArithmeticPrediction.TryComplete(before, "", out var result));
        Assert.Equal(expected, result);
    }
    [Theory]
    [InlineData("1 / 0 =")]
    [InlineData("2 + =")]
    [InlineData("2 ** 3 =")]
    [InlineData("sqrt(9) + 1 =")]
    [InlineData("(2 + 3 =")]
    [InlineData("2 + 3) =")]
    [InlineData("1,000.5 + 2 =")]
    [InlineData("a + 2 =")]
    [InlineData("a + b =")]
    [InlineData("2 = 3 =")]
    [InlineData("2; Process.Start(3) =")]
    public void RecognizedButUnsafeArithmeticNeverProducesGuessedResult(string before)
    {
        Assert.True(ArithmeticPrediction.TryComplete(before, "", out var result));
        Assert.Null(result);
    }
    [Fact]
    public void FollowingParagraphDoesNotHideAnAnswerOnCurrentLine()
    {
        Assert.True(ArithmeticPrediction.TryComplete("2 x 3 = ", "\n6 weitere Aufgaben", out var result));
        Assert.Equal("6", result);
        Assert.Equal("6", PredictionText.GetInsertion(result!, "2 x 3 = ", "\n6 weitere Aufgaben"));
    }
    [Fact]
    public async Task CalculatorWorksWithoutAnInstalledLanguageModel()
        => Assert.Equal("6", await PredictionGenerator.GenerateAsync("2 x 3 = ", "", new PredictionSettings(), null, CancellationToken.None));
    [Fact]
    public async Task ClosedSentenceDoesNotInvokeModelInRestrictedMode()
        => Assert.Empty(await PredictionGenerator.GenerateAsync("Fertig. ", "", new PredictionSettings(),
            (_, _, _, _) => throw new Exception("No request expected"), CancellationToken.None));
    [Theory]
    [InlineData("Bitte wenden Sie sich an Dr. ")]
    [InlineData("Wir treffen uns am 30. ")]
    public void AbbreviationsAndOrdinalsRemainOpen(string text)
        => Assert.False(PredictionPrompt.EndsSentence(text));
    [Fact]
    public void DoesNotReplaceExistingAnswer()
    {
        Assert.True(ArithmeticPrediction.TryComplete("2 x 3 = ", "6", out var result));
        Assert.Null(result);
    }
    [Fact]
    public void OrdinaryTextDoesNotTriggerCalculator()
        => Assert.False(ArithmeticPrediction.TryComplete("Ich freue mich ", "", out _));
    [Fact]
    public void LimitsInputComplexity()
    {
        Assert.True(ArithmeticPrediction.TryComplete(new string('(', 40) + "1" + new string(')', 40) + " =", "", out var result));
        Assert.Null(result);
    }
    [Fact]
    public async Task MathDoesNotCallLanguageModel()
    {
        var result = await PredictionGenerator.GenerateAsync("2 x 3 = ", "", new PredictionSettings(),
            (_, _, _, _) => throw new Exception("Model must not run"), CancellationToken.None);
        Assert.Equal("6", result);
    }
    [Fact]
    public async Task InvalidMathDoesNotFallBackToGuessing()
    {
        var result = await PredictionGenerator.GenerateAsync("1 / 0 = ", "", new PredictionSettings(),
            (_, _, _, _) => throw new Exception("Model must not run"), CancellationToken.None);
        Assert.Empty(result);
    }
    [Fact]
    public async Task DisablingMathUsesNormalPredictionWithChosenSentenceMode()
    {
        var settings = new PredictionSettings { CompleteCalculations = false, OnlyCurrentSentence = false };
        var result = await PredictionGenerator.GenerateAsync("2 x 3 = ", "", settings,
            (_, _, onlySentence, _) => Task.FromResult(onlySentence ? "wrong mode" : "model output"), CancellationToken.None);
        Assert.Equal("model output", result);
    }
    [Fact]
    public void PreferencesSurviveSerializationAndOlderSettingsKeepDefaults()
    {
        var saved = new PredictionSettings { OnlyCurrentSentence = false, CompleteCalculations = false };
        var loaded = JsonSerializer.Deserialize<PredictionSettings>(JsonSerializer.Serialize(saved))!;
        Assert.False(loaded.OnlyCurrentSentence);
        Assert.False(loaded.CompleteCalculations);
        var legacy = JsonSerializer.Deserialize<PredictionSettings>("{}")!;
        Assert.True(legacy.OnlyCurrentSentence);
        Assert.True(legacy.CompleteCalculations);
    }
    [Fact]
    public void ClosedSentenceCanBeContinuedWhenOptionIsOff()
    {
        Assert.Null(PredictionPrompt.Create("Das ist erledigt. ", "", text => text.Length / 4, onlyCurrentSentence: true));
        var openMode = PredictionPrompt.Create("Das ist erledigt. ", "", text => text.Length / 4, onlyCurrentSentence: false);
        Assert.NotNull(openMode);
        Assert.Equal("Als Nächstes", openMode.ReadCompletion(" Als Nächstes"));
    }
}
