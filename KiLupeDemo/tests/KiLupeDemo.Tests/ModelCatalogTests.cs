using System.IO;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class ModelCatalogTests
{
    [Fact]
    public void CatalogContainsTheStableObjectAndCorrectionChoices()
    {
        var catalog = ModelCatalog.Create(Path.GetTempPath());

        Assert.Contains(catalog.ObjectModels, option => option.Id == ObjectModelKind.YoloV8N);
        Assert.Contains(catalog.ObjectModels, option => option.Id == ObjectModelKind.RtDetr);
        Assert.Contains(catalog.TextCorrectionModels, option => option.Id == TextCorrectionModelKind.None);
        Assert.Contains(catalog.TextCorrectionModels, option => option.Id == TextCorrectionModelKind.GermanSpelling);
        Assert.Contains(catalog.TextCorrectionModels, option => option.Id == TextCorrectionModelKind.LocalLlm);
    }

    [Fact]
    public void MissingArtifactsAreReportedWithoutDisablingOtherChoices()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var catalog = ModelCatalog.Create(root, includeFallbackRoots: false);

        Assert.Contains(catalog.ObjectModels, option => option.Id == ObjectModelKind.RtDetr && !option.IsAvailable);
        Assert.Contains(catalog.TextCorrectionModels, option => option.Id == TextCorrectionModelKind.None && option.IsAvailable);

        var localLlm = catalog.TextCorrectionModels
            .Single(option => option.Id == TextCorrectionModelKind.LocalLlm);
        Assert.False(localLlm.IsAvailable);
        Assert.Contains("Q4_K_M.gguf", localLlm.StatusText, StringComparison.OrdinalIgnoreCase);
    }
}