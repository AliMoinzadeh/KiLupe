# Ki-Lupe Local LLM Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional offline .NET LLM correction provider without replacing the existing T5 correction path.

**Architecture:** Keep `ITextCorrectionService` as the UI-facing seam. Add a `LocalLlamaTextCorrectionService` that lazily loads a GGUF model through `LLamaSharp`, creates a fresh context per OCR line, applies a conservative German correction prompt, and returns only changed text. Register the service through `TextCorrectionModelKind`, `ModelCatalog`, and `AnalysisServiceFactory`; missing model files must remain a normal unavailable state.

**Tech Stack:** .NET 8 WPF, C#, `LLamaSharp` 0.27.0, `LLamaSharp.Backend.Cpu` 0.27.0, local Qwen2.5-3B-Instruct Q4_K_M GGUF, xUnit.

---

### Task 1: Add the model option and package dependencies

**Files:**
- Modify: `KiLupeDemo.csproj`
- Modify: `Models/AnalysisConfiguration.cs`
- Modify: `Services/ModelCatalog.cs`
- Modify: `Services/AnalysisServiceFactory.cs`
- Test: `tests/KiLupeDemo.Tests/ModelCatalogTests.cs`
- Test: `tests/KiLupeDemo.Tests/AnalysisServiceFactoryTests.cs`

- [ ] **Step 1: Add the failing catalog assertion**

Add a test that creates a catalog from a temporary root containing no GGUF file and asserts that `TextCorrectionModelKind.LocalLlm` exists, is unavailable, and reports a missing model path. Add a factory test that selects `LocalLlm` against the same root and asserts the returned service has model id `qwen2.5-3b-instruct-gguf` and `IsAvailable == false`.

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter "FullyQualifiedName~ModelCatalogTests|FullyQualifiedName~AnalysisServiceFactoryTests"
```

Expected: compilation fails because `TextCorrectionModelKind.LocalLlm` and the catalog option do not exist.

- [ ] **Step 3: Add LLamaSharp package references**

Add these package references to `KiLupeDemo.csproj`:

```xml
<PackageReference Include="LLamaSharp" Version="0.27.0" />
<PackageReference Include="LLamaSharp.Backend.Cpu" Version="0.27.0" />
```

Keep the existing ONNX packages unchanged.

- [ ] **Step 4: Add the enum and catalog entry**

Add `LocalLlm` to `TextCorrectionModelKind`. Make `ModelCatalog` search these candidates in order:

```text
artifacts/models/qwen2.5-3b-instruct/Qwen2.5-3B-Instruct-Q4_K_M.gguf
qwen2.5-3b-instruct/Qwen2.5-3B-Instruct-Q4_K_M.gguf
```

Create a catalog option named `Deutsch: lokales LLM (Qwen 3B)` whose availability is based on the file being present and whose missing status includes the expected filename.

- [ ] **Step 5: Route the factory**

Extend `AnalysisServiceFactory.CreateTextCorrectionService` with a switch expression that returns `LocalLlamaTextCorrectionService` for `LocalLlm` and keeps `OnnxTextCorrectionService` for `GermanSpelling`.

- [ ] **Step 6: Run the focused tests**

Run the same filter from Step 2. Expected: all catalog and factory tests pass; the missing model remains a controlled unavailable state.

### Task 2: Implement the lazy LLamaSharp correction adapter

**Files:**
- Create: `Services/LocalLlamaTextCorrectionService.cs`
- Test: `tests/KiLupeDemo.Tests/LocalLlamaTextCorrectionServiceTests.cs`

- [ ] **Step 1: Write missing-artifact and prompt tests**

Add tests with a temporary missing GGUF path:

```csharp
[Fact]
public async Task MissingModelIsUnavailableAndReturnsNoSuggestion()
{
    using var service = new LocalLlamaTextCorrectionService(
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "model.gguf"));

    Assert.False(service.IsAvailable);
    Assert.Contains("fehlt", service.StatusText, StringComparison.OrdinalIgnoreCase);
    Assert.Null(await service.CorrectAsync("Der mann gehen.", CancellationToken.None));
}

