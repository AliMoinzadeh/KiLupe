using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public static class ImageResultMarkerFactory
{
    public static IEnumerable<AnalysisResult> VisibleResults(IEnumerable<AnalysisResult> results)
    {
        return results.Where(result => result.Kind is AnalysisKind.Spelling or AnalysisKind.Object
            && !result.Bounds.IsEmpty);
    }
    public static Border Create(AnalysisResult result, Rect bounds, bool selected)
    {
        if (result.Kind == AnalysisKind.Spelling)
        {
            return new Border
            {
                Width = Math.Max(1, bounds.Width),
                Height = Math.Max(1, bounds.Height),
                Background = new SolidColorBrush(Color.FromArgb(100, 255, 255, 0)),
                IsHitTestVisible = false
            };
        }
        var accent = selected ? Colors.Yellow : Colors.LightGreen;
        return new Border
        {
            Width = Math.Max(42, bounds.Width),
            Height = Math.Max(26, bounds.Height),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(selected ? 4 : 2),
            Background = new SolidColorBrush(Color.FromArgb(42, accent.R, accent.G, accent.B)),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = result.Label,
                Foreground = new SolidColorBrush(accent),
                Background = new SolidColorBrush(Color.FromArgb(190, 16, 22, 20)),
                Padding = new Thickness(4, 2, 4, 2),
                VerticalAlignment = VerticalAlignment.Top
            }
        };
    }
}
