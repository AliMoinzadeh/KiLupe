using System.Windows;
using System.Windows.Media;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public enum DetectionMarkerShape
{
    Circle,
    Rectangle,
    Spelling
}

public sealed record DetectionMarker(
    AnalysisKind Kind,
    string Label,
    double Confidence,
    Rect ScreenBounds,
    DetectionMarkerShape Shape,
    Color Color);

public static class DetectionMarkerMapper
{
    public static DetectionMarker Map(
        AnalysisResult result,
        CaptureRegion captureRegion,
        Size imageSize)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateSize(nameof(captureRegion), captureRegion.Width, captureRegion.Height);
        ValidateSize(nameof(imageSize), imageSize.Width, imageSize.Height);

        var style = GetStyle(result.Kind);
        if (result.Kind == AnalysisKind.Status || result.Bounds.IsEmpty)
        {
            return new DetectionMarker(
                result.Kind,
                result.Kind == AnalysisKind.Spelling ? result.Details : result.Label,
                result.Confidence,
                Rect.Empty,
                style.Shape,
                style.Color);
        }

        var imageBounds = Rect.Intersect(
            new Rect(0, 0, imageSize.Width, imageSize.Height),
            result.Bounds);
        if (imageBounds.IsEmpty)
        {
            return new DetectionMarker(
                result.Kind,
                result.Kind == AnalysisKind.Spelling ? result.Details : result.Label,
                result.Confidence,
                Rect.Empty,
                style.Shape,
                style.Color);
        }

        var screenBounds = new Rect(
            captureRegion.Left + imageBounds.Left * captureRegion.Width / imageSize.Width,
            captureRegion.Top + imageBounds.Top * captureRegion.Height / imageSize.Height,
            imageBounds.Width * captureRegion.Width / imageSize.Width,
            imageBounds.Height * captureRegion.Height / imageSize.Height);
        screenBounds = Rect.Intersect(
            new Rect(captureRegion.Left, captureRegion.Top, captureRegion.Width, captureRegion.Height),
            screenBounds);

        return new DetectionMarker(
            result.Kind,
            result.Kind == AnalysisKind.Spelling ? result.Details : result.Label,
            result.Confidence,
            screenBounds,
            style.Shape,
            style.Color);
    }

    private static (DetectionMarkerShape Shape, Color Color) GetStyle(AnalysisKind kind)
    {
        return kind switch
        {
            AnalysisKind.Object => (DetectionMarkerShape.Circle, Color.FromRgb(0x9A, 0xE6, 0xB4)),
            AnalysisKind.Text => (DetectionMarkerShape.Rectangle, Color.FromRgb(0x67, 0xD7, 0xE8)),
            AnalysisKind.Spelling => (DetectionMarkerShape.Spelling, Colors.Yellow),
            _ => (DetectionMarkerShape.Rectangle, Colors.Transparent)
        };
    }

    private static void ValidateSize(string parameterName, double width, double height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Die Breite muss groesser als null sein.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Die Hoehe muss groesser als null sein.");
        }
    }
}