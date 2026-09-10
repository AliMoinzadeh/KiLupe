using System.Windows;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class CorrectionCoordinatorTests
{
    [Fact]
    public async Task CorrectsLinesContainingSpellingResults()
    {
        var service = new FakeCorrectionService("Das ist ein falscher Satz.");
        var coordinator = new CorrectionCoordinator(service);
        var results = new[]
        {
            new AnalysisResult(AnalysisKind.Text, "Das", 1, new Rect(0, 0, 20, 10), "OCR"),
            new AnalysisResult(AnalysisKind.Text, "falsch", 1, new Rect(25, 0, 35, 10), "OCR"),
            new AnalysisResult(AnalysisKind.Spelling, "falsch", 1, new Rect(25, 0, 35, 10), "Hunspell")
        };

        var suggestions = await coordinator.CreateSuggestionsAsync(results, CancellationToken.None);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Das falsch", suggestion.OriginalText);
        Assert.Equal("Das ist ein falscher Satz.", suggestion.CorrectedText);
    }

    [Fact]
    public async Task CorrectsTextLinesWithoutHunspellResults()
    {
        var service = new FakeCorrectionService("Ich habe einen Apfel gegessen.");
        var coordinator = new CorrectionCoordinator(service);
        var results = new[]
        {
            new AnalysisResult(AnalysisKind.Text, "Ich", 1, new Rect(0, 0, 15, 10), "OCR"),
            new AnalysisResult(AnalysisKind.Text, "habe", 1, new Rect(20, 0, 25, 10), "OCR"),
            new AnalysisResult(AnalysisKind.Text, "ein", 1, new Rect(50, 0, 15, 10), "OCR"),
            new AnalysisResult(AnalysisKind.Text, "Apfel", 1, new Rect(70, 0, 30, 10), "OCR"),
            new AnalysisResult(AnalysisKind.Text, "gegessen.", 1, new Rect(105, 0, 50, 10), "OCR")
        };

        var suggestions = await coordinator.CreateSuggestionsAsync(results, CancellationToken.None);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Ich habe ein Apfel gegessen.", suggestion.OriginalText);
        Assert.Equal("Ich habe einen Apfel gegessen.", suggestion.CorrectedText);
    }

    [Fact]
    public async Task DropsEmptyAndUnchangedCorrectionOutputs()
    {
        var service = new FakeCorrectionService(string.Empty);
        var coordinator = new CorrectionCoordinator(service);

        var suggestions = await coordinator.CreateSuggestionsAsync(
            new[]
            {
                new AnalysisResult(AnalysisKind.Text, "Fehler", 1, new Rect(0, 0, 20, 10), "OCR"),
                new AnalysisResult(AnalysisKind.Spelling, "Fehler", 1, new Rect(0, 0, 20, 10), "Hunspell")
            },
            CancellationToken.None);

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task CorrectsNonEmptyTextLinesDirectly()
    {
        var service = new FakeCorrectionService("Ich habe einen Apfel gegessen.");
        var coordinator = new CorrectionCoordinator(service);

        var suggestions = await coordinator.CreateSuggestionsAsync(
            new[] { "Ich habe ein Apfel gegessen.", "" },
            CancellationToken.None);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Ich habe ein Apfel gegessen.", suggestion.OriginalText);
        Assert.Equal("Ich habe einen Apfel gegessen.", suggestion.CorrectedText);
        Assert.Equal(new[] { "Ich habe ein Apfel gegessen." }, service.Calls);
    }

    [Fact]
    public async Task DirectTextCorrectionHonorsCancellationBeforeFirstLine()
    {
        var service = new FakeCorrectionService("Korrigiert.");
        var coordinator = new CorrectionCoordinator(service);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.CreateSuggestionsAsync(
                new[] { "Nicht mehr pruefen." },
                cancellation.Token));

        Assert.Empty(service.Calls);
    }

    private sealed class FakeCorrectionService : ITextCorrectionService
    {
        private readonly string correctedText;
        private readonly List<string> calls = new();

        public FakeCorrectionService(string correctedText)
        {
            this.correctedText = correctedText;
        }

        public string Name => "Testkorrektur";

        public string ModelId => "test";

        public bool IsAvailable => true;

        public string StatusText => "Bereit";

        public IReadOnlyList<string> Calls => calls;

        public Task<CorrectionSuggestion?> CorrectAsync(
            string text,
            CancellationToken cancellationToken)
        {
            calls.Add(text);
            return Task.FromResult<CorrectionSuggestion?>(
                new CorrectionSuggestion(text, correctedText, Name, "CPU"));
        }

        public void Dispose()
        {
        }
    }
}