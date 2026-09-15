using System.Windows;
using System.Windows.Threading;
using KiLupeDemo.Models;
namespace KiLupeDemo.Services;

public sealed class PredictionController : IDisposable
{
    private readonly Window owner;
    private readonly CaretContextReader reader = new();
    private readonly PredictionSession session = new();
    private readonly PredictionOverlayWindow overlay = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly SemaphoreSlim captureGate = new(1, 1);
    private LocalLlamaTextCorrectionService? model;
    private GlobalHotkeyService? hotkey;
    private CancellationTokenSource? inference;
    private PredictionSettings settings = new();
    private bool polling, accepting, disposed;
    private long settingsVersion;
    public string Status { get; private set; } = "Vorhersage ausgeschaltet.";
    public event EventHandler? StatusChanged;
    public PredictionController(Window owner)
    {
        this.owner = owner;
        timer.Tick += Poll;
    }
    public bool Configure(PredictionSettings value)
    {
        if (!value.IsValid) throw new ArgumentException("Ungueltige Vorhersage-Einstellungen.");
        string? modelPath = null;
        GlobalHotkeyService? nextHotkey = null;
        if (value.Enabled)
        {
            modelPath = ModelCatalog.Create().TextCorrectionModels.Single(item => item.Id == TextCorrectionModelKind.LocalLlm).ModelPath;
            if (modelPath is null && !value.CompleteCalculations) { SetStatus("Vorhersage nicht verfuegbar: lokales Qwen-Modell fehlt."); return false; }
            if (value.AllowInsertion)
            {
                if (hotkey is not null && value.AcceptKey == settings.AcceptKey && value.AcceptModifiers == settings.AcceptModifiers)
                    nextHotkey = hotkey;
                else
                {
                    try
                    {
                        nextHotkey = new GlobalHotkeyService(owner, value.AcceptModifiers, value.AcceptKey);
                        nextHotkey.Pressed += Accept;
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        SetStatus("Neuer Shortcut belegt. Bisherige Einstellungen bleiben aktiv.");
                        return false;
                    }
                }
            }
        }
        settingsVersion++;
        timer.Stop();
        inference?.Cancel();
        session.Clear();
        overlay.Hide();
        if (!ReferenceEquals(hotkey, nextHotkey)) hotkey?.Dispose();
        hotkey = nextHotkey;
        settings = value;
        if (!value.Enabled)
        {
            var previousModel = model;
            model = null;
            if (previousModel is not null) _ = Task.Run(previousModel.Dispose);
            SetStatus("Vorhersage ausgeschaltet.");
            return true;
        }
        if (modelPath is not null) model ??= new LocalLlamaTextCorrectionService(modelPath);
        SetStatus(model is null ? "Berechnungen und Zahlenfolgen aktiv. Fuer Textvorschlaege fehlt das lokale Qwen-Modell."
            : "Vorhersage aktiv. Zum Schreiben in ein anderes Programm wechseln.");
        timer.Start();
        return true;
    }
    private async Task<CaretSnapshot?> CaptureAsync()
    {
        var exclusions = settings.Exclusions;
        if (!await captureGate.WaitAsync(0)) return null;
        var capture = Task.Run(() =>
        {
            try { return reader.Read(exclusions); }
            finally { captureGate.Release(); }
        });
        if (await Task.WhenAny(capture, Task.Delay(800)) != capture)
            throw new TimeoutException("Das Textfeld antwortet nicht rechtzeitig.");
        return await capture;
    }
    private async void Poll(object? sender, EventArgs e)
    {
        if (polling || accepting || disposed) return;
        polling = true;
        var version = settingsVersion;
        try
        {
            if (session.Context is not null && CaretContextReader.GetForegroundWindow() != session.Context.Window)
            {
                session.Clear();
                inference?.Cancel();
                overlay.Hide();
            }
            var snapshot = await CaptureAsync();
            if (disposed || version != settingsVersion || accepting) return;
            var oldGeneration = session.Generation;
            var request = session.Observe(snapshot, Environment.TickCount64, settings.PauseMilliseconds);
            if (oldGeneration != session.Generation)
            {
                inference?.Cancel();
                overlay.Hide();
            }
            if (snapshot is null) SetStatus(reader.Status);
            if (request.HasValue && snapshot is not null)
            {
                inference?.Cancel();
                var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                inference = cancellation;
                _ = GenerateAsync(snapshot, request.Value, cancellation);
            }
        }
        catch (Exception exception) { session.Clear(); overlay.Hide(); SetStatus($"Vorhersage pausiert: {exception.Message}"); }
        finally { polling = false; }
    }
    private async Task GenerateAsync(CaretSnapshot snapshot, long generation, CancellationTokenSource cancellation)
    {
        try
        {
            SetStatus("Vorschlag wird lokal berechnet ...");
            var predictionModel = model;
            Func<string, string, bool, CancellationToken, Task<string>>? predict = predictionModel is null ? null
                : (before, after, onlySentence, token) => predictionModel.PredictAsync(before, after, token, onlySentence);
            var text = await PredictionGenerator.GenerateAsync(snapshot.Before, snapshot.After, settings, predict, cancellation.Token);
            if (disposed || cancellation.IsCancellationRequested || session.Generation != generation) return;
            var current = await CaptureAsync();
            if (disposed || cancellation.IsCancellationRequested || current != snapshot || session.Generation != generation) return;
            if (session.Complete(generation, text))
            {
                overlay.ShowSuggestion(snapshot, session.Suggestion!);
                SetStatus(settings.AllowInsertion ? $"Vorschlag: {settings.ShortcutLabel} zum Einfuegen." : "Vorschlag sichtbar. Einfuegen ist ausgeschaltet.");
            }
            else SetStatus("Keine passende Ergaenzung.");
        }
        catch (OperationCanceledException)
        {
            if (!disposed && generation == session.Generation)
                SetStatus("Berechnung abgebrochen. Beim Weiterschreiben wird erneut versucht.");
        }
        catch (Exception exception)
        {
            if (!disposed && generation == session.Generation) SetStatus($"Vorhersage fehlgeschlagen: {exception.Message}");
        }
        finally
        {
            if (ReferenceEquals(inference, cancellation)) inference = null;
            cancellation.Dispose();
        }
    }
    private async void Accept(object? sender, EventArgs e)
    {
        if (accepting || disposed || !settings.Enabled || !settings.AllowInsertion || session.Suggestion is null) return;
        accepting = true;
        var generation = session.Generation;
        var version = settingsVersion;
        try
        {
            // The shortcut must be released before emitting Unicode key events.
            for (var attempt = 0; attempt < 50 && !PredictionTextInserter.KeysReleased; attempt++) await Task.Delay(20);
            if (disposed || version != settingsVersion || generation != session.Generation || !PredictionTextInserter.KeysReleased) return;
            var current = await CaptureAsync();
            if (disposed || version != settingsVersion || generation != session.Generation) return;
            var text = session.Take(current, settings.AllowInsertion);
            overlay.Hide();
            if (text is null || current is null)
            {
                session.Clear();
                SetStatus("Vorschlag verworfen: Text oder Cursor wurde geaendert.");
                return;
            }
            SetStatus(PredictionTextInserter.Insert(current.Window, text)
                ? "Vorschlag eingefuegt."
                : "Einfuegen nicht vollstaendig bestaetigt. Bitte Text pruefen; kein automatischer Wiederholungsversuch.");
        }
        catch (Exception exception) { session.Clear(); overlay.Hide(); SetStatus($"Einfuegen fehlgeschlagen: {exception.Message}"); }
        finally { accepting = false; }
    }
    private void SetStatus(string value)
    {
        if (Status == value || disposed) return;
        Status = value;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        timer.Tick -= Poll;
        inference?.Cancel();
        hotkey?.Dispose();
        session.Clear();
        overlay.Close();
        var oldModel = model;
        model = null;
        if (oldModel is not null) _ = Task.Run(oldModel.Dispose);
        StatusChanged = null;
    }
}
