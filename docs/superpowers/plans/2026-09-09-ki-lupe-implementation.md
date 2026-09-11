# Ki-Lupe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first usable WPF version of Ki-Lupe with a normal image mode, a global floating overlay mode, local object/OCR/spelling adapters, and CPU fallback.

**Architecture:** Convert the current console project into a Windows-only WPF application. Keep UI and Windows-specific capture/hotkey code in the presentation/infrastructure layer, while analysis is exposed through small interfaces and a coordinator that throttles stale work. The first implementation provides usable image loading, magnification, overlay controls, capture, and optional model-backed analysis without crashing when model data is absent.

**Tech Stack:** .NET 8 WPF, C#, Microsoft.ML.OnnxRuntime.DirectML, OpenCvSharp, Windows user32/gdi32 interop, local YOLO ONNX model, local OCR and spelling data adapters.

---

## File Map

- Modify: `KiLupeDemo.csproj` - target Windows/WPF and retain the existing inference/image packages.
- Create: `App.xaml` - WPF application startup resources.
- Create: `App.xaml.cs` - application startup and service composition.
- Replace: `Program.cs` - remove console entry point after the WPF entry point exists.
- Create: `MainWindow.xaml` - normal-mode layout with image canvas, floating tools, and results panel.
- Create: `MainWindow.xaml.cs` - image loading, mouse tracking, magnifier rendering, and command wiring.
- Create: `Models/AnalysisResult.cs` - immutable object/text/spelling result contracts.
- Create: `Services/IAnalysisService.cs` - analysis service abstraction.
- Create: `Services/AnalysisCoordinator.cs` - cancellation, debounce, and analysis-id protection.
- Create: `Services/OnnxObjectDetectionService.cs` - DirectML/CPU ONNX session creation and model availability handling.
- Create: `Services/LocalTextAnalysisService.cs` - OCR/spelling adapter boundary with explicit unavailable-state results until local data is supplied.
- Create: `Services/ScreenCaptureService.cs` - cursor-region and screen capture through `Graphics.CopyFromScreen`.
- Create: `Services/GlobalHotkeyService.cs` - register/unregister `Ctrl+Alt+L` through a hidden WPF message hook.
- Create: `Services/OrbOverlayWindow.xaml` - transparent bottom orb bar.
- Create: `Services/OrbOverlayWindow.xaml.cs` - click-through window behavior and orb events.
- Create: `Services/AnalysisServiceFactory.cs` - configure DirectML first and CPU fallback.
- Create: `README.md` updates - build, model/data locations, and controls.
- Create: `tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj` - focused unit-test project.
- Create: `tests/KiLupeDemo.Tests/AnalysisCoordinatorTests.cs` - stale-result and cancellation tests.
- Create: `tests/KiLupeDemo.Tests/AnalysisResultTests.cs` - result aggregation/display tests.

## Scope Guardrails

- Do not migrate to WinUI 3 in this plan.
- Do not add cloud APIs or automatic text correction.
- Do not persist captured screen images.
- Keep OCR and spelling behind an adapter so the app can run with a clear unavailable message when local language data is not installed.
- Use a bounded timer/debounce rather than running inference on every mouse event.

### Task 1: Convert the project to WPF

**Files:**
- Modify: `KiLupeDemo.csproj`
- Create: `App.xaml`
- Create: `App.xaml.cs`
- Replace: `Program.cs`

- [ ] **Step 1: Update the project file for Windows desktop output**

Change the project properties to target Windows WPF while preserving the existing packages:

```xml
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.ML.OnnxRuntime.DirectML" Version="1.19.2" />
    <PackageReference Include="OpenCvSharp4" Version="4.10.0.20241108" />
    <PackageReference Include="OpenCvSharp4.runtime.win" Version="4.10.0.20241108" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the WPF application entry point**

Create `App.xaml`:

```xml
<Application x:Class="KiLupeDemo.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
  <Application.Resources>
    <SolidColorBrush x:Key="WindowBackgroundBrush" Color="#101614" />
    <SolidColorBrush x:Key="PanelBrush" Color="#18221F" />
    <SolidColorBrush x:Key="PanelBorderBrush" Color="#33413D" />
    <SolidColorBrush x:Key="AccentBrush" Color="#9AE6B4" />
  </Application.Resources>
</Application>
```

Create `App.xaml.cs`:

```csharp
using System.Windows;

namespace KiLupeDemo;

