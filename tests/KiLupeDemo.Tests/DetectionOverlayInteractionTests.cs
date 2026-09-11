using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class DetectionOverlayInteractionTests
{
    private static readonly AnalysisResult Error = new(AnalysisKind.Spelling, "Huas", 1, new Rect(10, 20, 40, 12), "Haus");

    [Fact]
    public void TextShowsOnlyHighlightUntilHovered()
    {
        OnUiThread(() =>
        {
            var window = new DetectionOverlayWindow { Width = 1920, Height = 1080 };
            try
            {
                window.ShowResults(new[] { Error }, new CaptureRegion(0, 0, 100, 100), new Size(100, 100), TimeSpan.FromMilliseconds(20));
                var canvas = (Canvas)window.FindName("MarkerCanvas");
                Assert.Single(canvas.Children.Cast<UIElement>());
                UpdatePointer(window, new Point(15, 25));
                Assert.Equal(2, canvas.Children.Count);
                Assert.Equal("Haus", ((TextBlock)((Border)canvas.Children[1]).Child).Text);
                UpdatePointer(window, new Point(80, 80));
                Assert.Single(canvas.Children.Cast<UIElement>());
                window.ClearResults();
                UpdatePointer(window, new Point(15, 25));
                Assert.Empty(canvas.Children.Cast<UIElement>());
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void TextRemainsAfterOldFadeDeadline()
    {
        OnUiThread(() =>
        {
            var window = new DetectionOverlayWindow { Width = 1920, Height = 1080 };
            try
            {
                window.ShowResults(new[] { Error }, new CaptureRegion(0, 0, 100, 100), new Size(100, 100), TimeSpan.FromMilliseconds(20));
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start();
                Dispatcher.PushFrame(frame);
                Assert.Single(((Canvas)window.FindName("MarkerCanvas")).Children.Cast<UIElement>());
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RefreshReplacesOldHoverTargetsAndShowsOnlyOneSuggestion()
    {
        OnUiThread(() =>
        {
            var window = new DetectionOverlayWindow { Width = 1920, Height = 1080 };
            try
            {
                var second = Error with { Bounds = new Rect(60, 20, 30, 12), Details = "Maus" };
                var region = new CaptureRegion(0, 0, 100, 100);
                var size = new Size(100, 100);
                window.ShowResults(new[] { Error, second }, region, size, TimeSpan.Zero);
                var canvas = (Canvas)window.FindName("MarkerCanvas");
                UpdatePointer(window, new Point(15, 25));
                Assert.Equal(3, canvas.Children.Count);
                UpdatePointer(window, new Point(65, 25));
                Assert.Equal(3, canvas.Children.Count);
                Assert.Equal("Maus", ((TextBlock)((Border)canvas.Children[2]).Child).Text);
                window.ShowResults(new[] { second }, region, size, TimeSpan.Zero);
                UpdatePointer(window, new Point(15, 25));
                Assert.Single(canvas.Children.Cast<UIElement>());
                window.ShowResults(Array.Empty<AnalysisResult>(), region, size, TimeSpan.Zero);
                Assert.Empty(canvas.Children.Cast<UIElement>());
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ObjectsScheduleFadeButTextDoesNot()
    {
        OnUiThread(() =>
        {
            var window = new DetectionOverlayWindow { Width = 1920, Height = 1080 };
            try
            {
                var timerField = typeof(DetectionOverlayWindow).GetField("fadeTimer", BindingFlags.NonPublic | BindingFlags.Instance)!;
                window.ShowResults(new[] { Error with { Kind = AnalysisKind.Object } }, new CaptureRegion(0, 0, 100, 100), new Size(100, 100), TimeSpan.FromSeconds(2));
                var timer = Assert.IsType<DispatcherTimer>(timerField.GetValue(window));
                Assert.True(timer.IsEnabled);
                Assert.Equal(TimeSpan.FromSeconds(2), timer.Interval);
                window.ShowResults(new[] { Error }, new CaptureRegion(0, 0, 100, 100), new Size(100, 100), TimeSpan.FromSeconds(2));
                Assert.False(timer.IsEnabled);
                Assert.Null(timerField.GetValue(window));
            }
            finally { window.Close(); }
        });
    }
    [Fact]
    public void UpdatingCorrectionContentDoesNotOpenPopup()
    {
        OnUiThread(() =>
        {
            var window = new CorrectionOverlayWindow { Left = -32000, Top = -32000 };
            try
            {
                window.SetState(CorrectionPresentationState.From(new[]
                {
                    new CorrectionSuggestion("Mein Text", "Mein korrigierter Text", "test", "CPU")
                }));
                Assert.False(window.IsVisible);
                Assert.Equal("Mein korrigierter Text", ((TextBox)window.FindName("SuggestionTextBox")).Text);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ManualSelectionRequiresExplicitValidText()
    {
        OnUiThread(() =>
        {
            var window = new SelectedTextDialog("Keine automatische Auswahl.");
            try
            {
                var layout = (DockPanel)window.Content;
                var input = Assert.Single(layout.Children.OfType<TextBox>());
                var actions = Assert.Single(layout.Children.OfType<StackPanel>());
                var submit = actions.Children.OfType<Button>().Single(button => !button.IsCancel);
                Assert.Equal(string.Empty, window.SelectedText);
                Assert.False(submit.IsEnabled);
                input.Text = "Mein Absatz.\r\nMit Zusammenhang.";
                Assert.True(submit.IsEnabled);
                Assert.Equal("Mein Absatz.\r\nMit Zusammenhang.", window.SelectedText);
                input.Text = new string('x', 20001);
                Assert.False(submit.IsEnabled);
                input.Text = " ";
                Assert.False(submit.IsEnabled);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PresentationModeIncludesOverlayExceptDuringOwnCapture()
    {
        OnUiThread(() =>
        {
            var window = new DetectionOverlayWindow { Width = 100, Height = 100 };
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
                AssertAffinity(handle, 0x11);
                window.SetPresentationMode(true);
                AssertAffinity(handle, 0);
                var result = window.CaptureWithoutMarkersAsync(() =>
                {
                    AssertAffinity(handle, 0x11);
                    return Task.FromResult(123);
                }).GetAwaiter().GetResult();
                Assert.Equal(123, result);
                AssertAffinity(handle, 0);
                Assert.Throws<OperationCanceledException>(() => window.CaptureWithoutMarkersAsync<int>(() =>
                    Task.FromException<int>(new OperationCanceledException())).GetAwaiter().GetResult());
                AssertAffinity(handle, 0);
                window.CaptureWithoutMarkersAsync(() =>
                {
                    window.SetPresentationMode(false);
                    AssertAffinity(handle, 0x11);
                    return Task.FromResult(0);
                }).GetAwaiter().GetResult();
                AssertAffinity(handle, 0x11);
            }
            finally { window.Close(); }
        });
    }

    private static void AssertAffinity(IntPtr handle, uint expected)
    {
        Assert.True(GetWindowDisplayAffinity(handle, out var actual));
        Assert.Equal(expected, actual);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);

    private static void UpdatePointer(DetectionOverlayWindow window, Point point)
    {
        var method = typeof(DetectionOverlayWindow).GetMethod("UpdateHoveredMarker", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method.Invoke(window, new object[] { point });
    }

    private static void OnUiThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
