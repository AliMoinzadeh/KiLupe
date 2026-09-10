using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class OnnxObjectDetectionServiceTests
{
    [Fact]
    public void MissingModelIsReportedAsUnavailable()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"KiLupeDemo-{Guid.NewGuid():N}.onnx");

        using var service = new OnnxObjectDetectionService(missingPath);

        Assert.False(service.IsAvailable);
        Assert.Contains("fehlt", service.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PyTorchModelWithoutOnnxModelExplainsConversionRequirement()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"KiLupeDemo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var onnxPath = Path.Combine(directory, "yolov8n.onnx");
        File.WriteAllBytes(Path.ChangeExtension(onnxPath, ".pt"), Array.Empty<byte>());

        try
        {
            using var service = new OnnxObjectDetectionService(onnxPath);

            Assert.False(service.IsAvailable);
            Assert.Contains("PyTorch", service.StatusText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExportedModelLoadsAndAcceptsAnImage()
    {
        var modelPath = FindModelPath();
        Assert.True(File.Exists(modelPath), $"Exportiertes Modell fehlt: {modelPath}");

        using var service = new OnnxObjectDetectionService(modelPath);
        Assert.True(service.IsAvailable, service.StatusText);

        var image = BitmapSource.Create(
            640,
            640,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[640 * 640 * 4],
            640 * 4);
        image.Freeze();

        var results = await service.AnalyzeAsync(image, CancellationToken.None);

        Assert.NotNull(results);
    }

    private static string FindModelPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "yolov8n.onnx");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "yolov8n.onnx");
    }
}