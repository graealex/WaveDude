# Wave Dude 2D — project notes for Claude

Last updated 2026-10-07. Update this file when the facts below change.

## What this is

**Wave Dude 2D v0.29** — a 2D wave-equation simulator (field + accumulated intensity views,
refractive-index scenes from images, point/composite sources, probes with live graphs, video export).
Educational tool by Huygens Optics (`info@huygensoptics.com`, patreon `huygens_optics`).
Licence: **GPL-3.0-or-later, Copyright (C) 2026 JJM Vleggaar (Huygens Optics)** per the headers in the
author's VB source; the C# files do not carry the header yet (see `_tools/VB_SOURCE_AUDIT.md`).

`2D_wave_sim_V01/` is the **author's original VB.NET source**, supplied after the C# port was made. It
is newer than the binary that was decompiled (GPL headers and a GPL Help disclaimer were added after
that build). Treat it as the authority for intent, comments and constant names; `_tools/VB_SOURCE_AUDIT.md`
(2026-10-08) lists everything the decompile lost, file by file, with the C# line each item belongs at.

`Wave_Dude_2D.sln` holds three projects:

| project | what | state |
|---|---|---|
| `Wave_Dude_2D/` | the original WinForms app, **.NET Framework 4.6.1**, legacy csproj, namespace `_2D_wave_sim_V01` | all the real code; builds and runs; **reference implementation, not the future** |
| `WaveDude/` | SDK class library, `net10.0`, nullable + implicit usings | empty; intended home for the UI-independent engine/model code |
| `WaveDude.WPF/` | SDK WPF app, `net10.0-windows`, references `WaveDude` | **the replacement UI**; layout shell only, builds clean (0 warnings) |

WPF shell as it stands:
- `MainWindow` = menu + `AvalonDock.DockingManager` (Project tree pane left, Preview document
  centre, Properties pane right) + status bar. Panes drag/dock/float/auto-hide; nothing is wired to
  data yet. `App.OnStartup` applies the theme before creating the window.
- Docking: `Dirkster.AvalonDock` 4.74.1 + `Dirkster.AvalonDock.Themes.VS2013` (MIT).
- Theming: WPF built-in Fluent theme via `Application.ThemeMode` (experimental API; `WPF0001`
  suppressed in the csproj). `Theming/ThemeController` switches System/Light/Dark, sets the matching
  `Vs2013Dark/LightTheme` on the docking manager (it does not follow Fluent on its own) and darkens
  the native title bar via DWM because WPF only does that on Windows 11. View > Theme drives it; the
  choice is not persisted yet. **Every window that hosts a `DockingManager` must subscribe to
  `ThemeController.Changed`** and re-read `CreateDockTheme()`.
- Preview document: `Panes/PreviewPane` (dummy, 2026-10-08). Two placeholder pictures (field,
  intensity) side by side or stacked (`Orientation`), each centred in its cell, with a `GridSplitter`
  between them (one 3×3 grid; the unused dimension is spanned, so each orientation keeps its own
  ratio; Enter or double-click resets to half). Below them the time axis, `Controls/TimelineBar`
  (2026-10-09, custom `OnRender`: tick labels, playhead with frame number, in/out range as a band
  with brackets, hover frame; UIA peer exposes it as a slider). Transport bar with
  `MediaCommands.Play/Pause` (Space toggles), `Commands/PreviewCommands` (step `,` `.`, Home/End to
  the in/out points, `I`/`O` set them, `X` clears, `L` loops; letters are `KeyBinding`s on the pane
  because `KeyGesture` rejects unmodified letters, and `CanTransport` refuses them inside text
  boxes), a frame and step readout, `FrameCount` bound to `Simulation.Steps`/`VisualizeEvery` via
  `FrameCountConverter`, and an editable speed combo
  (`SpeedPresets` 0.25…4, any value typed; `DoubleTextConverter` ignores text that does not parse
  yet, otherwise every keystroke logs a binding error to the status bar). Numbers follow the Windows
  locale everywhere: `App.OnStartup` overrides `FrameworkElement.Language` with the current culture
  (WPF defaults to en-US), so the preset list and the typed text both say `0,25` on a de-DE machine. Playback is a
  `DispatcherTimer` moving `Position` at `BaseFrameRate` × `Speed`; nothing renders yet. The render
  pipeline will set `FieldImage` / `IntensityImage`. `Editors.xaml` gained the editable-combo text
  box, a `Slider` style and keyboard-focus visuals for combo and slider. An open combo list sits
  on the combo's own background (`ComboBoxListBackground`, not the darker menu popup colour) and
  the highlighted item is the accent with white text (`ComboBoxItemHover*`), like a tree
  selection; agreed with the user 2026-10-09 after the menu colours read as inverted there.
  The in/out range is model state: `Display.ExportFrom`/`ExportTo` in **steps** (0 = start / end of
  the run, also edited on the Display view); the pane derives `InPoint`/`OutPoint` in frames with
  `StepToFrameConverter` and writes steps back only through the commands (the bar's bracket drags
  execute `SetInPoint`/`SetOutPoint` with the frame as parameter). Playback stops at the out point
  or wraps to the in point while looping.
