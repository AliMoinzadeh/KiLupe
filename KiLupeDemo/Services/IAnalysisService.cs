using System.Windows.Media.Imaging;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public interface IAnalysisService
{
    string Name { get; }

    bool IsAvailable { get; }

    string StatusText { get; }

    Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(
        BitmapSource image,
        CancellationToken cancellationToken);
}