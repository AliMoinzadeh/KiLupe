using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Microsoft.Win32;

namespace KiLupeDemo;

public partial class MainWindow : Window
{
    private const double MagnifierDiameter = 168;
    private const double Magnification = 2.5;
    private const double DefaultCorrectionPaneHeight = 220;
    private const double MinimumCorrectionPaneHeight = 150;

    private readonly ObservableCollection<AnalysisResult> results = new();
    private AnalysisCoordinator analysisCoordinator;
    private ITextCorrectionService? textCorrectionService;
    private CorrectionCoordinator? correctionCoordinator;
    private AnalysisConfiguration analysisConfiguration;
    private CorrectionPresentationState correctionPresentationState = new();
    private readonly ModelCatalog modelCatalog;
    private readonly ScreenCaptureService screenCaptureService = new();
    private GlobalHotkeyService? globalHotkeyService;
    private OrbOverlayWindow? overlayWindow;
    private CorrectionOverlayWindow? correctionOverlayWindow;
    private DetectionOverlayWindow? detectionOverlayWindow;
    private CancellationTokenSource? floatingCancellation;
    private CancellationTokenSource? activeFloatingAnalysis;
    private CancellationTokenSource? correctionCancellation;
    private bool floatingMode;
    private bool floatingPaused;
    private bool closing;
    private bool initializingModelSelectors;
    private double correctionPaneHeight = DefaultCorrectionPaneHeight;
    private long correctionGeneration;
    private FloatingAnalysisMode floatingAnalysisMode = FloatingAnalysisMode.ObjectsCursor;
    private BitmapSource? currentImage;
    private TextDocument? currentTextDocument;
    private readonly TextDocumentLoader textDocumentLoader = new();
    private WorkspaceContentMode workspaceContentMode;

    private sealed record ProviderChoice(
        InferenceProviderKind Id,
        string DisplayName);

    private enum FloatingAnalysisMode
    {
        ObjectsCursor,
        TextCursor,
        ObjectsScreen,
        TextScreen
    }

    private enum WorkspaceContentMode
    {
        None,
        Image,
        Text
    }

    public MainWindow()
    {
        InitializeComponent();
        SetWorkspaceContentMode(WorkspaceContentMode.None);
        ResultsList.ItemsSource = results;
        var startupConfiguration = StartupConfigurationLoader.Load(
            Path.Combine(AppContext.BaseDirectory, StartupConfigurationLoader.FileName));
        analysisConfiguration = startupConfiguration.Configuration;
        modelCatalog = ModelCatalog.Create();
        analysisCoordinator = new AnalysisCoordinator(
            AnalysisServiceFactory.CreateServices(analysisConfiguration));
        textCorrectionService = AnalysisServiceFactory.CreateTextCorrectionService(
            analysisConfiguration);
        correctionCoordinator = CreateCorrectionCoordinator(textCorrectionService);
        InitializeModelSelectors();
        UpdateModelStatus();
        if (startupConfiguration.Warning is not null)
        {
            StatusText.Text = startupConfiguration.Warning;
            StatusText.ToolTip = startupConfiguration.Warning;
        }

        try
        {
            globalHotkeyService = new GlobalHotkeyService(
                this,
                ModifierKeys.Control | ModifierKeys.Alt,
                Key.L);
            globalHotkeyService.Pressed += GlobalHotkeyService_Pressed;
        }
        catch (Win32Exception exception)
        {
            StatusText.Text = exception.Message;
        }
    }

    private void InitializeModelSelectors()
    {
        initializingModelSelectors = true;
        try
        {
            ContextAwareCorrectionCheckBox.IsChecked = analysisConfiguration.ContextAwareCorrection;
            ObjectModelSelector.ItemsSource = modelCatalog.ObjectModels;
            TextCorrectionSelector.ItemsSource = modelCatalog.TextCorrectionModels;
            ProviderSelector.ItemsSource = new[]
            {
                new ProviderChoice(InferenceProviderKind.Auto, "Auto"),
                new ProviderChoice(InferenceProviderKind.DirectMl, "DirectML"),
                new ProviderChoice(InferenceProviderKind.Cpu, "CPU")
            };
            ObjectModelSelector.SelectedItem = modelCatalog.ObjectModels.Single(option =>
                option.Id == analysisConfiguration.ObjectModel);
            TextCorrectionSelector.SelectedItem = modelCatalog.TextCorrectionModels.Single(option =>
                option.Id == analysisConfiguration.TextCorrectionModel);
            ProviderSelector.SelectedItem = ProviderSelector.Items
                .Cast<ProviderChoice>()
                .Single(option => option.Id == analysisConfiguration.Provider);
        }
        finally
        {
            initializingModelSelectors = false;
        }
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (initializingModelSelectors
            || ObjectModelSelector.SelectedItem is not ModelCatalogOption<ObjectModelKind> objectModel
            || TextCorrectionSelector.SelectedItem is not ModelCatalogOption<TextCorrectionModelKind> textModel
            || ProviderSelector.SelectedItem is not ProviderChoice provider)
        {
            return;
        }

        analysisConfiguration = new AnalysisConfiguration(
            objectModel.Id,
            textModel.Id,
            provider.Id)
        {
            ContextAwareCorrection = ContextAwareCorrectionCheckBox.IsChecked == true
        };
        RebuildAnalysisServices();
    }

