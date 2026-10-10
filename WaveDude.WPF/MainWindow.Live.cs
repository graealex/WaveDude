using System.IO;
using System.Windows;
using System.Windows.Threading;
using WaveDude.Model;
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
/// has its own engine, so a stored run in progress does not hold it back.
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
	private (string Path, DateTime Written, ScenePixels Pixels)? _imageCache;

	/// <summary>Whether the project was edited in a way that changes the simulation since the current run started.</summary>
	public bool IsRunStale
	{
		get => (bool)GetValue(IsRunStaleProperty);
		set => SetValue(IsRunStaleProperty, value);
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
		IsRunStale = false;
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
		if (!change.AffectsSimulation)
		{
			return;
		}
		if (Run != null)
		{
			IsRunStale = true;
		}
		if (Run is { IsAutomatic: true, IsActive: true })
		{
			// its snapshot is already obsolete: stop it now, the quiet period starts a fresh one
			Run.Stop();
		}
		ScheduleScratch();
		ScheduleRerun();
	}

	/// <summary>Whether the store cannot show the preview's position: no run, a stale one, or one that has not reached it.</summary>
	private bool NeedsScratch => Run == null || IsRunStale || Run.FrameCount <= PreviewPosition;

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
		// the user's frame comes first: an automatic run in progress would otherwise take every core (CPU) or
		// the card's time away from it; it continues once the frame is there
		if (Run is { IsAutomatic: true, IsActive: true } run && !run.IsPauseRequested)
		{
			run.Pause();
			_pausedForScratch = run;
		}
		_scratch.Request(spec, Math.Clamp(PreviewPosition, 0, Math.Max(0, spec.FrameCount - 1)));
	}

	private void OnScratchCompleted(object? sender, ScratchResult result)
	{
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
			return;
		}
		if (_scratch?.IsBusy == true)
		{
			// the frame the user is looking at comes first; the run would take the cores away from it
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

	/// <summary>The decoded scene image for an image scene, decoded once per file version; null for a blank canvas.</summary>
	private ScenePixels? LoadSceneImage()
	{
		if (Project.Scene.Mode != SceneMode.Image)
		{
			return null;
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
		_scratchTimer?.Stop();
		_rerunTimer?.Stop();
		_scratch?.Dispose();
		_scratch = null;
		_watcher?.Dispose();
		_watcher = null;
	}
}
