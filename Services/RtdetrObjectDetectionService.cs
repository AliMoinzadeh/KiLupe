using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KiLupeDemo.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace KiLupeDemo.Services;

public sealed class RtdetrObjectDetectionService : IAnalysisService, IDisposable
{
    private const int InputSize = 640;
    private const float ConfidenceThreshold = 0.45f;
    private static readonly string[] FallbackLabels =
    {
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
        "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
        "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
        "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
        "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
        "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    };

    private readonly InferenceSession? session;
    private readonly IReadOnlyList<string> labels;

    public RtdetrObjectDetectionService(
        string? modelPath = null,
        InferenceProviderKind provider = InferenceProviderKind.Auto)
    {
        ModelPath = modelPath ?? FindDefaultModelPath();
        labels = LoadLabels(ModelPath);

        if (!File.Exists(ModelPath))
        {
            StatusText = $"RT-DETR-ONNX-Modell fehlt: {ModelPath}";
            return;
        }

        try
        {
            session = InferenceProviderResolver.CreateSession(
                ModelPath,
                provider,
                out var effectiveProvider);
            StatusText = $"RT-DETR bereit ({effectiveProvider}).";
        }
        catch (Exception exception)
        {
            StatusText = $"RT-DETR-Modell konnte nicht geladen werden: {exception.Message}";
        }
    }

    public string Name => "RT-DETR-Objekterkennung";

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
            Path.Combine(AppContext.BaseDirectory, "rtdetr_v2_r18vd-ONNX", "onnx", "model.onnx"),
            Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "models", "rtdetr_v2_r18vd-ONNX", "onnx", "model.onnx")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
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
        var logits = outputs
            .FirstOrDefault(output => string.Equals(output.Name, "logits", StringComparison.OrdinalIgnoreCase))
            ?.AsTensor<float>();
        var boxes = outputs
            .FirstOrDefault(output => string.Equals(output.Name, "pred_boxes", StringComparison.OrdinalIgnoreCase))
            ?.AsTensor<float>();
        if (logits is null || boxes is null)
        {
            return Array.Empty<AnalysisResult>();
        }

        return RtdetrOutputParser.Parse(
            logits,
            boxes,
            image.PixelWidth,
            image.PixelHeight,
            labels,
            ConfidenceThreshold);
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

    private static IReadOnlyList<string> LoadLabels(string modelPath)
    {
        var configPath = FindConfigPath(modelPath);
        if (configPath is null)
        {
            return FallbackLabels;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!document.RootElement.TryGetProperty("id2label", out var id2Label))
            {
                return FallbackLabels;
            }

            var labels = FallbackLabels.ToList();
            foreach (var property in id2Label.EnumerateObject())
            {
                if (!int.TryParse(property.Name, out var index) || index < 0)
                {
                    continue;
                }

                while (labels.Count <= index)
                {
                    labels.Add($"Klasse {labels.Count}");
                }

                labels[index] = property.Value.GetString() ?? $"Klasse {index}";
            }

            return labels;
        }
        catch (JsonException)
        {
            return FallbackLabels;
        }
        catch (IOException)
        {
            return FallbackLabels;
        }
    }

    private static string? FindConfigPath(string modelPath)
    {
        var modelDirectory = Path.GetDirectoryName(modelPath);
        if (modelDirectory is null)
        {
            return null;
        }

        var candidates = new List<string>
        {
            Path.Combine(modelDirectory, "config.json")
        };
        var parentDirectory = Directory.GetParent(modelDirectory);
        if (parentDirectory is not null)
        {
            candidates.Add(Path.Combine(parentDirectory.FullName, "config.json"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}