public partial class App : Application
{
}
```

- [ ] **Step 3: Remove the obsolete console entry point**

Delete `Program.cs`. WPF generates `Main` from `App.xaml`; keeping the old console entry point would create an unnecessary second application path.

- [ ] **Step 4: Build the project**

Run:

```powershell
dotnet build KiLupeDemo.csproj --no-restore
```

Expected: the project compiles as a Windows executable. If restore assets still describe the old target, run `dotnet restore` once and repeat the build.

### Task 2: Define analysis contracts and focused tests

**Files:**
- Create: `Models/AnalysisResult.cs`
- Create: `Services/IAnalysisService.cs`
- Create: `Services/AnalysisCoordinator.cs`
- Create: `tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj`
- Create: `tests/KiLupeDemo.Tests/AnalysisCoordinatorTests.cs`
- Create: `tests/KiLupeDemo.Tests/AnalysisResultTests.cs`
- Modify: `KiLupeDemo.csproj` only if shared compilation requires an explicit root namespace.

- [ ] **Step 1: Add a test project and write the failing coordinator tests**

The test project should reference the main project and use xUnit:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\KiLupeDemo.csproj" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```

Write tests that assert a cancelled/stale request cannot replace a newer result and that an unavailable analyzer produces a visible status rather than throwing. Run:

```powershell
dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore
```

Expected: the tests fail because the contracts and coordinator do not exist yet.

- [ ] **Step 2: Add the result contracts**

Define `AnalysisKind` with `Object`, `Text`, and `Spelling`; define `AnalysisResult` with `Kind`, `Label`, `Confidence`, `Bounds`, and `Details`; define an `AnalysisSnapshot` containing the source size, request ID, results, and status text. Keep rectangles in image coordinates so both normal mode and screen mode can render them.

- [ ] **Step 3: Add the service abstraction**

Use one service interface that accepts a bitmap and cancellation token:

