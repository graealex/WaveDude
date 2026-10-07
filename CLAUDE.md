# Wave Dude 2D — project notes for Claude

Analysis snapshot: 2026-10-06. Update this file when the facts below change.

## What this is

**Wave Dude 2D v0.29** — a 2D wave-equation simulator (field + accumulated intensity views,
refractive-index scenes from images, point/composite sources, probes with live graphs, video export).
Educational tool by Huygens Optics (`info@huygensoptics.com`, patreon `huygens_optics`).

- WinForms, **.NET Framework 4.6.1**, legacy (non-SDK) `.csproj`, root namespace `_2D_wave_sim_V01`,
  assembly `Wave_Dude_2D`. Single project in `Wave_Dude_2D.sln`.
- **The C# in this repo started life as ILSpy-decompiled output of a VB.NET build.** As of
  2026-10-07 it has been cleaned up to read like hand-written C#: closure artifacts, VB `WithEvents`
  wrappers, `// Token` comments, `checked {}` blocks, `bool flag = …` temps, VB runtime helper
  calls and placeholder locals (`num2`, `text3`, …) are gone from all hand-maintained files.
  What still betrays the origin: the `My/` namespace + `WindowsFormsApplicationBase` startup, the
  `*.Designer.cs` files (left verbatim apart from the field/event-wiring block), `ref` parameters
  that are really `out`, and `this.` on every member access.
- Git: repo was just initialised; **nothing is committed yet** and there is **no `.gitignore`**
  (`.vs/` is untracked junk that must not be committed).
- No tests. No CI. No README besides this file.

## Build

- Toolchain found on this machine: Visual Studio 18 (2026) Community. `dotnet` 10 SDK is installed but
  the legacy csproj needs full MSBuild:
  ```
  "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Wave_Dude_2D.sln -p:Configuration=Debug -v:minimal -nologo -m
  ```
- **Current state (2026-10-07): compiles and runs.** 0 errors, 6 cosmetic warnings
  (`components` never assigned in the Designer files, unused `_hasRun`, one sign-extension in
  `MfVideoWriter`). Smoke test = launch exe, main window appears with title
  `2D wave simulation - (new project) *`.
- Standalone compile check that does not touch `bin/obj` (safe to run in parallel with other work):
  `_tools/CLEANUP_GUIDE.md` has the full `csc.exe` command line.
- `_tools/` holds the one-off Python scripts used for the decompiler clean-up (`replace_method.py`,
  `fix_semantic.py`, `split_form1.py`, …) plus the guide. They are history, not build inputs.

## Architecture map

```
My/MyApplication.Main  ->  MyProject.Forms.Form1   (VB "My" application framework; replace with Program.Main)
Form1 (14,473 lines)   ->  SimulationRunner (own Thread; events ProgressChanged/EngineReady/ProbesUpdated/PreviewReady/Finished)
                            |- WaveEngineFactory -> IWaveEngine
                            |     |- WaveSimulator      CPU, Parallel.For
                            |     '- GpuWaveSimulator   OpenCL via OpenClApi (P/Invoke OpenCL.dll), kernels as strings
                            |- FrameRenderer + ColorLut/ColorScheme   float field -> ARGB pixels
                            |- IFrameSink -> MfVideoWriter (Media Foundation SinkWriter P/Invoke, .mp4)
                            |               AviMjpegWriter (.avi fallback)
                            '- Probe* (Sampler/Collector/Series/Trace/GraphPainter/Overlay)
Persistence: ProjectStore / ProjectConfig / Project   (JSON via System.Web.Extensions JavaScriptSerializer)
             AppSettings  -> %AppData%\2D_wave_sim_V01\settings.json
             ColorSchemeStore -> *.json colour schemes in a user-chosen folder
Project folder layout:  <root>/<project>/configuration.json (+ legacy Configuration.txt), assets/, output_frames/, output_video/
Model classes (plain POCOs, safe to reuse as-is): SceneSettings, SimulationSettings, SourceSettings(+SourceElement, enums), ProbeSettings(+enums), ColorScheme
```

