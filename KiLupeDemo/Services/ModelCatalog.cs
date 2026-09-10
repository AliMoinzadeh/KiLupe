using System.IO;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public sealed record ModelCatalogOption<TId>(
    TId Id,
    string DisplayName,
    string? ModelPath,
    bool IsAvailable,
    string StatusText);

public sealed class ModelCatalog
{
    private ModelCatalog(
        IReadOnlyList<ModelCatalogOption<ObjectModelKind>> objectModels,
        IReadOnlyList<ModelCatalogOption<TextCorrectionModelKind>> textCorrectionModels)
    {
        ObjectModels = objectModels;
        TextCorrectionModels = textCorrectionModels;
    }

    public IReadOnlyList<ModelCatalogOption<ObjectModelKind>> ObjectModels { get; }

    public IReadOnlyList<ModelCatalogOption<TextCorrectionModelKind>> TextCorrectionModels { get; }

    public static ModelCatalog Create(
        string? rootDirectory = null,
        bool includeFallbackRoots = true)
    {
        var roots = GetSearchRoots(rootDirectory, includeFallbackRoots);
        var yoloPath = FindFile(roots, "yolov8n.onnx");
        var rtdetrPath = FindFile(
            roots,
            Path.Combine("rtdetr_v2_r18vd-ONNX", "onnx", "model.onnx"),
            Path.Combine("artifacts", "models", "rtdetr_v2_r18vd-ONNX", "onnx", "model.onnx"));
        var correctionPath = FindFile(
            roots,
            Path.Combine("german-spelling-correction-onnx", "model.onnx"),
            Path.Combine("artifacts", "models", "german-spelling-correction-onnx", "onnx", "model.onnx"),
            Path.Combine("artifacts", "models", "german-spelling-correction-onnx", "model.onnx"));
        var localLlmPath = FindFile(
            roots,
            Path.Combine(
                "artifacts",
                "models",
                "qwen2.5-3b-instruct",
                "Qwen2.5-3B-Instruct-Q4_K_M.gguf"),
            Path.Combine(
                "qwen2.5-3b-instruct",
                "Qwen2.5-3B-Instruct-Q4_K_M.gguf"));

        var objectModels = new[]
        {
            CreateOption(
                ObjectModelKind.YoloV8N,
                "YOLOv8n",
                yoloPath,
                "yolov8n.onnx"),
            CreateOption(
                ObjectModelKind.RtDetr,
                "RT-DETR",
                rtdetrPath,
                "RT-DETR ONNX")
        };
        var correctionModels = new[]
        {
            new ModelCatalogOption<TextCorrectionModelKind>(
                TextCorrectionModelKind.None,
                "Keine Textkorrektur",
                null,
                true,
                "Deaktiviert."),
            CreateCorrectionOption(correctionPath),
            CreateLocalLlmOption(localLlmPath)
        };

        return new ModelCatalog(objectModels, correctionModels);
    }

    private static ModelCatalogOption<TId> CreateOption<TId>(
        TId id,
        string displayName,
        string? modelPath,
        string missingName)
    {
        return new ModelCatalogOption<TId>(
            id,
            displayName,
            modelPath,
            modelPath is not null,
            modelPath is null ? $"Modell fehlt: {missingName}" : "Verfuegbar.");
    }

    private static ModelCatalogOption<TextCorrectionModelKind> CreateCorrectionOption(
        string? modelPath)
    {
        if (modelPath is null)
        {
            return new ModelCatalogOption<TextCorrectionModelKind>(
                TextCorrectionModelKind.GermanSpelling,
                "Deutsch: Rechtschreibkorrektur",
                null,
                false,
                "Modell fehlt: model.onnx");
        }

        var modelDirectory = Path.GetDirectoryName(modelPath) ?? string.Empty;
        var tokenizerPath = Path.Combine(modelDirectory, "tokenizer.json");
        return new ModelCatalogOption<TextCorrectionModelKind>(
            TextCorrectionModelKind.GermanSpelling,
            "Deutsch: Rechtschreibkorrektur",
            modelPath,
            File.Exists(tokenizerPath),
            File.Exists(tokenizerPath)
                ? "Modell und Tokenizer verfuegbar."
                : $"Tokenizer fehlt: {tokenizerPath}");
    }

    private static ModelCatalogOption<TextCorrectionModelKind> CreateLocalLlmOption(
        string? modelPath)
    {
        return new ModelCatalogOption<TextCorrectionModelKind>(
            TextCorrectionModelKind.LocalLlm,
            "Deutsch: lokales LLM (Qwen 3B)",
            modelPath,
            modelPath is not null,
            modelPath is null
                ? "Modell fehlt: Qwen2.5-3B-Instruct-Q4_K_M.gguf"
                : "Modell verfuegbar.");
    }

    private static IReadOnlyList<string> GetSearchRoots(
        string? rootDirectory,
        bool includeFallbackRoots)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            roots.Add(rootDirectory);
        }

        if (includeFallbackRoots)
        {
            roots.Add(AppContext.BaseDirectory);
            roots.Add(Directory.GetCurrentDirectory());
            roots.AddRange(FindProjectRoots(AppContext.BaseDirectory));
        }

        return roots
        .Where(root => !string.IsNullOrWhiteSpace(root))
        .Select(root => Path.GetFullPath(root!))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    private static IEnumerable<string> FindProjectRoots(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KiLupeDemo.csproj")))
            {
                yield return directory.FullName;
            }

            directory = directory.Parent;
        }
    }

    private static string? FindFile(
        IEnumerable<string> roots,
        params string[] relativePaths)
    {
        foreach (var root in roots)
        {
            foreach (var relativePath in relativePaths)
            {
                var path = Path.Combine(root, relativePath);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }
}