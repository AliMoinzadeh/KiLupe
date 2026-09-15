using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace KiLupeDemo.Services;

public sealed class PredictionSettingsWindow : Window
{
    public PredictionSettingsWindow(PredictionSettings settings, Func<PredictionSettings, bool> apply)
    {
        Title = "Vorhersagemodus";
        Width = 470;
        Height = 590;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(22) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var enabled = new CheckBox { Content = "Vorhersagemodus aktivieren", IsChecked = settings.Enabled, Margin = new Thickness(0, 0, 0, 14) };
        var insertion = new CheckBox { Content = "Einfuegen per Tastenkürzel erlauben", IsChecked = settings.AllowInsertion, Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(new TextBlock { Text = "Kurze Textvorschläge direkt am Schreibcursor", FontSize = 19, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(enabled);
        panel.Children.Add(insertion);
        panel.Children.Add(new TextBlock { Text = "Schreibpause (Millisekunden, 300–5000)" });
        var delay = new TextBox { Text = settings.PauseMilliseconds.ToString(), Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(delay);
        panel.Children.Add(new TextBlock { Text = "Tastenkürzel zum Übernehmen" });
        var modifiers = new ComboBox { ItemsSource = new[] { "Strg+Alt", "Strg+Umschalt" }, SelectedIndex = settings.AcceptModifiers == (ModifierKeys.Control | ModifierKeys.Alt) ? 0 : 1, Margin = new Thickness(0, 4, 0, 4) };
        var keys = new[] { Key.Space }.Concat(Enumerable.Range((int)Key.A, 26).Select(value => (Key)value)).ToArray();
        var key = new ComboBox { ItemsSource = keys, SelectedItem = settings.AcceptKey, Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(modifiers);
        panel.Children.Add(key);
        panel.Children.Add(new TextBlock { Text = "Ausgeschlossene Programme (z. B. notepad.exe; outlook.exe)", TextWrapping = TextWrapping.Wrap });
        var excluded = new TextBox { Text = settings.ExcludedProcesses, Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(excluded);
        panel.Children.Add(new TextBlock
        {
            Text = "Vorschläge werden lokal mit Qwen berechnet. Passwortfelder sind ausgeschlossen. Unterstützt werden beschreibbare Textfelder mit zugänglichem Textcursor; maximal 20.000 Zeichen. Mitten im Text erscheint eine kleine Vorschlagsfläche.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
        });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick };
        panel.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var save = new Button { Content = "Übernehmen", Padding = new Thickness(12, 6, 12, 6), IsDefault = true };
        var cancel = new Button { Content = "Abbrechen", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        save.Click += (_, _) =>
        {
            if (!int.TryParse(delay.Text, out var milliseconds) || key.SelectedItem is not Key selectedKey)
            { status.Text = "Bitte eine gültige Pause und Taste angeben."; return; }
            var updated = new PredictionSettings
            {
                Enabled = enabled.IsChecked == true, AllowInsertion = insertion.IsChecked == true,
                PauseMilliseconds = milliseconds, AcceptKey = selectedKey,
                AcceptModifiers = modifiers.SelectedIndex == 0 ? ModifierKeys.Control | ModifierKeys.Alt : ModifierKeys.Control | ModifierKeys.Shift,
                ExcludedProcesses = excluded.Text
            };
            if (!updated.IsValid) { status.Text = "Pause: 300–5000 ms. Strg+Alt+L ist für die Lupe reserviert."; return; }
            try
            {
                if (!apply(updated)) { status.Text = "Aktivierung fehlgeschlagen. Modellstatus prüfen oder anderen Shortcut wählen."; return; }
                updated.Save();
                DialogResult = true;
            }
            catch (Exception exception) { status.Text = $"Einstellungen konnten nicht gespeichert werden: {exception.Message}"; }
        };
    }
}
