using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace KiLupeDemo.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static int nextHotkeyId;

    private readonly HwndSource windowSource;
    private readonly IntPtr windowHandle;
    private readonly int hotkeyId;
    private bool registered;
    private bool disposed;

    public GlobalHotkeyService(Window window, ModifierKeys modifiers, Key key)
    {
        ArgumentNullException.ThrowIfNull(window);

        windowHandle = new WindowInteropHelper(window).EnsureHandle();
        windowSource = HwndSource.FromHwnd(windowHandle)
            ?? throw new InvalidOperationException("Das Fensterhandle konnte nicht verbunden werden.");
        hotkeyId = Interlocked.Increment(ref nextHotkeyId);
        windowSource.AddHook(WindowSourceHook);

        var nativeModifiers = (uint)modifiers | ModNoRepeat;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!RegisterHotKey(windowHandle, hotkeyId, nativeModifiers, virtualKey))
        {
            windowSource.RemoveHook(WindowSourceHook);
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Der globale Hotkey Strg+Alt+L ist bereits belegt oder konnte nicht registriert werden.");
        }

        registered = true;
    }

    public event EventHandler? Pressed;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (registered)
        {
            UnregisterHotKey(windowHandle, hotkeyId);
            registered = false;
        }

        windowSource.RemoveHook(WindowSourceHook);
        Pressed = null;
    }

    private IntPtr WindowSourceHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == hotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}