using System.Windows;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class CorrectionDockLayoutTests
{
    [Theory]
    [InlineData(DockEdge.Left)]
    [InlineData(DockEdge.Right)]
    [InlineData(DockEdge.Bottom)]
    public void SnapsCorrectionContainerToRequestedEdge(DockEdge edge)
    {
        var position = CorrectionDockLayout.Snap(
            edge,
            new Size(1000, 700),
            new Size(320, 220));

        Assert.True(position.X >= 0 && position.Y >= 0);
        Assert.True(position.X + 320 <= 1000);
        Assert.True(position.Y + 220 <= 700);
    }
}