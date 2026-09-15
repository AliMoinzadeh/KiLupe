using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace KiLupeDemo.Services;

public readonly record struct CaptureRegion(int Left, int Top, int Width, int Height);

public sealed record ScreenCaptureFrame(BitmapSource Image, CaptureRegion Region);

public sealed class ScreenCaptureService
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint Srccopy = 0x00CC0020;

    public BitmapSource CaptureCursorRegion(int size = 320)
    {
        return CaptureCursorFrame(size).Image;
    }

    public ScreenCaptureFrame CaptureCursorFrame(int size = 320) => CaptureCursorFrame(size, size);

    public ScreenCaptureFrame CaptureCursorFrame(int width, int height)
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Die Cursorposition konnte nicht gelesen werden.");
        }

        var screen = GetVirtualScreenRegion();
        var region = CalculateCursorRegion(
            cursorPosition.X,
            cursorPosition.Y,
            screen.Left,
            screen.Top,
            screen.Width,
            screen.Height,
            width, height);
        return new ScreenCaptureFrame(Capture(region), region);
    }

    public BitmapSource CaptureVirtualScreen()
    {
        return CaptureVirtualScreenFrame().Image;
    }

    public ScreenCaptureFrame CaptureVirtualScreenFrame()
    {
        var region = GetVirtualScreenRegion();
        return new ScreenCaptureFrame(Capture(region), region);
    }

    public BitmapSource Capture(CaptureRegion region)
    {
        ValidateRegion(region);

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Bildschirm konnte nicht geoeffnet werden.");
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previousObject = IntPtr.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Capture-Puffer konnte nicht erstellt werden.");
            }

            bitmap = CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (bitmap == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Das Capture-Bild konnte nicht erstellt werden.");
            }

            previousObject = SelectObject(memoryDc, bitmap);
            if (previousObject == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Das Capture-Bild konnte nicht aktiviert werden.");
            }

            if (!BitBlt(
                    memoryDc,
                    0,
                    0,
                    region.Width,
                    region.Height,
                    screenDc,
                    region.Left,
                    region.Top,
                    Srccopy))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Der sichtbare Bildschirmbereich konnte nicht gelesen werden.");
            }

            var image = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            if (previousObject != IntPtr.Zero && memoryDc != IntPtr.Zero)
            {
                SelectObject(memoryDc, previousObject);
            }

            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public static CaptureRegion CalculateCursorRegion(
        int centerX,
        int centerY,
        int screenLeft,
        int screenTop,
        int screenWidth,
        int screenHeight,
        int size) => CalculateCursorRegion(centerX, centerY, screenLeft, screenTop, screenWidth, screenHeight, size, size);

    public static CaptureRegion CalculateCursorRegion(
        int centerX, int centerY, int screenLeft, int screenTop,
        int screenWidth, int screenHeight, int requestedWidth, int requestedHeight)
    {
        if (screenWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenWidth));
        }

        if (screenHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenHeight));
        }

        if (requestedWidth <= 0 || requestedHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedWidth));
        }

        var width = Math.Min(requestedWidth, screenWidth);
        var height = Math.Min(requestedHeight, screenHeight);
        var left = Math.Clamp(centerX - width / 2, screenLeft, screenLeft + screenWidth - width);
        var top = Math.Clamp(centerY - height / 2, screenTop, screenTop + screenHeight - height);
        return new CaptureRegion(left, top, width, height);
    }

    public static CaptureRegion GetVirtualScreenRegion()
    {
        return new CaptureRegion(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            GetSystemMetrics(SmCxVirtualScreen),
            GetSystemMetrics(SmCyVirtualScreen));
    }

    private static void ValidateRegion(CaptureRegion region)
    {
        if (region.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Die Capture-Breite muss groesser als null sein.");
        }

        if (region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Die Capture-Hoehe muss groesser als null sein.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr deviceContext, int width, int height);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(
        IntPtr destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr source,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr deviceContext);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;

        public readonly int Y;
    }
}