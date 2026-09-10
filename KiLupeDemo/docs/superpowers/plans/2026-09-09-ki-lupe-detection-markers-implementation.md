# Ki-Lupe Detection Markers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show short-lived, color-coded detection markers at the real screen position in floating mode, while keeping the orb stable and clickable.

**Architecture:** Extend screen capture to return both the in-memory image and its virtual-screen region. Map analysis rectangles from image coordinates to screen coordinates through a pure mapper, then render them in a separate full-screen click-through WPF overlay. Keep the orb window independent and add a clear-markers command; the floating loop updates marker content without hiding either window.

**Tech Stack:** .NET 8 WPF, `BitmapSource`, Win32 window styles, WPF `Canvas`/`Ellipse`/`Border`, xUnit.

---

## File Map

- Modify: `Services/ScreenCaptureService.cs` - expose capture-region metadata and virtual-screen bounds.
- Create: `Services/DetectionMarkerMapper.cs` - pure image-to-screen coordinate conversion and result-to-marker styling.
- Create: `Services/DetectionOverlayWindow.xaml` - transparent full-screen marker surface.
- Create: `Services/DetectionOverlayWindow.xaml.cs` - marker rendering, click-through setup, fade, and clearing.
- Modify: `Services/OrbOverlayWindow.xaml` - add the clear-markers orb.
- Modify: `Services/OrbOverlayWindow.xaml.cs` - expose `ClearRequested` and keep the orb window interactive.
- Modify: `MainWindow.xaml.cs` - create/dispose the marker overlay and pass capture-region metadata through the floating loop.
- Modify: `tests/KiLupeDemo.Tests/ScreenCaptureServiceTests.cs` - verify capture frame metadata.
- Create: `tests/KiLupeDemo.Tests/DetectionMarkerMapperTests.cs` - verify screen-coordinate mapping and result styling.
- Modify: `README.md` - document marker colors, fade, and the clear orb.

## Task 1: Return screen coordinates with captures

**Files:**
- Modify: `Services/ScreenCaptureService.cs`
- Modify: `tests/KiLupeDemo.Tests/ScreenCaptureServiceTests.cs`

- [ ] **Step 1: Add a failing capture-frame contract test**

Add a test that constructs a `ScreenCaptureFrame` and verifies the image and region are retained:

```csharp
[Fact]
public void CaptureFrameKeepsImageAndScreenRegion()
{
    var image = BitmapSource.Create(
        4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 4 * 4], 4 * 4);
    var region = new CaptureRegion(-120, 80, 320, 320);

    var frame = new ScreenCaptureFrame(image, region);

    Assert.Same(image, frame.Image);
    Assert.Equal(region, frame.Region);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~ScreenCaptureServiceTests
```

Expected: compile failure because `ScreenCaptureFrame` does not exist yet.

- [ ] **Step 3: Implement the capture-frame record and public virtual-screen region**

Add this contract next to `CaptureRegion`:

```csharp
public sealed record ScreenCaptureFrame(BitmapSource Image, CaptureRegion Region);
```

Change `CaptureCursorRegion` and `CaptureVirtualScreen` to return `ScreenCaptureFrame`. Compute the region once, call the existing `Capture(CaptureRegion)` method, and return both values. Add:

```csharp
public static CaptureRegion GetVirtualScreenRegion()
```

which returns the current virtual-screen bounds. Keep `Capture(CaptureRegion)` private or public only if required by existing callers; it must continue to validate dimensions and never write a file.

- [ ] **Step 4: Run the focused tests and verify they pass**

Run the same filtered test command. Expected: all screen-capture tests pass.

## Task 2: Add pure marker mapping and styles

**Files:**
- Create: `Services/DetectionMarkerMapper.cs`
- Create: `tests/KiLupeDemo.Tests/DetectionMarkerMapperTests.cs`

- [ ] **Step 1: Write failing mapping and styling tests**

Use a 320x320 cursor capture at screen origin `(800, 400)` and assert that an image rectangle maps proportionally:

