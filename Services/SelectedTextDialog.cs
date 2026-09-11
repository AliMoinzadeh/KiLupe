using System.Windows;
using System.Windows.Controls;

namespace KiLupeDemo.Services;

/// <summary>Explicit paste fallback for editors that do not expose TextPattern selections.</summary>
public sealed class SelectedTextDialog : Window
{
    private readonly TextBox input;
    public string SelectedText => input.Text;

    public SelectedTextDialog(string reason)
    {
        Title = "Textauswahl pruefen";
        Width = 560;
        Height = 350;
        MinWidth = 380;
        MinHeight = 250;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var layout = new DockPanel { Margin = new Thickness(16) };
        var help = new TextBlock
        {
            Text = reason + "\n\nIn VS Code: Text markieren, Strg+C. Dann hier mit Strg+V einfuegen und Pruefen anklicken.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(help, Dock.Top);
        layout.Children.Add(help);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(actions, Dock.Bottom);
        var cancel = new Button { Content = "Abbrechen", IsCancel = true, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
        var submit = new Button { Content = "Pruefen", Padding = new Thickness(12, 5, 12, 5), IsEnabled = false };
        actions.Children.Add(cancel);
        actions.Children.Add(submit);
        layout.Children.Add(actions);
        input = new TextBox { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8) };
        input.TextChanged += (_, _) => submit.IsEnabled = SelectedTextReader.ValidateSelection(new[] { input.Text }).Success;
        submit.Click += (_, _) => { if (SelectedTextReader.ValidateSelection(new[] { input.Text }).Success) DialogResult = true; };
        layout.Children.Add(input);
        Content = layout;
        Loaded += (_, _) => input.Focus();
    }
}
