using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class AnalysisCoordinatorTests
{
    [Fact]
    public async Task NewRequestSupersedesPreviousRequest()
    {
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callNumber = 0;
        var service = new DelegateAnalysisService(
            "test",
            true,
            async (_, cancellationToken) =>
            {
                if (Interlocked.Increment(ref callNumber) == 1)
                {
                    firstStarted.SetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return new[]
                {
                    new AnalysisResult(AnalysisKind.Object, "new", 0.9, new Rect(1, 1, 2, 2), "")
                };
            });
        using var coordinator = new AnalysisCoordinator(new[] { service });
        var image = CreateImage();

        var firstRequest = coordinator.AnalyzeAsync(image);
        await firstStarted.Task;
        var secondRequest = coordinator.AnalyzeAsync(image);

        var secondSnapshot = await secondRequest;
        var firstSnapshot = await firstRequest;

        Assert.NotNull(secondSnapshot);
        Assert.Single(secondSnapshot!.Results);
        Assert.Equal("new", secondSnapshot.Results[0].Label);
        Assert.Null(firstSnapshot);
    }

    [Fact]
    public async Task UnavailableServiceProducesStatusResult()
    {
        var service = new DelegateAnalysisService("OCR", false, (_, _) =>
            Task.FromResult<IReadOnlyList<AnalysisResult>>(Array.Empty<AnalysisResult>()));
        using var coordinator = new AnalysisCoordinator(new[] { service });

        var snapshot = await coordinator.AnalyzeAsync(CreateImage());

        Assert.NotNull(snapshot);
        Assert.Contains(snapshot!.Results, result =>
            result.Kind == AnalysisKind.Status && result.Label.Contains("nicht verfuegbar"));
    }

    [Fact]
    public async Task AvailableServiceStatusIsIncludedInSnapshotStatus()
    {
        var service = new DelegateAnalysisService("OCR", true, (_, _) =>
            Task.FromResult<IReadOnlyList<AnalysisResult>>(new[]
            {
                new AnalysisResult(AnalysisKind.Text, "Wort", 1, new Rect(0, 0, 1, 1), "")
            }));
        service.SetStatus("OCR bereit; deutsches Woerterbuch geladen, englisches Woerterbuch fehlt.");
        using var coordinator = new AnalysisCoordinator(new[] { service });

        var snapshot = await coordinator.AnalyzeAsync(CreateImage());

        Assert.NotNull(snapshot);
        Assert.Contains("englisches Woerterbuch fehlt", snapshot!.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    private static BitmapSource CreateImage()
    {
        var image = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0, 0, 0, 255 },
            4);
            image.Freeze();
            return image;
    }

    private sealed class DelegateAnalysisService : IAnalysisService
    {
        private readonly Func<BitmapSource, CancellationToken, Task<IReadOnlyList<AnalysisResult>>> handler;

        public DelegateAnalysisService(
            string name,
            bool isAvailable,
            Func<BitmapSource, CancellationToken, Task<IReadOnlyList<AnalysisResult>>> handler)
        {
            Name = name;
            IsAvailable = isAvailable;
            this.handler = handler;
                StatusText = IsAvailable ? "Bereit." : "Testdienst nicht verfuegbar.";
        }

        public string Name { get; }

        public bool IsAvailable { get; }

        public string StatusText { get; private set; }

        public void SetStatus(string status)
        {
            StatusText = status;
        }

        public Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(
            BitmapSource image,
            CancellationToken cancellationToken)
        {
            return handler(image, cancellationToken);
        }
    }
}