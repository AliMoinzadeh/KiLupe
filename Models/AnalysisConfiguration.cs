namespace KiLupeDemo.Models;

public enum ObjectModelKind
{
    YoloV8N,
    RtDetr
}

public enum TextCorrectionModelKind
{
    None,
    GermanSpelling,
    LocalLlm
}

public enum InferenceProviderKind
{
    Auto,
    DirectMl,
    Cpu
}

public sealed record AnalysisConfiguration(
    ObjectModelKind ObjectModel,
    TextCorrectionModelKind TextCorrectionModel,
    InferenceProviderKind Provider)
{
    public bool ContextAwareCorrection { get; init; }

    public static AnalysisConfiguration Default { get; } =
        new(ObjectModelKind.YoloV8N, TextCorrectionModelKind.None, InferenceProviderKind.Auto);
}