using KiLupeDemo.Services;
using Microsoft.ML.OnnxRuntime.Tensors;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class RtdetrOutputParserTests
{
    [Fact]
    public void ParsesNormalizedBoxesAndDropsLowConfidenceQueries()
    {
        var logits = new DenseTensor<float>(
            new[]
            {
                4f, -2f, -3f,
                -4f, -4f, -1f
            },
            new[] { 1, 2, 3 });
        var boxes = new DenseTensor<float>(
            new[]
            {
                0.5f, 0.5f, 0.4f, 0.2f,
                0.5f, 0.5f, 0.2f, 0.2f
            },
            new[] { 1, 2, 4 });

        var results = RtdetrOutputParser.Parse(
            logits,
            boxes,
            sourceWidth: 200,
            sourceHeight: 100,
            labels: new[] { "person", "cat", "dog" },
            confidenceThreshold: 0.45f);

        var result = Assert.Single(results);
        Assert.Equal("person", result.Label);
        Assert.Equal(60, result.Bounds.X, precision: 3);
        Assert.Equal(40, result.Bounds.Y, precision: 3);
        Assert.Equal(80, result.Bounds.Width, precision: 3);
        Assert.Equal(20, result.Bounds.Height, precision: 3);
        Assert.InRange(result.Confidence, 0.98, 0.99);
    }
}