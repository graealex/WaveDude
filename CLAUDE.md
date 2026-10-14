# Wave Dude 2D — project notes for Claude

Last updated 2026-10-14. Update this file when the facts below change.

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
| `WaveDude/` | SDK class library, `net10.0`, nullable + implicit usings; packages NLog and `System.Numerics.Tensors` (Microsoft, MIT: vectorised float↔Half) | the model (`Model/`), the engines (`Engine/`), probes, rendering, the run driver and frame store (`Runs/`); see "Architecture (WaveDude library)" |
| `WaveDude.WPF/` | SDK WPF app, `net10.0-windows`, references `WaveDude` | **the replacement UI**; docking shell, editors, preview with live simulation; builds clean (0 warnings) |

WPF shell as it stands:
- `MainWindow` = menu + the in-house `Docking/DockHost` (Project tree pane left, Preview document
  centre, Properties pane right, Messages pane bottom, hidden by default) + status bar. The window
  is its own `DataContext` and owns `Project`, `Filename`, `SelectedItem` and the current `Run`
  (`MainWindow.Simulation.cs`); panes get them through dependency-property bindings. The dock
  layout and window placement persist in `Properties/Settings`; a first start gets
  `MainWindow.DefaultLayout()` (agreed 2026-10-09): a left column of a quarter of the width with the
  Project tree in its upper third and Properties below, the preview in the rest, Messages hidden;
  the preview starts stacked (`PreviewOrientation` default Vertical). No third-party packages but NLog.
- Theming: own `Themes/Dark.xaml` / `Light.xaml` dictionaries swapped by `Theming/ThemeController`
  (persisted in settings); the native title bar is darkened via DWM. Use `DynamicResource` for every
  theme brush.
- Logging: NLog; `Info` and up reach the status bar (`Logging/StatusBarTarget`, Warn/Error coloured),
  everything reaches the Messages pane and the log file next to the exe (Debug builds).
- **Simulation** (2026-10-09): menu Simulation > Run (F5), Pause/Resume (F6), Stop (Shift+F5, also
  Esc). `OnRun` decodes the scene image (`Rendering/SceneImageLoader`, WPF decoders → BGRA bytes),
  snapshots the project into a `RunSpec`, checks the cache drive, creates the `FrameStore` and
  starts a `SimulationRun` posting to the dispatcher's `SynchronizationContext`; the right status
  bar item shows `State: done / total steps, engine` (`RunStatusConverter`, AutomationId
  `StatusRun`). The project stays editable during a run; the run uses its snapshot. New/Open/Close
  and closing the window discard the run and delete its cache file.
- **Live editing** (`MainWindow.Live.cs`, 2026-10-09): a `ProjectWatcher` reports every model
  change; a simulation-affecting one (`ProjectChange.AffectsSimulation`: scene, solver, seed,
  sources; not display or probes) marks the run **stale** (`IsRunStale`: status text gets
  ", edited since", the timeline's available shading turns grey), asks the `ScratchRunner` for the
  frame the preview shows (debounced 120 ms, also on scrubbing while stale or without a run) and,
  with the persisted setting **Simulation > Re-run automatically after edits** (`AutoRerun`, default
  on), redoes the stored run after 1.5 s of quiet. An automatic run (`SimulationRun.IsAutomatic`)
  never moves the preview: the position stays (the reachable end of the axis stays the planned run
  while it fills, so nothing coerces it), Live is switched off whatever the button said, and the
  scratch frame stays on screen until the store has that frame. An edit during an automatic run
  stops it at once (its snapshot is obsolete) and the quiet period starts a fresh one; a run the
  user started keeps going and is just marked stale. Discarding a run keeps the position too.
  **Play means "show me the simulation"**: Play/Space issues `SimulationCommands.EnsureRun`, which
  starts an automatic run when there is none or it is stale (position kept, Live off, playback not
  interrupted); while the run has not reached the playhead, playback holds at the newest frame
  (no loop, no stop) and frames that do not exist yet show black, then it goes on at the chosen
  speed. Skipping to a position first and then pressing Play works the same way (2026-10-09).
  Nothing is requested while a run is active; when it ends and edits happened meanwhile, both are
  scheduled again (agreed 2026-10-09 after the replay-to-position behaviour read as live-follow).
  Scratch frames are requested whenever the store cannot show the position (no run, a stale run, or
  a run that has not got there yet), also while a stored run is active: the runner has its own
  engine, and holding it back made scrubbing during a run show black for seconds (2026-10-09).
  Opening a project requests frame 0 right away so the engine (an OpenCL compile) is built before
  the first click. While a scratch frame computes, an active automatic run is paused (on the CPU the
  run would otherwise take every core and the frame never arrives) and resumed when it is there; an
  automatic run is not started while a scratch frame computes. **A picture changes only when a frame
  for the position itself arrives** (scratch or stored); with nothing for it yet the last picture
  stays. Showing the run's newest frame meanwhile was tried twice and rejected both times: it reads
  as a replay from frame 0 (2026-10-10). That rule also holds without a run: the presenter is
  cleared only when both the run and the scratch frame are gone (the project replaced), not on
  every scrub step, which flashed the empty picture between frames (2026-10-13). A skipped scratch frame (predicted cost over
  `ScratchRunner.Budget`, 6 s) is said in the status bar once per frame.
- **Colour schemes and the scene image** (2026-10-13, decisions provisional, see the list at the end
  of this bullet): a `ColorScheme` is a list of `ColorStop`s, each a colour at a position 0..1
  (`Stops`; the table interpolates between neighbours and holds the end colours outside), observable
  (a stop's edit raises `Stops` on the scheme), XML-serialisable as attributes plus one
  `<Stop At="0.125" Color="#3020F4" />` per stop; `Rgb` prints and parses hex and has a
  `TypeConverter`, so a text box binds to a stop's `Color` directly. The author's files consolidate
  losslessly: his 9 evenly spaced stops stay 9 stops at k/8 (all 11 tables equal their
  interpolation, so his `colors` list was redundant there; his program reads only the table), the 12
  sampled colormaps were 255 stops at i/254 at first and are now **slimmed to their bends**
  (`ColorScheme.Simplify`, Ramer–Douglas–Peucker with a largest-channel tolerance of 3/255, applied
  once with `library_check -- simplify-schemes ColorSchemeList 3`, 2026-10-14): 3 to 21 stops each
  (Copper 3, Orange_hot 4, Hot_orange 5, Blue_to_yellow 7, Simply_red 7, Flare_inv 8, Greens 9,
  rainbow 12, magma 13, inferno 16, Ice_and_fire 21, Orange_symmetry 21). The stop arithmetic
  reproduces the author's resampling bit for bit for evenly spaced stops. **The shipped schemes are now `ColorSchemeList/*.wdscheme`** (converted 2026-10-13
  with `library_check -- convert-schemes <folder>`, a one-off kept in the harness), embedded as
  before; JSON is still read for files from the author's program, and the JSON export keeps his
  layout. `.wdscheme` is registered like `.wdproj` (`FileAssociation.Register` takes the ProgId and
  description); a double-clicked scheme file is added to the open project and selected.
  `Project.ColorSchemes` holds the project's own schemes (loaded files, edited copies) inside the
  `.wdproj`; `ColorSchemeCatalog.Resolve/LutFor(project, …)` look there first, then the user folder,
  then the built-ins.
  `Controls/ColorSchemePicker` (a `ComboBox`, value = the scheme name) lists the project's schemes
  tagged "project", then the shared ones of the layer's `Kind`, each with a gradient strip, plus
  "Load from file…" (JSON or `.wdscheme` into the project) and "Edit a copy in the project…" (clone,
  reduced to 9 stops, selected in the tree). The tree has a "Colour schemes" folder (add, duplicate,
  remove, move, drag like sources); `Views/ColorSchemeView` edits name, kind, category and one row
  per stop (position as a number box, swatch, hex), with "Add stop" (into the widest gap) and a
  remove button per row, plus "Reduce to 9 stops" for a 255-stop table, and a "Based on" combo that
  copies any shared or project scheme's stops into the one being edited (a new scheme starts grey).
  Edits
  recolour the preview at once (`ProjectWatcher` reports them as display changes,
  `PreviewPane.RefreshColors`). The scene image is picked with Scene > Browse… and **embedded**:
  `Scene.ImageData` holds the file's bytes (base64 element, `XmlSerializer`'s own encoding),
  `Scene.Image` just its name, the view shows "w × h, KB"; `LoadSceneImage` decodes the bytes once
  per array, a path is still honoured for old files. Open decisions: base64 vs BaseXML for the
  bytes (base64 is what `XmlSerializer` does natively; BaseXML would need an in-house codec); a colour
  picker dialog for the stops (hex text only today; the WinForms `ColorDialog` would need
  `UseWindowsForms`); renaming a project scheme does not update the layers that refer to it.
