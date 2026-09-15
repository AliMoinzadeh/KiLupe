using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;
public sealed class PredictionSafetyTests
{
    [Theory]
    [InlineData("notepad.exe; OUTLOOK.exe", "notepad")]
    [InlineData("notepad.exe; OUTLOOK.exe", "outlook")]
    [InlineData("  Code.exe, firefox.exe ", "FIREFOX")]
    public void ExclusionsMatchProcessNamesRegardlessOfCase(string configured, string process)
        => Assert.Contains(process, (new PredictionSettings { ExcludedProcesses = configured }).Exclusions);
    [Fact]
    public void SettingsRoundTripKeepsConsentOffAndPreservesChoices()
    {
        var settings = new PredictionSettings { PauseMilliseconds = 1500, AcceptKey = Key.P, ExcludedProcesses = "outlook.exe" };
        var loaded = JsonSerializer.Deserialize<PredictionSettings>(JsonSerializer.Serialize(settings))!;
        Assert.False(loaded.Enabled);
        Assert.False(loaded.AllowInsertion);
        Assert.Equal(1500, loaded.PauseMilliseconds);
        Assert.Equal(Key.P, loaded.AcceptKey);
        Assert.Contains("outlook", loaded.Exclusions);
    }
    [Theory]
    [InlineData(0)] [InlineData(299)] [InlineData(5001)]
    public void RejectsInvalidPause(int delay) => Assert.False((new PredictionSettings { PauseMilliseconds = delay }).IsValid);
    [Fact]
    public void RejectsShortcutThatWouldStealExistingMagnifierShortcut()
        => Assert.False((new PredictionSettings { AcceptKey = Key.L }).IsValid);
    [Fact]
    public void ClearsCompletedAndInFlightSuggestionsWhenDisabled()
    {
        var session = new PredictionSession();
        var snapshot = new CaretSnapshot(new IntPtr(1), "id", "notepad", "Hallo ", "", new Rect(0, 0, 1, 20));
        session.Observe(snapshot, 0, 300);
        var generation = session.Observe(snapshot, 300, 300)!.Value;
        session.Complete(generation, "Welt");
        session.Clear();
        Assert.False(session.Complete(generation, "spaete Antwort"));
        Assert.Null(session.Take(snapshot, true));
    }
    [Theory]
    [InlineData("restlicher Satz", false)]
    [InlineData("\nNaechster Satz", true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    public void UsesInlineOnlyWhenNoTextFollowsOnCurrentLine(string after, bool expected)
        => Assert.Equal(expected, new CaretSnapshot(new IntPtr(1), "id", "test", "Hallo", after, new Rect(0, 0, 1, 20)).IsLineEnd);
    [Fact]
    public async Task MissingPredictionModelReportsFailureInsteadOfInventingCompletion()
    {
        using var model = new LocalLlamaTextCorrectionService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.gguf"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.PredictAsync("Hallo ", "", CancellationToken.None));
    }
    [Fact]
    public async Task CancelledPredictionNeverLoadsModel()
    {
        using var model = new LocalLlamaTextCorrectionService("missing.gguf");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.PredictAsync("Hallo ", "", new CancellationToken(true)));
    }
    [Theory]
    [InlineData("text\ttext")]
    [InlineData("```text```")]
    [InlineData("<|im_start|>user")]
    public void RejectsControlCharactersAndProtocolMarkup(string response) => Assert.Empty(PredictionText.Normalize(response));
}