[Fact]
public void PromptRequiresOnlyCorrectedGermanText()
{
    var prompt = LocalLlamaTextCorrectionService.BuildCorrectionPrompt("Der mann gehen.");

    Assert.Contains("nur den korrigierten deutschen Text", prompt, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Der mann gehen.", prompt, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~LocalLlamaTextCorrectionServiceTests
```

Expected: compilation fails because the adapter does not exist.

- [ ] **Step 3: Implement the unavailable state and prompt builder**

The constructor stores the model path and reports `Modell fehlt: <path>` when the file is absent. `BuildCorrectionPrompt` must create a German system instruction plus the original line. Do not load native model libraries from the constructor when the file is absent.

- [ ] **Step 4: Implement lazy model loading**

Use these LLamaSharp objects and lifetimes:

```csharp
var parameters = new ModelParams(ModelPath)
{
    ContextSize = 1024,
    GpuLayerCount = 0
};
weights = LLamaWeights.LoadFromFile(parameters);
using var context = weights.CreateContext(parameters);
var executor = new InteractiveExecutor(context);
var session = new ChatSession(executor, history);
```

Keep the loaded `LLamaWeights` under a lock, create and dispose a context/session for each correction, and dispose weights in `Dispose`. Set `MaxTokens` to 128 and stop on Qwen assistant terminators. Convert the streamed `IAsyncEnumerable<string>` into one response while checking the cancellation token between chunks.

- [ ] **Step 5: Normalize and validate generated output**

Trim whitespace, remove a leading `Antwort:` marker and surrounding Markdown code fences if present, reject empty output, reject an ordinally unchanged output, and return `CorrectionSuggestion` with `ModelName = "Deutsches lokales LLM"` and `ProviderName = "LLamaSharp CPU"`. Store a load/inference exception in `StatusText` and return null instead of throwing into WPF.

- [ ] **Step 6: Run adapter tests**

Run the focused adapter test command from Step 2. Expected: missing model and prompt tests pass without loading native libraries.

### Task 3: Add download and runtime documentation

**Files:**
- Create: `scripts/Download-KiLupeLlmModel.ps1`
- Modify: `README.md`
- Modify: `docs/architecture.md`
- Modify: `docs/superpowers/specs/2026-09-10-ki-lupe-local-llm-correction-design.md`

- [ ] **Step 1: Add a separate opt-in download script**

Create a PowerShell script with `param([switch]$Force)` that downloads:

```text
https://huggingface.co/bartowski/Qwen2.5-3B-Instruct-GGUF/resolve/main/Qwen2.5-3B-Instruct-Q4_K_M.gguf?download=true
```

to `artifacts/models/qwen2.5-3b-instruct/Qwen2.5-3B-Instruct-Q4_K_M.gguf`, using a temporary `.download` file and never overwriting an existing file without `-Force`. Keep this separate from the existing model script because the GGUF is much larger than the detector/T5 artifacts.

- [ ] **Step 2: Document installation and behavior**

Document the download command, expected local model location, approximate multi-GB disk/RAM requirement, CPU-only first implementation, and the fact that runtime inference is offline. Explain that the T5 option remains the smaller spelling-focused fallback.

- [ ] **Step 3: Verify documentation references**

Search for the old correction-model enum and ensure the catalog, README, architecture document, and local-LLM spec use the same model id, path, and option name.

### Task 4: Wire the option into the existing UI and validate

**Files:**
- Modify: `MainWindow.xaml.cs`
- Test: `tests/KiLupeDemo.Tests/AnalysisServiceFactoryTests.cs`
- Test: `tests/KiLupeDemo.Tests/CorrectionCoordinatorTests.cs`

- [ ] **Step 1: Preserve selection behavior**

Ensure `TextCorrectionSelector` obtains the new catalog entry automatically and selecting it rebuilds the correction service without changing the OCR service or the object model. Keep the default configuration as `TextCorrectionModelKind.None`.

- [ ] **Step 2: Preserve line-level output behavior**

Use the existing coordinator and presentation state unchanged. Confirm that every non-empty OCR line is submitted to the selected service and only changed output is displayed in the bottom dock and Orb popup.

- [ ] **Step 3: Run the full test suite and build**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore
dotnet build KiLupeDemo.csproj --no-restore
```

Expected: all tests pass and the WPF project builds. `NU1900` from the inaccessible private feed may remain as a warning.

- [ ] **Step 4: Run the optional local smoke test**

After downloading the GGUF model, select `Deutsch: lokales LLM (Qwen 3B)` in the application and verify that an input such as `Der mann gehen in die schule.` produces a corrected text suggestion. If the model is not downloaded, verify that the option is visible but reports the missing local path and the T5 option still works.

---
