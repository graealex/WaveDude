using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WaveDude.Model;
using WaveDude.Rendering;
using WaveDude.Runs;
using WaveDude.WPF.Properties;
using WaveDude.WPF.Rendering;

namespace WaveDude.WPF;

/// <summary>
/// Keeps the preview current after edits: a <see cref="ProjectWatcher"/> notices every change, a
/// simulation-affecting one marks the run stale (<see cref="IsRunStale"/>), asks the <see cref="ScratchRunner"/>
/// for the frame the preview shows (debounced, newest wins) and, when <see cref="Settings.AutoRerun"/> is on,
/// starts a stored re-run once the user has been idle for a moment; an edit during an automatic run stops
/// that run at once, since its snapshot is obsolete. Scrubbing asks for scratch frames whenever the store
/// cannot show the position: no run, a stale run, or a run that has not got there yet. The scratch runner
/// has its own engine, and any run in progress is paused while a scratch frame computes, so the frame the
/// user looks at always comes first (the user's rule, 2026-10-14).
/// </summary>
public partial class MainWindow
{
	/// <summary>How long after the last edit or scrub a scratch frame is requested.</summary>
	private static readonly TimeSpan ScratchDelay = TimeSpan.FromMilliseconds(120);

	/// <summary>How long the user must be idle after an edit before the stored run is redone.</summary>
	private static readonly TimeSpan RerunDelay = TimeSpan.FromMilliseconds(1500);