UI -> engine marshalling is `Form1.PostToUi(Action)` = `BeginInvoke` guarded by `IsHandleCreated`.
Everything engine-side (`WaveSimulator`, `GpuWaveSimulator`, `OpenClApi`, `Mf*`, `IMF*Native`, `Probe*`,
`FrameRenderer`) is UI-independent and should be left alone during UI work beyond compile fixes.

## The UI as it stands (the thing to make viable)

`Form1` is still a god-class, but it is now a **partial class split along its feature seams**
(~6,500 lines of logic in total, ~347 methods, 161 event handlers, 78 `MessageBox.Show` calls):

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

Controls are plain private fields declared at the bottom of `Form1.Designer.cs`; event handlers are
attached in `InitializeComponent` under the comment `// event wiring (was VB WithEvents/Handles)`.
The VS designer has **not** been verified to open these forms — the Designer files are decompiled
output with `global::` prefixes; expect to regenerate them when the layout is rebuilt (roadmap 5).

`Form1.Designer.cs` (3,564 lines): **318 controls** — 91 Labels / 59 Buttons / 39 NumericUpDowns /
28 CheckBoxes / 15 TextBoxes / 13 GroupBoxes / 13 ComboBoxes / 11 Panels / 10 RadioButtons / 6 ListViews /
2 PictureBoxes; one `TabControl` (`tabMainFunctions`, owner-drawn, tabs gated via `Enabled` until a
project is open) with six pages: **Settings, Sources, Probes, Preview, Manage output, Colors**.
Persistent bottom strip: `grpSimulationControlButtons` (Start / Pause-Resume), `grpShareImage`,
`grpNavigateTabs` (jump buttons), `btnSaveConfiguration`; `MenuStrip1` (File: New / Open / Choose project
root / Set c-value / Save / Exit; Info: Help / About); `StatusStrip1`.

Why it is not viable today:

- **Fixed 1781x989 client size, 297 absolute `Location`s, zero `Dock`/`Anchor`, no `MinimumSize`,
  no `TableLayoutPanel`/`FlowLayoutPanel`.** Does not fit a 1080p screen with taskbar; nothing reflows.
- `AutoScaleMode.Font` at 6x13 with **Microsoft Sans Serif** in 7 sizes (9.75–20.25 pt); 2 stray Segoe UI.
  No `app.manifest`, no DPI awareness -> blurry/clipped on >100 % scaling.
- Ad-hoc colours (`Azure`, `Gainsboro`, `SystemColors.Menu`), hand-made owner-drawn tabs.
- Secondary UI: `AboutForm` (version string stale: code says 0.27, designer 0.29), `HelpForm` (long
  help text as string constants in code), `frmDescription`; `ColorPickerControl` is a code-only
  UserControl hard-sized to 423x411; `BrowseForFolder` builds its own folder dialog form by hand;
  `SetExplorerWindowViewMode` late-binds `Shell.Application` COM to restyle Explorer windows (fragile).
- Help text in `HelpForm.cs` documents the intended behaviour of every control — use it as the spec.

## Recommended roadmap ("make the UI viable")

Do these in order; each step leaves the app runnable.

0. ~~**Git hygiene**~~ — `.gitignore` added. **Nothing is committed yet**; the pre-cleanup
   decompiled baseline is not recoverable from git (only from the original binary).
