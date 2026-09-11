# Ki-Lupe Text File Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load supported local text files in Ki-Lupe and send their non-empty lines through the existing T5 or Local LLM correction workflow without creating artificial image coordinates.

**Architecture:** Add a small `TextDocumentLoader` boundary that reads UTF-8/UTF-16 text and returns a `TextDocument` with preserved full text and lines. Extend `CorrectionCoordinator` with a direct line-based overload. `MainWindow` tracks either an image or text document, renders text in a read-only viewer, and routes `Text pruefen` to OCR for images or direct correction for text documents.

**Tech Stack:** .NET 8, WPF, C#, xUnit, existing `ITextCorrectionService`, `CorrectionCoordinator`, `CorrectionPresentationState`, and `Microsoft.Win32.OpenFileDialog`.

---

## File Map

- Create `Models/TextDocument.cs`: immutable loaded-text value with path, full content, and line sequence.
- Create `Services/TextDocumentLoader.cs`: supported extension list, BOM-aware UTF-8/UTF-16 loading, size guard, and controlled IO/encoding errors.
- Create `tests/KiLupeDemo.Tests/TextDocumentLoaderTests.cs`: loader encoding, line, extension, empty-file, and size/error coverage.
- Modify `Services/CorrectionCoordinator.cs`: direct `IEnumerable<string>` correction overload sharing filtering and cancellation behavior with the OCR path.
- Modify `tests/KiLupeDemo.Tests/CorrectionCoordinatorTests.cs`: direct line correction and cancellation coverage.
- Modify `MainWindow.xaml`: add `Textdatei laden` action and a read-only text document surface in the left workspace.
- Modify `MainWindow.xaml.cs`: track workspace content mode, load text files, switch rendering, route analysis actions, and preserve current image behavior.
- Modify `tests/KiLupeDemo.Tests/CorrectionDockLayoutTests.cs` only if a pure helper is extracted for mode/layout state; otherwise no UI test file change is needed.
- Modify `README.md` and `docs/architecture.md`: document supported text formats, direct line correction, and the absence of position markers for text files.

## Task 1: Add the text document model and loader

**Files:**
- Create `Models/TextDocument.cs`
- Create `Services/TextDocumentLoader.cs`
- Create `tests/KiLupeDemo.Tests/TextDocumentLoaderTests.cs`

- [x] **Step 1: Write failing loader tests**

Create tests that assert:

```csharp
[Fact]
public void LoadsUtf8TextAndPreservesLines() { /* ... */ }

[Fact]
public void LoadsUtf16LittleEndianWithBom() { /* ... */ }

[Fact]
public void LoadsEmptyFileAsOneEmptyDocument() { /* ... */ }

[Fact]
public void ExposesOnlySupportedTextExtensions() { /* ... */ }

[Fact]
public void RejectsFilesLargerThanConfiguredLimit() { /* ... */ }
```

Use unique temporary directories and delete them in `finally`. Do not depend on project assets or the user's files.

- [x] **Step 2: Run the focused loader tests and verify they fail**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~TextDocumentLoaderTests
```

Expected: compilation/test failure because `TextDocument` and `TextDocumentLoader` do not yet exist.

- [x] **Step 3: Implement the immutable text document**

Use an immutable record in `Models/TextDocument.cs`:

```csharp
namespace KiLupeDemo.Models;

public sealed record TextDocument(
    string FilePath,
    string Text,
    IReadOnlyList<string> Lines);
```

- [x] **Step 4: Implement the loader**

`TextDocumentLoader` must:

- expose `SupportedExtensions` containing `.txt`, `.md`, `.log`, `.csv`, `.json`, `.xml` using case-insensitive comparison;
- expose `MaximumFileBytes = 10 * 1024 * 1024`;
- reject a missing path with `FileNotFoundException`;
- reject files larger than `MaximumFileBytes` with `InvalidDataException`;
- open the file with `FileMode.Open`, `FileAccess.Read`, and `FileShare.Read`;
- use `StreamReader` with strict UTF-8 and `detectEncodingFromByteOrderMarks: true`, which handles UTF-8, UTF-16 LE, and UTF-16 BE BOMs;
- split using `\r\n`, `\n`, and `\r` while preserving empty lines;
- return `TextDocument(filePath, text, lines)`;
- provide `IsSupportedExtension(string filePath)` for the dialog/test boundary.

The loader must never write to the source file.

- [x] **Step 5: Run the focused loader tests and verify they pass**

Run the same focused command. Expected: all loader tests pass with no compiler errors.

## Task 2: Add direct line correction

**Files:**
- Modify `Services/CorrectionCoordinator.cs`
- Modify `tests/KiLupeDemo.Tests/CorrectionCoordinatorTests.cs`

- [x] **Step 1: Add a failing direct-line test**

Add a test with lines such as:

```csharp
var suggestions = await coordinator.CreateSuggestionsAsync(
    new[] { "Ich habe ein Apfel gegessen.", "", "Der Satz ist korrekt." },
    CancellationToken.None);