	/// <summary>Identifies the <see cref="IsRunStale"/> property.</summary>
	public static readonly DependencyProperty IsRunStaleProperty = DependencyProperty.Register(
		nameof(IsRunStale), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

	/// <summary>Identifies the <see cref="IsComputingFrame"/> property.</summary>
	public static readonly DependencyProperty IsComputingFrameProperty = DependencyProperty.Register(
		nameof(IsComputingFrame), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

	/// <summary>Identifies the <see cref="IsProbesStale"/> property.</summary>
	public static readonly DependencyProperty IsProbesStaleProperty = DependencyProperty.Register(
		nameof(IsProbesStale), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

	/// <summary>Identifies the <see cref="ScratchFrame"/> property.</summary>
	public static readonly DependencyProperty ScratchFrameProperty = DependencyProperty.Register(
		nameof(ScratchFrame), typeof(ScratchResult), typeof(MainWindow), new PropertyMetadata(null));

	/// <summary>Identifies the <see cref="PreviewPosition"/> property.</summary>
	public static readonly DependencyProperty PreviewPositionProperty = DependencyProperty.Register(
		nameof(PreviewPosition), typeof(int), typeof(MainWindow), new PropertyMetadata(0, (d, _) => ((MainWindow)d).OnPreviewPositionChanged()));

	private ProjectWatcher? _watcher;
	private ScratchRunner? _scratch;
	private SimulationRun? _pausedForScratch;
	private int _lastSkippedFrame = -1;
	private DispatcherTimer? _scratchTimer;
	private DispatcherTimer? _rerunTimer;
	private (RunSpec Spec, int Frame)? _scratchRequested;
	private (string Path, DateTime Written, ScenePixels Pixels)? _imageCache;
	private (byte[] Data, ScenePixels Pixels)? _embeddedCache;

	/// <summary>Whether the project was edited in a way that changes the simulation since the current run started.</summary>
	public bool IsRunStale
	{
		get => (bool)GetValue(IsRunStaleProperty);
		set => SetValue(IsRunStaleProperty, value);
	}

	/// <summary>
	/// Whether a probe was edited (measurement, position, list) since the current run started: its frames are
	/// still right, its recordings are not. The graphs then draw from the scratch frame's recording, and Play
	/// or F5 starts a run that refreshes them; no run is started by the edit itself (the user's rule,
	/// 2026-10-14: a probe never changes the picture, and a drag must not launch a simulation).
	/// </summary>
	public bool IsProbesStale
	{
		get => (bool)GetValue(IsProbesStaleProperty);
		set => SetValue(IsProbesStaleProperty, value);
	}

	/// <summary>
	/// Whether a scratch frame for the preview's position is being computed: from the request until its result
	/// (or a newer request's). The preview dims its pictures and shows a spinner meanwhile, so the wait reads as
	/// work in progress rather than as a preview that does not update (the author's impression; the user's
	/// rule, 2026-10-14).
	/// </summary>
	public bool IsComputingFrame
	{
		get => (bool)GetValue(IsComputingFrameProperty);
		private set => SetValue(IsComputingFrameProperty, value);
	}

	/// <summary>The newest scratch frame (or skip notice) for the preview's position; null when none applies.</summary>
	public ScratchResult? ScratchFrame
	{
		get => (ScratchResult?)GetValue(ScratchFrameProperty);
		set => SetValue(ScratchFrameProperty, value);
	}

	/// <summary>The preview's frame index, bound from the pane so scratch requests follow it.</summary>
	public int PreviewPosition
	{
		get => (int)GetValue(PreviewPositionProperty);
		set => SetValue(PreviewPositionProperty, value);
	}

	/// <summary>A new project: watch it instead of the old one; nothing is stale and no scratch frame applies.</summary>
	private void OnProjectReplaced(Project? project)
	{
		_watcher?.Dispose();
		_watcher = null;
		_imageCache = null;
		_embeddedCache = null;
		// the new project's own schemes and scene image; the pane does not exist yet when the constructor sets the first project
		Dispatcher.InvokeAsync(() =>
		{
			previewPane?.RefreshColors();
			UpdateScenePixels();
		}, DispatcherPriority.Background);
		IsRunStale = false;
		IsProbesStale = false;
		IsDirty = false;
		ScratchFrame = null;
		if (project != null)
		{
			_watcher = new ProjectWatcher(project);
			_watcher.Changed += OnProjectChanged;
			// warm up: the first scratch frame builds the engine (an OpenCL compile), better now than on the first click
			ScheduleScratch();
		}
	}

	private void OnProjectChanged(object? sender, ProjectChange change)
	{
		IsDirty = true;
		if (change.Item is ColorScheme or ObservableCollection<ColorScheme>)
		{
			// a project scheme edited, added or removed: recolour the frame shown
			previewPane.RefreshColors();
			return;
		}
		if (change.AffectsProbes)
		{
			// the frames stay, the recordings are stale: the scratch frame brings the new recording for the position
			if (Run != null && !IsProbesStale)
			{
				Logger.Debug("Probes edited ({Item}.{Property}): the run's recordings are stale", change.Item, change.Property);
				IsProbesStale = true;
			}
			ScheduleScratch();
			return;
		}
		if (!change.AffectsSimulation)
		{
			return;
		}
		if (Run != null && !IsRunStale)
		{
			Logger.Debug("Edited ({Item}.{Property}): the run is stale", change.Item, change.Property ?? "list");
		}
		if (change.Item is Scene)
		{
			UpdateScenePixels();
		}
		if (Run != null)
		{
			IsRunStale = true;
		}
		if (Run is { IsAutomatic: true, IsActive: true })
		{
			// its snapshot is already obsolete: stop it now, the quiet period starts a fresh one
			Logger.Debug("The automatic run is stopped: its snapshot is obsolete");
			Run.Stop();
		}
		ScheduleScratch();
		ScheduleRerun();
	}

	/// <summary>Whether the store cannot show the preview's position, or its recordings are stale: no run, a stale one, stale probes, or a run that has not reached it.</summary>
	private bool NeedsScratch => Run == null || IsRunStale || IsProbesStale || Run.FrameCount <= PreviewPosition;

	private void OnPreviewPositionChanged()
	{
		if (NeedsScratch)
		{
			ScheduleScratch();
		}
	}

	/// <summary>After <see cref="ScratchDelay"/> of quiet, asks for the frame at the preview's position.</summary>
	private void ScheduleScratch()
	{
		_scratchTimer ??= MakeTimer(ScratchDelay, RequestScratch);
		_scratchTimer.Stop();
		_scratchTimer.Start();
	}

	private void RequestScratch()
	{
		_scratchTimer!.Stop();
		if (!NeedsScratch || Project.Simulation.Steps < 1)
		{
			return;
		}
		RunSpec? spec = TrySnapshotProject();
		if (spec == null)
		{
			return;
		}
		if (_scratch == null)
		{
			_scratch = new ScratchRunner(SynchronizationContext.Current);
			_scratch.Completed += OnScratchCompleted;
		}
		// the user's frame comes first: a run in progress, automatic or started by the user, would otherwise take
		// every core (CPU) or the card's time away from it; it continues once the frame is there (any run since
		// 2026-10-14; a run the user paused stays paused)
		if (Run is { IsActive: true } run && !run.IsPauseRequested)
		{
			run.Pause();
			_pausedForScratch = run;
		}
		int frame = Math.Clamp(PreviewPosition, 0, Math.Max(0, spec.FrameCount - 1));
		Logger.Debug("Preview frame {Frame} requested from scratch ({Reason})", frame,
			Run == null ? "no run" : IsRunStale ? "the run is stale" : IsProbesStale ? "the probes changed" : "the run has not got there yet");
		_scratchRequested = (spec, frame);
		_scratch.Request(spec, frame);
		SetScratchBusy(frame);
		IsComputingFrame = true;
	}

	private void OnScratchCompleted(object? sender, ScratchResult result)
	{
		// the request this result answers is the newest one: the indicator goes off (a newer request keeps it on)
		if (_scratchRequested is { } requested && ReferenceEquals(requested.Spec, result.Spec) && requested.Frame == result.Frame)
		{
			_scratchRequested = null;
			SetScratchBusy(-1);
			IsComputingFrame = false;
		}
		if (_pausedForScratch is { } paused)
		{
			_pausedForScratch = null;
			if (ReferenceEquals(paused, Run) && paused.IsActive)
			{
				paused.Resume();
			}
		}
		if (result.Snapshot == null && result.Frame != _lastSkippedFrame)
		{
			// the status bar says why nothing appears; once per frame, so scrubbing does not flood it
			_lastSkippedFrame = result.Frame;
			Logger.Info("Preview of frame {Frame} skipped: it would take {Seconds:0} s to compute; wait for the run, or use the GPU", result.Frame, result.Estimate.TotalSeconds);
		}
		ScratchFrame = result;
	}

	/// <summary>After <see cref="RerunDelay"/> of quiet, redoes the stored run when the setting is on and the run is stale.</summary>
	private void ScheduleRerun()
	{
		if (!Settings.Default.AutoRerun)
		{
			return;
		}
		_rerunTimer ??= MakeTimer(RerunDelay, AutoRerun);
		_rerunTimer.Stop();
		_rerunTimer.Start();
	}

	private void AutoRerun()
	{
		_rerunTimer!.Stop();
		if (!Settings.Default.AutoRerun || !IsRunStale || Run?.IsActive == true)
		{
			Logger.Debug("No automatic re-run: {Reason}", !Settings.Default.AutoRerun ? "switched off" : !IsRunStale ? "the run is not stale" : "a run is active");
			return;
		}
		if (_scratch?.IsBusy == true || _scratchTimer?.IsEnabled == true || IsExporting)
		{
			// the frame the user is looking at comes first, requested or computing; the run would take the cores away
			// from it. An export reads the current run's store, so it keeps its run until it is done.
			Logger.Debug("Automatic re-run postponed: {Reason}", IsExporting ? "an export is in progress" : "a preview frame is being computed");
			ScheduleRerun();
			return;
		}
		try
		{
			StartRun(automatic: true);
		}
		catch (Exception ex) when (ex is RunSpecException or IOException or UnauthorizedAccessException or NotSupportedException)
		{
			// no message box for something the user did not ask for; the status bar shows the error
			Logger.Error(ex, "The automatic re-run could not start: {Message}", ex.Message);
		}
	}

	/// <summary>Called when a run ends: edits made meanwhile are still pending, so look again.</summary>
	private void OnRunFinished()
	{
		if (IsRunStale)
		{
			ScheduleScratch();
			ScheduleRerun();
		}
		else if (IsProbesStale)
		{
			ScheduleScratch();
		}
	}

	/// <summary>The project as a run spec with the scene image decoded (cached by path and write time), or null when it cannot run (logged at Debug).</summary>
	private RunSpec? TrySnapshotProject()
	{
		try
		{
			return RunSpec.Create(Project, LoadSceneImage(), Settings.Default.UseGpu);
		}
		catch (Exception ex) when (ex is RunSpecException or IOException or UnauthorizedAccessException or NotSupportedException)
		{
			Logger.Debug("No preview: {Message}", ex.Message);
			return null;
		}
	}

	/// <summary>Hands the preview the decoded scene image (null for a canvas, or when it cannot be read: that is logged once the run reports it).</summary>
	private void UpdateScenePixels()
	{
		if (previewPane == null)
		{
			return;
		}
		try
		{
			previewPane.ScenePixels = LoadSceneImage();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or RunSpecException)
		{
			Logger.Debug("Scene image not shown: {Message}", ex.Message);
			previewPane.ScenePixels = null;
		}
	}

	/// <summary>The decoded scene image for an image scene, decoded once per file version; null for a blank canvas.</summary>
	private ScenePixels? LoadSceneImage()
	{
		if (Project.Scene.Mode != SceneMode.Image)
		{
			return null;
		}
		if (Project.Scene.ImageData is { } data)
		{
			// embedded: decoded once per byte array (a new pick is a new array)
			if (_embeddedCache is { } embedded && ReferenceEquals(embedded.Data, data))
			{
				return embedded.Pixels;
			}
			ScenePixels decoded = SceneImageLoader.Load(data);
			_embeddedCache = (data, decoded);
			return decoded;
		}
		string path = ResolveScenePath();
		DateTime written = File.GetLastWriteTimeUtc(path);
		if (_imageCache is { } cached && cached.Path == path && cached.Written == written)
		{
			return cached.Pixels;
		}
		ScenePixels pixels = SceneImageLoader.Load(path);
		_imageCache = (path, written, pixels);
		return pixels;
	}

	private DispatcherTimer MakeTimer(TimeSpan interval, Action tick)
	{
		var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = interval };
		timer.Tick += (_, _) => tick();
		return timer;
	}

	private void ShutdownScratch()
	{
		_scratchRequested = null;
		_scratchTimer?.Stop();
		_rerunTimer?.Stop();
		_scratch?.Dispose();
		_scratch = null;
		_watcher?.Dispose();
		_watcher = null;
	}
}
