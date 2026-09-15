using System.Diagnostics;
using System.Text.Json;
using KiLupeDemo.Models;
using KiLupeDemo.Services;

public static class PredictionQualityCheck
{
    public static readonly (string Id, string Before, string After)[] Cases =
    {
        ("reported", "Warum sollte ich vertrauen auf deine korrektheit? du hast gerade selber einen Fehler ", ""),
        ("past_tense", "Ich habe meinen Schlüssel zu Hause ", ""),
        ("conditional", "Wenn es morgen regnet, ", ""),
        ("request", "Könntest du mir bitte ", ""),
        ("deadline", "Bitte schicken Sie mir die Unterlagen bis ", ""),
        ("partial_word", "Ich freue mich dar", ""),
        ("middle", "Wir treffen uns ", "um 15 Uhr."),
        ("english", "Thank you for your message. I will ", ""),
        ("passive", "Die Rechnung wurde gestern ", ""),
        ("causal", "Das ist kein Problem, weil ", "")
    };

    public static int Run()
    {
        var path = ModelCatalog.Create().TextCorrectionModels.Single(item => item.Id == TextCorrectionModelKind.LocalLlm).ModelPath!;
        using var model = new LocalLlamaTextCorrectionService(path);
        foreach (var item in Cases)
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var watch = Stopwatch.StartNew();
            var raw = model.PredictAsync(item.Before, item.After, cancel.Token).GetAwaiter().GetResult();
            var insertion = PredictionText.GetInsertion(raw, item.Before, item.After);
            Console.WriteLine("QUALITY " + JsonSerializer.Serialize(new
            {
                item.Id, item.Before, item.After, Raw = raw, Insertion = insertion,
                Combined = item.Before + insertion + item.After, Seconds = watch.Elapsed.TotalSeconds
            }));
        }
        return 0;
    }
}