- **Keyboard first.** Every control must be reachable and operable with Tab and the arrow keys and
  show its keyboard focus (accent border); the user tests without the mouse. The preview pane is
  the model: explicit `TabIndex` order, the radio group is one tab stop (`IsTabStop` bound to
  `IsChecked`, arrows handled in code because WPF's directional navigation skips non-tab-stops),
  Play/Pause hand focus to each other when they get disabled, `AutomationProperties.AutomationId`
  on everything a harness needs. Toolbar buttons in `ProjectTree`/`MessagesPane` are still
  `Focusable=False` (reachable only through menus/shortcuts), not yet brought in line.
- File type: `FileAssociation.Register` (called from `App.OnStartup`) registers `.wdproj` → ProgId
  `WaveDude.Project` → `"exe" "%1"` under HKCR when elevated, else HKCU\Software\Classes (same order
  as DTCODataSimulator), idempotently; a double-click arrives as `e.Args[0]` and goes to
  `MainWindow.OpenFile`. That a per-user entry overrides a machine-wide one is intended: a per-user
  install, or a plain copy with no installer, must win over a global installation for that user.
  `assoc`/`ftype` in cmd do not see HKCU registrations; test with a real launch.

History: the C# in `Wave_Dude_2D/` started as ILSpy-decompiled output of a VB.NET binary. It has
since been made to compile, cleaned to idiomatic C#, documented, and partly refactored (details
below). Baseline commit `1266f10` is the first compiling, cleaned, documented state; everything
after it (AVI fix, style pass, engine refactor, WPF shell) is uncommitted at the time of writing.

Repo root also holds runtime data that is tracked on purpose: `ColorSchemeList/` (the shipped colour
schemes, `*.json`) and `Project/` (a sample project root). `.vs/`, `bin/`, `obj/` are ignored.

No automated tests in the solution; two standalone harnesses exist under `_tools/` (see Verification).

## Build

- Visual Studio 18 (2026) Community and the .NET 10 SDK are installed.
- Whole solution (the legacy csproj needs full MSBuild):
  ```
  "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Wave_Dude_2D.sln -restore -p:Configuration=Debug -v:minimal -nologo -m
  ```
- WPF + library only: `dotnet build WaveDude.WPF/WaveDude.WPF.csproj`.
- Expected result: 0 errors; 6 warnings in `Wave_Dude_2D` that are known and harmless
  (`components` never assigned in the four generated Designer files, unused `Form1._hasRun`, one
  sign-extension in `MfVideoWriter`). `DocumentationFile` is on, so malformed `///` XML or a wrong
  `<param>` name is a build warning — keep it at zero.
- Smoke test: launch `Wave_Dude_2D/bin/Debug/Wave_Dude_2D.exe`; the main window must appear with
  title `2D wave simulation - (new project) *`.
- Standalone compile check that does not touch `bin/obj` (safe while other work is in flight):
  ```
  cd Wave_Dude_2D && "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe" -nologo -target:winexe -out:%TEMP%\check.exe -langversion:7.3 -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:Microsoft.VisualBasic.dll -r:System.Web.Extensions.dll -nowarn:0168,0649,0414,0675 *.cs My/*.cs My/Resources/*.cs Properties/*.cs
  ```

## Verification harnesses (`_tools/`)

Run these after touching the code they cover; both are console programs compiled with `csc` against
the project sources (same reference list as above, `-target:exe`, `-nowarn:...,1591`).

