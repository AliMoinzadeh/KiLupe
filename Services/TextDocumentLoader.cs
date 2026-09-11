using System.IO;
using System.Text;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public sealed class TextDocumentLoader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static IReadOnlyList<string> SupportedExtensions { get; } = new[]
    {
        ".txt",
        ".md",
        ".log",
        ".csv",
        ".json",
        ".xml"
    };

    public const long MaximumFileBytes = 10L * 1024 * 1024;

    public static bool IsSupportedExtension(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var extension = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(
            extension,
            StringComparer.OrdinalIgnoreCase);
    }

    public TextDocument Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Textdatei wurde nicht gefunden: {fullPath}",
                fullPath);
        }

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"Textdatei ist groesser als {MaximumFileBytes / (1024 * 1024)} MB.");
        }

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var reader = new StreamReader(
            stream,
            StrictUtf8,
            detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var lines = text.Split(
            new[] { "\r\n", "\n", "\r" },
            StringSplitOptions.None);
        return new TextDocument(fullPath, text, lines);
    }
}