    private void ContextAwareCorrection_Changed(object sender, RoutedEventArgs e)
    {
        if (initializingModelSelectors || analysisConfiguration is null)
            return;
        analysisConfiguration = analysisConfiguration with
        {
            ContextAwareCorrection = ContextAwareCorrectionCheckBox.IsChecked == true
        };
        RebuildAnalysisServices();
    }

    private void RebuildAnalysisServices()
    {
        activeFloatingAnalysis?.Cancel();
        correctionCancellation?.Cancel();
        correctionCancellation?.Dispose();
        correctionCancellation = null;

        var previousCoordinator = analysisCoordinator;
        analysisCoordinator = new AnalysisCoordinator(
            AnalysisServiceFactory.CreateServices(analysisConfiguration));
        previousCoordinator.Dispose();

        textCorrectionService?.Dispose();
        textCorrectionService = AnalysisServiceFactory.CreateTextCorrectionService(
            analysisConfiguration);
        correctionCoordinator = CreateCorrectionCoordinator(textCorrectionService);
        correctionPresentationState = new CorrectionPresentationState();
        correctionGeneration++;
        UpdateCorrectionSurface();
        UpdateModelStatus();
        StatusText.Text = "Modellauswahl uebernommen.";
    }

    private static CorrectionCoordinator? CreateCorrectionCoordinator(
        ITextCorrectionService? service)
    {
        return service?.IsAvailable == true
            ? new CorrectionCoordinator(service)
            : null;
    }

    private void UpdateModelStatus()
    {
        var objectModel = modelCatalog.ObjectModels.Single(option =>
            option.Id == analysisConfiguration.ObjectModel);
        var textModel = modelCatalog.TextCorrectionModels.Single(option =>
            option.Id == analysisConfiguration.TextCorrectionModel);
        var providerName = analysisConfiguration.Provider switch
        {
            InferenceProviderKind.DirectMl => "DirectML angefordert; kein NPU-Versprechen",
            InferenceProviderKind.Cpu => "CPU angefordert",
            _ => "Auto: DirectML, sonst CPU"
        };
        ContextAwareCorrectionCheckBox.IsEnabled = analysisConfiguration.TextCorrectionModel == TextCorrectionModelKind.LocalLlm;
        ModelStatusText.Text = $"{objectModel.StatusText} {textModel.StatusText} {providerName}.";
    }

    private void SetWorkspaceContentMode(WorkspaceContentMode mode)
    {
        workspaceContentMode = mode;
        var showImage = mode == WorkspaceContentMode.Image && currentImage is not null;
        var showText = mode == WorkspaceContentMode.Text && currentTextDocument is not null;

        WorkspaceImage.Visibility = showImage
            ? Visibility.Visible
            : Visibility.Collapsed;
        WorkspaceTextDocument.Visibility = showText
            ? Visibility.Visible
            : Visibility.Collapsed;
        ResultsCanvas.Visibility = showImage
            ? Visibility.Visible
            : Visibility.Collapsed;
        MagnifierLayer.Visibility = showImage
            ? Visibility.Visible
            : Visibility.Collapsed;
        EmptyStateText.Visibility = showImage || showText
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (!showImage)
        {
            ResultsCanvas.Children.Clear();
            HideMagnifier();
        }
    }

