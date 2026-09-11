using System.IO;
using System.Text;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class TextDocumentLoaderTests
{
    [Fact]
    public void LoadsUtf8TextAndPreservesLines()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "notes.txt");
        const string text = "Erste Zeile\nZweite Zeile\n";
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            var document = new TextDocumentLoader().Load(path);

            Assert.Equal(path, document.FilePath);
            Assert.Equal(text, document.Text);
            Assert.Equal(new[] { "Erste Zeile", "Zweite Zeile", "" }, document.Lines);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadsUtf16LittleEndianWithBom()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "notes.txt");
        const string text = "Deutsche Zeile";
        File.WriteAllText(path, text, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

        try
        {
            var document = new TextDocumentLoader().Load(path);

            Assert.Equal(text, document.Text);
            Assert.Equal(new[] { text }, document.Lines);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadsUtf16BigEndianWithBom()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "notes.txt");
        const string text = "Grosse Ueberschrift";
        File.WriteAllText(path, text, new UnicodeEncoding(bigEndian: true, byteOrderMark: true));

        try
        {
            var document = new TextDocumentLoader().Load(path);

            Assert.Equal(text, document.Text);
            Assert.Equal(new[] { text }, document.Lines);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadsEmptyFileAsOneEmptyDocumentLine()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "empty.md");
        File.WriteAllBytes(path, Array.Empty<byte>());

        try
        {
            var document = new TextDocumentLoader().Load(path);

            Assert.Empty(document.Text);
            Assert.Equal(new[] { "" }, document.Lines);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExposesOnlySupportedTextExtensions()
    {
        Assert.True(TextDocumentLoader.IsSupportedExtension("report.TXT"));
        Assert.True(TextDocumentLoader.IsSupportedExtension("notes.md"));
        Assert.True(TextDocumentLoader.IsSupportedExtension("events.log"));
        Assert.True(TextDocumentLoader.IsSupportedExtension("table.csv"));
        Assert.True(TextDocumentLoader.IsSupportedExtension("data.json"));
        Assert.True(TextDocumentLoader.IsSupportedExtension("layout.xml"));
        Assert.False(TextDocumentLoader.IsSupportedExtension("document.pdf"));
        Assert.False(TextDocumentLoader.IsSupportedExtension("document.docx"));
    }

    [Fact]
    public void RejectsFilesLargerThanConfiguredLimit()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "large.txt");
        using (var stream = File.Create(path))
        {
            stream.SetLength(TextDocumentLoader.MaximumFileBytes + 1);
        }

        try
        {
            Assert.Throws<InvalidDataException>(() => new TextDocumentLoader().Load(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingFileRaisesFileNotFoundException()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "KiLupeMissing",
            Guid.NewGuid().ToString("N"),
            "missing.txt");

        Assert.Throws<FileNotFoundException>(() => new TextDocumentLoader().Load(path));
    }

    [Fact]
    public void RejectsInvalidUtf8()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "invalid.txt");
        File.WriteAllBytes(path, new byte[] { 0xC3, 0x28 });

        try
        {
            Assert.Throws<DecoderFallbackException>(() => new TextDocumentLoader().Load(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "KiLupeTextDocumentTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
