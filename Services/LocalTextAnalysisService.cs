using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using KiLupeDemo.Models;
using Tesseract;
using WeCantSpell.Hunspell;
using WpfRect = System.Windows.Rect;

namespace KiLupeDemo.Services;

public sealed class LocalTextAnalysisService : IAnalysisService, IDisposable
{
    private readonly TesseractEngine? engine;
    private readonly WordList? germanDictionary;
    private readonly WordList? englishDictionary;
    private readonly SpellingChecker spellingChecker;
    private readonly object engineLock = new();

    static LocalTextAnalysisService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public LocalTextAnalysisService(
        string? dataDirectory = null,
        string? dictionaryDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        DictionaryDirectory = dictionaryDirectory ?? Path.Combine(AppContext.BaseDirectory, "dictionaries");
        var germanData = Path.Combine(DataDirectory, "deu.traineddata");
        var englishData = Path.Combine(DataDirectory, "eng.traineddata");

        if (!File.Exists(germanData) || !File.Exists(englishData))
        {
            spellingChecker = new SpellingChecker(null, null);
            StatusText = $"Sprachdaten fehlen: {DataDirectory}; Woerterbuchpfad: {DictionaryDirectory}";
            return;
        }

        try
        {
            engine = new TesseractEngine(DataDirectory, "deu+eng", EngineMode.Default);
            germanDictionary = LoadDictionary("de_DE");
            englishDictionary = LoadDictionary("en_US");
            spellingChecker = new SpellingChecker(
                germanDictionary is null ? null : germanDictionary.Check,
                englishDictionary is null ? null : englishDictionary.Check);
            StatusText = BuildStatusText();
        }
        catch (Exception exception)
        {
            spellingChecker = new SpellingChecker(null, null);
            StatusText = $"OCR konnte nicht geladen werden: {exception.Message}";
        }
    }

    public string Name => "OCR und Rechtschreibung";

    public string DataDirectory { get; }

    public string DictionaryDirectory { get; }

    public string StatusText { get; }

    public bool IsAvailable => engine is not null;

    public Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(
        BitmapSource image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (engine is null || cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<IReadOnlyList<AnalysisResult>>(Array.Empty<AnalysisResult>());
        }

        return Task.Run(() =>
        {
            lock (engineLock)
            {
                if (cancellationToken.IsCancellationRequested)
                    return (IReadOnlyList<AnalysisResult>)Array.Empty<AnalysisResult>();
                return Analyze(image, cancellationToken);
            }
        });
    }

    public void Dispose()
    {
        lock (engineLock)
        {
            engine?.Dispose();
        }
    }

    private WordList? LoadDictionary(string name)
    {
        var affixPath = Path.Combine(DictionaryDirectory, $"{name}.aff");
        var dictionaryPath = Path.Combine(DictionaryDirectory, $"{name}.dic");
        if (!File.Exists(affixPath) || !File.Exists(dictionaryPath))
        {
            return null;
        }

        try
        {
            return WordList.CreateFromFiles(dictionaryPath, affixPath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private string BuildStatusText()
    {
        return (germanDictionary is not null, englishDictionary is not null) switch
        {
            (true, true) => "OCR und Rechtschreibpruefung bereit.",
            (true, false) => "OCR bereit; deutsches Woerterbuch geladen, englisches Woerterbuch fehlt.",
            (false, true) => "OCR bereit; englisches Woerterbuch geladen, deutsches Woerterbuch fehlt.",
            _ => $"OCR bereit; Woerterbuecher fehlen noch: {DictionaryDirectory}"
        };
    }

    private IReadOnlyList<AnalysisResult> Analyze(
        BitmapSource image,
        CancellationToken cancellationToken)
    {
        var pngBytes = EncodePng(image);
        using var pix = Pix.LoadFromMemory(pngBytes);
        using var page = ProcessPage(pix);
        using var iterator = page.GetIterator();
        iterator.Begin();

        var results = new List<AnalysisResult>();
        do
        {
            if (cancellationToken.IsCancellationRequested)
                return Array.Empty<AnalysisResult>();
            var word = iterator.GetText(PageIteratorLevel.Word)?.Trim();
            if (string.IsNullOrWhiteSpace(word)
                || !iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
            {
                continue;
            }

            var confidence = Math.Clamp(iterator.GetConfidence(PageIteratorLevel.Word) / 100.0, 0, 1);
            var imageBounds = new WpfRect(
                bounds.X1,
                bounds.Y1,
                Math.Max(1, bounds.X2 - bounds.X1),
                Math.Max(1, bounds.Y2 - bounds.Y1));
            results.Add(new AnalysisResult(
                AnalysisKind.Text,
                word,
                confidence,
                imageBounds,
                "Tesseract"));

            if (LooksMisspelled(word))
            {
                results.Add(new AnalysisResult(
                    AnalysisKind.Spelling,
                    word,
                    confidence,
                    imageBounds,
                    string.Join(" / ", (germanDictionary?.Suggest(word) ?? Enumerable.Empty<string>())
                        .Concat(englishDictionary?.Suggest(word) ?? Enumerable.Empty<string>())
                        .Distinct().Take(3).DefaultIfEmpty("Kein Vorschlag verfuegbar"))));
            }
        }
        while (iterator.Next(PageIteratorLevel.Word));

        return cancellationToken.IsCancellationRequested ? Array.Empty<AnalysisResult>() : results;
    }

    private Page ProcessPage(Pix pix)
    {
        lock (engineLock)
        {
            return engine!.Process(pix);
        }
    }

    private bool LooksMisspelled(string word)
    {
        return spellingChecker.IsMisspelled(word);
    }

    private static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}