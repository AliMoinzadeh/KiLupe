using System.Windows;

namespace KiLupeDemo.Services;

public partial class CorrectionOverlayWindow : Window
{
    public CorrectionOverlayWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? HiddenRequested;

    public void SetState(CorrectionPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsVisible)
        {
            Hide();
            return;
        }

        OriginalText.Text = $"Original: {state.OriginalText}";
        SuggestionTextBox.Text = state.SuggestionText;
        StatusText.Text = state.StatusText;
        CopyButton.IsEnabled = SuggestionTextBox.Text.Length > 0;
        CopySelectionButton.IsEnabled = SuggestionTextBox.Text.Length > 0;

    }

    public void ShowNear(Window anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        var workArea = SystemParameters.WorkArea;
        var preferredLeft = anchor.Left + anchor.Width - Width;
        var aboveTop = anchor.Top - Height - 10;
        var belowTop = anchor.Top + anchor.Height + 10;
        var preferredTop = aboveTop >= workArea.Top || belowTop > workArea.Bottom
            ? aboveTop
            : belowTop;

        Left = Math.Clamp(
            preferredLeft,
            workArea.Left,
            Math.Max(workArea.Left, workArea.Right - Width));
        Top = Math.Clamp(
            preferredTop,
            workArea.Top,
            Math.Max(workArea.Top, workArea.Bottom - Height));
        if (!IsVisible)
        {
            Show();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        HiddenRequested?.Invoke(this, EventArgs.Empty);
        base.OnClosed(e);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        CopyText(SuggestionTextBox.Text, "Vorschlag kopiert.");
    }

    private void CopySelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SuggestionTextBox.SelectedText))
        {
            StatusText.Text = "Keine Textauswahl zum Kopieren.";
            return;
        }

        CopyText(SuggestionTextBox.SelectedText, "Auswahl kopiert.");
    }

    private void CopyText(string text, string successMessage)
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

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        HiddenRequested?.Invoke(this, EventArgs.Empty);
    }
}
