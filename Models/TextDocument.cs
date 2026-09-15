namespace KiLupeDemo.Models;

public sealed record TextDocument(
    string FilePath,
    string Text,
    IReadOnlyList<string> Lines);
