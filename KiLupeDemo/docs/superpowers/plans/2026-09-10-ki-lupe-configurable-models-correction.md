# Ki-Lupe Configurable Models and Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add UI-selectable local object/correction models and show copyable sentence-correction suggestions in the main and floating modes.

**Architecture:** Keep OCR and Hunspell as the positional baseline, select YOLO or RT-DETR through a configuration-driven service factory, and introduce a separate text-correction contract because generated corrections do not have reliable screen rectangles. Render one shared correction view either in a dockable main-window container or in a popup anchored to the orb.

**Tech Stack:** .NET 8 WPF, C#, xUnit, Microsoft.ML.OnnxRuntime.DirectML, Tesseract, Hunspell, local Hugging Face/ONNX model artifacts.

---

## File map

- Create `Models/AnalysisConfiguration.cs` for model and provider IDs.
- Create `Models/CorrectionSuggestion.cs` for generated correction data.
- Create `Services/ModelCatalog.cs` for fixed model entries and availability checks.
- Create `Services/ITextCorrectionService.cs` for the sentence-correction adapter boundary.
- Create `Services/CorrectionLineGrouper.cs` for deterministic OCR-word line grouping.
- Create `Services/CorrectionCoordinator.cs` for grouping OCR lines and invoking the selected correction service.
- Create `Services/OnnxTextCorrectionService.cs` for the local T5/ONNX adapter and greedy decoder.
- Create `Services/CorrectionOverlayWindow.xaml` and `.xaml.cs` for the floating correction popup.
- Modify `Services/AnalysisServiceFactory.cs` to build services from `AnalysisConfiguration`.
- Modify `Services/OnnxObjectDetectionService.cs` and `Services/RtdetrObjectDetectionService.cs` to accept a provider selection and report the effective provider.
- Modify `MainWindow.xaml` and `MainWindow.xaml.cs` for selectors, the dockable correction container, correction lifecycle, and copy actions.
- Modify `KiLupeDemo.csproj` and `scripts/Download-KiLupeModels.ps1` for local correction-model assets.
- Add focused tests under `tests/KiLupeDemo.Tests` for each pure contract and coordinator behavior.
- Update `README.md` and `docs/architecture.md` after runtime behavior is implemented.

## Task 1: Add configuration, catalog, and correction contracts

**Files:**
- Create: `Models/AnalysisConfiguration.cs`
- Create: `Models/CorrectionSuggestion.cs`
- Create: `Services/ModelCatalog.cs`
- Create: `Services/ITextCorrectionService.cs`
- Test: `tests/KiLupeDemo.Tests/ModelCatalogTests.cs`
- Test: `tests/KiLupeDemo.Tests/CorrectionSuggestionTests.cs`

- [ ] **Step 1: Write failing catalog tests**

```csharp
[Fact]
public void CatalogContainsTheStableObjectAndCorrectionChoices()
{
    var catalog = ModelCatalog.Create(Path.GetTempPath());

    Assert.Contains(catalog.ObjectModels, option => option.Id == ObjectModelKind.YoloV8n);
    Assert.Contains(catalog.ObjectModels, option => option.Id == ObjectModelKind.RtDetr);
    Assert.Contains(catalog.TextCorrectionModels, option => option.Id == TextCorrectionModelKind.None);
    Assert.Contains(catalog.TextCorrectionModels, option => option.Id == TextCorrectionModelKind.GermanSpelling);
}

[Fact]
public void MissingArtifactsAreReportedWithoutDisablingOtherChoices()
{
    var catalog = ModelCatalog.Create(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

    Assert.Contains(catalog.ObjectModels, option => option.Id == ObjectModelKind.YoloV8n && !option.IsAvailable);
    Assert.Contains(catalog.TextCorrectionModels, option => option.Id == TextCorrectionModelKind.None && option.IsAvailable);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail for missing types**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~ModelCatalogTests"
```

Expected: compilation failure because the configuration and catalog contracts do not exist yet.

- [ ] **Step 3: Implement the minimal contracts**

Define these public types:

