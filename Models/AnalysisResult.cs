using System.Windows;

namespace KiLupeDemo.Models;

public enum AnalysisKind
{
    Object,
    Text,
    Spelling,
    Status
}

public sealed record AnalysisResult(
    AnalysisKind Kind,
    string Label,
    double Confidence,
    Rect Bounds,
    string Details);

public sealed record AnalysisSnapshot(
    long RequestId,
    Size SourceSize,
    IReadOnlyList<AnalysisResult> Results,
    string StatusText);