Assert.Single(suggestions);
Assert.Equal("Ich habe ein Apfel gegessen.", suggestions[0].OriginalText);
Assert.Equal("Ich habe einen Apfel gegessen.", suggestions[0].CorrectedText);
```

Use the existing fake correction service and assert that empty lines are never passed to it. Add a cancellation test using a service that observes the token if the existing fake cannot express it.

- [x] **Step 2: Run the focused coordinator tests and verify the new test fails**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~CorrectionCoordinatorTests
```

Expected: the new overload call does not compile until implemented.

- [x] **Step 3: Implement the overload without changing OCR behavior**

Add:

```csharp
public Task<IReadOnlyList<CorrectionSuggestion>> CreateSuggestionsAsync(
    IEnumerable<string> lines,
    CancellationToken cancellationToken)
```

It must check `ITextCorrectionService.IsAvailable`, iterate in source order, throw on cancellation before each line, skip whitespace-only lines, call `CorrectAsync` once per non-empty line, and drop null/empty/unchanged results. Refactor the existing OCR overload to use the same private `CorrectLinesAsync` helper after `CorrectionLineGrouper.Group(...)` so filtering remains identical.

- [x] **Step 4: Run focused coordinator tests**

Expected: all existing OCR tests and the new direct-line tests pass.

## Task 3: Wire the WPF text document mode

**Files:**
- Modify `MainWindow.xaml`
- Modify `MainWindow.xaml.cs`

- [x] **Step 1: Add the text action and viewer markup**

Add a `Textdatei laden` button next to `Bild oeffnen`. Inside `ImageSurface`, add a named read-only `TextBox` (or equivalent scrollable text viewer) with wrapping, vertical scrolling, and `Visibility="Collapsed"`. Keep the existing image, result canvas, and magnifier elements unchanged for image mode.

- [x] **Step 2: Add explicit workspace content state**

In `MainWindow.xaml.cs`, add a private content enum with `None`, `Image`, and `Text`, plus a `TextDocument? currentTextDocument` and a `TextDocumentLoader`. Do not infer the mode from whether `currentImage` happens to be null.

- [x] **Step 3: Implement text file loading**

Add `OpenTextFile_Click` with this filter:

```text
Textdateien|*.txt;*.md;*.log;*.csv;*.json;*.xml|Alle Dateien|*.*
```

Load successfully before changing the current workspace. On success:

- cancel active correction work;
- set `currentTextDocument`, clear `currentImage`;
- set the viewer text to the full file text;
- switch to text rendering;
- clear old results and correction presentation;
- report `Textdatei geladen: <filename>`.

On failure, keep the current workspace visible and report the exception message.

- [x] **Step 4: Preserve image loading and add rendering helpers**

Image loading must clear `currentTextDocument`, set `currentImage`, and switch to image rendering. Add a helper that sets these visibilities:

- image mode: image visible, text viewer hidden, markers/magnifier enabled;
- text mode: image hidden, text viewer visible, result canvas and magnifier hidden;
- none: empty-state visible, both content surfaces hidden.

Ensure `ClearResults()` and correction cancellation are called when switching documents.

- [x] **Step 5: Route actions by content mode**

`AnalyzeObjects_Click` must require image mode and display a clear status for text mode. `AnalyzeText_Click` must:

- use the existing `AnalysisCoordinator` OCR service for image mode;
- call `correctionCoordinator.CreateSuggestionsAsync(currentTextDocument.Lines, CancellationToken.None)` for text mode;
- apply the returned suggestions through the existing request-generation and `CorrectionPresentationState` path;
- display a useful status for empty files or unavailable correction models.

Do not send text-document lines through OCR or fabricate `AnalysisResult` bounds.

- [x] **Step 6: Run the WPF build immediately after the UI edit**

Run:

```powershell
dotnet build KiLupeDemo.csproj --no-restore
```

Expected: successful `net8.0-windows` build with no XAML/compiler errors.

## Task 4: Update documentation

**Files:**
- Modify `README.md`
- Modify `docs/architecture.md`

- [x] **Step 1: Document supported input modes**

State that Ki-Lupe accepts images for OCR/object analysis and local `.txt`, `.md`, `.log`, `.csv`, `.json`, and `.xml` files for direct line correction. Explain that text files use T5/Local LLM directly and therefore have no screen/image position markers.

- [x] **Step 2: Document non-goals and behavior**

Mention that PDF/DOCX parsing, semantic CSV/JSON/XML editing, automatic overwrite, and network access are not included. Explain that corrections are suggestions only and the source file is never modified.

## Task 5: Full validation

**Files:**
- No new source files beyond the tasks above.

- [x] **Step 1: Run focused loader and coordinator tests**

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~TextDocumentLoaderTests

dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~CorrectionCoordinatorTests
```

- [x] **Step 2: Run the full test suite**

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore
```

Expected: zero failures; note any existing non-fatal ONNX runtime warnings separately.

- [x] **Step 3: Run the final WPF build**

```powershell
dotnet build KiLupeDemo.csproj --no-restore
```

Expected: successful build with no compiler or XAML errors.

- [x] **Step 4: Review the final behavior contract**

Verify that image opening and OCR still work, text opening shows the full source text, `Text pruefen` produces line suggestions when a correction model is available, `Objekte suchen` reports that a picture is required for text mode, and no source file is changed.