- `_tools/engine_check/Program.cs` — add `-main:EngineCheck.Program` and all `Wave_Dude_2D` sources.
  Runs `WaveSimulator` and `GpuWaveSimulator` over one scene (image source, continuous / Gaussian-pulsed /
  detaching smoothed-square object sources, an out-of-canvas source that must be dropped, point and
  integrated-line probes with a start step), with both stencils, and requires field, intensity, probe
  samples and line profiles to agree within 1e-3 relative (actual: ~1e-7). Also exercises probe
  re-install, `SetProbes(null)` and double dispose. Prints `skip GPU: …` when no OpenCL device works.
- `_tools/avi_check/Program.cs` — compile with `AviMjpegWriter.cs` only. Writes a 3-frame MJPEG AVI and
  walks the RIFF tree (44 structural checks: header offsets, strh dwQuality = -1, movi size, idx1 offsets, EOF).

- `_tools/ui_check/PreviewCheck.ps1` drives the WPF preview pane end to end (39 checks: play/pause,
  speed incl. locale spelling, timeline keys, frame steps, in/out/loop, split radios, splitter, tab
  order, focus hand-off, foreground untouched).
  Run `powershell -ExecutionPolicy Bypass -File _tools/ui_check/PreviewCheck.ps1` (`-Trace` adds
  foreground/focus per line); exit code = failures. **How UI harnesses must work here**: the user may
  be using the machine, so no mouse, no `SendKeys`, no `SetForegroundWindow`. Post `WM_KEYDOWN`/
  `WM_CHAR` to the app's HWND (WPF processes them while inactive) and read state back through
  `System.Windows.Automation` properties. Do **not** call UIA *patterns* (`Invoke`, `SetValue`,
  `Select`, `SetFocus`): on this app each one focuses its target and activates the window.
  `_tools/ui_check/ComboListShot.ps1` opens the speed combo by keyboard and screenshots the list
  (the popup has no HWND in UIA, so its screen rectangle is copied), for judging list colours.
  `PrintWindow(…, PW_RENDERFULLCONTENT)` captures the inactive window. UIA lookups can miss while
  the UI thread is busy, so `Find` retries; the harness parks the foreground on its own off-screen
  form (minimise the app, activate the form, show the app without activation). The older scripts in the
  folder (`MessagesCheck`, `ProjectTreeCheck`, `StatusBar*Check`) predate this rule and still use
  patterns/mouse; rewrite them the same way when they are next needed.

Other files in `_tools/` are one-off scripts from the clean-up (`replace_method.py`, `fix_semantic.py`,
`split_form1.py`, `modernize.py`) and the agent guides (`CLEANUP_GUIDE.md`, `DOC_GUIDE.md`). They are
history, not build inputs. `field_usage.py <Class> …` is still useful: it lists every private
field/const of a class with the methods that use it, flagging single-method ones.

## Architecture (Wave_Dude_2D)

```
My/MyApplication.Main  ->  MyProject.Forms.Form1   (VB "My" application framework; to be replaced by Program.Main)
Form1 (partial, 8 files) -> SimulationRunner (own Thread; events ProgressChanged/EngineReady/ProbesUpdated/PreviewReady/Finished,
                            all raised on the worker thread; Form1.PostToUi marshals via BeginInvoke)
                            |- WaveEngineFactory -> IWaveEngine (Width/Height/Stride, FieldData, IntensityData, AdvanceOneStep,
                            |     |                              PrepareFrame, SetProbes/SampleProbes/TakeProbeSamples/ReadProbeProfiles)
                            |     |- WaveSimulator      CPU, Parallel.For over row bands; IDisposable is a no-op
                            |     '- GpuWaveSimulator   OpenCL via OpenClApi (P/Invoke OpenCL.dll); kernels are C# string literals
                            |- FrameRenderer + ColorLut/ColorScheme   float field -> ARGB pixels
                            |- IFrameSink -> MfVideoWriter (Media Foundation SinkWriter P/Invoke, .mp4)
                            |               AviFrameSink/AviMjpegWriter (.avi fallback)
                            '- Probe* (Sampler/Collector/Series/Trace/Update sampled on the worker; GraphPainter/Overlay paint on the UI thread)
Persistence: ProjectStore / ProjectConfig / Project   (JSON via System.Web.Extensions JavaScriptSerializer, snake_case keys)
             AppSettings  -> %AppData%\2D_wave_sim_V01\settings.json
             ColorSchemeStore -> *.json colour schemes in a user-chosen folder (ColorSchemeList/ ships the defaults;
                                 FindDefaultFolder walks up from the exe looking for a *.sln, so a dev build edits the repo copy)
Project folder layout:  <root>/<project>/configuration.json (+ legacy Configuration.txt), assets/, output_frames/, output_video/
Model classes (plain POCOs): SceneSettings, SimulationSettings, SourceSettings(+SourceElement, enums), ProbeSettings(+enums), ColorScheme
```

