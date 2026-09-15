using System.IO;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public static class AnalysisServiceFactory
{
    public static IReadOnlyList<IAnalysisService> CreateDefaultServices()
    {
        return CreateServices(AnalysisConfiguration.Default);
    }

    public static IReadOnlyList<IAnalysisService> CreateServices(
        AnalysisConfiguration configuration,
        string? modelRoot = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var catalog = ModelCatalog.Create(
            modelRoot,
            includeFallbackRoots: modelRoot is null);
        var objectModel = catalog.ObjectModels.Single(option => option.Id == configuration.ObjectModel);
        var objectModelPath = objectModel.ModelPath
            ?? GetExpectedObjectModelPath(configuration.ObjectModel, modelRoot);
        IAnalysisService objectService = configuration.ObjectModel switch
        {
            ObjectModelKind.RtDetr => new RtdetrObjectDetectionService(objectModelPath, configuration.Provider),
            _ => new OnnxObjectDetectionService(objectModelPath, configuration.Provider)
        };

        return new IAnalysisService[]
        {
            objectService,
            new LocalTextAnalysisService()
        };
    }

    public static ITextCorrectionService? CreateTextCorrectionService(
        AnalysisConfiguration configuration,
        string? modelRoot = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.TextCorrectionModel == TextCorrectionModelKind.None)
        {
            return null;
        }

        var catalog = ModelCatalog.Create(
            modelRoot,
            includeFallbackRoots: modelRoot is null);
        var option = catalog.TextCorrectionModels.Single(model =>
            model.Id == configuration.TextCorrectionModel);
        var modelPath = option.ModelPath
            ?? GetExpectedCorrectionModelPath(configuration.TextCorrectionModel, modelRoot);
        return configuration.TextCorrectionModel switch
        {
            TextCorrectionModelKind.GermanSpelling => new OnnxTextCorrectionService(
                modelPath,
                Path.GetDirectoryName(modelPath),
                configuration.Provider),
            TextCorrectionModelKind.LocalLlm => new LocalLlamaTextCorrectionService(
                option.ModelPath
                    ?? GetExpectedCorrectionModelPath(configuration.TextCorrectionModel, modelRoot),
                configuration.ContextAwareCorrection),
            _ => throw new InvalidOperationException(
                $"Unbekanntes Textkorrekturmodell: {configuration.TextCorrectionModel}")
        };
    }

    public static AnalysisCoordinator CreateDefault()
    {
        return new AnalysisCoordinator(CreateDefaultServices());
    }

    private static string GetExpectedObjectModelPath(
        ObjectModelKind model,
        string? modelRoot)
    {
        var root = modelRoot ?? AppContext.BaseDirectory;
        return model switch
        {
            ObjectModelKind.RtDetr => Path.Combine(
                root,
                "rtdetr_v2_r18vd-ONNX",
                "onnx",
                "model.onnx"),
            _ => Path.Combine(root, "yolov8n.onnx")
        };
    }

    private static string GetExpectedCorrectionModelPath(
        TextCorrectionModelKind model,
        string? modelRoot)
    {
        var root = modelRoot ?? AppContext.BaseDirectory;
        return model switch
        {
            TextCorrectionModelKind.LocalLlm => Path.Combine(
                root,
                "artifacts",
                "models",
                "qwen2.5-3b-instruct",
                "Qwen2.5-3B-Instruct-Q4_K_M.gguf"),
            _ => Path.Combine(
                root,
                "german-spelling-correction-onnx",
                "model.onnx")
        };
    }
}