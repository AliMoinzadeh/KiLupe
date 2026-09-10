using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class RtdetrObjectDetectionServiceTests
{
    [Fact]
    public async Task DownloadedModelLoadsAndAcceptsAnImage()
    {
        var modelPath = FindModelPath();
        Assert.True(File.Exists(modelPath), $"RT-DETR-Modell fehlt: {modelPath}");

        using var service = new RtdetrObjectDetectionService(modelPath);
        Assert.True(service.IsAvailable, service.StatusText);

        var image = BitmapSource.Create(
            640,
            480,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[640 * 480 * 4],
            640 * 4);
        image.Freeze();

        var results = await service.AnalyzeAsync(image, CancellationToken.None);

        Assert.NotNull(results);
    }

    private static string FindModelPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "artifacts",
                "models",
                "rtdetr_v2_r18vd-ONNX",
                "onnx",
                "model.onnx");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "artifacts",
            "models",
            "rtdetr_v2_r18vd-ONNX",
            "onnx",
            "model.onnx");
    }
}