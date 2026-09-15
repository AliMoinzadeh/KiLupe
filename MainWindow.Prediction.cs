using System.Windows;
using KiLupeDemo.Services;
namespace KiLupeDemo;

public partial class MainWindow
{
    private PredictionController? predictionController;
    private PredictionSettings predictionSettings = new();
    private void InitializePrediction()
    {
        predictionController = new PredictionController(this);
        predictionController.StatusChanged += (_, _) => PredictionStatusText.Text = predictionController.Status;
        try { predictionSettings = PredictionSettings.Load(); }
        catch (Exception exception) { PredictionStatusText.Text = $"Vorhersage-Einstellungen nicht geladen: {exception.Message}"; return; }
        predictionController.Configure(predictionSettings);
        PredictionStatusText.Text = predictionController.Status;
    }
    private void PredictionSettings_Click(object sender, RoutedEventArgs e)
    {
        if (predictionController is null) return;
        new PredictionSettingsWindow(predictionSettings, updated =>
        {
            if (!predictionController.Configure(updated)) return false;
            predictionSettings = updated;
            return true;
        }) { Owner = this }.ShowDialog();
    }
}
