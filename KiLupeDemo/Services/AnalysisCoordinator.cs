using System.Windows;
using System.Windows.Media.Imaging;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public sealed class AnalysisCoordinator : IDisposable
{
    private readonly IReadOnlyList<IAnalysisService> services;
    private readonly object syncRoot = new();
    private CancellationTokenSource? activeCancellation;
    private long nextRequestId;
    private bool disposed;

    public AnalysisCoordinator(IEnumerable<IAnalysisService> services)
    {
        this.services = services.ToArray();
    }

    public Task<AnalysisSnapshot?> AnalyzeAsync(
        BitmapSource image,
        CancellationToken cancellationToken = default)
    {
        return AnalyzeAsync(image, static _ => true, cancellationToken);
    }

    public async Task<AnalysisSnapshot?> AnalyzeAsync(
        BitmapSource image,
        Func<IAnalysisService, bool> serviceFilter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(serviceFilter);
        var sourceSize = new Size(image.PixelWidth, image.PixelHeight);

        CancellationTokenSource requestCancellation;
        long requestId;

        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeCancellation?.Cancel();
            requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            activeCancellation = requestCancellation;
            requestId = ++nextRequestId;
        }

        try
        {
            var results = new List<AnalysisResult>();
            var serviceStatuses = new List<string>();

            foreach (var service in services.Where(serviceFilter))
            {
                requestCancellation.Token.ThrowIfCancellationRequested();

                if (!service.IsAvailable)
                {
                    AddServiceStatus(serviceStatuses, service.StatusText);
                    results.Add(new AnalysisResult(
                        AnalysisKind.Status,
                        $"{service.Name} nicht verfuegbar",
                        1,
                        Rect.Empty,
                        service.StatusText));
                    continue;
                }

                AddServiceStatus(serviceStatuses, service.StatusText);
                try
                {
                    var serviceResults = await Task.Run(
                            () => service.AnalyzeAsync(image, requestCancellation.Token),
                            requestCancellation.Token)
                        .ConfigureAwait(false);
                    results.AddRange(serviceResults);
                }
                catch (OperationCanceledException) when (requestCancellation.Token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    results.Add(new AnalysisResult(
                        AnalysisKind.Status,
                        $"{service.Name} fehlgeschlagen",
                        1,
                        Rect.Empty,
                        exception.Message));
                }
            }

            lock (syncRoot)
            {
                if (requestId != nextRequestId || requestCancellation.IsCancellationRequested)
                {
                    return null;
                }
            }

            var statusText = results.Count == 0
                ? "Keine Treffer gefunden."
                : $"{results.Count} Ergebnis(se) gefunden.";
            if (serviceStatuses.Count > 0)
            {
                statusText = $"{statusText} {string.Join(" | ", serviceStatuses)}";
            }

            return new AnalysisSnapshot(
                requestId,
                sourceSize,
                results,
                statusText);
        }
        catch (OperationCanceledException) when (requestCancellation.Token.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(activeCancellation, requestCancellation))
                {
                    activeCancellation = null;
                }
            }

            requestCancellation.Dispose();
        }
    }

    private static void AddServiceStatus(ICollection<string> serviceStatuses, string statusText)
    {
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            serviceStatuses.Add(statusText);
        }
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            activeCancellation?.Cancel();
            activeCancellation = null;
            foreach (var disposable in services.OfType<IDisposable>())
            {
                disposable.Dispose();
            }
        }
    }
}