using System.Windows;

namespace KiLupeDemo.Services;

public enum DockEdge
{
    Left,
    Right,
    Bottom
}

public static class CorrectionDockLayout
{
    private const double Inset = 12;

    public static Point Snap(
        DockEdge edge,
        Size availableSize,
        Size panelSize)
    {
        var width = Math.Min(Math.Max(0, panelSize.Width), Math.Max(0, availableSize.Width));
        var height = Math.Min(Math.Max(0, panelSize.Height), Math.Max(0, availableSize.Height));
        var maxX = Math.Max(0, availableSize.Width - width);
        var maxY = Math.Max(0, availableSize.Height - height);
        var centeredX = Math.Clamp((availableSize.Width - width) / 2, 0, maxX);
        var centeredY = Math.Clamp((availableSize.Height - height) / 2, 0, maxY);

        return edge switch
        {
            DockEdge.Left => new Point(
                Math.Clamp(Inset, 0, maxX),
                centeredY),
            DockEdge.Right => new Point(
                Math.Clamp(availableSize.Width - width - Inset, 0, maxX),
                centeredY),
            _ => new Point(
                centeredX,
                Math.Clamp(availableSize.Height - height - Inset, 0, maxY))
        };
    }
}