    private void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Bilder|*.png;*.jpg;*.jpeg;*.bmp|Alle Dateien|*.*",
            Title = "Bild fuer die Ki-Lupe oeffnen"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            currentImage = LoadImage(dialog.FileName);
            currentTextDocument = null;
            WorkspaceImage.Source = currentImage;
            WorkspaceTextDocument.Clear();
            SetWorkspaceContentMode(WorkspaceContentMode.Image);
            ClearResults();
            StatusText.Text = $"Bild geladen: {Path.GetFileName(dialog.FileName)}";
            HideMagnifier();
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Bild konnte nicht geladen werden: {exception.Message}";
        }
    }

    private void SaveTextFile_Click(object sender, RoutedEventArgs e)
    {
        if (workspaceContentMode != WorkspaceContentMode.Text || currentTextDocument is null)
        {
            StatusText.Text = "Bitte zuerst eine Textdatei laden.";
            return;
        }
        var dialog = new SaveFileDialog
        {
            FileName = Path.GetFileName(currentTextDocument.FilePath),
            Filter = "Textdateien|*.txt;*.md;*.log;*.csv;*.json;*.xml|Alle Dateien|*.*",
            DefaultExt = Path.GetExtension(currentTextDocument.FilePath)
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, WorkspaceTextDocument.Text, new System.Text.UTF8Encoding(false));
            StatusText.Text = $"Text gespeichert: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Text konnte nicht gespeichert werden: {exception.Message}";
        }
    }

    private void WorkspaceTextDocument_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (currentTextDocument is null || currentTextDocument.Text == WorkspaceTextDocument.Text) return;
        var text = WorkspaceTextDocument.Text;
        currentTextDocument = currentTextDocument with
        {
            Text = text,
            Lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
        };
        ClearResults();
        StatusText.Text = "Text bearbeitet. Zum Aktualisieren erneut Text pruefen.";
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderResultOverlays();
        if (workspaceContentMode != WorkspaceContentMode.Text || ResultsList.SelectedItem is not AnalysisResult result) return;
        if (result.Bounds.IsEmpty) return;
        var start = (int)result.Bounds.X;
        var length = (int)result.Bounds.Width;
        if (start < 0 || start + length > WorkspaceTextDocument.Text.Length) return;
        WorkspaceTextDocument.Select(start, length);
        WorkspaceTextDocument.ScrollToLine(WorkspaceTextDocument.GetLineIndexFromCharacterIndex(start));
    }

    private void OpenTextFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Textdateien|*.txt;*.md;*.log;*.csv;*.json;*.xml|Alle Dateien|*.*",
            Title = "Textdatei fuer die Ki-Lupe laden"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!TextDocumentLoader.IsSupportedExtension(dialog.FileName))
        {
            StatusText.Text = "Nicht unterstuetztes Textformat. Erlaubt sind TXT, MD, LOG, CSV, JSON und XML.";
            return;
        }

        try
        {
            var document = textDocumentLoader.Load(dialog.FileName);
            currentTextDocument = document;
            currentImage = null;
            WorkspaceImage.Source = null;
            WorkspaceTextDocument.Text = document.Text;
            SetWorkspaceContentMode(WorkspaceContentMode.Text);
            ClearResults();
            StatusText.Text = $"Textdatei geladen: {Path.GetFileName(document.FilePath)}";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Textdatei konnte nicht geladen werden: {exception.Message}";
        }
    }

    private async void AnalyzeObjects_Click(object sender, RoutedEventArgs e)
    {
        if (workspaceContentMode == WorkspaceContentMode.Text)
        {
            StatusText.Text = "Objektanalyse ist nur fuer Bilder verfuegbar.";
            return;
        }

        if (workspaceContentMode != WorkspaceContentMode.Image || currentImage is null)
        {
            StatusText.Text = "Bitte zuerst ein Bild oeffnen.";
            return;
        }

        StatusText.Text = "Objektanalyse laeuft...";
        var snapshot = await analysisCoordinator.AnalyzeAsync(
            currentImage,
            IsObjectAnalysisService);
        if (snapshot is not null)
        {
            ApplySnapshot(snapshot);
            await ApplyCorrectionsAsync(snapshot, CancellationToken.None);
        }
    }

    private async void AnalyzeText_Click(object sender, RoutedEventArgs e)
    {
        if (workspaceContentMode == WorkspaceContentMode.Text)
        {
            var document = currentTextDocument;
            if (document is null)
            {
                StatusText.Text = "Bitte zuerst eine Textdatei laden.";
                return;
            }

            StatusText.Text = "Textkorrektur laeuft...";
            var generation = correctionGeneration;
            await ApplyCorrectionsAsync(document, CancellationToken.None);
            if (generation == correctionGeneration
                && ReferenceEquals(currentTextDocument, document))
            {
                StatusText.Text = string.IsNullOrWhiteSpace(document.Text)
                    ? "Die Textdatei ist leer."
                    : correctionCoordinator is null
                        ? textCorrectionService?.StatusText ?? "Textkorrektur ist nicht verfuegbar."
                        : correctionPresentationState.IsVisible
                            ? "Korrekturvorschlaege bereit."
                            : "Keine Korrekturvorschlaege gefunden.";
            }

            return;
        }

        if (workspaceContentMode != WorkspaceContentMode.Image || currentImage is null)
        {
            StatusText.Text = "Bitte zuerst ein Bild oeffnen.";
            return;
        }

        StatusText.Text = "OCR und Rechtschreibpruefung laufen...";
        var snapshot = await analysisCoordinator.AnalyzeAsync(
            currentImage,
            service => service is LocalTextAnalysisService);
        if (snapshot is not null)
        {
            ApplySnapshot(snapshot);
            await ApplyCorrectionsAsync(snapshot, CancellationToken.None);
        }
    }

    private void FloatingMode_Click(object sender, RoutedEventArgs e)
    {
        ToggleFloatingMode();
    }

    private void GlobalHotkeyService_Pressed(object? sender, EventArgs e)
    {
        ToggleFloatingMode();
    }

    private void ToggleFloatingMode()
    {
        if (floatingMode)
        {
            StopFloatingMode();
            return;
        }

        StartFloatingMode();
    }

    private void StartFloatingMode()
    {
        if (floatingMode)
        {
            return;
        }

        if (workspaceContentMode == WorkspaceContentMode.Text)
        {
            StatusText.Text = "Schwebemodus ist bei geladenen Textdateien nicht verfuegbar.";
            return;
        }

        ClearCorrections();
        floatingMode = true;
        floatingPaused = false;
        floatingAnalysisMode = FloatingAnalysisMode.ObjectsCursor;
        floatingCancellation = new CancellationTokenSource();

        overlayWindow = new OrbOverlayWindow();
        overlayWindow.PauseRequested += OverlayWindow_PauseRequested;
        overlayWindow.ModeRequested += OverlayWindow_ModeRequested;
        overlayWindow.ResultsRequested += OverlayWindow_ResultsRequested;
        overlayWindow.ClearRequested += OverlayWindow_ClearRequested;
        overlayWindow.CorrectionRequested += OverlayWindow_CorrectionRequested;
        overlayWindow.ExitRequested += OverlayWindow_ExitRequested;
        correctionOverlayWindow = new CorrectionOverlayWindow();
        overlayWindow.SetCorrectionAvailable(textCorrectionService?.IsAvailable == true);
        overlayWindow.SetPaused(false);
        overlayWindow.SetMode(GetFloatingModeLabel());
        overlayWindow.SetStatus("Starte Analyse...");

        try
        {
            detectionOverlayWindow = new DetectionOverlayWindow();
            detectionOverlayWindow.SetPresentationMode(PresentationModeCheckBox.IsChecked == true);
            detectionOverlayWindow.ShowOnVirtualScreen();
        }
        catch (Exception exception)
        {
            detectionOverlayWindow?.Close();
            detectionOverlayWindow = null;
            overlayWindow.SetStatus($"Markierungen nicht verfuegbar: {exception.Message}");
        }

        overlayWindow.ShowAtTaskbar();

        StatusText.Text = "Schwebemodus aktiv.";

        _ = RunFloatingAnalysisLoopAsync(floatingCancellation);
    }

    private void StopFloatingMode(bool restoreWindow = true)
    {
        if (!floatingMode && overlayWindow is null)
        {
            return;
        }

        floatingMode = false;
        floatingPaused = false;
        floatingCancellation?.Cancel();
        activeFloatingAnalysis?.Cancel();

        var overlay = overlayWindow;
        overlayWindow = null;
        var correctionOverlay = correctionOverlayWindow;
        correctionOverlayWindow = null;
        var detectionOverlay = detectionOverlayWindow;
        detectionOverlayWindow = null;
        detectionOverlay?.ClearResults();
        detectionOverlay?.Close();
        correctionOverlay?.Close();
        if (overlay is not null)
        {
            overlay.PauseRequested -= OverlayWindow_PauseRequested;
            overlay.ModeRequested -= OverlayWindow_ModeRequested;
            overlay.ResultsRequested -= OverlayWindow_ResultsRequested;
            overlay.ClearRequested -= OverlayWindow_ClearRequested;
            overlay.CorrectionRequested -= OverlayWindow_CorrectionRequested;
            overlay.ExitRequested -= OverlayWindow_ExitRequested;
            overlay.Close();
        }

        if (restoreWindow && !closing)
        {
            if (!IsVisible)
            {
                Show();
            }

            Activate();
            StatusText.Text = "Schwebemodus beendet.";
        }
    }

    private void OverlayWindow_PauseRequested(object? sender, EventArgs e)
    {
        floatingPaused = !floatingPaused;
        if (floatingPaused)
        {
            activeFloatingAnalysis?.Cancel();
            detectionOverlayWindow?.ClearResults();
        }

        overlayWindow?.SetPaused(floatingPaused);
        overlayWindow?.SetStatus(floatingPaused ? "Pausiert" : "Analyse laeuft...");
    }

    private void OverlayWindow_ModeRequested(object? sender, EventArgs e)
    {
        detectionOverlayWindow?.ClearResults();
        floatingAnalysisMode = (FloatingAnalysisMode)(((int)floatingAnalysisMode + 1) % 4);
        ClearCorrections();
        overlayWindow?.SetMode(GetFloatingModeLabel());
        activeFloatingAnalysis?.Cancel();
    }

    private void OverlayWindow_ClearRequested(object? sender, EventArgs e)
    {
        detectionOverlayWindow?.ClearResults();
        ClearCorrections();
    }

    private bool selectionCorrectionRunning;

    private async void OverlayWindow_CorrectionRequested(object? sender, EventArgs e)
    {
        if (selectionCorrectionRunning || !floatingMode || floatingCancellation is null) return;
        ClearCorrections();
        selectionCorrectionRunning = true;
        var generation = correctionGeneration;
        var token = floatingCancellation.Token;
        try
        {
            overlayWindow?.SetStatus("Lese ausgewaehlten Text...");
            var selection = await Task.Run(() => new SelectedTextReader().ReadSelection(), token)
                .WaitAsync(TimeSpan.FromSeconds(3), token);
            if (generation != correctionGeneration || !floatingMode || token.IsCancellationRequested) return;
            var selectedText = selection.Text ?? string.Empty;
            if (!selection.Success)
            {
                var dialog = new SelectedTextDialog(selection.Message);
                using var closeOnCancel = token.Register(() => Dispatcher.BeginInvoke(new Action(dialog.Close)));
                if (dialog.ShowDialog() != true) return;
                selectedText = dialog.SelectedText;
            }
            if (generation != correctionGeneration || !floatingMode || token.IsCancellationRequested) return;
            correctionOverlayWindow?.Hide();
            overlayWindow?.SetStatus("Pruefe ausgewaehlten Text...");
            await ApplyCorrectionsAsync(generation,
                (coordinator, cancellationToken) => coordinator.CreateSuggestionsAsync(new[] { selectedText }, cancellationToken), token);
            if (generation != correctionGeneration || !floatingMode || token.IsCancellationRequested) return;
            if (correctionPresentationState.IsVisible && overlayWindow is not null)
            {
                correctionOverlayWindow?.ShowNear(overlayWindow);
                overlayWindow.SetStatus("Korrektur fuer die Textauswahl bereit.");
            }
            else
            {
                overlayWindow?.SetStatus("Keine Korrektur fuer die Auswahl gefunden.");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (TimeoutException)
        {
            overlayWindow?.SetStatus("Das Programm antwortet nicht auf die Abfrage der Textauswahl.");
        }
        catch (Exception exception)
        {
            overlayWindow?.SetStatus(exception.Message);
        }
        finally
        {
            selectionCorrectionRunning = false;
        }
    }
    private void OverlayWindow_ResultsRequested(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        StopFloatingMode();
    }

    private void OverlayWindow_ExitRequested(object? sender, EventArgs e)
    {
        StopFloatingMode();
    }

    private async Task RunFloatingAnalysisLoopAsync(CancellationTokenSource loopCancellation)
    {
        try
        {
            while (!loopCancellation.IsCancellationRequested && floatingMode)
            {
                if (floatingPaused)
                {
                    await Task.Delay(150, loopCancellation.Token);
                    continue;
                }

                var mode = floatingAnalysisMode;
                using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    loopCancellation.Token);
                activeFloatingAnalysis = operationCancellation;
                try
                {
                    var frame = await CaptureFloatingImageAsync(mode, operationCancellation.Token);
                    var snapshot = await analysisCoordinator.AnalyzeAsync(
                        frame.Image,
                        service => UsesTextAnalysis(mode)
                            ? service is LocalTextAnalysisService
                            : IsObjectAnalysisService(service),
                        operationCancellation.Token);
                    if (snapshot is null || !floatingMode || floatingPaused)
                    {
                        continue;
                    }

                    currentImage = frame.Image;
                    currentTextDocument = null;
                    WorkspaceImage.Source = frame.Image;
                    SetWorkspaceContentMode(WorkspaceContentMode.Image);
                    ApplySnapshot(snapshot);

                    var detectedResults = snapshot.Results
                        .Where(result => UsesTextAnalysis(mode) ? result.Kind == AnalysisKind.Spelling : result.Kind == AnalysisKind.Object)
                        .ToArray();
                    if (detectedResults.Length == 0)
                    {
                        detectionOverlayWindow?.ClearResults();
                    }
                    else
                    {
                        detectionOverlayWindow?.ShowResults(
                            detectedResults,
                            frame.Region,
                            new Size(frame.Image.PixelWidth, frame.Image.PixelHeight),
                            TimeSpan.FromSeconds(2));
                    }

                    var floatingStatus = detectedResults.Length == 0
                        ? snapshot.StatusText
                        : $"{detectedResults.Length} Treffer: {string.Join(", ", detectedResults.Take(2).Select(result => result.Label))}";
                    if (!selectionCorrectionRunning) overlayWindow?.SetStatus(floatingStatus);

                }
                catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    overlayWindow?.SetStatus($"Analysefehler: {exception.Message}");
                }
                finally
                {
                    if (ReferenceEquals(activeFloatingAnalysis, operationCancellation))
                    {
                        activeFloatingAnalysis = null;
                    }
                }

                var delay = IsFullScreenMode(mode) ? 1800 : 450;
                await Task.Delay(delay, loopCancellation.Token);
            }
        }
        catch (OperationCanceledException) when (loopCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(floatingCancellation, loopCancellation))
            {
                floatingCancellation = null;
            }

            loopCancellation.Dispose();
        }
    }

    private void PresentationMode_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            detectionOverlayWindow?.SetPresentationMode(PresentationModeCheckBox.IsChecked == true);
            StatusText.Text = PresentationModeCheckBox.IsChecked == true
                ? "Video-Call aktiv: Den ganzen Bildschirm teilen."
                : "Markierungen werden aus Bildschirmaufnahmen ausgeschlossen.";
        }
        catch (Win32Exception exception)
        {
            PresentationModeCheckBox.IsChecked = false;
            StatusText.Text = exception.Message;
        }
        catch (System.Runtime.InteropServices.COMException exception)
        {
            StatusText.Text = exception.Message;
        }
    }

    private Task<ScreenCaptureFrame> CaptureFloatingImageAsync(
        FloatingAnalysisMode mode,
        CancellationToken cancellationToken)
    {
        Task<ScreenCaptureFrame> Capture() => Task.Run(
            () => IsFullScreenMode(mode)
                ? screenCaptureService.CaptureVirtualScreenFrame()
                : screenCaptureService.CaptureCursorFrame(),
            cancellationToken);
        return detectionOverlayWindow is { } markers
            ? markers.CaptureWithoutMarkersAsync(Capture)
            : Capture();
    }
    private string GetFloatingModeLabel()
    {
        return floatingAnalysisMode switch
        {
            FloatingAnalysisMode.ObjectsCursor => "Objekte / Cursor",
            FloatingAnalysisMode.TextCursor => "Text / Cursor",
            FloatingAnalysisMode.ObjectsScreen => "Objekte / Bildschirm",
            FloatingAnalysisMode.TextScreen => "Text / Bildschirm",
            _ => "Objekte / Cursor"
        };
    }

    private static bool UsesTextAnalysis(FloatingAnalysisMode mode)
    {
        return mode is FloatingAnalysisMode.TextCursor or FloatingAnalysisMode.TextScreen;
    }

    private static bool IsFullScreenMode(FloatingAnalysisMode mode)
    {
        return mode is FloatingAnalysisMode.ObjectsScreen or FloatingAnalysisMode.TextScreen;
    }

    private static bool IsObjectAnalysisService(IAnalysisService service)
    {
        return service is OnnxObjectDetectionService or RtdetrObjectDetectionService;
    }

    private Task ApplyCorrectionsAsync(
        AnalysisSnapshot snapshot,
        CancellationToken parentCancellation)
    {
        return ApplyCorrectionsAsync(
            snapshot.RequestId,
            (coordinator, cancellationToken) => coordinator.CreateSuggestionsAsync(
                snapshot.Results,
                cancellationToken),
            parentCancellation);
    }

    private Task ApplyCorrectionsAsync(
        TextDocument document,
        CancellationToken parentCancellation)
    {
        return ApplyCorrectionsAsync(
            correctionGeneration,
            (coordinator, cancellationToken) => coordinator.CreateSuggestionsAsync(
                document.Lines,
                cancellationToken),
            parentCancellation);
    }

    private async Task ApplyCorrectionsAsync(
        long requestId,
        Func<CorrectionCoordinator, CancellationToken, Task<IReadOnlyList<CorrectionSuggestion>>> createSuggestions,
        CancellationToken parentCancellation)
    {
        correctionCancellation?.Cancel();
        correctionCancellation?.Dispose();
        correctionCancellation = null;

        var coordinator = correctionCoordinator;
        var generation = correctionGeneration;
        if (coordinator is null)
        {
            correctionPresentationState.Clear(requestId);
            UpdateCorrectionSurface();
            return;
        }

        var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            parentCancellation);
        correctionCancellation = operationCancellation;
        try
        {
            var suggestions = await createSuggestions(coordinator, operationCancellation.Token);
            if (generation != correctionGeneration
                || operationCancellation.IsCancellationRequested)
            {
                return;
            }

            if (workspaceContentMode == WorkspaceContentMode.Text && currentTextDocument is not null)
            {
                results.Clear();
                var searchStart = 0;
                foreach (var suggestion in suggestions)
                {
                    var start = currentTextDocument.Text.IndexOf(suggestion.OriginalText, searchStart, StringComparison.Ordinal);
                    if (start < 0 || suggestion.OriginalText.Length == 0) continue;
                    searchStart = start + suggestion.OriginalText.Length;
                    results.Add(new AnalysisResult(AnalysisKind.Spelling, suggestion.OriginalText, 1,
                        new Rect(start, 0, suggestion.OriginalText.Length, 1), suggestion.CorrectedText));
                }
                ResultCountText.Text = $"{results.Count} Treffer";
            }
            correctionPresentationState.Apply(requestId, suggestions);
            UpdateCorrectionSurface();
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (generation == correctionGeneration)
            {
                correctionPresentationState.Clear(requestId);
                CorrectionStatusText.Text = $"Korrektur fehlgeschlagen: {exception.Message}";
                UpdateCorrectionSurface();
            }
        }
        finally
        {
            if (ReferenceEquals(correctionCancellation, operationCancellation))
            {
                correctionCancellation = null;
                operationCancellation.Dispose();
            }
        }
    }

    private void UpdateCorrectionSurface()
    {
        overlayWindow?.SetCorrectionAvailable(textCorrectionService?.IsAvailable == true);
        if (!correctionPresentationState.IsVisible)
        {
            HideCorrectionPane();
            correctionOverlayWindow?.Hide();
            return;
        }

        CorrectionOriginalText.Text = $"Original: {correctionPresentationState.OriginalText}";
        CorrectionSuggestionTextBox.Text = correctionPresentationState.SuggestionText;
        CorrectionStatusText.Text = correctionPresentationState.StatusText;
        CorrectionPanel.Visibility = Visibility.Visible;
        CorrectionGridSplitter.Visibility = Visibility.Visible;
        CorrectionSplitterRow.Height = new GridLength(7);
        CorrectionRow.MinHeight = MinimumCorrectionPaneHeight;
        if (CorrectionRow.Height.GridUnitType != GridUnitType.Pixel
            || CorrectionRow.Height.Value <= 0)
        {
            CorrectionRow.Height = new GridLength(
                Math.Max(MinimumCorrectionPaneHeight, correctionPaneHeight),
                GridUnitType.Pixel);
        }

        ConstrainCorrectionPaneHeight();
        if (floatingMode && overlayWindow is not null && correctionOverlayWindow is not null)
        {
            correctionOverlayWindow.SetState(correctionPresentationState);
            if (correctionOverlayWindow.IsVisible) correctionOverlayWindow.ShowNear(overlayWindow);
        }
        else
        {
            correctionOverlayWindow?.Hide();
        }
    }

    private void WorkspaceGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (CorrectionPanel.Visibility == Visibility.Visible)
        {
            ConstrainCorrectionPaneHeight();
        }
    }

    private void CorrectionGridSplitter_DragCompleted(
        object sender,
        DragCompletedEventArgs e)
    {
        if (CorrectionRow.ActualHeight > 0)
        {
            correctionPaneHeight = CorrectionRow.ActualHeight;
        }
    }

    private void ConstrainCorrectionPaneHeight()
    {
        if (LeftWorkspaceGrid.ActualHeight <= 0)
        {
            return;
        }

        var availableHeight = LeftWorkspaceGrid.ActualHeight;
        var splitterHeight = Math.Max(0, CorrectionGridSplitter.ActualHeight);
        var maximumHeight = Math.Max(
            MinimumCorrectionPaneHeight,
            availableHeight - ImageWorkspaceRow.MinHeight - splitterHeight);
        var height = Math.Clamp(
            CorrectionRow.Height.Value,
            MinimumCorrectionPaneHeight,
            maximumHeight);
        if (Math.Abs(CorrectionRow.Height.Value - height) > 0.5)
        {
            CorrectionRow.Height = new GridLength(height, GridUnitType.Pixel);
        }

        correctionPaneHeight = height;
    }

    private void HideCorrectionPane()
    {
        if (CorrectionRow.ActualHeight > 0)
        {
            correctionPaneHeight = CorrectionRow.ActualHeight;
        }

        CorrectionPanel.Visibility = Visibility.Collapsed;
        CorrectionGridSplitter.Visibility = Visibility.Collapsed;
        CorrectionSplitterRow.Height = new GridLength(0);
        CorrectionRow.MinHeight = 0;
        CorrectionRow.Height = new GridLength(0);
        ImageWorkspaceRow.Height = new GridLength(1, GridUnitType.Star);
    }

    private void CopyCorrectionButton_Click(object sender, RoutedEventArgs e)
    {
        CopyCorrectionText(CorrectionSuggestionTextBox.Text, "Vorschlag kopiert.");
    }

    private void CopyCorrectionSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CorrectionSuggestionTextBox.SelectedText))
        {
            StatusText.Text = "Keine Textauswahl zum Kopieren.";
            return;
        }

        CopyCorrectionText(
            CorrectionSuggestionTextBox.SelectedText,
            "Auswahl kopiert.");
    }

    private void CopyCorrectionText(string text, string successMessage)
    {
        if (string.IsNullOrEmpty(text))
        {
            StatusText.Text = "Kein Korrekturtext vorhanden.";
            return;
        }

        try
        {
            Clipboard.SetText(text);
            StatusText.Text = successMessage;
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Kopieren fehlgeschlagen: {exception.Message}";
        }
    }

    private void HideCorrectionButton_Click(object sender, RoutedEventArgs e)
    {
        HideCorrectionPane();
    }

    private void ClearCorrections()
    {
        correctionCancellation?.Cancel();
        correctionPresentationState = new CorrectionPresentationState();
        correctionGeneration++;
        UpdateCorrectionSurface();
    }

    private void ImageSurface_MouseMove(object sender, MouseEventArgs e)
    {
        if (currentImage is null)
        {
            return;
        }

        var surfacePoint = e.GetPosition(ImageSurface);
        if (!TryGetImagePoint(surfacePoint, out var imagePoint, out _))
        {
            HideMagnifier();
            return;
        }

        var sampleWidth = Math.Max(1, (int)Math.Round(currentImage.PixelWidth / Magnification * 0.18));
        var sampleHeight = Math.Max(1, (int)Math.Round(currentImage.PixelHeight / Magnification * 0.18));
        sampleWidth = Math.Min(sampleWidth, currentImage.PixelWidth);
        sampleHeight = Math.Min(sampleHeight, currentImage.PixelHeight);

        var sampleX = Math.Clamp(
            (int)Math.Round(imagePoint.X - sampleWidth / 2.0),
            0,
            currentImage.PixelWidth - sampleWidth);
        var sampleY = Math.Clamp(
            (int)Math.Round(imagePoint.Y - sampleHeight / 2.0),
            0,
            currentImage.PixelHeight - sampleHeight);

        var cropped = new CroppedBitmap(
            currentImage,
            new Int32Rect(sampleX, sampleY, sampleWidth, sampleHeight));
        cropped.Freeze();
        MagnifierImage.Source = cropped;

        var left = Math.Clamp(
            surfacePoint.X - MagnifierDiameter / 2,
            0,
            Math.Max(0, ImageSurface.ActualWidth - MagnifierDiameter));
        var top = Math.Clamp(
            surfacePoint.Y - MagnifierDiameter / 2,
            0,
            Math.Max(0, ImageSurface.ActualHeight - MagnifierDiameter));
        Canvas.SetLeft(Magnifier, left);
        Canvas.SetTop(Magnifier, top);
        Magnifier.Visibility = Visibility.Visible;
    }

    private void ImageSurface_MouseLeave(object sender, MouseEventArgs e)
    {
        HideMagnifier();
    }

    private void ImageSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderResultOverlays();
    }

    private static BitmapImage LoadImage(string path)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private bool TryGetImagePoint(Point surfacePoint, out Point imagePoint, out Rect imageRect)
    {
        imageRect = GetDisplayedImageRect();
        if (currentImage is null || imageRect.IsEmpty || !imageRect.Contains(surfacePoint))
        {
            imagePoint = default;
            return false;
        }

        imagePoint = new Point(
            (surfacePoint.X - imageRect.Left) * currentImage.PixelWidth / imageRect.Width,
            (surfacePoint.Y - imageRect.Top) * currentImage.PixelHeight / imageRect.Height);
        return true;
    }

    private Rect GetDisplayedImageRect()
    {
        if (currentImage is null || currentImage.PixelWidth == 0 || currentImage.PixelHeight == 0)
        {
            return Rect.Empty;
        }

        var scale = Math.Min(
            ImageSurface.ActualWidth / currentImage.PixelWidth,
            ImageSurface.ActualHeight / currentImage.PixelHeight);
        var width = currentImage.PixelWidth * scale;
        var height = currentImage.PixelHeight * scale;
        return new Rect(
            (ImageSurface.ActualWidth - width) / 2,
            (ImageSurface.ActualHeight - height) / 2,
            width,
            height);
    }

    private void ClearResults()
    {
        results.Clear();
        ResultsCanvas.Children.Clear();
        ResultCountText.Text = "Noch keine Analyse";
        ClearCorrections();
    }

    private void ApplySnapshot(AnalysisSnapshot snapshot)
    {
        results.Clear();
        foreach (var result in snapshot.Results)
        {
            results.Add(result);
        }

        var detectionCount = snapshot.Results.Count(result => result.Kind != AnalysisKind.Status);
        ResultCountText.Text = detectionCount == 0
            ? "Keine Treffer"
            : $"{detectionCount} Treffer";
        StatusText.Text = snapshot.StatusText;
        RenderResultOverlays();
    }

    private void RenderResultOverlays()
    {
        ResultsCanvas.Children.Clear();
        if (workspaceContentMode != WorkspaceContentMode.Image || currentImage is null)
        {
            return;
        }

        var imageRect = GetDisplayedImageRect();
        foreach (var result in results.Where(result =>
                     result.Kind != AnalysisKind.Status && !result.Bounds.IsEmpty))
        {
            var left = imageRect.Left + result.Bounds.Left * imageRect.Width / currentImage.PixelWidth;
            var top = imageRect.Top + result.Bounds.Top * imageRect.Height / currentImage.PixelHeight;
            var width = Math.Max(42, result.Bounds.Width * imageRect.Width / currentImage.PixelWidth);
            var height = Math.Max(26, result.Bounds.Height * imageRect.Height / currentImage.PixelHeight);
            var selected = ReferenceEquals(ResultsList.SelectedItem, result);
            var accent = selected ? Colors.Yellow : result.Kind == AnalysisKind.Spelling ? Colors.OrangeRed : Colors.LightGreen;
            var border = new Border
            {
                Width = width,
                Height = height,
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(selected ? 4 : 2),
                Background = new SolidColorBrush(Color.FromArgb(42, accent.R, accent.G, accent.B)),
                CornerRadius = new CornerRadius(4),
                Child = new TextBlock
                {
                    Text = result.Label,
                    Foreground = new SolidColorBrush(accent),
                    Background = new SolidColorBrush(Color.FromArgb(190, 16, 22, 20)),
                    Padding = new Thickness(4, 2, 4, 2),
                    VerticalAlignment = VerticalAlignment.Top
                }
            };
            Canvas.SetLeft(border, left);
            Canvas.SetTop(border, top);
            ResultsCanvas.Children.Add(border);
        }
    }

    private void HideMagnifier()
    {
        Magnifier.Visibility = Visibility.Collapsed;
        MagnifierImage.Source = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        closing = true;
        StopFloatingMode(restoreWindow: false);
        globalHotkeyService?.Dispose();
        correctionCancellation?.Cancel();
        correctionCancellation?.Dispose();
        textCorrectionService?.Dispose();
        analysisCoordinator.Dispose();
        base.OnClosed(e);
    }
}