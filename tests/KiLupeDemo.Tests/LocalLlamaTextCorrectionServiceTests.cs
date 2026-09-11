using System.IO;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class LocalLlamaTextCorrectionServiceTests
{
    [Fact]
    public async Task MissingModelIsUnavailableAndReturnsNoSuggestion()
    {
        using var service = new LocalLlamaTextCorrectionService(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "model.gguf"));

        Assert.False(service.IsAvailable);
        Assert.Contains("fehlt", service.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await service.CorrectAsync("Der mann gehen.", CancellationToken.None));
    }

    [Fact]
    public void PromptRequiresOnlyCorrectedGermanText()
    {
        var prompt = LocalLlamaTextCorrectionService.BuildCorrectionPrompt("Der mann gehen.");

        Assert.Contains("nur den korrigierten deutschen Text", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Der mann gehen.", prompt, StringComparison.Ordinal);
    }
}
