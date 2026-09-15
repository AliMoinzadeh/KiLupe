using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;
using Xunit.Abstractions;
namespace KiLupeDemo.Tests;
public class LoadedTextCorrectionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ConfiguredModelCorrectsLoadedTextFile()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".txt");
        try
        {
            System.IO.File.WriteAllText(path, "Das ist ein Sazt mit einem Feler.");
            var document = new TextDocumentLoader().Load(path);
            using var service = AnalysisServiceFactory.CreateTextCorrectionService(
                AnalysisConfiguration.Default with { TextCorrectionModel = TextCorrectionModelKind.GermanSpelling });
            output.WriteLine(service?.StatusText ?? "No service");
            Assert.NotNull(service);
            Assert.True(service.IsAvailable, service.StatusText);
            var suggestions = await new CorrectionCoordinator(service).CreateSuggestionsAsync(document.Lines, CancellationToken.None);
            foreach (var suggestion in suggestions) output.WriteLine(suggestion.CorrectedText);
            Assert.NotEmpty(suggestions);
        }
        finally { System.IO.File.Delete(path); }
    }
}
