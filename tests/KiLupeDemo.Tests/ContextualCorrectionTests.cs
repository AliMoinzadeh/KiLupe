using System.IO;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class ContextualCorrectionTests
{
    [Theory]
    [InlineData("Datei öffnen", "fragment", "Datei öffnen.", null)]
    [InlineData("Datei öffnen", "fragment", "Bitte Datei öffnen", null)]
    [InlineData("Speichern", "sentence", "Speichern.", null)]
    [InlineData("Du hat", "fragment", "Du hast", null)]
    [InlineData("Einstellugen", "fragment", "Einstellungen", "Einstellungen")]
    [InlineData("Einstellugen", "uncertain", "Einstellungen", "Einstellungen")]
    [InlineData("Du hat eine Nachricht.", "sentence", "Du hast eine Nachricht.", "Du hast eine Nachricht.")]
    public void AppliesOnlyCorrectionsAllowedByContext(string original, string context, string corrected, string? expected)
    {
        var response = System.Text.Json.JsonSerializer.Serialize(new { context, text = corrected });
        var result = ContextualCorrectionPolicy.ReadCorrection(original, response,
            word => word == "Einstellugen");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"context\":\"other\",\"text\":\"Test\"}")]
    [InlineData("{\"context\":\"sentence\"}")]
    [InlineData("{\"context\":\"sentence\",\"text\":42}")]
    public void InvalidResponsesProduceNoCorrection(string response)
    {
        Assert.Null(ContextualCorrectionPolicy.ReadCorrection("Original", response, _ => true));
    }

    [Fact]
    public void ContextOptionLoadsFromConfiguration()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, """{"textCorrectionModel":"LocalLlm","contextAwareCorrection":true}""");
            var result = StartupConfigurationLoader.Load(path);
            Assert.Null(result.Warning);
            Assert.True(result.Configuration.ContextAwareCorrection);
        }
        finally { File.Delete(path); }
    }
}
