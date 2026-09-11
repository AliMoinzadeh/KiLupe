using System.IO;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class AnalysisServiceFactoryTests
{
    [Fact]
    public void FactoryCreatesRtdetrWhenThatModelIsSelected()
    {
        var services = AnalysisServiceFactory.CreateServices(
            new AnalysisConfiguration(
                ObjectModelKind.RtDetr,
                TextCorrectionModelKind.None,
                InferenceProviderKind.Cpu),
            modelRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        try
        {
            Assert.Contains(services, service => service is RtdetrObjectDetectionService);
            Assert.DoesNotContain(services, service => service is OnnxObjectDetectionService);
            Assert.Contains(services, service => service is LocalTextAnalysisService);
        }
        finally
        {
            foreach (var disposable in services.OfType<IDisposable>())
            {
                disposable.Dispose();
            }
        }
    }

    [Fact]
    public void FactoryCreatesCorrectionAdapterAccordingToCatalogAvailability()
    {
        var modelRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var catalogAvailability = ModelCatalog.Create(
                modelRoot,
                includeFallbackRoots: false)
            .TextCorrectionModels
            .Single(option => option.Id == TextCorrectionModelKind.GermanSpelling)
            .IsAvailable;
        var service = AnalysisServiceFactory.CreateTextCorrectionService(
            new AnalysisConfiguration(
                ObjectModelKind.YoloV8N,
                TextCorrectionModelKind.GermanSpelling,
                InferenceProviderKind.Cpu),
            modelRoot);

        try
        {
            Assert.NotNull(service);
            Assert.Equal(catalogAvailability, service!.IsAvailable);
            if (!catalogAvailability)
            {
                Assert.Contains("fehlt", service.StatusText, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Fact]
    public void FactoryCreatesLocalLlmCorrectionAdapterWhenSelected()
    {
        var modelRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        using var service = AnalysisServiceFactory.CreateTextCorrectionService(
            new AnalysisConfiguration(
                ObjectModelKind.YoloV8N,
                TextCorrectionModelKind.LocalLlm,
                InferenceProviderKind.Cpu),
            modelRoot);

        Assert.NotNull(service);
        Assert.Equal("qwen2.5-3b-instruct-gguf", service!.ModelId);
        Assert.False(service.IsAvailable);
        Assert.Contains("fehlt", service.StatusText, StringComparison.OrdinalIgnoreCase);
    }
}