- **Scene image in the preview** (2026-10-13, the user's choice after the author's app was checked):
  the original shows the scene image itself as the picking background of its Sources/Probes tabs and
  lays a "glowing edges" outline (per-channel Sobel × 0.25, clamped) over the field and intensity
  previews, blended additively, gain 0–5 per preview, off by default, UI state only, baked into
  exports when on. Here: `GlowingEdges` in the library (`Compute`, and `BlendAdditive` for the
  export, both harness-checked); `DisplayLayer.Overlay` (default **on**), `OverlayGain` (0–5, 1),
  `OverlayInExport` (default **off**; no export code exists yet, it must honour it with
  `BlendAdditive`) stored in the project; `MainWindow.UpdateScenePixels` hands the decoded image
  to `PreviewPane.ScenePixels` on project replace and scene edits; the pane shows the image itself
  where no frame exists (and takes the canvas size from it, which was wrong for image scenes
  before), and draws `SceneBitmaps.Overlay` as a translucent `Image` over each picture (attached
  `PreviewPane.OverlayImage` on the picture cells). WPF composites source-over, not additively:
  the layer's alpha is the edge strength × gain and its colour the edge colour at full brightness,
  so the preview and a baked export differ slightly at strong edges.
- **Languages** (2026-10-14): `Strings/Strings.EN|DE|FR|NL.xaml` carry the same keys (menu headers
  with their mnemonics; the shortcut letters I/O/X/L/F/F5 stay). `Localization/LanguageController`
  mirrors the theme controller: View > Language lists "System" then the tables by their own names,
  the choice is the persisted setting `Language` ("" = system, which falls back to English), and the
  change swaps the merged string dictionary at once. Everything read through `DynamicResource`
  changes live; the dock pane titles, the theme menu entries, the tree's folder labels
  (`NamedCollection.Label` reads its resource key on demand and raises a change on a language switch;
  `Key` keeps the automation ids stable) and the picker's action rows are re-read on the change too,
  through `LanguageController.PropertyChanged` (2026-10-14, after "Sondes" and "Propriétés" stayed
  French). The Properties
  pane is localised through generated keys: `Label.<Type>.<Member>`, `Hint.<Type>.<Member>` and
  `Enum.<Type>.<Member>`; the `meta:Label`/`meta:Hint` extensions return a `DynamicResource` when
  the key exists (live) and the attribute text otherwise, `EnumNameConverter` looks the key up first.
  The model's `DisplayName`/`Description` attributes stay the English source; `_tools/…` is not
  needed: the generator was a one-off script, the English block in `Strings.EN.xaml` is what the
  attributes said on 2026-10-14. **Adding a model property means adding its keys to all four
  tables** (the key-set check in the notes' verification list catches a missing one). Not
  localised: log messages.
- Preview document: `Panes/PreviewPane`. Two pictures (field,
  intensity) side by side or stacked (`Orientation`, persisted as the `PreviewOrientation` user
  setting), each centred in its cell with its label laid out by `Controls/PictureFrame` (label above
  the picture, or rotated 90° on its left when the cell is wider than the picture's aspect ratio, so
  the label never eats the picture's room), with a `GridSplitter`
  between them (one 3×3 grid; the unused dimension is spanned, so each orientation keeps its own
  ratio; Enter or double-click resets to half). Below them the time axis, `Controls/TimelineBar`
  (2026-10-09, custom `OnRender`: tick labels, playhead with frame number, in/out range as a band
  with brackets, hover frame; UIA peer exposes it as a slider). Transport bar with
  `MediaCommands.Play/Pause` (Space toggles), `Commands/PreviewCommands` (step `,` `.`, Home/End to
  the in/out points, `I`/`O` set them, `X` clears, `L` loops; letters are `KeyBinding`s on the pane
  because `KeyGesture` rejects unmodified letters, and `CanTransport` refuses them inside text
  boxes; the pane's `OnKeyDown` marks those letters handled even when the command cannot run,
  otherwise WPF takes an unhandled letter as a menu mnemonic, opens the File menu and activates the
  window, which the harness caught as "the app took the foreground"), a frame and step readout, `FrameCount` bound to `Simulation.Steps`/`VisualizeEvery` via
  `FrameCountConverter`, and an editable speed combo
  (`SpeedPresets` 0.25…4, any value typed; `DoubleTextConverter` ignores text that does not parse
  yet, otherwise every keystroke logs a binding error to the status bar). Numbers follow the Windows
  locale everywhere: `App.OnStartup` overrides `FrameworkElement.Language` with the current culture
  (WPF defaults to en-US), so the preset list and the typed text both say `0,25` on a de-DE machine. Playback is a
  `DispatcherTimer` moving `Position` at `Fps` × `Speed`, where `Fps` is the project's
  `Display.Video.Fps` (speed 1 plays as the export would). The pictures come from the `Run`'s
  `FrameStore` through `Rendering/FramePresenter` (thread-pool colouring with `FrameRenderer`, only
  the newest requested frame is rendered, `WriteableBitmap.WritePixels` on the UI thread); the
  palette, brightness, black level and visibility of each picture follow `Display.Field` /
  `Display.Intensity` (`ColorSchemeCatalog` caches the LUTs), so a finished run can be re-coloured.
  While a run is in progress the timeline axis is the planned run (`PlannedLastFrame`), the frames
  that exist are shaded (`TimelineBar.Available`) and `LastFrame` is the newest one; **`IsLive`**
  follows it until the user scrubs, steps or plays, and the Live toggle (`F`, AutomationId
  `PreviewLive`) re-attaches. Live only exists while a run is in progress: the button is disabled
  otherwise and switches itself off when the run ends (2026-10-09, after it "did not know what to
  do" on a finished run). For an automatic re-run the reachable end of the axis is always the
  planned run, filling or cancelled, so the position is never pulled back, and a position without
  a stored frame shows black (or the scratch frame) rather than some other frame. Playback ends at `min(OutPoint, LastFrame)`. Without a run the
  pictures are **black frames of the canvas size** (`CanvasWidth`/`CanvasHeight`: the run's size,
  else `Scene.Width/Height`; a scene image's size is only known once it ran). Over each picture a
  `Controls/SceneOverlay` draws the project's sources and probes as draggable handles
  (`Controls/MarkerThumb`, a focusable `Thumb`, 20 px: ring = source, square = probe point, both with
  four crosshair ticks outside and a gap around the exact pixel; a line probe gets two handles and
  a dashed line, a composite source its emitter dots, every item its name). Every stroke, dot, line
  and label is drawn over a dark halo first (`HaloThickness`), so the handles read on any picture
  and colour scheme (agreed 2026-10-09 after thin white rings vanished on the intensity picture).
  Dragging writes `Source.X/Y`, `Probe.AX/AY/BX/BY` in canvas pixels; the arrow keys move the
  focused handle by one pixel (ten with Shift); grabbing or focusing a handle selects the item
  (`PreviewPane.SelectedItem` ↔ `MainWindow.SelectedItem` ↔ the Project tree, both ways). Handles
  carry AutomationIds `Source:<name>`, `Probe:<name>:A|B`. The overlay is styled explicitly where it
  is placed: WPF applies implicit styles inside a control template only to `Control`s, and the
  overlay is a `Panel` (its handles came out as bare default thumbs until then). `Editors.xaml` gained the editable-combo text
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
  the model: explicit `TabIndex` order, the menu bar is not in the Tab cycle (F10 focuses it,
  2026-10-14), the radio group is one tab stop (`IsTabStop` bound to
  `IsChecked`, arrows handled in code because WPF's directional navigation skips non-tab-stops),
  Play/Pause hand focus to each other when they get disabled, `AutomationProperties.AutomationId`
  on everything a harness needs. Every shortcut a harness must press needs an **unmodified** key
  (posted messages cannot carry Shift/Ctrl/Alt), which is why Stop also has Esc. Toolbar buttons
  in `ProjectTree`/`MessagesPane` are still `Focusable=False` (reachable only through
  menus/shortcuts), not yet brought in line.
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
schemes, `*.wdscheme` XML since 2026-10-13; **embedded into `WaveDude.dll`** as resources
`ColorSchemes/<file>` by the library's csproj, loaded by `ColorSchemeStore.LoadBuiltIn`; the user's own schemes come from
`%LOCALAPPDATA%\WaveDude\ColorSchemes` and shadow built-ins of the same name; agreed 2026-10-10, no
external files) and `Project/` (a sample project root). `.vs/`, `bin/`, `obj/` are ignored.

No automated tests in the solution; standalone harnesses exist under `_tools/` (see Verification).

## Build

- Visual Studio 18 (2026) Community and the .NET 10 SDK are installed.
- Whole solution (the legacy csproj needs full MSBuild):
  ```
  "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Wave_Dude_2D.sln -restore -p:Configuration=Debug -v:minimal -nologo -m
  ```
- WPF + library only: `dotnet build WaveDude.WPF/WaveDude.WPF.csproj` (the UI harnesses run the Debug exe).
- Publish: `dotnet publish WaveDude.WPF/WaveDude.WPF.csproj -p:PublishProfile=SingleFile` →
  `WaveDude.WPF/bin/Publish/`: one framework-dependent win-x64 `WaveDude.WPF.exe`, nothing else needed.
  The profile takes the framework from the project.
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

**Run only the check that covers the change** (the user's rule, 2026-10-14, after every small edit had
been followed by minutes of GUI harness runs): a clean build plus one targeted probe or screenshot is
the proof for a local change (a label, a glyph, one binding); `library_check` (about 20 s, no GUI) for
library changes; a UI harness only when the flow it covers changed, and only that one. Never re-run
passing harnesses for comfort, never all of them after an edit.

- `_tools/library_check/` — an SDK console project (not in the `.sln`) referencing `WaveDude.csproj`:
  `dotnet run --project _tools/library_check -c Release`. 70+ checks over the library: scene from BGRA
  bytes; CPU vs GPU field, intensity, point samples and line profiles within 1e-3 (actual ~1e-7) for
  both stencils, probe re-install, `SetProbes(null)`, double dispose, the factory fallback; seed
  determinism (same project seed → bit-identical field, different seed → different, **CPU = GPU on
  randomized gaps**); `Emitter.FromSource` mapping; `FrameStore` round trip in `%TEMP%` (zero-copy
  and copying reads, ordering, capacity, deletion); `FrameRenderer` index mapping; every shipped
  shipped `.wdscheme` loads (11 with the author's 9 stops, 12 with 255), `SaveXml` reproduces the
  shipped file byte for byte, the JSON export keeps the author's layout, stops at uneven positions
  interpolate as expected, `AddStop` fills the widest gap; a project file round-trips its own schemes
  and the embedded scene image bytes; editing a stop notifies once; a headless `SimulationRun` (CPU, Completed,
  frames and probe trace counted) and a GPU run paused, resumed and stopped; a 1 GB full-HD store
  must dispose in under 1.5 s (no flush); `CaptureFrame`/`AwaitFrame` (deferred, both planes),
  `Reset` reproducing a run and `SetEmitters` matching a fresh engine, on both engines. Prints
  `skip GPU: …` when no OpenCL device works. `dotnet run --project _tools/library_check -c Release
  -- bench [w h]` prints the per-step cost table above for this machine.

The two legacy harnesses are console programs compiled with `csc` against the `Wave_Dude_2D` sources
(same reference list as above, `-target:exe`, `-nowarn:...,1591`):

- `_tools/engine_check/Program.cs` — add `-main:EngineCheck.Program` and all `Wave_Dude_2D` sources.
  Runs `WaveSimulator` and `GpuWaveSimulator` over one scene (image source, continuous / Gaussian-pulsed /
  detaching smoothed-square object sources, an out-of-canvas source that must be dropped, point and
  integrated-line probes with a start step), with both stencils, and requires field, intensity, probe
  samples and line profiles to agree within 1e-3 relative (actual: ~1e-7). Also exercises probe
  re-install, `SetProbes(null)` and double dispose. Prints `skip GPU: …` when no OpenCL device works.
- `_tools/avi_check/Program.cs` — compile with `AviMjpegWriter.cs` only. Writes a 3-frame MJPEG AVI and
  walks the RIFF tree (44 structural checks: header offsets, strh dwQuality = -1, movi size, idx1 offsets, EOF).

- `_tools/ui_check/PreviewCheck.ps1` drives the WPF preview pane end to end on the default project
  (43 checks: play/pause at the 60 fps clock, speed incl. locale spelling, timeline keys, frame
  steps, in/out/loop, split radios, splitter, tab order, focus hand-off, foreground untouched).
  `_tools/ui_check/RunCheck.ps1` runs simulations (33 checks): `tiny.wdproj` (64×48, 200 steps) with
  F5 to completion, live follow, `,` detaching, the field picture differing between frames (MD5 of
  a `PrintWindow` crop), `F` re-attaching, playback of the stored frames, a second run, the source
  handle sitting at the source's canvas position on both pictures, staying there when the window
  is resized (`SetWindowPos` without activation; the handles once lagged a layout pass behind the
  labels, 2026-10-10) and moving with the arrow keys while the tree selection follows, a scratch frame replacing the black canvas after an edit with
  no run, and after the run: an edit marks it stale, the shown frame is recomputed from scratch,
  the automatic re-run completes with the position kept and reproduces the original picture; then
  `tiny_long.wdproj` (2 000 000 steps, a frame every 1000) for F6 pause/resume and Esc stop with the
  partial frames kept, then an edit starting an automatic re-run that leaves the position and Live
  alone, and a second edit stopping it at once before a fresh one starts. Both dot-source `Common.ps1` (native helpers, lookups, `Start-App`/`Stop-App`,
  `CanvasPoint` for the picture's uniform mapping, `TreeItemSelected`). The split orientation is a
  persisted user setting, so a harness must set the layout it expects instead of assuming it.
  `_tools/ui_check/LanguageCheck.ps1` (11 checks) drives View > Language by keyboard: F10 focuses the
  menu bar (a plain F10 is handled by `MainWindow.OnPreviewKeyDown`, since WPF's own menu mode needs a
  real Alt/F10), Down opens a menu, Left/Right move between menus, Enter picks; switches to German,
  checks menus, tooltips and property labels, restarts the app to check the setting was kept, and
  switches back to System. A window Windows never activated has no focused element and loses posted
  keys; `Start-App` posts `WM_SETFOCUS` then (`FocusedId` "(none)"). Settles are 60 ms per key
  (`Key`'s default), 100–300 ms where layout follows, `WaitFor` for anything the app computes.
  `FocusedId` is **one** UI Automation query for the element with keyboard focus (a template part's
  id such as `PART_EditableTextBox` is mapped to its owner); asking 17 ids one by one cost half a
  second per Tab and made `FocusOn` crawl. Timings after the 2026-10-14 clean-up: RunCheck 67 s
  (was 455), PreviewCheck 12 s (was 36), LanguageCheck 17 s (was 76).
  Run `powershell -ExecutionPolicy Bypass -File _tools/ui_check/<script>.ps1` (`-Trace` adds
  foreground/focus per line); exit code = failures. **How UI harnesses must work here**: the user may
  be using the machine, so no mouse, no `SendKeys`, no `SetForegroundWindow`. Post `WM_KEYDOWN`/
  `WM_CHAR` to the app's HWND (WPF processes them while inactive) and read state back through
  `System.Windows.Automation` properties. Do **not** call UIA *patterns* (`Invoke`, `SetValue`,
  `Select`, `SetFocus`): on this app each one focuses its target and activates the window. Modifier
  keys cannot be posted (WPF reads them from the real keyboard state), so only unmodified shortcuts
  are testable.
  `_tools/ui_check/ComboListShot.ps1` opens the speed combo by keyboard and screenshots the list
  (the popup has no HWND in UIA, so its screen rectangle is copied), for judging list colours.
  `PrintWindow(…, PW_RENDERFULLCONTENT)` captures the inactive window. UIA lookups can miss while
  the UI thread is busy, so `Find` retries; the harness parks the foreground on its own off-screen
  form (minimise the app, activate the form, show the app without activation). The older scripts in the
  folder (`MessagesCheck`, `ProjectTreeCheck`, `StatusBar*Check`) predate this rule and still use
  patterns/mouse; rewrite them on `Common.ps1` when they are next needed.

Other files in `_tools/` are one-off scripts from the clean-up (`replace_method.py`, `fix_semantic.py`,
`split_form1.py`, `modernize.py`) and the agent guides (`CLEANUP_GUIDE.md`, `DOC_GUIDE.md`). They are
history, not build inputs. `field_usage.py <Class> …` is still useful: it lists every private
field/const of a class with the methods that use it, flagging single-method ones.

## Architecture (WaveDude library, 2026-10-09)

```
Project (Model/, UI thread)  --RunSpec.Create-->  RunSpec (immutable snapshot: EngineOptions, Emitters, ProbeSpecs, ScenePixels)
MainWindow.OnRun: FrameCache.CheckSpace -> FrameStore.Create -> new SimulationRun(spec, store, SynchronizationContext).Start()
SimulationRun (Task.Run worker; ManualResetEventSlim pause gate, CancellationTokenSource stop)
   |- SceneData.FromBgra / Empty -> WaveEngineFactory.Create -> IWaveEngine (WaveSimulator CPU | GpuWaveSimulator OpenCL)
   |- per step: AdvanceOneStep, SampleProbes; every VisualizeEvery steps: PrepareFrame -> FrameStore.Write (float -> Half)
   |- per frame: engine.CaptureFrame(snapshot) (GPU: to_half kernel + non-blocking read into a pinned FrameSnapshot;
   |             CPU: row copy) -> writer task: engine.AwaitFrame -> FrameStore.Write -> snapshot back to a pool of 3
   |- ProbeSampler/ProbeCollector -> ProbeUpdate batches (<= 20 Hz) applied to run.Traces on the UI context
   '- State/StepsDone/FrameCount/EngineDescription posted to the UI context (INotifyPropertyChanged, bindable)
ScratchRunner (one long-running task, one kept engine: Reset/SetEmitters between requests, rebuilt when the scene or
   the options change; computes "frame k of the project as it is now" from step 0, newest request wins, skips when
   the predicted cost exceeds Budget) -> ScratchResult (a FrameSnapshot) -> FramePresenter via SnapshotFrameSource
FrameStore (temporary file in %LOCALAPPDATA%\WaveDude\cache, DeleteOnClose, pre-sized; Half planes, padding stripped;
   |           positional RandomAccess reads/writes through the OS cache, no memory mapping)
   '- read by FramePresenter (WPF): CopyLayer -> FrameRenderer (LUT, Parallel.For) -> WriteableBitmap
```

- Namespaces: `WaveDude.Engine` (engines, `SceneData`, `Emitter`, `EngineOptions`, `OpenClApi`),
  `WaveDude.Probes` (`ProbeSpec`, sampler, collector, series, trace, update), `WaveDude.Rendering`
  (`Rgb` with hex text and a `TypeConverter`, `ColorScheme` as one evenly spaced colour list that is
  observable and XML-serialisable, `ColorLut`, `ColorSchemeStore` reading the author's JSON and
  reading/writing XML, built-in schemes embedded, `FrameRenderer`),
  `WaveDude.Runs` (`RunSpec`, `SimulationRun`, `RunState`, `FrameStore`, `FrameLayer(s)`,
  `FrameCache`, `ScenePixels`). All copied from `Wave_Dude_2D` and adapted; the legacy app keeps
  its own copies and does not reference the library. Not ported: video sinks (`MfVideoWriter`,
  `AviMjpegWriter`, `AviFrameSink`), GDI+ painters (`ProbeGraphPainter`, `ProbeOverlay`), the glow
  overlay (now ported as `GlowingEdges`); export will render from the store instead.
- **The engine choice is not in the project** (2026-10-09, the user's call: the old app's flag was an
  artefact): `Simulation > Use the GPU (OpenCL)` is the persisted user setting `UseGpu` (default on),
  passed to `RunSpec.Create(project, image, preferGpu)`; the CPU is the fallback with a note when no
  device works. A `Gpu` attribute in an old `.wdproj` is ignored by the serializer. Nothing but the
  menu item writes the setting; every change is logged with a time stamp and a CPU run caused by it
  says so (2026-10-10, after the GPU was found off twice with no known cause). Note that each exe path
  (Debug, Release, Publish) has its own `user.config` folder under `%LOCALAPPDATA%\WaveDude.WPF`, so
  check the one of the build that was running.
- **Engine contract** (`IWaveEngine`): padded grid (`Stride = Width + 2`), one driver thread at a
  time, `PrepareFrame` before reading, CPU `FieldData` is a different array every sub-step, GPU
  `FieldData` a host copy. Added 2026-10-09: `CaptureFrame(FrameSnapshot, needIntensity)` /
  `AwaitFrame` (the frame as `Half` planes without padding; the GPU converts on the card with the
  `to_half` kernel and reads asynchronously with OpenCL events into pinned arrays, `AwaitFrame` may
  run on another thread; the CPU copies rows at capture and converts at await, so the stepping
  thread pays little), `Reset()` (step 0; probes removed) and `SetEmitters()` (new object sources on
  the same scene; the GPU keeps them in their own `Resources` group and rebinds the intensity
  retention). Engines take `SceneData`, `EngineOptions` and
  `IReadOnlyList<Emitter>`; `Emitter.FromSource` resolves a model `Source` + `SourceElement`
  (amplitude/100, phase in radians, `PulseWidth` → on-width, `OffWidth` → off-width base,
  `Array.RandomOffWidth` → redraw amount, `DetachAfter`). `RunSpec.Create` regenerates the
  elements first (the model only does that on load/reseed/add).
- **Determinism**: every emitter gets a non-zero `RngSeed` from `Pcg32(projectSeed,
  StableHash(name) ^ GapStream)`; both engines redraw the per-period off-width with the same
  xorshift32 (`Emitter.Xorshift32`, the GPU kernel's generator), so a run reproduces from the
  project file and CPU = GPU bit-for-bit on the gap sequence. The legacy engines used unseeded
  `Random` and disagreed with each other.
- **Frames vs. steps**: a frame is recorded after step `i` when `i % VisualizeEvery == 0`, so frame
  k shows the state after 0-based step `k × VisualizeEvery` (what the readout's `step` prints);
  `RunSpec.FrameCount = ceil(Steps / VisualizeEvery)`, the same as `FrameCountConverter`.
- **FrameStore**: 64-byte header (`WDFS`, version, width, height, layers, every, capacity) then
  fixed-size frames, planes in `FrameLayer` order, only those present (`RunSpec.Layers`: field
  always; intensity when the intensity preview or export is on or a probe needs it). The worker
  writes in order (`RandomAccess.Write` of a vectorised Half staging buffer) and publishes `Count`;
  readers use `CopyLayer` (`RandomAccess.Read`; safe against a concurrent `Dispose`, which waits for
  readers). **Not memory-mapped, on purpose** (2026-10-09): the first version mapped the file and
  touching 16 MB of fresh pages per frame cost 14-20 ms per frame on the writer and about as much
  on the reader, and unmapping flushed every dirty page to disk (a minute-long freeze on the second
  run and on closing, for a 10 GB file about to be deleted). Size: a 1920×1080 run of 2500 steps at
  `VisualizeEvery` 2 with both planes is **10.4 GB** on disk (`FrameStore.SizeOf`;
  `FrameCache.CheckSpace` refuses with the number when the drive lacks it); the file is marked
  `Temporary` so the cache manager keeps it in RAM while it can. It is deleted on dispose/exit/crash
  (`DeleteOnClose`); `FrameCache.Sweep` at start-up removes leftovers; the window disposes a store
  on the thread pool, never on the UI thread, and `Dispose` closes the file outside the store's lock
  (deleting 10 GB takes a second; a reader gets false instead of waiting). When a run is replaced the
  window drops `Run` before clearing the stale flag, so the preview never falls back to the old store
  for a moment (that showed the old frame for a second after every edit, 2026-10-09).
- **Numbers (RTX 2080, 64 GB RAM, SATA SSD RAID, Debug build, 2026-10-09)**, from
  `library_check bench 1920 1080` (Release) and the app's "Run timing" Debug line:
  | per step at 1920×1080 | GPU 9-point | GPU 4th order | CPU 9-point |
  |---|---|---|---|
  | pure stepping | 0.25 ms | 0.5 ms | 6 ms |
  | with pipelined half captures every 2 steps | 1.3 ms | 1.6 ms | 13 ms |
  | GPU engine build (OpenCL compile) | 230–300 ms | | CPU build 10 ms |
  The stored full-HD run (2500 steps, 1250 frames, 10.4 GB) still takes **14 s**: the stepping thread
  waits for free snapshots because the writer stores at ~11 ms per frame, which is the disk. Windows
  throttles a process whose dirty cache pages outrun the drive, so a store larger than the cache's
  allowance runs at disk speed whatever the code does (a 1 GB store writes at 600–1100 MB/s). The
  fix, if wanted: a RAM-first store with an explicit budget that spills to the file in the
  background (64 GB here would hold the whole run), or fewer bytes (`VisualizeEvery`, resolution).
  A second OpenCL queue would overlap the 4 ms readback with compute (today an in-order queue
  serialises them). The presenter costs ~22 ms per presented frame, so playback at 60 fps skips
  frames at 1080p (the clock stays correct; only the newest request is rendered).
  **Scratch frames** (the live preview after an edit) cost steps × the pure step time plus one
  capture: frame 1249 of the default project (2499 steps, 4th order) measured **1.5 s** on the GPU
  in the app; on the CPU 14 ms per 4th-order step (frame 150 in 4 s), so frames beyond ~200 are
  skipped (`ScratchRunner.Budget` is 6 s, predicted from the stepping cost of the last request with
  at least 50 steps; a request with no measurement yet always computes). Frames early in the run are proportionally cheaper (frame 0: 90 ms including
  the capture and the UI round trip).
- Known limits: probes are recorded (`SimulationRun.Traces`) but not drawn yet; export is not
  wired; `OpenClApi` imports `"OpenCL"` (resolves to `OpenCL.dll` here, `libOpenCL.so` elsewhere).
  The library is compiled with `<Optimize>true</Optimize>` in every configuration (2026-10-10): a
  Debug build of it ran the CPU engine's stencil loop at 33 ms per full-HD step instead of 14, which
  made the CPU preview unusable from the app. Flip it locally to step through engine code. The
  conversions go through `TensorPrimitives` for the same reason.

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

### What stayed in Wave_Dude_2D

The engines, probes, colour schemes and renderer were **copied** into `WaveDude/` on 2026-10-09 (see
"Architecture (WaveDude library)"); `Wave_Dude_2D/` is untouched, keeps its own copies and does not
reference the library. Still only there: `SimulationRunner`/`SimulationJob`/`SimulationResult`
(replaced by `RunSpec`/`SimulationRun`), the video sinks (`MfVideoWriter` + `IMF*Native`,
`AviMjpegWriter`, `AviFrameSink`), the GDI+ painters (`ProbeGraphPainter`, `ProbeOverlay`), the glow
overlay, the JSON project/settings persistence (`ProjectStore`, `ProjectConfig`, `AppSettings`,
`JavaScriptSerializer`; the WPF app uses `.wdproj` XML instead). The library stays `net10.0` without
`System.Drawing`: pixels are BGRA byte arrays (`ScenePixels`) and packed `int` BGRA32 buffers.

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

- `Wave_Dude_2D.WaveEngineFactory.Create`: the "first choice failed" branch is dead
  (`GpuWaveSimulator.TryCreate` never returns an engine together with a message). Fixed in the
  library's copy only.
- `SimulationSettings.FromConfig` per-key fallbacks differ from the constructor defaults
  (steps 2500 vs 0, 4th-order false vs true, c 0.563 vs 1.0, decay 10 vs 0). **Not a bug**: the VB
  docs (`SimulationSettings.vb:47-49, 92-119`) say old projects must keep their exact behaviour while new
  projects take the Designer values via `StartNewProject`. The C# `<remarks>` now says so.
- Still from the VB audit, not done: no GPL headers in the C# (deliberately deferred; the C# tree is
  temporary); `AssemblyCopyright` lacks the author's name; decompiler artefacts remain (`MessageBoxIcon`
  aliases, `Keys.Return`, `VBByRefStr ref string` on two OpenCL P/Invokes and `OpenClApi` not `static`
  (both fixed in the library's copy only),
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
- WPF: no progress bar for a run (status text only); the Simulation menu has no About/Help; probes
  recorded but not drawn; export not rendered from the store yet; the preview skips frames at
  1080p (see the numbers in the library architecture section).
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

## Author feedback (Jeroen Vleggaar, e-mail of 2026-10-10)

Thirteen points on a sneak peek of the WPF app; status as of 2026-10-12:
- Done: (3) handles misplaced after a pane move (the arrange-size bug above); (13) Enter commits a
  text edit, Escape reverts it (`Controls/EditBehavior`, set by the base `TextBox` style; the default
  binding waited for Tab) and every numeric field is a `Controls/NumberBox` (2026-10-12: a `TextBox`
  with up/down repeat buttons; Up/Down step by `Increment`, PageUp/PageDown by ten, the wheel while
  focused; each step clamps to `Minimum`/`Maximum`, commits through the `Text` binding at once and
  selects the text; the views set the model's ranges); (6) a read-only **wavelength** row in the source view
  (`Simulation.Wavelength`: λ = c / f, the author's `UpdateLambdaLabel` formula; "(nominal)" with the
  9-point stencil, which disperses; the Properties pane got a `Project` DP for it); (8) the probe
  measurement combo offers only what the layer allows (`Probe.MeasurementsFor`, the layer setter
  coerces) and the intensity measurements read "Value" / "Integrated value" (`[Description]` on the
  enum members, honoured by `EnumNameConverter`); (9) the four random amounts of an array sit under
  their own header "Random offsets, added per emitter"; (2) the in/out tooltips say "export range".
- Done 2026-10-13: (1) a Run button (ring with a triangle, F5) left of Play, (4) two toggles left of
  the split radios show or hide each picture (bound to the layer's `Preview`, so hiding the intensity
  also stops storing it; the remaining picture takes the whole area and the splitter collapses,
  `UpdatePictureLayout`), (6/3) the scene image and its outline in the preview.
  (11) check boxes on the source and probe rows of the tree (bound to `Enabled` / `Active`,
  `ProjectCommands.ToggleEnabled` on Space and in the context menu; disabled rows read grey; rows
  carry `Tree:<name>` automation ids and the boxes `Enabled:<name>`).
  (1, second half) the step buttons now draw the frame-step look, "◀|" "|▶", instead of the players'
  skip glyphs, and the Run glyph is a 14 px ring (2026-10-13).
- Still open: "re-run automatically does not always work" (by design: display and probe edits do
  not re-run, and the run waits 1.5 s of quiet).
- Roadmap already: (7) probe graphs, (10) export, (12) project format (the user answered: images
  embedded or relative, output next to the file, the JSON format imported).
- The author also suggests dropping the 9-point Laplacian (confusing; only useful to show dispersion).

## Handoff 2026-10-14: state and next work

Everything up to here is committed in five commits (editors, colour schemes + scene image, preview
outline/toggles/tree, localisation, harnesses). Working state: a project with sources, probes, an
embedded scene image and its own colour schemes is edited in the tree and the Properties pane, the
preview shows scratch frames and stored runs with the scene outline, runs go to a temp frame store,
the language switches live, and three UI harnesses plus `library_check` are green (RunCheck 67 s,
PreviewCheck 12 s, LanguageCheck 17 s, `library_check` about 20 s). The next session is meant for
**probes** and **export**; here is what exists for each and what is decided.

**Probes (nothing drawn yet).**
- Recorded: `SimulationRun.Traces` (`ProbeTrace` per active probe, `ProbeSeries` of samples per
  visualised step, line probes as profiles), filled on the UI context in `ProbeUpdate` batches
  (at most 20 Hz). `ScratchRunner` does not sample probes; only a run has traces (decide whether a
  scratch frame should carry a single sample).
- Model: `Probe` has everything the author's graph box used: `Kind` (point/line), `Layer`,
  `Measurement` (amplitude | value | integrated value), `BoxSize`, `BoxX/BoxY` (canvas pixels),
  `ScaleMode` (running window / full run), `Window` (steps), `AutoScaleX/Y`, `YMin/YMax`,
  `StartAfter`. The handles on the pictures exist (`SceneOverlay`, ids `Probe:<name>:A|B`).
- Spec: the author's `ProbeGraphPainter.cs` / `ProbeOverlay.cs` in `Wave_Dude_2D/` (GDI+, not
  ported) draw the box over the output picture; `HelpForm.cs` describes the controls. Agreed plan: a
  **probe signal lane** under the timeline on the same time axis (roadmap item 5), and the graph box
  over the picture as the author had it. Rendering belongs in the library as pixel/geometry code (no
  GDI+); the WPF side draws it (an `OnRender` layer like `TimelineBar`). Author's point 7.

**Export (nothing wired).**
- Frames: `FrameStore.CopyLayer(frame, layer, Span<Half>)` of a run (finished, or after
  `Run.Completion`), rendered with `FrameRenderer.RenderField/RenderIntensity` and the layer's LUT
  (`ColorSchemeCatalog.LutFor(project, name, blackLevel)`, brightness), then, per layer and only when
  `DisplayLayer.OverlayInExport`, `GlowingEdges.BlendAdditive` with the layer's `OverlayGain` (the
  author bakes the outline additively; the preview only composites it). Range:
  `Display.ExportFrom/ExportTo` in **steps** (0 = start/end), one frame per `VisualizeEvery` steps.
  Which outputs: `DisplayLayer.Export`. Encoder settings: `Video.Fps`, `Video.Quality`.
- Writers: none in the library yet; the legacy `Wave_Dude_2D/MfVideoWriter.cs` (+ `IMF*Native`,
  Media Foundation SinkWriter, .mp4) and `AviMjpegWriter.cs` (MJPEG fallback, spec-exact, see
  `_tools/avi_check`) compile anywhere and were listed as movable as-is; stills can use WPF's
  `PngBitmapEncoder` on the rendered `int[]`. The author names exports with a running number
  (`_0001`, one above the highest in the folder) and stores them next to the project; the user told
  him output goes relative to the project file (the folder of `Filename`; an unsaved project must be
  saved first, like `RunErrorUnsavedImagePath`).
- Needs: a progress item (status text only today), cancellation, a menu entry (Simulation or File),
  and a decision whether export needs a finished run (simplest: export the frames that exist).
  Author's point 10.

**Also open, smaller.** Import of the author's JSON projects (`Wave_Dude_Examples/*/configuration.json`;
the legacy `ProjectConfig`/`ProjectStore` read them; the user promised the author an importer);
a colour picker dialog for stops (hex only now; WinForms `ColorDialog` needs `UseWindowsForms`);
offer `ColorScheme.Simplify` in the editor; renaming a project scheme does not update the layers that
refer to it; base64 vs BaseXML for the embedded image; the RAM-first frame store (numbers above);
dropping the 9-point Laplacian (author's suggestion, user undecided); timeline event tracks, zoom and
pan (roadmap); Help > About has no content; dock pane titles are not exposed to UI Automation.

## Roadmap

1. ~~Make it compile~~ · 2. ~~clean up / document~~ · 2b. ~~AVI fix, style pass, engine refactor~~
3. ~~**Extract the engine into `WaveDude/`**~~ (done 2026-10-09 as a copy; the WinForms app stays
   legacy and is not changed). Still open from this item: real test projects (the user chose to
   keep the `dotnet run` harness for now).
4. ~~**Make the engine loop `Task`-based**~~ (`SimulationRun`, 2026-10-09).
5. **WPF UI** in `WaveDude.WPF`: the preview shows a live run and replays it from the frame store
   (done 2026-10-09); editors, colour schemes, scene image and outline, languages (done 2026-10-14,
   see the handoff section). Next, in this order of value: **probes** (graph box and signal lane),
   **export from the store** (video and stills, rendered with the current display settings, the
   export range, the outline when opted in), a progress bar.
   Store: a RAM-first frame store with a budget and background spill (see the numbers above),
   so a run is compute-bound instead of disk-bound.
   Timeline, agreed 2026-10-09 (the scrub bar, in/out range, transport keys and the available-frames
   shading are done; these stay inside the preview pane, below the pictures):
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