```csharp
[Fact]
public void MapsImageBoundsIntoCaptureScreenBounds()
{
    var marker = DetectionMarkerMapper.Map(
        new AnalysisResult(AnalysisKind.Object, "cat", 0.94, new Rect(80, 40, 160, 120), "YOLO"),
        new CaptureRegion(800, 400, 320, 320),
        new Size(320, 320));

    Assert.Equal(new Rect(880, 440, 160, 120), marker.ScreenBounds);
    Assert.Equal(DetectionMarkerShape.Circle, marker.Shape);
}

[Fact]
public void MapsTextAndSpellingToDistinctStyles()
{
    var region = new CaptureRegion(0, 0, 100, 100);
    var size = new Size(100, 100);

    var text = DetectionMarkerMapper.Map(
        new AnalysisResult(AnalysisKind.Text, "Katze", 1, new Rect(10, 20, 40, 12), "OCR"), region, size);
    var spelling = DetectionMarkerMapper.Map(
        new AnalysisResult(AnalysisKind.Spelling, "Katze", 1, new Rect(10, 20, 40, 12), "Hunspell"), region, size);

    Assert.Equal(DetectionMarkerShape.Rectangle, text.Shape);
    Assert.Equal(DetectionMarkerShape.Spelling, spelling.Shape);
    Assert.NotEqual(text.Color, spelling.Color);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter FullyQualifiedName~DetectionMarkerMapperTests
```

Expected: compile failure because the mapper and marker types do not exist.

- [ ] **Step 3: Implement the pure mapper**

Define:

```csharp
public enum DetectionMarkerShape
{
    Circle,
    Rectangle,
    Spelling
}

public sealed record DetectionMarker(
    AnalysisKind Kind,
    string Label,
    double Confidence,
    Rect ScreenBounds,
    DetectionMarkerShape Shape,
    Color Color);
```

Implement:

```csharp
public static DetectionMarker Map(
    AnalysisResult result,
    CaptureRegion captureRegion,
    Size imageSize)
```

Ignore `AnalysisKind.Status`. Scale `result.Bounds` independently on X and Y from image pixels into the capture region, translate by `captureRegion.Left/Top`, clamp to the capture region, and assign these styles:

- `Object`: `Circle`, light green `#9AE6B4`.
- `Text`: `Rectangle`, cyan `#67D7E8`.
- `Spelling`: `Spelling`, orange-red `#FF6B5E`.

Throw `ArgumentOutOfRangeException` for non-positive image dimensions or capture dimensions. Return an empty/invalid marker only for an empty result rectangle; the overlay will skip it.

- [ ] **Step 4: Run the mapper tests and verify they pass**

Run the filtered mapper test command. Expected: all mapping and style tests pass.

## Task 3: Implement the full-screen click-through marker overlay

**Files:**
- Create: `Services/DetectionOverlayWindow.xaml`
- Create: `Services/DetectionOverlayWindow.xaml.cs`

- [ ] **Step 1: Add the transparent window layout**

Create a borderless topmost WPF window with `AllowsTransparency="True"`, `Background="Transparent"`, `ShowInTaskbar="False"`, `ShowActivated="False"`, and a full-window `Canvas` named `MarkerCanvas` with `IsHitTestVisible="False"`.

- [ ] **Step 2: Implement positioning and click-through behavior**

On source initialization, set the window to `ScreenCaptureService.GetVirtualScreenRegion()`. Apply `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, and `WS_EX_TRANSPARENT`; return `HTTRANSPARENT` for non-client hit testing. Keep the overlay independent from the interactive orb window. Do not call `Hide`/`Show` for each capture.

- [ ] **Step 3: Implement marker rendering**

Expose:

```csharp
public void ShowResults(
    IReadOnlyList<AnalysisResult> results,
    CaptureRegion captureRegion,
    Size imageSize,
    TimeSpan visibleDuration)