### Engines (refactored 2026-10-07, verified by `engine_check`)

- `IWaveEngine` does **not** extend `IDisposable`; implementations decide, and `SimulationRunner`
  disposes via `(engine as IDisposable)?.Dispose()`. Both engines have no thread affinity and no
  internal synchronisation: exactly one thread may drive an engine at a time (today the runner's
  worker; the UI thread only reads `FieldData`/`IntensityData` while the run is paused). That is the
  contract to keep when the driving loop becomes `Task`-based.
- Numerics shared through `WaveSimulator`'s `internal static` helpers: `ComputeSubSteps`,
  `LowestSourceLevel`, `IntensityRetention`, `MaxOf`, `NominalDecayLevel`. Units: dx = dt = 1, c in
  cells/step, `c2 = 1/n²`, frequencies in radians/step (`level/255 × 0.5 × scale`), `Time` in steps.
  Scheme: leapfrog, 9-point Laplacian (0.066/0.184/−1) or 4th-order 5-point-per-axis with sub-stepping,
  soft source blending (`u = 0.9u + 0.1v`), two-stage EMA intensity. The docs in both files spell it out.
- `GpuWaveSimulator` keeps device handles in two `Resources` groups (`_scene`: program + kernels +
  buffers for the run; `_probes`: everything `SetProbes` creates, released and rebuilt on every call).
  Kernel arguments are bound at build time; only time (arg 4/11) and the per-sub-step track flag
  (`wave_step` arg 8) change per launch. The object-source kernel has 17 arguments — the table in
  `BuildObjectSourceKernels` is indexed by argument number; an off-by-one there shows up as
  `CL_INVALID_ARG_SIZE`, which the harness catches.
- `WaveSimulator` swaps its two field arrays every sub-step, so `FieldData` must be re-read per frame;
  `GpuWaveSimulator.FieldData` is a stable host array refreshed only by `PrepareFrame`.

### Moving engine code into `WaveDude/` (net10.0, no `-windows`)

Files with no Windows dependency, movable as-is: `WaveSimulator`, `GpuWaveSimulator`, `OpenClApi`,
`GpuDeviceInfo`, `IWaveEngine`, `WaveEngineFactory`, `ProbeSampler`, `ProbeCollector`, `ProbeSeries`,
`ProbeTrace`, `ProbeUpdate`, the `Probe*`/`Source*` enums, `SourceElement`, `SourceSettings`,
`SceneSettings`, `SimulationSettings`, `ProbeSettings`, `MfVideoWriter` + `IMF*Native` (COM, Windows at
runtime but compiles anywhere), `AviMjpegWriter`.
Files that use `System.Drawing` (`Bitmap`/`Graphics`) and need either `net10.0-windows` or an
abstraction first: `SceneData` (`FromImage`), `FrameRenderer` (`ToRgbBytes`/`ToBitmap`),
`SimulationRunner`/`SimulationJob`/`SimulationResult` (carry `Bitmap`s), `ProbeGraphPainter`,
`ProbeOverlay`, `AviFrameSink`, `ColorLut`, `ColorScheme`. Decision pending: target the library at
`net10.0-windows` (simplest; WPF can use `System.Drawing.Common`) or split pixel buffers from GDI+
(cleaner for a `WriteableBitmap` preview).
Also pending for the move: `JavaScriptSerializer` → `System.Text.Json` keeping the snake_case keys so
existing `configuration.json`/`settings.json` files load; namespace `_2D_wave_sim_V01` → `WaveDude`
(the AppData folder name must stay, or be migrated).

## The WinForms UI (reference implementation)

`Form1` is a partial class split along feature seams (~6,500 lines of logic, ~347 methods, 161 event
handlers, 78 `MessageBox.Show` calls):

