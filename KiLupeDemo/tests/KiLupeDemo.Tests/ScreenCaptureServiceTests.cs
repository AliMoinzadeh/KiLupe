using System.Windows.Media;
using System.Windows.Media.Imaging;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class ScreenCaptureServiceTests
{
    [Fact]
    public void CursorRegionIsClampedToScreenBounds()
    {
        var region = ScreenCaptureService.CalculateCursorRegion(
            centerX: 10,
            centerY: 20,
            screenLeft: 0,
            screenTop: 0,
            screenWidth: 1920,
            screenHeight: 1080,
            size: 320);

        Assert.Equal(new CaptureRegion(0, 0, 320, 320), region);
    }

    [Fact]
    public void ZeroSizedRegionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScreenCaptureService.CalculateCursorRegion(0, 0, 0, 0, 1920, 1080, 0));
    }

    [Fact]
    public void CaptureFrameKeepsImageAndScreenRegion()
    {
        var image = BitmapSource.Create(
            4,
            4,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[4 * 4 * 4],
            4 * 4);
        var region = new CaptureRegion(-120, 80, 320, 320);

        var frame = new ScreenCaptureFrame(image, region);

        Assert.Same(image, frame.Image);
        Assert.Equal(region, frame.Region);
    }
}