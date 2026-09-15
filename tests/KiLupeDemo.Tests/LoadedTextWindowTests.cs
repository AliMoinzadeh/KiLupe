using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

[Collection("WpfApplication")]
public class LoadedTextWindowTests
{
    [Fact]
    public void CheckButtonKeepsSpellingResultsWithoutModelOrWhenModelReturnsNothingOrFails()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            App? app = null;
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
            try
            {
                File.WriteAllText(path, "Ansicht\r\nEinstellugen");
                var document = new TextDocumentLoader().Load(path);
                app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.InitializeComponent();
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                window = new MainWindow();
                ((ITextCorrectionService?)Field("textCorrectionService").GetValue(window))?.Dispose();
                Field("currentTextDocument").SetValue(window, document);
                ((TextBox)window.FindName("WorkspaceTextDocument")).Text = document.Text;
                var mode = typeof(MainWindow).GetNestedType("WorkspaceContentMode", BindingFlags.NonPublic)!;
                Method("SetWorkspaceContentMode").Invoke(window, new[] { Enum.Parse(mode, "Text") });
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.Left = -10000;
                window.Top = -10000;
                window.Show();
                window.Measure(new Size(1280, 800));
                window.Arrange(new Rect(0, 0, 1280, 800));
                window.UpdateLayout();
                foreach (var kind in new[] { "none", "empty", "failure" })
                {
                    var service = kind == "none" ? null : new ModelStub(kind == "failure");
                    Field("textCorrectionService").SetValue(window, service);
                    Field("correctionCoordinator").SetValue(window, service is null ? null : new CorrectionCoordinator(service));
                    Method("AnalyzeText_Click").Invoke(window, new object[] { window, new RoutedEventArgs() });
                    var frame = new DispatcherFrame();
                    var deadline = DateTime.UtcNow.AddSeconds(15);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                    timer.Tick += (_, _) =>
                    {
                        var statusText = ((TextBlock)window.FindName("StatusText")).Text;
                        if (!statusText.Contains("laeuft") || DateTime.UtcNow > deadline)
                            frame.Continue = false;
                    };
                    timer.Start();
                    Dispatcher.PushFrame(frame);
                    timer.Stop();
                    window.UpdateLayout();
                    var textBox = (TextBox)window.FindName("WorkspaceTextDocument");
                    var adorner = Assert.Single(AdornerLayer.GetAdornerLayer(textBox).GetAdorners(textBox).OfType<TextErrorAdorner>());
                    Assert.True(adorner.GetHighlightBounds().Count > 0, $"TextBox size={textBox.ActualWidth}x{textBox.ActualHeight}, layout={textBox.IsArrangeValid}, visible={textBox.IsVisible}, text={textBox.Text}, errors={((ListBox)window.FindName("ResultsList")).Items.Count}");
                    if (kind == "none")
                    {
                        Assert.Equal(0, textBox.SelectionLength);
                        var bitmap = new RenderTargetBitmap(1280, 800, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var preview = File.Create(Path.Combine(AppContext.BaseDirectory, "text-highlights-preview.png"));
                        encoder.Save(preview);
                    }
                    var list = (ListBox)window.FindName("ResultsList");
                    Assert.Equal("Einstellugen", Assert.IsType<AnalysisResult>(Assert.Single(list.Items.Cast<object>())).Label);
                    var status = ((TextBlock)window.FindName("StatusText")).Text;
                    Assert.Contains("1", status);
                    if (kind == "failure") Assert.Contains("Testfehler", status);
                    list.SelectedIndex = 0;
                    Assert.Equal("Einstellugen", ((TextBox)window.FindName("WorkspaceTextDocument")).SelectedText);
                }
                var editedBox = (TextBox)window.FindName("WorkspaceTextDocument");
                editedBox.Text = "Ansicht";
                window.UpdateLayout();
                Assert.Empty(((ListBox)window.FindName("ResultsList")).Items);
                Assert.Empty(AdornerLayer.GetAdornerLayer(editedBox).GetAdorners(editedBox).OfType<TextErrorAdorner>().Single().GetHighlightBounds());
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                window?.Close();
                app?.Shutdown();
                File.Delete(path);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static FieldInfo Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static MethodInfo Method(string name) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private sealed class ModelStub(bool fail) : ITextCorrectionService
    {
        public string Name => "Test";
        public string ModelId => "Test";
        public bool IsAvailable => true;
        public string StatusText => "Ready";
        public void Dispose() { }
        public Task<CorrectionSuggestion?> CorrectAsync(string text, CancellationToken token) => fail
            ? Task.FromException<CorrectionSuggestion?>(new InvalidOperationException("Testfehler"))
            : Task.FromResult<CorrectionSuggestion?>(null);
    }
}

[CollectionDefinition("WpfApplication", DisableParallelization = true)]
public class WpfApplicationCollection { }
