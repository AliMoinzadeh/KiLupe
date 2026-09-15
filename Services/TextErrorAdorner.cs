using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public sealed class TextErrorAdorner : Adorner
{
    private readonly TextBox textBox;
    private IReadOnlyList<AnalysisResult> errors = Array.Empty<AnalysisResult>();
    private readonly Brush markerBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 0));

    public TextErrorAdorner(TextBox textBox) : base(textBox)
    {
        this.textBox = textBox;
        IsHitTestVisible = false;
        markerBrush.Freeze();
        textBox.SizeChanged += (_, _) => InvalidateVisual();
        textBox.TextChanged += (_, _) => InvalidateVisual();
        textBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => InvalidateVisual()));
    }

    public void SetResults(IEnumerable<AnalysisResult> results)
    {
        errors = results.Where(result => result.Kind == AnalysisKind.Spelling).ToArray();
        InvalidateVisual();
    }

    public IReadOnlyList<Rect> GetHighlightBounds()
    {
        var rectangles = new List<Rect>();
        if (!textBox.IsArrangeValid || textBox.ActualWidth <= 0 || textBox.ActualHeight <= 0) return rectangles;
        var viewport = new Rect(0, 0, textBox.ActualWidth, textBox.ActualHeight);
        var presenter = FindVisualChild<ScrollContentPresenter>(textBox);
        if (presenter is not null)
            viewport = new Rect(presenter.TranslatePoint(new Point(), textBox), new Size(presenter.ActualWidth, presenter.ActualHeight));

        var text = textBox.Text;
        var firstLine = textBox.GetFirstVisibleLineIndex();
        var lastLine = textBox.GetLastVisibleLineIndex();
        if (firstLine < 0 || lastLine < 0) return rectangles;
        var visibleStart = textBox.GetCharacterIndexFromLineIndex(firstLine);
        var visibleEnd = textBox.GetCharacterIndexFromLineIndex(lastLine) + textBox.GetLineLength(lastLine);
        foreach (var error in errors)
        {
            if (error.Bounds.IsEmpty || error.Bounds.X < 0 || error.Bounds.Right > text.Length) continue;
            var start = Math.Max((int)error.Bounds.X, visibleStart);
            var end = Math.Min((int)error.Bounds.Right, visibleEnd);
            var segment = Rect.Empty;
            for (var index = start; index < end; index++)
            {
                if (text[index] is '\r' or '\n') continue;
                var leading = textBox.GetRectFromCharacterIndex(index, trailingEdge: false);
                var trailing = textBox.GetRectFromCharacterIndex(index, trailingEdge: true);
                if (leading.IsEmpty || trailing.IsEmpty || trailing.X <= leading.X) continue;
                var glyph = new Rect(leading.X, leading.Y, trailing.X - leading.X, leading.Height);
                glyph.Intersect(viewport);
                if (glyph.IsEmpty || glyph.Width <= 0 || glyph.Height <= 0) continue;
                if (!segment.IsEmpty && Math.Abs(segment.Top - glyph.Top) < .5 && glyph.Left <= segment.Right + 1)
                    segment.Union(glyph);
                else
                {
                    if (!segment.IsEmpty) rectangles.Add(segment);
                    segment = glyph;
                }
            }
            if (!segment.IsEmpty) rectangles.Add(segment);
        }
        return rectangles;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        foreach (var rectangle in GetHighlightBounds())
            drawingContext.DrawRectangle(markerBrush, null, rectangle);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var descendant = FindVisualChild<T>(child);
            if (descendant is not null) return descendant;
        }
        return null;
    }
}
