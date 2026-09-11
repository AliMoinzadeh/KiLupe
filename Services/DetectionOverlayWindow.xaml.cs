using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public partial class DetectionOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExToolWindow = 0x00000080;
    private const long WsExTransparent = 0x00000020;
    private const uint WdaExcludeFromCapture = 0x00000011;

    private HwndSource? windowSource;
    private DispatcherTimer? fadeTimer;
    private CaptureRegion virtualScreenRegion;
    private int renderVersion;

    public DetectionOverlayWindow()
    {
        InitializeComponent();
        virtualScreenRegion = ScreenCaptureService.GetVirtualScreenRegion();
    }

    public void ShowOnVirtualScreen()
    {
        virtualScreenRegion = ScreenCaptureService.GetVirtualScreenRegion();
        Left = virtualScreenRegion.Left;
        Top = virtualScreenRegion.Top;
        Width = virtualScreenRegion.Width;
        Height = virtualScreenRegion.Height;

        if (!IsVisible)
        {
            Show();
        }

        SetVisible(true);
    }

    public void SetVisible(bool visible)
    {
        Visibility = visible ? Visibility.Visible : Visibility.Hidden;
    }

    public void ShowResults(
        IReadOnlyList<AnalysisResult> analysisResults,
        CaptureRegion captureRegion,
        Size imageSize,
        TimeSpan visibleDuration)
    {
        ArgumentNullException.ThrowIfNull(analysisResults);
        if (visibleDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleDuration));
        }

        ClearResults();
        if (analysisResults.Count == 0 || !IsValidRegion(captureRegion))
        {
            return;
        }

        var currentVersion = renderVersion;
        foreach (var result in analysisResults.Where(result => result.Kind != AnalysisKind.Status))
        {
            DetectionMarker marker;
            try
            {
                marker = DetectionMarkerMapper.Map(result, captureRegion, imageSize);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (marker.ScreenBounds.IsEmpty)
            {
                continue;
            }

            var element = CreateMarkerElement(marker);
            PositionElement(element, marker.ScreenBounds);
            MarkerCanvas.Children.Add(element);
            if (marker.Shape == DetectionMarkerShape.Circle)
            {
                var label = CreateObjectLabel(marker);
                PositionObjectLabel(label, marker.ScreenBounds);
                MarkerCanvas.Children.Add(label);
            }
        }

        if (MarkerCanvas.Children.Count == 0)
        {
            return;
        }

        MarkerCanvas.BeginAnimation(UIElement.OpacityProperty, null);
        MarkerCanvas.Opacity = 1;
        ScheduleFade(currentVersion, visibleDuration);
    }

    public void ClearResults()
    {
        renderVersion++;
        StopFadeTimer();
        MarkerCanvas.BeginAnimation(UIElement.OpacityProperty, null);
        MarkerCanvas.Opacity = 1;
        MarkerCanvas.Children.Clear();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        windowSource?.AddHook(WindowSourceHook);

        if (windowSource is null)
        {
            return;
        }

        var currentStyle = GetWindowLongPtr(windowSource.Handle, GwlExStyle).ToInt64();
        _ = SetWindowLongPtr(
            windowSource.Handle,
            GwlExStyle,
            new IntPtr(currentStyle | WsExNoActivate | WsExToolWindow | WsExTransparent));
        _ = SetWindowDisplayAffinity(windowSource.Handle, WdaExcludeFromCapture);
    }

    protected override void OnClosed(EventArgs e)
    {
        ClearResults();
        if (windowSource is not null)
        {
            windowSource.RemoveHook(WindowSourceHook);
        }

        base.OnClosed(e);
    }

    private UIElement CreateMarkerElement(DetectionMarker marker)
    {
        var brush = new SolidColorBrush(marker.Color);
        brush.Freeze();

        return marker.Shape switch
        {
            DetectionMarkerShape.Circle => CreateCircle(marker, brush),
            DetectionMarkerShape.Spelling => CreateSpellingMarker(marker, brush),
            _ => CreateTextMarker(marker, brush)
        };
    }

    private static Ellipse CreateCircle(DetectionMarker marker, Brush brush)
    {
        var diameter = GetCircleDiameter(marker.ScreenBounds);
        return new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Margin = new Thickness(-diameter / 2, -diameter / 2, 0, 0),
            Fill = new SolidColorBrush(Color.FromArgb(28, marker.Color.R, marker.Color.G, marker.Color.B)),
            Stroke = brush,
            StrokeThickness = 3,
            IsHitTestVisible = false,
            ToolTip = marker.Label
        };
    }

    private static Border CreateObjectLabel(DetectionMarker marker)
    {
        var brush = new SolidColorBrush(marker.Color);
        brush.Freeze();

        return new Border
        {
            MinWidth = 42,
            MaxWidth = 260,
            Padding = new Thickness(4, 2, 4, 2),
            Background = new SolidColorBrush(Color.FromArgb(190, 16, 22, 20)),
            Child = CreateLabel(marker.Label, brush),
            IsHitTestVisible = false
        };
    }

    private static Border CreateTextMarker(DetectionMarker marker, Brush brush)
    {
        return new Border
        {
            MinWidth = 42,
            MinHeight = 24,
            BorderBrush = brush,
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(34, marker.Color.R, marker.Color.G, marker.Color.B)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2, 4, 2),
            Child = CreateLabel(marker.Label, brush),
            IsHitTestVisible = false
        };
    }

    private static Border CreateSpellingMarker(DetectionMarker marker, Brush brush)
    {
        return new Border
        {
            MinWidth = 42,
            MinHeight = 24,
            BorderBrush = brush,
            BorderThickness = new Thickness(0, 0, 0, 3),
            Background = new SolidColorBrush(Color.FromArgb(28, marker.Color.R, marker.Color.G, marker.Color.B)),
            Padding = new Thickness(4, 2, 4, 2),
            Child = CreateLabel(marker.Label, brush),
            IsHitTestVisible = false
        };
    }

    private static TextBlock CreateLabel(string label, Brush brush)
    {
        return new TextBlock
        {
            Text = label,
            Foreground = brush,
            Background = new SolidColorBrush(Color.FromArgb(190, 16, 22, 20)),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 260
        };
    }

    private void PositionElement(UIElement element, Rect screenBounds)
    {
        var localLeft = screenBounds.Left - virtualScreenRegion.Left;
        var localTop = screenBounds.Top - virtualScreenRegion.Top;
        var scaleX = Width / virtualScreenRegion.Width;
        var scaleY = Height / virtualScreenRegion.Height;

        if (element is Ellipse ellipse)
        {
            Canvas.SetLeft(element, (localLeft + screenBounds.Width / 2) * scaleX);
            Canvas.SetTop(element, (localTop + screenBounds.Height / 2) * scaleY);
            ellipse.Width *= scaleX;
            ellipse.Height *= scaleY;
            ellipse.Margin = new Thickness(-ellipse.Width / 2, -ellipse.Height / 2, 0, 0);
            return;
        }

        Canvas.SetLeft(element, localLeft * scaleX);
        Canvas.SetTop(element, localTop * scaleY);
        element.SetValue(FrameworkElement.WidthProperty, Math.Max(42, screenBounds.Width * scaleX));
        element.SetValue(FrameworkElement.HeightProperty, Math.Max(24, screenBounds.Height * scaleY));
    }

    private void PositionObjectLabel(FrameworkElement label, Rect screenBounds)
    {
        var localLeft = screenBounds.Left - virtualScreenRegion.Left;
        var localTop = screenBounds.Top - virtualScreenRegion.Top;
        var scaleX = Width / virtualScreenRegion.Width;
        var scaleY = Height / virtualScreenRegion.Height;
        var diameter = GetCircleDiameter(screenBounds);
        var centerX = (localLeft + screenBounds.Width / 2) * scaleX;
        var centerY = (localTop + screenBounds.Height / 2) * scaleY;
        var circleLeft = centerX - diameter * scaleX / 2;
        var circleBottom = centerY + diameter * scaleY / 2;
        var labelTop = circleBottom + 4;

        if (labelTop + 28 > Height)
        {
            labelTop = Math.Max(0, centerY - diameter * scaleY / 2 - 30);
        }

        Canvas.SetLeft(label, Math.Max(0, circleLeft));
        Canvas.SetTop(label, Math.Max(0, labelTop));
    }

    private static double GetCircleDiameter(Rect screenBounds)
    {
        return Math.Max(36, Math.Max(screenBounds.Width, screenBounds.Height) + 14);
    }

    private void ScheduleFade(int version, TimeSpan visibleDuration)
    {
        StopFadeTimer();
        var timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = visibleDuration == TimeSpan.Zero
                ? TimeSpan.FromMilliseconds(1)
                : visibleDuration
        };
        fadeTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!ReferenceEquals(fadeTimer, timer) || version != renderVersion)
            {
                return;
            }

            fadeTimer = null;
            var animation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500))
            {
                FillBehavior = FillBehavior.Stop
            };
            animation.Completed += (_, _) =>
            {
                if (version != renderVersion)
                {
                    return;
                }

                MarkerCanvas.BeginAnimation(UIElement.OpacityProperty, null);
                MarkerCanvas.Opacity = 1;
                MarkerCanvas.Children.Clear();
            };
            MarkerCanvas.BeginAnimation(UIElement.OpacityProperty, animation);
        };
        timer.Start();
    }

    private void StopFadeTimer()
    {
        fadeTimer?.Stop();
        fadeTimer = null;
    }

    private static bool IsValidRegion(CaptureRegion region)
    {
        return region.Width > 0 && region.Height > 0;
    }

    private IntPtr WindowSourceHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmNcHitTest)
        {
            handled = true;
            return new IntPtr(HtTransparent);
        }

        return IntPtr.Zero;
    }

    private static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : new IntPtr(GetWindowLong32(windowHandle, index));
    }

    private static IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr value)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, value)
            : new IntPtr(SetWindowLong32(windowHandle, index, value.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr windowHandle, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr windowHandle, uint affinity);
}