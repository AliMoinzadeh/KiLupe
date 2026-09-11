# Ki-Lupe Hybrid Text and Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make local OCR/spelling operational and create repeatable local tests for RT-DETR and German sentence-correction candidates without embedding large model weights in the app.

**Architecture:** Keep `LocalTextAnalysisService` as the OCR boundary, extract word spelling decisions into a small testable class, and make dictionary availability independent. Add a separate RT-DETR ONNX service with its own preprocessing and query-output parser. Use a PowerShell download script and a Python smoke-test script for Hugging Face candidates; model results inform later UI integration of sentence-level corrections.

**Tech Stack:** .NET 8 WPF, xUnit, Tesseract, WeCantSpell.Hunspell, Microsoft.ML.OnnxRuntime.DirectML, Python 3.14, onnxruntime, Transformers, SentencePiece.

---

### Task 1: Lock down independent spelling decisions

**Files:**
- Create: `Services/SpellingChecker.cs`
- Create: `tests/KiLupeDemo.Tests/SpellingCheckerTests.cs`
- Modify: `Services/LocalTextAnalysisService.cs`

- [ ] **Step 1: Write failing tests**

Add tests that construct `SpellingChecker` with delegate-backed German and English dictionaries. Verify a known German word is accepted when only German is present, an English word is accepted when only English is present, and an unknown word is rejected when at least one dictionary exists.