public void ClearResults()
```

`ShowResults` clears the previous canvas, maps non-status results with `DetectionMarkerMapper`, and draws:

- `Circle`: an `Ellipse` sized to the larger side of the screen bounds, centered on the result, with a translucent fill and colored outline.
- `Rectangle`: a `Border` with colored outline and a small label.
- `Spelling`: a `Border` with red/orange bottom emphasis and a translucent fill.

Animate the canvas/window opacity from `1` to `0` over 500 ms after `visibleDuration`; clear the canvas in the animation completion handler. A new result cancels the previous animation and resets opacity to `1`.

- [ ] **Step 4: Add overlay lifecycle tests at the pure seam**

Keep WPF window tests out of the headless unit suite. Cover all coordinate and style behavior through `DetectionMarkerMapperTests`; manually validate that the overlay is visible, click-through, and fades on Windows.

- [ ] **Step 5: Build the application**

Run:

```powershell
dotnet build KiLupeDemo.csproj --no-restore
```

Expected: WPF build succeeds with no new compiler/XAML errors.

## Task 4: Add an explicit clear command to the orb

**Files:**
- Modify: `Services/OrbOverlayWindow.xaml`
- Modify: `Services/OrbOverlayWindow.xaml.cs`

- [ ] **Step 1: Add the clear orb**

Add a compact `C` button with tooltip `Markierungen loeschen` beside the existing pause/mode/results/exit buttons. Keep the orb width stable so adding the button does not resize the layout while visible.

- [ ] **Step 2: Expose the clear event**

Add:

```csharp
public event EventHandler? ClearRequested;
```

Raise it from the clear button click handler. Do not alter the existing results and exit event semantics.

- [ ] **Step 3: Build and manually inspect the orb**

Run `dotnet build KiLupeDemo.csproj --no-restore`, then start the WPF app with F5. Confirm the orb remains visible during analysis, all buttons receive clicks, and `C` clears markers without leaving floating mode.

## Task 5: Integrate capture metadata and marker lifecycle

**Files:**
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Add overlay ownership and event wiring**

Add a nullable `DetectionOverlayWindow` field. Create it when floating mode starts, subscribe to its clear operation through the orb, show it on the virtual screen, and dispose/close it in `StopFloatingMode` and `OnClosed`.

- [ ] **Step 2: Pass `ScreenCaptureFrame` through the loop**

Change `CaptureFloatingImageAsync` to return `ScreenCaptureFrame`. Pass `frame.Image` to `AnalysisCoordinator`, then pass `snapshot.Results`, `frame.Region`, and `new Size(frame.Image.PixelWidth, frame.Image.PixelHeight)` to `ShowResults`.

Filter out status results before rendering. If no non-status results exist, call `ClearResults()` and leave the status message visible in the orb.

- [ ] **Step 3: Clear markers at all state boundaries**

Call `ClearResults()` when:

- pause is activated;
- the mode changes;
- the clear orb is clicked;
- floating mode stops;
- the main window closes;
- a new capture completes without detections.

Do not hide/show the marker overlay around each capture. If the platform captures transparent overlay pixels in full-screen mode, apply `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` to the marker overlay; otherwise clear only the canvas immediately before capture as a fallback without touching the orb window.

- [ ] **Step 4: Run focused and full tests**

Run:

```powershell
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore --filter "FullyQualifiedName~ScreenCaptureServiceTests|FullyQualifiedName~DetectionMarkerMapperTests"
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore
```

Expected: all existing and new tests pass.

## Task 6: Document and manually verify the user-visible behavior

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Document marker behavior**

Add the marker colors, approximate fade duration, `C` clear command, and the fact that markers are drawn in memory on a click-through overlay and are never saved.

- [ ] **Step 2: Run final verification**

Run:

```powershell
dotnet restore --ignore-failed-sources
dotnet build KiLupeDemo.csproj --no-restore
dotnet test tests\KiLupeDemo.Tests\KiLupeDemo.Tests.csproj --no-restore
```

Expected: build succeeds and all tests pass. The known `NU1900` warning may remain if the private package feed is unavailable.

- [ ] **Step 3: Manual Windows checklist**

1. Press `Strg+Alt+L` to enter floating mode.
2. Point at a known cat image and confirm a green circle appears at the detected screen position.
3. Run text mode and confirm cyan text markers and orange-red spelling markers appear.
4. Confirm markers fade without orb flicker.
5. Click `C` and confirm markers disappear while the orb remains active.
6. Click pause, mode, results, and exit; confirm every action clears markers at the expected boundary.