```csharp
public enum ObjectModelKind { YoloV8n, RtDetr }
public enum TextCorrectionModelKind { None, GermanSpelling }
public enum InferenceProviderKind { Auto, DirectMl, Cpu }

public sealed record AnalysisConfiguration(
    ObjectModelKind ObjectModel,
    TextCorrectionModelKind TextCorrectionModel,
    InferenceProviderKind Provider)
{
    public static AnalysisConfiguration Default { get; } =
        new(ObjectModelKind.YoloV8n, TextCorrectionModelKind.None, InferenceProviderKind.Auto);
}

public sealed record CorrectionSuggestion(
    string OriginalText,
    string CorrectedText,
    string ModelName,
    string ProviderName);

public interface ITextCorrectionService : IDisposable
{
    string Name { get; }
    string ModelId { get; }
    bool IsAvailable { get; }
    string StatusText { get; }
    Task<CorrectionSuggestion?> CorrectAsync(string text, CancellationToken cancellationToken);
}
```

`ModelCatalog.Create(rootDirectory)` must resolve artifacts under both `rootDirectory` and the current project directory, while the `None` entry is always available. Each catalog option exposes `Id`, `DisplayName`, `ModelPath`, `IsAvailable`, and `StatusText`.

- [ ] **Step 4: Run the focused tests and verify they pass**

Run the same `dotnet test` command. Expected: all catalog and suggestion contract tests pass.

## Task 2: Route object analysis through the selected model and provider

**Files:**
- Modify: `Services/AnalysisServiceFactory.cs`
- Modify: `Services/OnnxObjectDetectionService.cs`
- Modify: `Services/RtdetrObjectDetectionService.cs`
- Test: `tests/KiLupeDemo.Tests/AnalysisServiceFactoryTests.cs`
- Test: `tests/KiLupeDemo.Tests/InferenceProviderResolverTests.cs`

- [ ] **Step 1: Write failing selection tests**

```csharp
[Fact]
public void FactoryCreatesRtdetrWhenThatModelIsSelected()
{
    var services = AnalysisServiceFactory.CreateServices(
        new AnalysisConfiguration(ObjectModelKind.RtDetr, TextCorrectionModelKind.None, InferenceProviderKind.Cpu),
        modelRoot: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

    Assert.Contains(services, service => service is RtdetrObjectDetectionService);
    Assert.DoesNotContain(services, service => service is OnnxObjectDetectionService);
    Assert.Contains(services, service => service is LocalTextAnalysisService);
}

[Theory]
[InlineData(InferenceProviderKind.Cpu, "CPU")]
[InlineData(InferenceProviderKind.DirectMl, "DirectML")]
public void ProviderResolverReportsTheRequestedProvider(InferenceProviderKind requested, string expected)
{
    Assert.Equal(expected, InferenceProviderResolver.GetRequestedName(requested));
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~AnalysisServiceFactoryTests|FullyQualifiedName~InferenceProviderResolverTests"
```

Expected: compilation failure because the parameterized factory and provider resolver do not exist.

- [ ] **Step 3: Implement configuration-driven service creation**

Add `CreateServices(AnalysisConfiguration configuration, string? modelRoot = null)` and keep `CreateDefaultServices()` as a call using `AnalysisConfiguration.Default`. Construct either `OnnxObjectDetectionService` or `RtdetrObjectDetectionService`, always append `LocalTextAnalysisService`, and append the correction service only through the separate correction factory used by `MainWindow`.

Add `InferenceProviderResolver` as a pure helper with `GetRequestedName` and use it from both ONNX services. `Auto` tries DirectML first and constructs a CPU session on failure; explicit DirectML reports an unavailable service instead of silently changing the requested mode; CPU constructs the default `InferenceSession`.

