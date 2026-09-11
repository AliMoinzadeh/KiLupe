using System.Windows;
using KiLupeDemo.Models;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace KiLupeDemo.Services;

public static class RtdetrOutputParser
{
    public static IReadOnlyList<AnalysisResult> Parse(
        Tensor<float> logits,
        Tensor<float> boxes,
        int sourceWidth,
        int sourceHeight,
        IReadOnlyList<string> labels,
        float confidenceThreshold)
    {
        ArgumentNullException.ThrowIfNull(logits);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(labels);

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return Array.Empty<AnalysisResult>();
        }

        var logitsDimensions = logits.Dimensions.ToArray();
        var boxDimensions = boxes.Dimensions.ToArray();
        if (logitsDimensions.Length != 3
            || boxDimensions.Length != 3
            || logitsDimensions[0] != 1
            || boxDimensions[0] != 1)
        {
            return Array.Empty<AnalysisResult>();
        }

        var boxQueryFirst = boxDimensions[2] == 4;
        var queryCount = boxQueryFirst ? boxDimensions[1] : boxDimensions[2];
        var boxValuesPerQuery = boxQueryFirst ? boxDimensions[2] : boxDimensions[1];
        if (boxValuesPerQuery != 4 || queryCount <= 0)
        {
            return Array.Empty<AnalysisResult>();
        }

        var logitsQueryFirst = logitsDimensions[1] == queryCount;
        var logitsQueryDimension = logitsQueryFirst ? logitsDimensions[1] : logitsDimensions[2];
        var classCount = logitsQueryFirst ? logitsDimensions[2] : logitsDimensions[1];
        if (logitsQueryDimension != queryCount || classCount <= 0)
        {
            return Array.Empty<AnalysisResult>();
        }

        var logitsValues = logits.ToArray();
        var boxValues = boxes.ToArray();
        var results = new List<AnalysisResult>();
        for (var query = 0; query < queryCount; query++)
        {
            var bestClass = -1;
            var bestScore = 0f;
            for (var classIndex = 0; classIndex < classCount; classIndex++)
            {
                var logit = ReadMatrixValue(
                    logitsValues,
                    logitsQueryFirst,
                    logitsDimensions[2],
                    query,
                    classIndex);
                var score = Sigmoid(logit);
                if (score > bestScore)
                {
                    bestClass = classIndex;
                    bestScore = score;
                }
            }

            if (bestClass < 0 || bestScore < confidenceThreshold)
            {
                continue;
            }

            var centerX = ReadMatrixValue(
                boxValues,
                boxQueryFirst,
                boxDimensions[2],
                query,
                0);
            var centerY = ReadMatrixValue(
                boxValues,
                boxQueryFirst,
                boxDimensions[2],
                query,
                1);
            var width = ReadMatrixValue(
                boxValues,
                boxQueryFirst,
                boxDimensions[2],
                query,
                2);
            var height = ReadMatrixValue(
                boxValues,
                boxQueryFirst,
                boxDimensions[2],
                query,
                3);
            var left = Math.Clamp((centerX - width / 2) * sourceWidth, 0, sourceWidth);
            var top = Math.Clamp((centerY - height / 2) * sourceHeight, 0, sourceHeight);
            var right = Math.Clamp((centerX + width / 2) * sourceWidth, 0, sourceWidth);
            var bottom = Math.Clamp((centerY + height / 2) * sourceHeight, 0, sourceHeight);
            if (right <= left || bottom <= top)
            {
                continue;
            }

            var label = bestClass < labels.Count
                ? labels[bestClass]
                : $"Klasse {bestClass}";
            results.Add(new AnalysisResult(
                AnalysisKind.Object,
                label,
                bestScore,
                new Rect(left, top, right - left, bottom - top),
                "RT-DETR-ONNX"));
        }

        return results;
    }

    private static float ReadMatrixValue(
        float[] values,
        bool queryFirst,
        int secondDimension,
        int query,
        int column)
    {
        return queryFirst
            ? values[query * secondDimension + column]
            : values[column * secondDimension + query];
    }

    private static float Sigmoid(float value)
    {
        return 1f / (1f + MathF.Exp(-value));
    }
}