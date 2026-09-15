using KiLupeDemo.Services;
using Xunit;
using Xunit.Abstractions;

namespace KiLupeDemo.Tests;

public sealed class ContextualCorrectionModelTests(ITestOutputHelper output)
{
    [LocalModelFact]
    public async Task LocalModelDistinguishesLabelsTyposAndSentences()
    {
        using var service = new LocalLlamaTextCorrectionService(
            Environment.GetEnvironmentVariable("KILUPE_TEST_MODEL")!, contextAwareCorrection: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var examples = new (string Original, string? Expected)[]
        {
            ("Datei öffnen", null),
            ("Datei Bearbeiten Ansicht Hilfe", null),
            ("Einstellugen", "Einstellungen"),
            ("Du hat eine Nachricht.", "Du hast eine Nachricht."),
            ("wenn die Anwendung", null),
            ("Einstellungen", null),
            ("Ansicht anpassen", null),
            ("Werkzeuge Fenster Hilfe", null),
            ("Speichren", "Speichern"),
            ("Wir ist heute fertig.", "Wir sind heute fertig.")
        };
        foreach (var (original, expected) in examples)
        {
            var result = await service.CorrectAsync(original, timeout.Token);
            output.WriteLine($"{original} -> {result?.CorrectedText ?? "(unveraendert)"}");
            Assert.Equal(expected, result?.CorrectedText);
        }
    }
}

public sealed class LocalModelFactAttribute : FactAttribute
{
    public LocalModelFactAttribute()
    {
        if (!System.IO.File.Exists(Environment.GetEnvironmentVariable("KILUPE_TEST_MODEL")))
            Skip = "Set KILUPE_TEST_MODEL to a local Qwen2.5-3B-Instruct GGUF to run inference checks.";
    }
}