- [ ] **Step 4: Run focused tests and the existing coordinator tests**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~AnalysisServiceFactoryTests|FullyQualifiedName~InferenceProviderResolverTests|FullyQualifiedName~AnalysisCoordinatorTests"
```

Expected: all selected-model and existing coordinator tests pass.

## Task 3: Build the pure OCR-line correction flow

**Files:**
- Create: `Services/CorrectionLineGrouper.cs`
- Create: `Services/CorrectionCoordinator.cs`
- Test: `tests/KiLupeDemo.Tests/CorrectionLineGrouperTests.cs`
- Test: `tests/KiLupeDemo.Tests/CorrectionCoordinatorTests.cs`

- [ ] **Step 1: Write failing line-grouping tests**

```csharp
[Fact]
public void GroupsWordsWithNearbyVerticalCentersIntoOneLine()
{
    var results = new[]
    {
        new AnalysisResult(AnalysisKind.Text, "Das", 1, new Rect(0, 0, 25, 12), "OCR"),
        new AnalysisResult(AnalysisKind.Text, "ist", 1, new Rect(30, 1, 18, 12), "OCR"),
        new AnalysisResult(AnalysisKind.Text, "falsch", 1, new Rect(0, 30, 35, 12), "OCR")
    };

    var lines = CorrectionLineGrouper.Group(results);

    Assert.Equal(2, lines.Count);
    Assert.Equal("Das ist", lines[0].Text);
}
```

- [ ] **Step 2: Write failing coordinator tests**

```csharp
[Fact]
public async Task CorrectsLinesContainingSpellingResults()
{
    var service = new FakeCorrectionService("Das ist ein falscher Satz.");
    var coordinator = new CorrectionCoordinator(service);
    var results = new[]
    {
        new AnalysisResult(AnalysisKind.Text, "Das", 1, new Rect(0, 0, 20, 10), "OCR"),
        new AnalysisResult(AnalysisKind.Text, "falsch", 1, new Rect(25, 0, 35, 10), "OCR"),
        new AnalysisResult(AnalysisKind.Spelling, "falsch", 1, new Rect(25, 0, 35, 10), "Hunspell")
    };

    var suggestions = await coordinator.CreateSuggestionsAsync(results, CancellationToken.None);

    var suggestion = Assert.Single(suggestions);
    Assert.Equal("Das falsch", suggestion.OriginalText);
    Assert.Equal("Das ist ein falscher Satz.", suggestion.CorrectedText);
}

[Fact]
public async Task DropsEmptyAndUnchangedCorrectionOutputs()
{
    var service = new FakeCorrectionService(string.Empty);
    var coordinator = new CorrectionCoordinator(service);

    var suggestions = await coordinator.CreateSuggestionsAsync(
        new[]
        {
            new AnalysisResult(AnalysisKind.Text, "Fehler", 1, new Rect(0, 0, 20, 10), "OCR"),
            new AnalysisResult(AnalysisKind.Spelling, "Fehler", 1, new Rect(0, 0, 20, 10), "Hunspell")
        },
        CancellationToken.None);

    Assert.Empty(suggestions);
}
```

- [ ] **Step 3: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~CorrectionLineGrouperTests|FullyQualifiedName~CorrectionCoordinatorTests"
```

Expected: compilation failure because the grouping and coordinator types do not exist.

- [ ] **Step 4: Implement deterministic grouping and filtering**

Group only `AnalysisKind.Text` results by vertical-center distance relative to the median word height, sort each line by `Bounds.Left`, and join labels with single spaces. Pass each non-empty line once to `ITextCorrectionService`, reject null, empty, whitespace-only, and ordinally unchanged outputs, and preserve the original line text in `CorrectionSuggestion`. Hunspell remains responsible for independent word-level markers and is not a prerequisite for sentence correction.

- [ ] **Step 5: Run focused tests and verify they pass**

Run the same `dotnet test` command. Expected: all grouping and coordinator tests pass.

## Task 4: Add and smoke-test the compact local correction model

**Files:**
- Create: `Services/OnnxTextCorrectionService.cs`
- Modify: `KiLupeDemo.csproj`
- Modify: `scripts/Download-KiLupeModels.ps1`
- Modify: `tools/evaluate-huggingface-models.py`
- Test: `tests/KiLupeDemo.Tests/OnnxTextCorrectionServiceTests.cs`

- [ ] **Step 1: Add service availability tests before model code**

```csharp
[Fact]
public async Task MissingCorrectionModelReturnsUnavailableAndNoSuggestion()
{
    using var service = new OnnxTextCorrectionService(
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "model.onnx"),
        tokenizerDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
        InferenceProviderKind.Cpu);

    Assert.False(service.IsAvailable);
    Assert.Contains("fehlt", service.StatusText, StringComparison.OrdinalIgnoreCase);
    Assert.Null(await service.CorrectAsync("Ein Satz.", CancellationToken.None));
}
```

