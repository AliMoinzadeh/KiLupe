using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace KiLupeDemo.Services;

public sealed record SelectionReadResult(string? Text, string Message)
{
    public bool Success => Text is not null;
}

public sealed class SelectedTextReader
{
    private const int MaximumCharacters = 20000;

    public SelectionReadResult ReadSelection()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            // Some editors focus a child within the element supplying TextPattern.
            // Stay in that ancestor chain; never scan other controls or documents.
            for (var depth = 0; element is not null && depth < 8; depth++)
            {
                if (element.Current.IsPassword)
                    return new(null, "Aus geschuetzten Feldern wird kein Text gelesen.");
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern))
                {
                    var ranges = ((TextPattern)pattern).GetSelection();
                    return ValidateSelection(ranges.Select(range => range.GetText(MaximumCharacters + 1)).ToArray());
                }
                if (element.Current.ControlType == ControlType.Window) break;
                element = TreeWalker.ControlViewWalker.GetParent(element);
            }
            return ValidateSelection(null);
        }
        catch (Exception exception) when (exception is ElementNotAvailableException
            or InvalidOperationException or COMException or UnauthorizedAccessException)
        {
            return new(null, "Die Textauswahl ist gerade nicht zugaenglich. Bitte den gewuenschten Text kopieren und einfuegen.");
        }
    }

    public static SelectionReadResult ValidateSelection(IReadOnlyList<string>? ranges)
    {
        if (ranges is null)
            return new(null, "Dieses Programm stellt die Textauswahl nicht bereit. Bitte den markierten Text kopieren und einfuegen.");
        if (ranges.Count != 1)
            return new(null, "Bitte einen zusammenhaengenden Textabschnitt auswaehlen.");
        var text = ranges[0];
        if (string.IsNullOrWhiteSpace(text))
            return new(null, "Bitte zuerst einen Satz oder Absatz markieren oder hier einfuegen.");
        if (text.Length > MaximumCharacters)
            return new(null, "Die Auswahl ist zu lang. Bitte hoechstens 20.000 Zeichen verwenden.");
        return new(text, string.Empty);
    }
}