1. ~~**Make it compile**~~ — done 2026-10-06.
2. **Strip VB residue** — done 2026-10-07 (WithEvents block, Token comments, VB runtime
   helpers, placeholder names, control flow), followed by an XML-doc pass: every type and member
   in the hand-maintained files has `///` docs (~1,400 members; `DocumentationFile` is on in the
   csproj so malformed docs are build warnings). Threading, units and coordinate systems are stated
   in the docs — read them before changing anything on the engine/UI boundary.
   **Latent issues found while documenting, left unchanged on purpose** (behaviour-preserving pass):
   - `AviMjpegWriter`: the header actually written is 236 bytes but the `Pos*` patch offsets assume
     224, so the `movi` size is patched into the BITMAPINFOHEADER and `idx1` offsets are off by 12.
     Players may or may not tolerate it; see the class `<remarks>`.
   - `WaveEngineFactory.Create`: the "first choice failed" branch is dead (`GpuWaveSimulator.TryCreate`
     never returns an engine together with a message).
   - `SimulationSettings.FromConfig` per-key fallbacks differ from the constructor defaults
     (steps 2500 vs 0, 4th-order false vs true, c 0.563 vs 1.0, decay 10 vs 0).
   - `btnRenameProject_Click` re-opens the project from disk, silently discarding unsaved edits.
   - `GpuWaveSimulator.SetProbes` leaks earlier probe kernel handles if called twice; `SetProbes(null)`
     does not clear an installed sampler (CPU engine does).
   - Unused members kept for signature stability: `Form1.ThumbWidth/ThumbHeight/FVM_*`, `_hasRun`,
     `SourceElement.DetachRampSteps` (engines use literal 30), `SimulationRunner.JpegQuality`,
     `AviMjpegWriter.MaxFileSize`, `AppSettings.LastRootFolder/LastProjectName` (written, never read).
   Still open: replace `My/*` with a plain `Program.Main`
   (`EnableVisualStyles`, `SetCompatibleTextRenderingDefault`, `SetHighDpiMode`); convert
   `ref` parameters that are really `out`; drop the `Microsoft.VisualBasic` reference once nothing
   uses it.
3. **Retarget** — recommended: SDK-style csproj on **.NET 8 (or 10) `net8.0-windows` + `UseWindowsForms`**.
   Consequences: `JavaScriptSerializer` (System.Web.Extensions) -> `System.Text.Json` with the same
   snake_case keys (`last_root_folder`, ...) so existing projects still load; `Microsoft.VisualBasic`
   still exists on .NET; P/Invoke code (OpenCL, Media Foundation, shell32, gdi32) ports unchanged.
   Fallback if staying on Framework: 4.8 + `app.manifest` with PerMonitorV2.
4. **Split `Form1` by tab** into UserControls (`SettingsPage`, `SourcesPage`, `ProbesPage`,
   `PreviewPage`, `OutputPage`, `ColorsPage`) following the existing line-range seams; keep a thin
   shell form owning menu, status bar, bottom strip, and the `SimulationRunner` lifecycle. Move
   project/scene/simulation state out of the form into a small view-model/service so pages share it.
5. **Layout** — per page: `TableLayoutPanel`/`FlowLayoutPanel`, `Dock`/`Anchor`, `MinimumSize`,
   splitter between settings and preview, single font (Segoe UI 9 pt, let DPI scaling do the rest),
   drop custom colours and owner-drawn tabs; replace `MessageBox` validation with inline hints / `ErrorProvider`.
6. Only after 1–5: consider moving off WinForms (WPF/Avalonia). Not before — the engine/UI boundary
   must be clean first, and WinForms is sufficient for this app's needs.

## Conventions / gotchas when editing here

- Formatting is governed by `.editorconfig` (tabs in `.cs`). In Git Bash, `grep -E` needs `$'	'` for
  tabs; `grep -P` is broken (locale error).
- Namespace stays `_2D_wave_sim_V01` until step 3 (settings path and resources depend on it).
- `Microsoft.VisualBasic` is still referenced only for `WindowsFormsApplicationBase` in `My/`.
- `ColorScheme.StopCount` is 9 and `Form1` asserts the nine `btnColorN` buttons match it.
- Probe count is hard-wired to 2 (`ProbeCount`, duplicated `*2` controls/handlers) — a `ProbeControlSet`
  array already abstracts it; generalising is cheap once the UI is split.
- Video export requires Media Foundation (Windows N editions need the Media Feature Pack); AVI/MJPEG is
  the fallback. GPU path needs an OpenCL ICD (`OpenCL.dll`); the CPU engine is the fallback.