| file | owns |
|---|---|
| `Form1.cs` | ctor, load/close, menu, project open/save/new, title, **all fields and nested types** (`ProjectListEntry`, `NativeSize`, `ThumbnailOptions`, `ProbeControlSet`) |
| `Form1.Projects.cs` | "Projects in current root" list on the Settings tab |
| `Form1.Output.cs` | Manage-output tab (scene/image/video lists, thumbnails, shell interop) |
| `Form1.Scene.cs` | scene properties, scene image, glow-edge overlay |
| `Form1.Colors.cs` | colour-scheme tab and stop editor |
| `Form1.Simulation.cs` | simulation settings, run/pause/stop, preview, export, Explorer view-mode COM hack |
| `Form1.Probes.cs` | probes tab (2 hard-wired probes via `ProbeControlSet[]`) |
| `Form1.Sources.cs` | sources tab |

Controls are private fields at the bottom of `Form1.Designer.cs`; handlers are attached in
`InitializeComponent` under `// event wiring (was VB WithEvents/Handles)`. The Designer files are
decompiled output with `global::` prefixes and have not been opened in the VS designer — do not
expect round-tripping. `Form1.Designer.cs` has 318 controls on a fixed 1781×989 form with absolute
positions, no `Dock`/`Anchor`, Microsoft Sans Serif in seven sizes, no DPI manifest — the reason the
UI is being rebuilt in WPF rather than restyled. Six tabs: Settings, Sources, Probes, Preview, Manage
output, Colors; a persistent bottom strip with Start/Pause, share-image and quick-navigate buttons.
`HelpForm.cs` holds the user-facing description of every control and is the closest thing to a
functional spec for the WPF UI.

## Known issues (open)

- `WaveEngineFactory.Create`: the "first choice failed" branch is dead (`GpuWaveSimulator.TryCreate`
  never returns an engine together with a message).
- `SimulationSettings.FromConfig` per-key fallbacks differ from the constructor defaults
  (steps 2500 vs 0, 4th-order false vs true, c 0.563 vs 1.0, decay 10 vs 0). **Not a bug**: the VB
  docs (`SimulationSettings.vb:47-49, 92-119`) say old projects must keep their exact behaviour while new
  projects take the Designer values via `StartNewProject`. The C# `<remarks>` now says so.
- Still from the VB audit, not done: no GPL headers in the C# (deliberately deferred; the C# tree is
  temporary); `AssemblyCopyright` lacks the author's name; decompiler artefacts remain (`MessageBoxIcon`
  aliases, `Keys.Return`, `VBByRefStr ref string` on two OpenCL P/Invokes, `OpenClApi` not `static`,
  nested `*EventHandler` delegates in `SimulationRunner`, `colCreatedOn2` declared in `Form1.Simulation.cs`
  instead of the Designer); the csproj does not copy `ColorSchemeList/` to the output as the vbproj did.
- `btnRenameProject_Click` re-opens the project from disk, silently discarding unsaved edits.
- `AboutForm` code says "Version 0.27", designer says 0.29; `HelpForm` text has typos ("everly").
- Unused members: `Form1._hasRun`, `SimulationRunner.JpegQuality`,
  `AppSettings.LastRootFolder/LastProjectName` (written, never read).
- `My/*` + `WindowsFormsApplicationBase` startup is the only remaining `Microsoft.VisualBasic` use;
  replace with `Program.Main` (`EnableVisualStyles`, `SetCompatibleTextRenderingDefault`) when convenient.
- Private `ref` parameters that are really `out` remain outside the engines (`TryPictureToCanvas`,
  `TryParseInt`, `DampingProblem`, `PromptForCValue`, `ComputeRanges`, `RgbToHsv`, …).
- WPF: theme choice not persisted; panes empty.
- WPF preview, frames vs. steps: the transport bar, scrubber and playback count **frames**
  (0..`LastFrame`, one per `VisualizeEvery` steps), while sources, probes, the export range and the
  Properties pane use **steps**; the readout shows both (`773 / 1249  step 1546`), which reads as
  "why is step twice the frame". Undecided (2026-10-09): label it `step 1546 of 2500`, drop the
  step from the bar, or label the timeline ticks in steps. Do not change on your own.

Fixed on 2026-10-07 (kept here so nobody re-reports them): `AviMjpegWriter` header/offset mismatch
(header was 236 bytes vs. 224 assumed; now spec-exact, see `avi_check`); `GpuWaveSimulator.SetProbes`
leaking the previous probe set and ignoring `null`; GPU engine carrying 28 buffer fields that existed
only to be released, plus a hand-copied `ComputeSubSteps`; `SourceElement.DetachRampSteps` now used by
both engines instead of a literal 30.

