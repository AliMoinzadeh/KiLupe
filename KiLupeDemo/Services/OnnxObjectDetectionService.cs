using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KiLupeDemo.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace KiLupeDemo.Services;

public sealed class OnnxObjectDetectionService : IAnalysisService, IDisposable
{
    private const int InputSize = 640;
    private static readonly string[] CocoLabels =
    {
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
        "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
        "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
        "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
        "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
        " toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    };

    private readonly InferenceSession? session;

    public OnnxObjectDetectionService(
        string? modelPath = null,
        InferenceProviderKind provider = InferenceProviderKind.Auto)
    {
        ModelPath = modelPath ?? FindDefaultModelPath();

        if (!File.Exists(ModelPath))
        {
            var pytorchModelPath = FindRelatedPytorchModelPath(ModelPath);
            StatusText = pytorchModelPath is null
                ? $"ONNX-Modell fehlt: {ModelPath}"
                : $"ONNX-Modell fehlt: {ModelPath}. PyTorch-Modell gefunden: {pytorchModelPath}. Bitte nach ONNX exportieren.";
            return;
        }

        try
        {
            session = InferenceProviderResolver.CreateSession(
                ModelPath,
                provider,
                out var effectiveProvider);
            StatusText = $"Objekterkennung bereit ({effectiveProvider}).";
        }
        catch (Exception exception)
        {
            StatusText = $"ONNX-Modell konnte nicht geladen werden: {exception.Message}";
        }
    }

    public string Name => "Objekterkennung";

    public string ModelPath { get; }

    public string StatusText { get; }

    public bool IsAvailable => session is not null;

    public Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(
        BitmapSource image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (session is null)
        {
            return Task.FromResult<IReadOnlyList<AnalysisResult>>(Array.Empty<AnalysisResult>());
        }

        return Task.Run(() => Analyze(image, cancellationToken), cancellationToken);
    }

    public void Dispose()
    {
        session?.Dispose();
    }

    private static string FindDefaultModelPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "yolov8n.onnx"),
            Path.Combine(Directory.GetCurrentDirectory(), "yolov8n.onnx")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string? FindRelatedPytorchModelPath(string modelPath)
    {
        var relatedPath = Path.ChangeExtension(modelPath, ".pt");
        if (File.Exists(relatedPath))
        {
            return relatedPath;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "yolov8n.pt"),
            Path.Combine(Directory.GetCurrentDirectory(), "yolov8n.pt")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private IReadOnlyList<AnalysisResult> Analyze(
        BitmapSource image,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = CreateInputTensor(image);
        var inputName = session!.InputMetadata.Keys.First();

        using var outputs = session.Run(new[]
        {
            NamedOnnxValue.CreateFromTensor(inputName, input)
        });

        cancellationToken.ThrowIfCancellationRequested();
        var output = outputs.FirstOrDefault()?.AsTensor<float>();
        return output is null
            ? Array.Empty<AnalysisResult>()
            : ParseOutput(output, image.PixelWidth, image.PixelHeight);
    }

    private static DenseTensor<float> CreateInputTensor(BitmapSource image)
    {
        if (image.PixelWidth == 0 || image.PixelHeight == 0)
        {
            throw new InvalidOperationException("Das Bild hat keine gueltige Groesse.");
        }

        var resized = new TransformedBitmap(
            image,
            new ScaleTransform(
                InputSize / (double)image.PixelWidth,
                InputSize / (double)image.PixelHeight));
        resized.Freeze();

        var rgb = new FormatConvertedBitmap(resized, PixelFormats.Rgb24, null, 0);
        rgb.Freeze();
        var pixels = new byte[InputSize * InputSize * 3];
        rgb.CopyPixels(pixels, InputSize * 3, 0);

        var values = new float[InputSize * InputSize * 3];
        var planeSize = InputSize * InputSize;
        for (var y = 0; y < InputSize; y++)
        {
            for (var x = 0; x < InputSize; x++)
            {
                var pixelOffset = (y * InputSize + x) * 3;
                var pixelIndex = y * InputSize + x;
                values[pixelIndex] = pixels[pixelOffset] / 255f;
                values[planeSize + pixelIndex] = pixels[pixelOffset + 1] / 255f;
                values[planeSize * 2 + pixelIndex] = pixels[pixelOffset + 2] / 255f;
            }
        }

        return new DenseTensor<float>(values, new[] { 1, 3, InputSize, InputSize });
    }

    private static IReadOnlyList<AnalysisResult> ParseOutput(
        Tensor<float> output,
        int sourceWidth,
        int sourceHeight)
    {
        var dimensions = output.Dimensions.ToArray();
        if (dimensions.Length != 3 || dimensions[0] != 1)
        {
            return Array.Empty<AnalysisResult>();
        }

        var channelFirst = dimensions[1] <= dimensions[2];
        var channels = channelFirst ? dimensions[1] : dimensions[2];
        var candidates = channelFirst ? dimensions[2] : dimensions[1];
        var objectnessOffset = channels >= 85 ? 5 : 4;
        var classCount = channels - objectnessOffset;
        if (classCount <= 0)
        {
            return Array.Empty<AnalysisResult>();
        }

        var values = output.ToArray();
        var results = new List<AnalysisResult>();
        for (var candidate = 0; candidate < candidates; candidate++)
        {
            var centerX = ReadValue(values, channelFirst, channels, candidates, 0, candidate);
            var centerY = ReadValue(values, channelFirst, channels, candidates, 1, candidate);
            var width = ReadValue(values, channelFirst, channels, candidates, 2, candidate);
            var height = ReadValue(values, channelFirst, channels, candidates, 3, candidate);
            var objectness = objectnessOffset == 5
                ? ReadValue(values, channelFirst, channels, candidates, 4, candidate)
                : 1f;

            var bestClass = -1;
            var bestScore = 0f;
            for (var classIndex = 0; classIndex < classCount; classIndex++)
            {
                var score = ReadValue(
                    values,
                    channelFirst,
                    channels,
                    candidates,
                    objectnessOffset + classIndex,
                    candidate) * objectness;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = classIndex;
                }
            }

            if (bestClass < 0 || bestScore < 0.45f)
            {
                continue;
            }

            var normalized = Math.Abs(centerX) <= 1.5f
                && Math.Abs(centerY) <= 1.5f
                && Math.Abs(width) <= 1.5f
                && Math.Abs(height) <= 1.5f;
            var scaleX = normalized ? sourceWidth : sourceWidth / (double)InputSize;
            var scaleY = normalized ? sourceHeight : sourceHeight / (double)InputSize;
            var left = Math.Clamp((centerX - width / 2) * scaleX, 0, sourceWidth);
            var top = Math.Clamp((centerY - height / 2) * scaleY, 0, sourceHeight);
            var right = Math.Clamp((centerX + width / 2) * scaleX, 0, sourceWidth);
            var bottom = Math.Clamp((centerY + height / 2) * scaleY, 0, sourceHeight);
            if (right <= left || bottom <= top)
            {
                continue;
            }

            var label = bestClass < CocoLabels.Length
                ? CocoLabels[bestClass].Trim()
                : $"Klasse {bestClass}";
            results.Add(new AnalysisResult(
                AnalysisKind.Object,
                label,
                bestScore,
                new System.Windows.Rect(left, top, right - left, bottom - top),
                "YOLO-ONNX"));
        }

        return results;
    }

    private static float ReadValue(
        float[] values,
        bool channelFirst,
        int channels,
        int candidates,
        int channel,
        int candidate)
    {
        return channelFirst
            ? values[channel * candidates + candidate]
            : values[candidate * channels + channel];
    }
}