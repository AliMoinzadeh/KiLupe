using KiLupeDemo.Services;
using Xunit;
namespace KiLupeDemo.Tests;
public sealed class SelectedTextReaderTests
{
    [Fact]
    public void UnsupportedControlIsAnExpectedResult()
    {
        var result = SelectedTextReader.ValidateSelection(null);
        Assert.False(result.Success);
        Assert.Null(result.Text);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }
    [Theory]
    [InlineData("")]
    [InlineData(" \r\n")]
    public void EmptySelectionIsAnExpectedResult(string text)
    {
        Assert.False(SelectedTextReader.ValidateSelection(new[] { text }).Success);
    }
    [Fact]
    public void DoesNotJoinUnrelatedSelections()
    {
        Assert.False(SelectedTextReader.ValidateSelection(new[] { "eins", "zwei" }).Success);
    }
    [Fact]
    public void PreservesSelectedParagraphExactly()
    {
        const string text = "  Ein Satz.\r\nDer folgende Satz.  ";
        var result = SelectedTextReader.ValidateSelection(new[] { text });
        Assert.True(result.Success);
        Assert.Equal(text, result.Text);
    }
    [Fact]
    public void RejectsTooLongSelectionInsteadOfTruncatingIt()
    {
        Assert.False(SelectedTextReader.ValidateSelection(new[] { new string('x', 20001) }).Success);
    }
}
