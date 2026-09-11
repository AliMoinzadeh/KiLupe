using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace KiLupeDemo.Services;

public partial class OrbOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExToolWindow = 0x00000080;

    private HwndSource? windowSource;

    public OrbOverlayWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? PauseRequested;

    public event EventHandler? ModeRequested;

    public event EventHandler? ResultsRequested;

    public event EventHandler? ClearRequested;

    public event EventHandler? CorrectionRequested;

    public event EventHandler? ExitRequested;

    public void ShowAtTaskbar()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Bottom - Height - 12;
        if (!IsVisible)
        {
            Show();
        }
    }

    public void SetPaused(bool paused)
    {
        PauseButton.Content = paused ? ">" : "II";
        PauseButton.ToolTip = paused ? "Fortsetzen" : "Pause";
    }

    public void SetMode(string mode)
    {
        ModeText.Text = mode;
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    public void SetCorrectionAvailable(bool available)
    {
        CorrectionButton.IsEnabled = available;
        CorrectionButton.ToolTip = available
            ? "Auswahl pruefen: zuerst einen zusammenhaengenden Text im Programm markieren"
            : "Textkorrekturmodell ist nicht verfuegbar";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        windowSource?.AddHook(WindowSourceHook);

        if (windowSource is not null)
        {
            var currentStyle = GetWindowLongPtr(windowSource.Handle, GwlExStyle).ToInt64();
            _ = SetWindowLongPtr(
                windowSource.Handle,
                GwlExStyle,
                new IntPtr(currentStyle | WsExNoActivate | WsExToolWindow));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (windowSource is not null)
        {
            windowSource.RemoveHook(WindowSourceHook);
        }

        base.OnClosed(e);
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        PauseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        ModeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResultsButton_Click(object sender, RoutedEventArgs e)
    {
        ResultsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CorrectionButton_Click(object sender, RoutedEventArgs e)
    {
        CorrectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private IntPtr WindowSourceHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmNcHitTest)
        {
            return IntPtr.Zero;
        }

        var packedPoint = lParam.ToInt64();
        var screenPoint = new Point(
            (short)(packedPoint & 0xFFFF),
            (short)((packedPoint >> 16) & 0xFFFF));
        var localPoint = PointFromScreen(screenPoint);
        var hitTarget = VisualTreeHelper.HitTest(this, localPoint)?.VisualHit;
        if (!IsOrbButton(hitTarget))
        {
            handled = true;
            return new IntPtr(HtTransparent);
        }

        return IntPtr.Zero;
    }

    private static bool IsOrbButton(DependencyObject? target)
    {
        while (target is not null)
        {
            if (target is Button)
            {
                return true;
            }

            target = target is Visual
                ? VisualTreeHelper.GetParent(target)
                : null;
        }

        return false;
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
}