- [ ] **Step 2: Run the test and verify it fails because the adapter is absent**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~OnnxTextCorrectionServiceTests"
```

Expected: compilation failure because `OnnxTextCorrectionService` does not exist.

- [ ] **Step 3: Download and inspect the model artifacts**

Extend `scripts/Download-KiLupeModels.ps1` with a directory under `artifacts\models\german-spelling-correction-onnx`. Download the tested Apache-2.0 German spelling-correction ONNX export plus tokenizer/config files without overwriting existing files unless `-Force` is passed. Keep the model directory ignored by source control.

Before enabling the catalog entry, update `tools/evaluate-huggingface-models.py` to print the ONNX input/output names, tokenizer files, decoder input requirements, and one generated correction. The adapter must support the observed model contract rather than assuming a single static output tensor.

- [ ] **Step 4: Implement the local ONNX correction adapter**

The service must:

1. locate `model.onnx` and tokenizer files,
2. create an ONNX session using the selected provider,
3. tokenize a bounded input string,
4. run encoder/decoder generation with a hard maximum token count,
5. stop on EOS or repeated no-progress output,
6. decode the output and return a `CorrectionSuggestion` only when it differs from the input.

Use the same provider status convention as object detection. Never throw from the constructor for missing local assets; expose the reason through `StatusText` and `IsAvailable`.

- [ ] **Step 5: Add the asset to build output only when present**

Add `None Update` entries for the model directory metadata/tokenizer files with `CopyToOutputDirectory="PreserveNewest"` only if the repository already contains those small files. Do not copy downloaded weight files from `artifacts` into Git or silently require them at build time.

- [ ] **Step 6: Run missing-asset tests, the Python smoke test, and build**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~OnnxTextCorrectionServiceTests"
python tools\evaluate-huggingface-models.py --text-model german-spelling-correction-onnx
dotnet build KiLupeDemo.csproj
```

Expected: the missing-asset test passes; the smoke test reports one nonempty correction when assets are downloaded; the WPF project builds.

