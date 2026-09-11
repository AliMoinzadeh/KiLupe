using System.IO;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Microsoft.ML.Tokenizers;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class OnnxTextCorrectionServiceTests
{
    [Fact]
    public async Task MissingCorrectionModelReturnsUnavailableAndNoSuggestion()
    {
        using var service = new OnnxTextCorrectionService(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "model.onnx"),
            tokenizerDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            InferenceProviderKind.Cpu);

        Assert.False(service.IsAvailable);
        Assert.Contains("fehlt", service.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await service.CorrectAsync("Ein Satz.", CancellationToken.None));
    }

    [Fact]
    public async Task DownloadedCorrectionModelProducesSuggestion()
    {
        var modelPath = FindDownloadedModel();
        if (modelPath is null)
        {
            return;
        }

        using var service = new OnnxTextCorrectionService(
            modelPath,
            Path.GetDirectoryName(modelPath),
            InferenceProviderKind.Cpu);

        Assert.True(service.IsAvailable, service.StatusText);
        var suggestion = await service.CorrectAsync(
            "dies ist ein falsch geschriebener satz",
            CancellationToken.None);

        Assert.NotNull(suggestion);
        Assert.NotEqual(
            "dies ist ein falsch geschriebener satz",
            suggestion!.CorrectedText,
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task DownloadedCorrectionModelCorrectsMisspelling()
    {
        var modelPath = FindDownloadedModel();
        if (modelPath is null)
        {
            return;
        }

        using var service = new OnnxTextCorrectionService(
            modelPath,
            Path.GetDirectoryName(modelPath),
            InferenceProviderKind.Cpu);

        var suggestion = await service.CorrectAsync(
            "Das ist ein falsh geschriebener Satz.",
            CancellationToken.None);

        Assert.NotNull(suggestion);
        Assert.Equal("Das ist ein falsch geschriebener Satz.", suggestion!.CorrectedText);
    }

    [Fact]
    public void DownloadedTokenizerLoads()
    {
        var modelPath = FindDownloadedModel();
        if (modelPath is null)
        {
            return;
        }

        using var stream = File.OpenRead(
            Path.Combine(Path.GetDirectoryName(modelPath)!, "tokenizer.json"));
        var tokenizer = SentencePieceTokenizer.CreateFromTokenizerJson(
            stream,
            addBeginningOfSentence: false,
            addEndOfSentence: false);

        Assert.NotEmpty(tokenizer.EncodeToIds("correct: ein test"));
    }

    private static string? FindDownloadedModel()
    {
        var relativePath = Path.Combine(
            "artifacts",
            "models",
            "german-spelling-correction-onnx",
            "model.onnx");
        var roots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };
        return roots
            .Select(root => Path.Combine(root, relativePath))
            .FirstOrDefault(File.Exists);
    }
}