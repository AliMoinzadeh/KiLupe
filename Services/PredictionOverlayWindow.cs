using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace KiLupeDemo.Services;

public sealed class PredictionOverlayWindow : Window
{
    private readonly TextBlock text = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 380, Foreground = Brushes.SlateGray };
    private readonly Border border;
    public PredictionOverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        border = new Border { Child = text, CornerRadius = new CornerRadius(5) };
        Content = border;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(hwnd, -20, new IntPtr(GetWindowLongPtr(hwnd, -20).ToInt64() | 0x08000000 | 0x20 | 0x80));
        };
    }

    public void ShowSuggestion(CaretSnapshot snapshot, string suggestion)
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var source = HwndSource.FromHwnd(hwnd)!;
        var transform = source.CompositionTarget.TransformFromDevice;
        text.Text = suggestion;
        text.FontSize = Math.Clamp(snapshot.Bounds.Height * transform.M22 * 0.75, 10, 24);
        text.TextWrapping = TextWrapping.NoWrap;
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var availableWidth = snapshot.EditorBounds.IsEmpty ? 380
            : Math.Max(0, (snapshot.EditorBounds.Right - snapshot.Bounds.Right - 8) * transform.M11);
        var inline = snapshot.IsLineEnd && text.DesiredSize.Width + 4 <= availableWidth
            && text.DesiredSize.Width < text.MaxWidth;
        text.TextWrapping = inline ? TextWrapping.NoWrap : TextWrapping.Wrap;
        border.Padding = inline ? new Thickness(2, 0, 2, 0) : new Thickness(10, 7, 10, 7);
        border.Background = inline ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(242, 246, 243));
        var point = transform.Transform(new Point(snapshot.Bounds.Right + 2, inline ? snapshot.Bounds.Top : snapshot.Bounds.Bottom + 4));
        Left = point.X;
        Top = point.Y;
        if (!IsVisible) Show();
        UpdateLayout();
        var monitor = MonitorFromPoint(new NativePoint { X = (int)snapshot.Bounds.X, Y = (int)snapshot.Bounds.Y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(monitor, ref info))
        {
            var topLeft = transform.Transform(new Point(info.Work.Left, info.Work.Top));
            var bottomRight = transform.Transform(new Point(info.Work.Right, info.Work.Bottom));
            point.X = Math.Clamp(point.X, topLeft.X, Math.Max(topLeft.X, bottomRight.X - ActualWidth));
            point.Y = Math.Clamp(point.Y, topLeft.Y, Math.Max(topLeft.Y, bottomRight.Y - ActualHeight));
        }
        Left = point.X;
        Top = point.Y;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}
