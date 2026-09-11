using System.IO;
using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;
public sealed class StartupConfigurationTests
{
    [Fact]
    public void MissingFileUsesDefaults()
    {
        var result = StartupConfigurationLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.Equal(AnalysisConfiguration.Default, result.Configuration);
    }
    [Fact]
    public void LoadsConfiguredModelsAndProvider()
    {
        var result = Load("""{"objectModel":"RtDetr","textCorrectionModel":"LocalLlm","provider":"Cpu"}""");
        Assert.Equal(new AnalysisConfiguration(ObjectModelKind.RtDetr, TextCorrectionModelKind.LocalLlm, InferenceProviderKind.Cpu), result.Configuration);
        Assert.Null(result.Warning);
    }
    [Fact]
    public void MissingFieldsKeepDefaults()
    {
        var result = Load("""{"textCorrectionModel":"GermanSpelling"}""");
        Assert.Equal(AnalysisConfiguration.Default with { TextCorrectionModel = TextCorrectionModelKind.GermanSpelling }, result.Configuration);
    }
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("""{"objectModel":"Typo"}""")]
    [InlineData("""{"provider":42}""")]
    [InlineData("""{"provider":"42"}""")]
    [InlineData("""{"textCorrectionMode":"LocalLlm"}""")]
    public void InvalidConfigurationFallsBackWithWarning(string json)
    {
        var result = Load(json);
        Assert.Equal(AnalysisConfiguration.Default, result.Configuration);
        Assert.False(string.IsNullOrWhiteSpace(result.Warning));
    }
    private static StartupConfigurationResult Load(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try { File.WriteAllText(path, json); return StartupConfigurationLoader.Load(path); }
        finally { File.Delete(path); }
    }
}