Fixed on 2026-10-08 from the VB audit (`_tools/VB_SOURCE_AUDIT.md`): the author's rationale comments,
Python-provenance notes and wrong C# docs ported into every C# file; folded constants restored
(`ColorScheme.StopCount/EntryCount`, `SceneSettings.Max*`, `Thumb*`, `FVM_*`, hex OpenCL constants,
`4f/3f`-style fractions, local consts); locals renamed where the VB name was clearer. Behaviour
restored to the author's: `AviMjpegWriter` strh `dwQuality` = -1 (new `avi_check` check);
`WaveSimulator.ApplySources` reuses `_levelValue` instead of allocating per step; `ColorSchemeStore`
probes for `*.sln` (was `*.vbproj`, which never matched above the C# exe); NaN-safe `!(a > b)` checks in
`ProbeGraphPainter`/`ProbeSettings`; `ProbeSampler` uses `!= Amplitude`; `Form1.StopRole` parity test;
`HelpForm` disclaimer is the author's GPL wording.

## Roadmap

1. ~~Make it compile~~ · 2. ~~clean up / document~~ · 2b. ~~AVI fix, style pass, engine refactor~~
3. **Extract the engine into `WaveDude/`** (see "Moving engine code" above), with the WinForms app
   referencing it so both UIs run the same code. Replace `JavaScriptSerializer`; decide the
   `System.Drawing` question; turn the two harnesses into real test projects while at it.
4. **Make the engine loop `Task`-based**: `SimulationRunner` becomes an `async` driver
   (`Task.Run` loop, `CancellationToken`, `IProgress<T>`/channels instead of raw events on a worker
   thread), keeping the one-driver-at-a-time engine contract.
5. **WPF UI** in `WaveDude.WPF`: fill the Project / Preview / Properties panes; preview as a
   `WriteableBitmap` fed from `FrameRenderer` pixel buffers; MVVM over the existing settings POCOs;
   persist the theme choice. Use `HelpForm.cs` as the functional spec.
   Timeline, agreed 2026-10-09 (the scrub bar, in/out range and transport keys are done; these
   stay inside the preview pane, below the pictures):
   - **Event tracks** under the bar, one row per source and probe on the same time axis: a source
     draws a bar from its start to its end step (pulse envelope as a filled shape), a probe draws
     its start step as a flag; clicking selects the item in the Project tree, dragging a bar's
     edge changes its start/stop steps. Needs a scrollable track area and a collapse rule for
     many rows.
   - **Probe signal lane**: the probe's sampled amplitude/intensity over time drawn like an audio
     track on the same axis, so graph and picture line up; only meaningful once a run has data.
   - **Zoom and pan of the time axis**: Ctrl+wheel zooms around the cursor, Shift+wheel pans, a
     thin overview bar shows the visible window; needed once tracks exist, since 2500 steps at
     one pixel per frame do not fit.
   Deferred, possibly never: filmstrip thumbnails behind the bar (needs cached frames).
6. Retire `Wave_Dude_2D` once the WPF app covers it.

## Conventions

- Style: modern concise C# — expression-bodied accessors, **no `this.` qualification**, no
  `IDisposable` on interfaces, `out` not `ref` for outputs, private state only for what genuinely
  outlives a method (run `_tools/field_usage.py` when in doubt). `.editorconfig` encodes it.
- Fix bugs you find and prove the fix with a harness; do not just document them.
- Every type and member carries `///` docs stating threading, units and coordinate systems
  (canvas pixels vs picture-box pixels; steps vs seconds; radians vs degrees). Keep that up.
- Formatting: tabs in `.cs`, Allman braces, governed by `.editorconfig`. In Git Bash `grep -P` is
  broken (locale); use `grep -E`.
- Namespace stays `_2D_wave_sim_V01` in `Wave_Dude_2D` (settings path and resources depend on it).
- `ColorScheme.StopCount` is 9 and `Form1` asserts the nine `btnColorN` buttons match it.
- Probe count is hard-wired to 2 in the UI (`ProbeCount`, duplicated `*2` controls); the engines and
  `ProbeSampler` take any number.
- Video export needs Media Foundation (Windows N: Media Feature Pack); AVI/MJPEG is the fallback.
  GPU path needs an OpenCL ICD (`OpenCL.dll`); the CPU engine is the fallback. This machine has an
  RTX 2080 with working OpenCL, so `engine_check` exercises both paths here.
