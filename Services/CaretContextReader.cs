using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
namespace KiLupeDemo.Services;

public sealed class CaretContextReader
{
    public string Status { get; private set; } = "Bereit.";
    public CaretSnapshot? Read(IReadOnlySet<string> excludedProcesses)
    {
        try
        {
            var window = GetForegroundWindow();
            GetWindowThreadProcessId(window, out var processId);
            if (window == IntPtr.Zero || processId == Environment.ProcessId) return Unavailable("Zum Schreiben in ein anderes Programm wechseln.");
            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;
            if (excludedProcesses.Contains(processName)) return Unavailable("Vorhersage fuer dieses Programm ausgeschlossen.");
            var focused = AutomationElement.FocusedElement;
            if (focused is null || focused.Current.ProcessId != processId || focused.Current.IsPassword)
                return Unavailable("Kein zugaengliches Textfeld.");
            var id = string.Join(".", focused.GetRuntimeId());
            var element = focused;
            for (var depth = 0; element is not null && depth < 6; depth++)
            {
                if (element.Current.IsPassword || element.Current.ProcessId != processId) return Unavailable("Geschuetztes Textfeld.");
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out var value))
                {
                    var pattern = (TextPattern)value;
                    var selection = pattern.GetSelection();
                    if (selection.Length != 1 || selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start, selection[0], TextPatternRangeEndpoint.End) != 0)
                        return Unavailable("Vorhersage pausiert bei markiertem Text.");
                    var caret = selection[0];
                    if (caret.GetAttributeValue(TextPattern.IsReadOnlyAttribute) is not bool readOnly || readOnly)
                        return Unavailable("Dieses Feld stellt keinen beschreibbaren Text bereit.");
                    var beforeRange = pattern.DocumentRange.Clone();
                    beforeRange.MoveEndpointByRange(TextPatternRangeEndpoint.End, caret, TextPatternRangeEndpoint.Start);
                    var afterRange = pattern.DocumentRange.Clone();
                    afterRange.MoveEndpointByRange(TextPatternRangeEndpoint.Start, caret, TextPatternRangeEndpoint.End);
                    var before = beforeRange.GetText(20001);
                    var after = afterRange.GetText(20001);
                    if (before.Length + after.Length > 20000 || string.IsNullOrWhiteSpace(before))
                        return Unavailable("Textfeld leer oder laenger als 20.000 Zeichen.");
                    var bounds = GetCaretBounds(window, caret);
                    if (bounds.IsEmpty || bounds.Height <= 0 || !element.Current.BoundingRectangle.IntersectsWith(bounds))
                        return Unavailable("Dieses Programm stellt keine sichtbare Cursorposition bereit.");
                    if (GetForegroundWindow() != window || !Automation.Compare(focused, AutomationElement.FocusedElement))
                        return Unavailable("Textfokus hat sich geaendert.");
                    Status = "Bereit fuer einen Vorschlag.";
                    return new(window, id, processName, before, after, bounds) { EditorBounds = element.Current.BoundingRectangle };
                }
                if (element.Current.ControlType == ControlType.Window) break;
                element = TreeWalker.ControlViewWalker.GetParent(element);
            }
            return Unavailable("Dieses Programm stellt den Textcursor nicht bereit.");
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException
            or COMException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            return Unavailable("Textcursor momentan nicht zugaenglich.");
        }
    }

    private CaretSnapshot? Unavailable(string status) { Status = status; return null; }

    private static Rect GetCaretBounds(IntPtr window, TextPatternRange caret)
    {
        var rects = caret.GetBoundingRectangles();
        if (rects.Length == 1 && rects[0].Height > 0) return rects[0];
        var thread = GetWindowThreadProcessId(window, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (GetGUIThreadInfo(thread, ref info) && info.Caret != IntPtr.Zero)
        {
            var point = new NativePoint { X = info.CaretRect.Left, Y = info.CaretRect.Top };
            if (ClientToScreen(info.Caret, ref point))
                return new Rect(point.X, point.Y, Math.Max(1, info.CaretRect.Right - info.CaretRect.Left), Math.Max(1, info.CaretRect.Bottom - info.CaretRect.Top));
        }
        // Guessing from adjacent glyphs is unsafe at line wraps and in bidirectional text.
        return Rect.Empty;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo
    {
        public int Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public NativeRect CaretRect;
    }
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
}
