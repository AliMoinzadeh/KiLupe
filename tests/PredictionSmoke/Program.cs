using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KiLupeDemo.Services;
using KiLupeDemo.Models;

public static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--fixture"))
        {
            var box = new TextBox { Text = "Ich freue mich auf ", FontSize = 20, AcceptsReturn = true, Margin = new Thickness(15) };
            var window = new Window { Title = "KiLupe prediction smoke fixture", Width = 600, Height = 200, Content = box };
            window.Activated += (_, _) => window.Dispatcher.BeginInvoke(new Action(() => { box.Focus(); System.Windows.Input.Keyboard.Focus(box); box.CaretIndex = box.Text.Length; }));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            timer.Tick += (_, _) => window.Close(); timer.Start();
            new Application { ShutdownMode = ShutdownMode.OnMainWindowClose }.Run(window);
            return 0;
        }
        if (args.Contains("--quality")) return PredictionQualityCheck.Run();
        if (args.Contains("--model"))
        {
            var path = ModelCatalog.Create().TextCorrectionModels.Single(item => item.Id == TextCorrectionModelKind.LocalLlm).ModelPath!;
            using var model = new LocalLlamaTextCorrectionService(path);
            using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var watch = Stopwatch.StartNew();
            const string before = "Vielen Dank für Ihre Nachricht. Ich werde ";
            var response = model.PredictAsync(before, "", cancel.Token).GetAwaiter().GetResult();
            var suggestion = PredictionText.GetInsertion(response, before);
            Console.WriteLine($"Completion: [{suggestion}] ({watch.Elapsed.TotalSeconds:F1}s)");
            return string.IsNullOrWhiteSpace(suggestion) ? 1 : 0;
        }
        using var fixture = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--fixture") { UseShellExecute = false })!;
        try
        {
            Thread.Sleep(1800);
            fixture.Refresh();
            Console.WriteLine($"Fixture pid={fixture.Id}; hwnd={fixture.MainWindowHandle}; exited={fixture.HasExited}");
            Console.WriteLine($"Foreground request={SetForegroundWindow(fixture.MainWindowHandle)}; foreground={CaretContextReader.GetForegroundWindow()}");
            Thread.Sleep(300);
            var focused = Task.Run(() => System.Windows.Automation.AutomationElement.FocusedElement).GetAwaiter().GetResult();
            if (focused?.Current.ProcessId == fixture.Id)
                Console.WriteLine($"Fixture focus: {focused.Current.ControlType.ProgrammaticName}; {string.Join(",", focused.GetSupportedPatterns().Select(p => p.ProgrammaticName))}");
            else { Console.WriteLine("Fixture did not receive foreground focus."); return 2; }
            var reader = new CaretContextReader();
            var snapshot = Task.Run(() => reader.Read(new HashSet<string>(StringComparer.OrdinalIgnoreCase))).GetAwaiter().GetResult();
            Console.WriteLine($"Capture: {reader.Status}; before=[{snapshot?.Before}]; bounds={snapshot?.Bounds}");
            if (snapshot?.Before != "Ich freue mich auf ") return 1;
            var overlay = new PredictionOverlayWindow();
            overlay.ShowSuggestion(snapshot, "unser Treffen.");
            var current = Task.Run(() => reader.Read(new HashSet<string>(StringComparer.OrdinalIgnoreCase))).GetAwaiter().GetResult();
            if (current != snapshot) { Console.WriteLine("Overlay changed focus/context"); return 1; }
            overlay.Hide();
            if (!PredictionTextInserter.Insert(snapshot.Window, "unser Treffen.")) return 1;
            Thread.Sleep(200);
            current = Task.Run(() => reader.Read(new HashSet<string>(StringComparer.OrdinalIgnoreCase))).GetAwaiter().GetResult();
            Console.WriteLine($"After insert: [{current?.Before}]");
            overlay.Close();
            return current?.Before == "Ich freue mich auf unser Treffen." ? 0 : 1;
        }
        finally { if (!fixture.HasExited) { fixture.CloseMainWindow(); fixture.WaitForExit(3000); } }
    }
}