```csharp
[Fact]
public void GermanDictionaryCanFlagWordsWithoutEnglishDictionary()
{
    var checker = new SpellingChecker(
        germanCheck: word => word == "katze",
        englishCheck: null);

    Assert.False(checker.IsMisspelled("Katze"));
    Assert.True(checker.IsMisspelled("qzxwort"));
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run `dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~SpellingCheckerTests`.

Expected: compile failure because `SpellingChecker` does not exist.

- [ ] **Step 3: Implement the minimal checker**

Create `SpellingChecker` with nullable `Func<string, bool>` delegates, normalize letters/apostrophes/hyphens to lowercase, return `false` for words shorter than two letters, return `false` when no dictionary delegate is available, and otherwise return true only when no available delegate recognizes the word. Replace `LocalTextAnalysisService.LooksMisspelled` with this class.

- [ ] **Step 4: Run the focused test and full tests**

Run the focused command and then `dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore`. Both must pass.

---

### Task 2: Make language data discoverable and buildable

**Files:**
- Modify: `Services/LocalTextAnalysisService.cs`
- Modify: `KiLupeDemo.csproj`
- Create: `scripts/Download-KiLupeLanguageData.ps1`
- Modify: `README.md`
- Create: `.gitignore`

- [ ] **Step 1: Add path/status regression tests**

Extend `LocalTextAnalysisServiceTests` with a missing-data test that passes separate OCR and dictionary directories and asserts both paths occur in `StatusText`. Add a test that the dictionary directory property is the supplied directory.

- [ ] **Step 2: Run the focused tests and verify the new assertions fail**

Run `dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~LocalTextAnalysisServiceTests`.

Expected: the constructor has no separate dictionary path and the status does not mention it.

- [ ] **Step 3: Implement independent paths and status**

Add an optional `dictionaryDirectory` constructor argument and `DictionaryDirectory` property. Resolve dictionaries from that directory, not from `AppContext.BaseDirectory` directly. Keep `IsAvailable` tied to Tesseract only. Report whether German and English dictionaries are available separately, while preserving OCR-only operation.

- [ ] **Step 4: Copy data files when present**

Add MSBuild `None Update` items for `tessdata\*.traineddata` and `dictionaries\*.aff`/`*.dic` with `CopyToOutputDirectory=PreserveNewest`. The app must still build when the folders are absent.

- [ ] **Step 5: Add a reproducible download script**

Create a PowerShell script that creates `tessdata` and `dictionaries`, downloads `deu.traineddata` and `eng.traineddata` from the official Tesseract `tessdata_fast` repository, downloads LibreOffice `de_DE_frami.aff/.dic` and `en_US.aff/.dic`, and renames the German `frami` files to the names expected by the service. Use `Invoke-WebRequest`, fail on HTTP errors, and never overwrite a file unless `-Force` is supplied.

- [ ] **Step 6: Document and run the data setup**

Document `powershell -ExecutionPolicy Bypass -File scripts/Download-KiLupeLanguageData.ps1` in `README.md`, run it once, build, and run all tests. Keep downloaded model/data binaries ignored by `.gitignore`.

---

### Task 3: Add an RT-DETR ONNX service and parser tests

**Files:**
- Create: `Services/RtdetrObjectDetectionService.cs`
- Create: `tests/KiLupeDemo.Tests/RtdetrObjectDetectionServiceTests.cs`
- Modify: `Services/IAnalysisService.cs` only if a shared object-detection marker is needed

- [ ] **Step 1: Write parser tests**

Expose a pure internal/public parser seam or a small `RtdetrOutputParser` class. Test a `[1, 2, 3]` logits tensor plus `[1, 2, 4]` normalized center-box tensor: sigmoid the best class, apply the confidence threshold, convert center boxes to pixel rectangles, and use the COCO label for the winning class. Test that low-confidence queries are discarded.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run `dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~RtdetrObjectDetectionServiceTests`.

Expected: compile failure because the RT-DETR parser/service does not exist.

- [ ] **Step 3: Implement the minimal RT-DETR adapter**

Load an ONNX file supplied by constructor path, try DirectML and retain CPU fallback, prepare RGB `1x3x640x640` input with the downloaded processor settings (`0..1` rescale and no mean/std normalization), locate `logits` and `pred_boxes` outputs by name, and parse 300 query predictions using sigmoid scores and normalized `(center_x, center_y, width, height)` boxes. Read labels from an adjacent `config.json` when available, otherwise use the COCO fallback labels.

- [ ] **Step 4: Run parser tests and the full test suite**

The pure parser tests must pass before downloading or running the large model.

---

### Task 4: Download and smoke-test Hugging Face candidates

**Files:**
- Create: `tools/requirements-model-eval.txt`
- Create: `tools/evaluate-huggingface-models.py`
- Modify: `README.md`
- Create locally, ignored: `artifacts/models/`, `artifacts/fixtures/`

- [ ] **Step 1: Add the evaluation dependencies**

Use `onnxruntime`, `Pillow`, `transformers`, `sentencepiece`, and `torch` in `tools/requirements-model-eval.txt`. Do not add these packages to the WPF application.

- [ ] **Step 2: Download the RT-DETR export and metadata**

Download only `onnx/model.onnx`, `config.json`, and `preprocessor_config.json` from `onnx-community/rtdetr_v2_r18vd-ONNX` into `artifacts/models/rtdetr_v2_r18vd-ONNX/`. Download the public bus fixture into `artifacts/fixtures/bus.jpg` if it is not already present.

- [ ] **Step 3: Run the RT-DETR smoke test**

The Python script must load the ONNX file, print input/output names and shapes, preprocess the fixture to RGB `640x640`, run one inference, decode `logits`/`pred_boxes`, and print detections above `0.45` with latency. It must fail with a clear message if a model file is missing.

- [ ] **Step 4: Run sentence-correction candidates**

The same script must run `oliverguhr/spelling-correction-german-base` and `aiassociates/t5-small-grammar-correction-german` on a fixed German sentence containing spelling, capitalization, and punctuation errors. Print model id, elapsed time, input, and generated output. Do not assert one exact generated string; assert only non-empty output and report the result for human quality comparison.

- [ ] **Step 5: Record results and limitations**

Document actual local download sizes, latency, output shapes, detected labels, generated corrections, and licenses in `README.md`. Explain that sentence correction has line-level rather than exact word-level boxes and that the non-commercial German grammar model is optional.

---

### Task 5: Final validation

**Files:**
- No new source files; update only documentation if a command differs from the plan.

- [ ] **Step 1: Build the WPF app**

Run `dotnet build KiLupeDemo.csproj --no-restore` and require exit code 0.

- [ ] **Step 2: Run all tests**

Run `dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore` and require zero failures.

- [ ] **Step 3: Run the model smoke test again**

Run the documented Python command after the local model files exist and verify it reports both object detections and text-model output.

- [ ] **Step 4: Check debug instrumentation and generated files**

Search for temporary debug prefixes, confirm model binaries are ignored, and leave no temporary downloaded fixtures outside the documented local directories.
