# Ki-Lupe Correction Bottom Dock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed correction overlay with a bottom-docked, resizable correction pane inside the left image workspace while keeping the right results pane readable and responsive.

**Architecture:** The left workspace becomes a two-row `Grid` owned by `MainWindow`: the image/marker surface stays in the first row, and the correction content plus a horizontal `GridSplitter` occupy the second row only when correction suggestions are visible. The right results list remains independent, but its `ScrollViewer` disables horizontal scrolling and its text blocks wrap. The existing correction state and coordinator contracts remain unchanged.

**Tech Stack:** .NET 8 WPF, XAML `GridSplitter`, `ScrollViewer`, existing `CorrectionPresentationState`, xUnit tests.

---

### Task 1: Reshape the main workspace

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Replace the left `Border` content with a two-row workspace grid.**

Keep the existing image surface and result overlay in the first row. Add a `GridSplitter` and correction pane in the second row. The correction row starts collapsed and has a minimum height so wrapped text and both copy buttons remain visible.

- [ ] **Step 2: Move correction content into the dock row.**

Remove the correction panel's absolute `Canvas.Left`, `Canvas.Top`, `Width`, and `Height` positioning. Use a header row, wrapped original/suggestion text, and a button row with `HorizontalAlignment="Right"` so the controls cannot be clipped by a fixed-height overlay.

- [ ] **Step 3: Drive dock visibility from `UpdateCorrectionSurface`.**

When no suggestion exists, collapse the splitter and correction row and restore the image row to `*`. When suggestions exist, show the splitter and correction row, assign a bounded default correction height, and keep the existing correction state and Orb synchronization.

- [ ] **Step 4: Preserve the existing drag handlers only where needed.**

The old floating-panel mouse-drag handlers must no longer control the docked panel. Remove their event hookups and dead state if the compiler confirms they are unused. Keep copy, hide, and correction state behavior intact.

### Task 2: Make the results pane wrap cleanly

**Files:**
- Modify: `MainWindow.xaml`

- [ ] **Step 1: Disable horizontal scrolling on `ResultsList`.**

Set its horizontal `ScrollBarVisibility` to `Disabled` and vertical visibility to `Auto`.

- [ ] **Step 2: Make each result item measure to the available width.**

Set the `ListBoxItem` horizontal content alignment to `Stretch` and set wrapping plus `TextTrimming="None"` on label, details, and kind text where necessary.

- [ ] **Step 3: Keep result cards from forcing a wider layout.**

Use `MaxWidth="{Binding ActualWidth, ElementName=ResultsList}"` or an equivalent stretch layout and ensure long OCR details wrap within the card.

### Task 3: Add layout regression coverage

**Files:**
- Create or modify: `tests/KiLupeDemo.Tests/CorrectionDockLayoutTests.cs`
- Modify: `docs/architecture.md`

- [ ] **Step 1: Keep the pure docking layout tests green.**

Verify the existing correction docking calculations remain valid after the fixed overlay is removed.

- [ ] **Step 2: Document the new UI boundary.**

Describe the correction panel as a bottom-docked, resizable subpane of the left image workspace rather than a free-floating Canvas overlay. Record that horizontal scrolling is disabled for the right results list.

### Task 4: Verify the change

**Files:**
- None

- [ ] **Step 1: Run the WPF build.**

Run `dotnet build KiLupeDemo.csproj --no-restore`; expected result is zero errors.

- [ ] **Step 2: Run focused correction and layout tests.**

Run `dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore --filter "FullyQualifiedName~Correction"`; expected result is all selected tests passing.

- [ ] **Step 3: Run the complete test suite.**

Run `dotnet test tests/KiLupeDemo.Tests/KiLupeDemo.Tests.csproj --no-restore`; expected result is all tests passing.