```csharp
public interface IAnalysisService
{
    string Name { get; }
    bool IsAvailable { get; }
    Task<IReadOnlyList<AnalysisResult>> AnalyzeAsync(
        BitmapSource image,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement the minimal coordinator**

`AnalysisCoordinator` must increment a request ID, cancel the previous request, run the configured services off the UI thread, and return only the newest request's results. It must catch a service-level exception and add a status result rather than terminating the application.

- [ ] **Step 5: Run tests to verify green**

Run:

```powershell
dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore
```

Expected: all coordinator and result tests pass.

### Task 3: Add the normal image workspace and magnifier

**Files:**
- Create: `MainWindow.xaml`
- Create: `MainWindow.xaml.cs`

- [ ] **Step 1: Add the normal-mode layout**

Use a dark WPF window with a toolbar, a central `Canvas`/`Image` workspace, a circular magnifier rendered by an `Adorner` or clipped `Image`, and a right-side result list. The toolbar must contain buttons for `Bild oeffnen`, `Objekte suchen`, `Text pruefen`, and `Schwebemodus`.

- [ ] **Step 2: Implement image selection and display**

Use `OpenFileDialog` for PNG/JPEG/BMP files. Load the selected file as a frozen `BitmapImage`, set it as the workspace image, reset prior results, and show a status message. Do not store a copy on disk.

- [ ] **Step 3: Implement mouse-following magnification**

On workspace mouse move, map the pointer to image coordinates, update the clipped magnifier with a 2x to 3x scale, and keep the magnifier bounded within the image. Do not start model inference from every mouse event; only update the visual magnifier synchronously.

- [ ] **Step 4: Build the normal workspace**

Run:

```powershell
dotnet build KiLupeDemo.csproj --no-restore
```

Expected: the WPF window opens with an empty workspace, can load an image, and follows the pointer with a visible circular magnifier.

### Task 4: Implement local object analysis with DirectML fallback

**Files:**
- Create: `Services/OnnxObjectDetectionService.cs`
- Create: `Services/AnalysisServiceFactory.cs`
- Modify: `MainWindow.xaml.cs`
- Modify: `README.md`

- [ ] **Step 1: Add model discovery and availability tests**

Test that a missing `yolov8n.onnx` path reports `IsAvailable == false` and a user-facing status. Test that provider setup tries `AppendExecutionProvider_DML(0)` and falls back to a CPU `SessionOptions` when provider creation fails.

- [ ] **Step 2: Implement ONNX session creation**

Search for the model next to the executable and in the project root. Create `SessionOptions`, try `AppendExecutionProvider_DML(0)`, and retain the same options without DirectML on failure. Do not throw solely because DirectML is unavailable.

- [ ] **Step 3: Implement image tensor preparation and result parsing**

Convert a `BitmapSource` to RGB 640x640 float input, run the model using the model's first input name, and parse the YOLO output into `AnalysisResult` records with object labels, confidence, and bounding rectangles. Use a configurable confidence threshold of `0.45` and include `cat` in the default object label map.

- [ ] **Step 4: Wire object analysis to the toolbar**

Run the service through `AnalysisCoordinator`, update the result list on the dispatcher, and show a clear setup status if the ONNX model is absent. The app must continue to work as a magnifier without the model.

- [ ] **Step 5: Run build and tests**

Run:

```powershell
dotnet build KiLupeDemo.csproj --no-restore
dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore
```

Expected: both commands succeed; a missing model produces a controlled status rather than a startup exception.

### Task 5: Add OCR and spelling adapter boundary

**Files:**
- Create: `Services/LocalTextAnalysisService.cs`
- Modify: `KiLupeDemo.csproj`
- Modify: `MainWindow.xaml.cs`
- Modify: `README.md`

- [ ] **Step 1: Add unavailable-language-data tests**

Test that absent German/English OCR or dictionary data returns a status result and an empty detection list. This keeps the application usable before language files are installed.

- [ ] **Step 2: Add the local text-analysis implementation**

Use a local OCR implementation with `deu` and `eng` data paths configured relative to the executable. Convert recognized words into text results with image rectangles, then compare normalized words to local German/English dictionaries and add spelling results for unknown words. Keep all file access local and read-only.

- [ ] **Step 3: Wire text analysis to `Text pruefen`**

Use the same coordinator and result rendering as object detection. If only OCR is available, show text results and a status that spelling data is missing; if both are unavailable, show the setup paths.

- [ ] **Step 4: Run the focused checks**

Run:

```powershell
dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore
```

Expected: unavailable-data tests pass and no UI exception occurs when language data is absent.

### Task 6: Add cursor-region and screen capture

**Files:**
- Create: `Services/ScreenCaptureService.cs`
- Create: `Services/GlobalHotkeyService.cs`
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Test capture bounds as pure logic**

Add tests for clamping a cursor-centered rectangle to the selected monitor bounds and for rejecting zero-sized capture regions.

- [ ] **Step 2: Implement cursor position and capture**

Use `GetCursorPos` and `System.Drawing.Graphics.CopyFromScreen` to capture a bounded rectangle around the cursor. Convert the captured bitmap to a frozen `BitmapSource`, dispose GDI objects promptly, and never save the capture to a file.

- [ ] **Step 3: Implement the global hotkey**

Register `Ctrl+Alt+L` against the WPF window handle using `RegisterHotKey`; unregister it on window close. Toggle between normal and floating mode and show an error status if another process owns the hotkey.

- [ ] **Step 4: Run capture and build checks**

Run:

```powershell
dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore
dotnet build KiLupeDemo.csproj --no-restore
```

Expected: tests and build pass; capture code is isolated from UI state and can be exercised manually on Windows.

### Task 7: Add the click-through orb overlay

**Files:**
- Create: `Services/OrbOverlayWindow.xaml`
- Create: `Services/OrbOverlayWindow.xaml.cs`
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Add the transparent orb layout**

Create a borderless, topmost, transparent WPF window positioned above the taskbar. Render four compact orbs for pause, mode, results, and exit. Keep the window transparent outside the orb hit areas.

- [ ] **Step 2: Add click-through behavior**

Use `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE` for the transparent surface and remove click-through only for the orb hit regions. The overlay must not prevent the user from interacting with the application underneath.

- [ ] **Step 3: Add overlay events**

Expose events for pause toggling, analysis mode selection, showing results, and exit. Keep event handlers on `MainWindow` so the overlay has no dependency on analysis implementations.

- [ ] **Step 4: Add cursor-region analysis loop**

When floating mode is active and not paused, use a `DispatcherTimer` or cancellation loop with a bounded interval. Capture the cursor region, submit it to `AnalysisCoordinator`, and update the orb state and compact status label. Skip submissions when the pointer has not moved enough or an analysis is still current.

- [ ] **Step 5: Add optional full-screen mode**

Add a mode toggle that captures the visible monitor at a slower interval. Temporarily hide or exclude the overlay during capture so its own controls are not analyzed. Reuse the same coordinator and result display.

- [ ] **Step 6: Manually validate the overlay**

Run:

```powershell
dotnet run --project KiLupeDemo.csproj
```

Verify: `Ctrl+Alt+L` shows the orbs, the normal application window minimizes, pause stops capture, the mode orb switches between cursor and screen analysis, results can be opened, and the exit orb returns to normal mode.

### Task 8: Finish documentation and verification

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-09-09-ki-lupe-design.md` only if implementation constraints require an explicit correction.

- [ ] **Step 1: Document setup**

Document the following exact setup:

- `dotnet build` and `dotnet run` commands.
- `yolov8n.onnx` location and supported input assumptions.
- Local OCR language data locations for `deu` and `eng`.
- Local dictionary locations.
- `Ctrl+Alt+L`, orb controls, pause behavior, and CPU fallback.
- The fact that screen captures are processed in memory and not persisted.

- [ ] **Step 2: Run the complete verification**

Run:

```powershell
dotnet restore
dotnet build KiLupeDemo.csproj
dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj
```

Expected: build and tests pass. A package-source vulnerability warning may remain if the private Azure DevOps feed is unreachable; it is not a compilation failure.

- [ ] **Step 3: Review the final diff**

Run:

```powershell
git diff -- KiLupeDemo.csproj App.xaml App.xaml.cs Program.cs MainWindow.xaml MainWindow.xaml.cs Models Services README.md tests docs/superpowers/plans
```

Check that no captured images, model binaries, generated `bin`/`obj` files, or secrets were added.