## Task 5: Add main-window model selectors and dockable correction content

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`
- Create: `Services/CorrectionDockLayout.cs`
- Test: `tests/KiLupeDemo.Tests/CorrectionDockLayoutTests.cs`

- [ ] **Step 1: Write failing docking tests**

```csharp
[Theory]
[InlineData(DockEdge.Left)]
[InlineData(DockEdge.Right)]
[InlineData(DockEdge.Bottom)]
public void SnapsCorrectionContainerToRequestedEdge(DockEdge edge)
{
    var position = CorrectionDockLayout.Snap(edge, new Size(1000, 700), new Size(320, 220));

    Assert.True(position.X >= 0 && position.Y >= 0);
    Assert.True(position.X + 320 <= 1000);
    Assert.True(position.Y + 220 <= 700);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~CorrectionDockLayoutTests"
```

Expected: compilation failure because the docking helper does not exist.

- [ ] **Step 3: Implement the pure docking helper**

Create `DockEdge` with `Left`, `Right`, and `Bottom`, and implement `Snap` with a 12-pixel inset and clamping against the available work area. Keep WPF mouse event handling in `MainWindow`; the helper must remain UI-independent apart from `Point`/`Size` values.

- [ ] **Step 4: Add the selectors and correction panel to XAML**

Add model selectors to the existing right-side panel with `ComboBox` bindings or explicit item population. Add a hidden correction `Border` with a drag header, original text, a read-only or editable multiline suggestion `TextBox`, and buttons named `CopyCorrectionButton` and `CopyCorrectionSelectionButton`. Keep the panel within the existing grid and preserve the current result list.

- [ ] **Step 5: Wire selection changes and copy behavior**

Store the active `AnalysisConfiguration` in `MainWindow`, rebuild `AnalysisCoordinator` and `ITextCorrectionService` after a selection change, cancel active analyses first, and update the status with availability. Copy-all uses the whole `TextBox.Text`; copy-selection uses `SelectedText` and does nothing when it is empty. Catch clipboard failures and report them in `StatusText`.

- [ ] **Step 6: Run docking tests and build**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~CorrectionDockLayoutTests"
dotnet build KiLupeDemo.csproj
```

Expected: docking tests pass and the WPF project builds with the new controls.

## Task 6: Add the orb popup and connect correction generation to both modes

**Files:**
- Create: `Services/CorrectionOverlayWindow.xaml`
- Create: `Services/CorrectionOverlayWindow.xaml.cs`
- Modify: `Services/OrbOverlayWindow.xaml`
- Modify: `Services/OrbOverlayWindow.xaml.cs`
- Modify: `MainWindow.xaml.cs`
- Test: `tests/KiLupeDemo.Tests/CorrectionPresentationTests.cs`

- [ ] **Step 1: Write failing presentation-state tests**

```csharp
[Fact]
public void EmptySuggestionHidesTheCorrectionSurface()
{
    var state = CorrectionPresentationState.From(Array.Empty<CorrectionSuggestion>());

    Assert.False(state.IsVisible);
    Assert.Equal(string.Empty, state.SuggestionText);
}

[Fact]
public void LatestSuggestionWinsOverOlderFloatingResults()
{
    var state = new CorrectionPresentationState();

    state.Apply(10, new CorrectionSuggestion("alt", "neu", "test", "CPU"));
    state.Apply(9, new CorrectionSuggestion("alt", "veraltet", "test", "CPU"));

    Assert.Equal("neu", state.SuggestionText);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~CorrectionPresentationTests"
```

Expected: compilation failure because the presentation-state helper does not exist.

- [ ] **Step 3: Implement shared presentation state and popup window**

Create a small state helper keyed by `AnalysisSnapshot.RequestId`. Create `CorrectionOverlayWindow` as a topmost, non-activating WPF window positioned relative to the orb. Its multiline `TextBox` must allow normal selection. Expose `SetSuggestions`, `ShowNear(Point)`, `Hide`, and copy button events.

- [ ] **Step 4: Add an orb command for the correction popup**

Add a button to `OrbOverlayWindow` with a short text/icon-compatible label and a tooltip. Raise `CorrectionRequested`; `MainWindow` toggles the popup without ending floating mode. Keep the existing clear, mode, results, pause, and exit commands unchanged.

- [ ] **Step 5: Invoke the correction coordinator after text analysis**

For main-window analysis and floating text modes, call `CorrectionCoordinator.CreateSuggestionsAsync(snapshot.Results, operationToken)` after the snapshot is accepted. Pass the selected `ITextCorrectionService` into the coordinator; do not call the model directly from XAML event handlers. Apply only the newest request ID to `CorrectionPresentationState`, then project that state into both the docked panel and popup. Show the first bounded set of suggestions in each surface; leave object-only modes unchanged. Dispose and recreate the correction service when the text-model selection changes.

- [ ] **Step 6: Test the service-to-presentation integration path**

Add a test using a fake `ITextCorrectionService` that returns one changed line and a second fake that returns an unchanged line. Assert that the coordinator produces only the changed suggestion, `CorrectionPresentationState` becomes visible with that text, and a lower request ID cannot replace it. This test must exercise the same request ID passed by `MainWindow` rather than testing only the popup controls.

- [ ] **Step 7: Run focused correction tests and a full test/build pass**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --filter "FullyQualifiedName~CorrectionPresentationTests|FullyQualifiedName~CorrectionCoordinatorTests"
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj
dotnet build KiLupeDemo.csproj
```

Expected: all tests pass and the WPF build succeeds.

## Task 7: Document, validate, and package the feature

**Files:**
- Modify: `README.md`
- Modify: `docs/architecture.md`
- Modify: `.gitignore` only if `artifacts/models` is not already ignored

- [ ] **Step 1: Document the selectable models and local asset commands**

Document the model selectors, provider status semantics, correction popup/docking behavior, model license/size constraint, and the exact PowerShell download command. State that NPU execution is conditional on an installed provider and that CPU fallback remains supported.

- [ ] **Step 2: Validate repository hygiene**

Run:

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; downloaded weights remain untracked only under ignored artifact paths; no generated `bin`/`obj` files are added.

- [ ] **Step 3: Run the final verification set**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj
dotnet build KiLupeDemo.csproj
```

Expected: all tests pass and the application builds successfully. Record any existing `NU1900` or ONNX provider warnings without treating them as feature failures.

## Plan self-review

- Model selection is covered by Tasks 1 and 2.
- Provider fallback and the NPU limitation are covered by Task 2 and Task 7.
- OCR word markers remain intact while sentence corrections use a separate contract in Task 3.
- The compact local model download, tokenizer validation, decoder adapter, and missing-asset behavior are covered by Task 4.
- Main-window docking and copy actions are covered by Task 5.
- Orb popup behavior, stale-request protection, and floating integration are covered by Task 6.
- Documentation and full validation are covered by Task 7.
- No unresolved `TBD`, `TODO`, or unspecified implementation placeholder is used in the plan.