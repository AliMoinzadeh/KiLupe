using System.IO;
using KiLupeDemo.Services;
using System.Windows.Media.Imaging;
using WeCantSpell.Hunspell;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class LocalTextAnalysisServiceTests
{
    [Fact]
    public void MissingLanguageDataIsReportedAsUnavailable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"KiLupeDemo-{Guid.NewGuid():N}");
        using var service = new LocalTextAnalysisService(root);

        Assert.False(service.IsAvailable);
        Assert.Contains("Sprachdaten fehlen", service.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfiguredDictionaryDirectoryIsPreservedWhenLanguageDataIsMissing()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"KiLupeDemo-tessdata-{Guid.NewGuid():N}");
        var dictionaryDirectory = Path.Combine(Path.GetTempPath(), $"KiLupeDemo-dictionaries-{Guid.NewGuid():N}");
        using var service = new LocalTextAnalysisService(dataDirectory, dictionaryDirectory);

        Assert.Equal(dataDirectory, service.DataDirectory);
        Assert.Equal(dictionaryDirectory, service.DictionaryDirectory);
        Assert.Contains(dictionaryDirectory, service.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DownloadedLanguageDataMakesTheLocalServiceAvailable()
    {
        var dataDirectory = FindWorkspaceDirectory("tessdata");
        var dictionaryDirectory = FindWorkspaceDirectory("dictionaries");
        Assert.True(File.Exists(Path.Combine(dataDirectory, "deu.traineddata")));
        Assert.True(File.Exists(Path.Combine(dataDirectory, "eng.traineddata")));
        Assert.True(File.Exists(Path.Combine(dictionaryDirectory, "de_DE.aff")));
        Assert.True(File.Exists(Path.Combine(dictionaryDirectory, "en_US.aff")));

        using var service = new LocalTextAnalysisService(dataDirectory, dictionaryDirectory);

        Assert.True(service.IsAvailable, service.StatusText);
        Assert.Contains("Rechtschreibpruefung", service.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DownloadedDictionariesCanBeLoadedIndividually()
    {
        var dictionaryDirectory = FindWorkspaceDirectory("dictionaries");
        foreach (var name in new[] { "de_DE", "en_US" })
        {
            var affixPath = Path.Combine(dictionaryDirectory, $"{name}.aff");
            var dictionaryPath = Path.Combine(dictionaryDirectory, $"{name}.dic");
            var dictionary = WordList.CreateFromFiles(dictionaryPath, affixPath);

            Assert.NotNull(dictionary);
        }
    }

    [Fact]
    public async Task OcrReturnsTextAndSpellingResultsForFixture()
    {
        var imagePath = FindWorkspaceFile(Path.Combine("artifacts", "fixtures", "text-spelling.png"));
        Assert.True(File.Exists(imagePath), $"OCR-Fixture fehlt: {imagePath}");

        var dataDirectory = FindWorkspaceDirectory("tessdata");
        var dictionaryDirectory = FindWorkspaceDirectory("dictionaries");
        using var service = new LocalTextAnalysisService(dataDirectory, dictionaryDirectory);
        using var stream = File.OpenRead(imagePath);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        var results = await service.AnalyzeAsync(image, CancellationToken.None);

        Assert.Contains(results, result =>
            result.Kind == Models.AnalysisKind.Text
            && result.Label.Contains("Dies", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, result =>
            result.Kind == Models.AnalysisKind.Spelling
            && result.Label.Contains("Sazt", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindWorkspaceDirectory(string name)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, name);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), name);
    }

    private static string FindWorkspaceFile(string relativePath)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), relativePath);
    }
}