using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using KiLupeDemo.Models;
using WeCantSpell.Hunspell;

namespace KiLupeDemo.Services;

public sealed class TextDocumentSpellingService
{
    private readonly List<WordList> dictionaries = new();
    private readonly SpellingChecker checker;
    private static readonly Regex Words = new(@"\p{L}+(?:['’-]\p{L}+)*", RegexOptions.CultureInvariant);

    public TextDocumentSpellingService(string? dictionaryDirectory = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var root = dictionaryDirectory ?? Path.Combine(AppContext.BaseDirectory, "dictionaries");
        foreach (var name in new[] { "de_DE", "en_US" })
        {
            var path = Path.Combine(root, name + ".dic");
            if (!File.Exists(path) || !File.Exists(Path.ChangeExtension(path, ".aff"))) continue;
            try { dictionaries.Add(WordList.CreateFromFiles(path)); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        checker = new SpellingChecker(dictionaries.Count == 0 ? null : word => dictionaries.Any(dictionary => dictionary.Check(word)), null);
    }

    public bool IsAvailable => dictionaries.Count > 0;

    public IReadOnlyList<AnalysisResult> Analyze(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var results = new List<AnalysisResult>();
        var cache = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (Match word in Words.Matches(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!cache.TryGetValue(word.Value, out var suggestion))
            {
                suggestion = checker.IsMisspelled(word.Value)
                    ? string.Join(" / ", dictionaries.SelectMany(dictionary => dictionary.Suggest(word.Value))
                        .Distinct().Take(3).DefaultIfEmpty("Kein Vorschlag verfuegbar"))
                    : null;
                cache.Add(word.Value, suggestion);
            }
            if (suggestion is null) continue;
            results.Add(new(AnalysisKind.Spelling, word.Value, 1,
                new Rect(word.Index, 0, word.Length, 1), suggestion));
        }
        return results;
    }
}
