using System.IO;
using System.Windows;
using System.Windows.Input;
using WaveDude.Model;
using WaveDude.Runs;
using WaveDude.WPF.Properties;
using WaveDude.WPF.Rendering;

namespace WaveDude.WPF;

/// <summary>
/// Handlers of <see cref="Commands.SimulationCommands"/>: the window owns the current
/// <see cref="Run"/> and its frame store. A run snapshots the project when it starts; editing
/// the project meanwhile is allowed and does not affect it. The store lives until the run is
/// replaced, the project is replaced or the window closes.
/// </summary>
public partial class MainWindow
{
	/// <summary>Identifies the <see cref="Run"/> property.</summary>
	public static readonly DependencyProperty RunProperty = DependencyProperty.Register(
		nameof(Run), typeof(SimulationRun), typeof(MainWindow),
		new PropertyMetadata(null, (_, _) => CommandManager.InvalidateRequerySuggested()));

	/// <summary>The current run (active or finished, its frames in the preview), or null.</summary>
	public SimulationRun? Run
	{
		get => (SimulationRun?)GetValue(RunProperty);
		set => SetValue(RunProperty, value);
	}

	private void CanRun(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Run?.IsActive != true && !IsExporting;

	private void OnRun(object sender, ExecutedRoutedEventArgs e)
	{
		try
		{
			StartRun();
		}
		catch (Exception ex) when (ex is RunSpecException or IOException or UnauthorizedAccessException or NotSupportedException)
		{
			Logger.Error(ex, "The run could not start: {Message}", ex.Message);
			ShowError((string)FindResource("RunError"), ex);
		}
	}

	/// <summary>Decodes the scene image, snapshots the project, checks the disk, creates the store and starts the worker.</summary>
	/// <param name="automatic">Started after an edit rather than by the user: the preview keeps its position.</param>
	/// <param name="trackIntensity">Store the intensity plane whatever the project needs (an export of the intensity asks for it).</param>
	private void StartRun(bool automatic = false, bool trackIntensity = false)
	{
		if (!Settings.Default.UseGpu)
		{
			Logger.Info("Running on the CPU: 'Use the GPU' is off in the Simulation menu");
		}
		RunSpec spec = RunSpec.Create(Project, LoadSceneImage(), Settings.Default.UseGpu, trackIntensity);
		// the frames stay in memory up to the budget (the user's share of the RAM, capped by what is free now); only
		// the rest needs room on the cache drive (2026-10-14)
		long budget = FrameMemory.Budget(Settings.Default.FrameMemoryPercent);
		int ramFrames = Math.Min(spec.FrameCount, FrameStore.FramesInBudget(spec.Width, spec.Height, spec.Layers, budget));
		if (ramFrames < spec.FrameCount)
		{
			FrameCache.CheckSpace(FrameStore.SizeOf(spec.Width, spec.Height, spec.Layers, spec.FrameCount - ramFrames));
		}
		DiscardRun();
		FrameStore store = FrameStore.Create(FrameCache.NewPath(), spec.Width, spec.Height, spec.Layers, spec.FrameCount, spec.VisualizeEvery, budget);
		var run = new SimulationRun(spec, store, SynchronizationContext.Current, automatic);
		run.PropertyChanged += (_, args) =>
		{
			if (args.PropertyName == nameof(SimulationRun.State))
			{
				CommandManager.InvalidateRequerySuggested();
			}
		};
		run.Finished += (_, _) => OnRunFinished();
		Logger.Info("{Kind} started: {Steps} steps, {Frames} frames of {Width}×{Height}, {RamFrames} in memory ({RamBytes}) and {DiskFrames} on disk; edits made from now on do not affect it",
			automatic ? "Re-run" : "Run", spec.Steps, spec.FrameCount, spec.Width, spec.Height, store.RamFrames, FrameMemory.Gigabytes(store.RamBytes), spec.FrameCount - store.RamFrames);
		IsRunStale = false;
		IsProbesStale = false;
		Run = run;
		run.Start();
	}

	/// <summary>The scene image's full path: as given when rooted, otherwise relative to the project file.</summary>
	/// <exception cref="RunSpecException">The path is relative and the project has not been saved.</exception>
	private string ResolveScenePath()
	{
		string image = Project.Scene.Image;
		if (image.Length == 0)
		{
			throw new RunSpecException((string)FindResource("RunErrorNoImage"));
		}
		if (Path.IsPathRooted(image))
		{
			return image;
		}
		string? folder = Filename != null ? Path.GetDirectoryName(Filename) : null;
		if (folder == null)
		{
			throw new RunSpecException((string)FindResource("RunErrorUnsavedImagePath"));
		}
		return Path.GetFullPath(image, folder);
	}

	private void CanControlRun(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Run?.IsActive == true;

	/// <summary>Stop applies to a run in progress, or to an export in progress (Esc cancels it).</summary>
	private void CanStop(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Run?.IsActive == true || Export?.IsActive == true;

	/// <summary>Play pressed: a missing or stale run is started like an automatic one (position kept, not followed); errors go to the log and status bar. Not while an export reads the store.</summary>
	private void OnEnsureRun(object sender, ExecutedRoutedEventArgs e)
	{
		if (IsExporting || Run?.IsActive == true || (Run != null && !IsRunStale && !IsProbesStale))
		{
			return;
		}
		try
		{
			StartRun(automatic: true);
		}
		catch (Exception ex) when (ex is RunSpecException or IOException or UnauthorizedAccessException or NotSupportedException)
		{
			Logger.Error(ex, "The run could not start: {Message}", ex.Message);
		}
	}

	private void OnTogglePause(object sender, ExecutedRoutedEventArgs e) => Run?.TogglePause();

	/// <summary>Cancels the export when one is running (it has the user's attention then), else stops the run.</summary>
	private void OnStop(object sender, ExecutedRoutedEventArgs e)
	{
		if (Export?.IsActive == true)
		{
			Export.Cancel();
		}
		else
		{
			Run?.Stop();
		}
	}

	/// <summary>
	/// Drops the current run: stops it and disposes its store (deleting the cache file) once the worker has
	/// let go of it. Called before a new run, when the project is replaced and on closing.
	/// </summary>
	private void DiscardRun()
	{
		if (Export?.IsActive == true)
		{
			// the export reads this run's store; a project replaced under it ends it with what it has
			Logger.Warn("Export cancelled: its run is being discarded");
			Export.Cancel();
		}
		SimulationRun? old = Run;
		if (old != null)
		{
			Logger.Debug("Run discarded: {State}, {Frames} frames, cache {Path}", old.State, old.FrameCount, old.Store.Path);
		}
		// the run goes first: with it gone the preview shows the scratch frame; clearing the stale flag before
		// would make it pick the old store for a moment (and wait on its file being deleted)
		Run = null;
		IsRunStale = false;
		IsProbesStale = false;
		if (old == null)
		{
			return;
		}
		old.Stop();
		// on the thread pool: unmapping a run's cache file is quick, but never the UI thread's wait
		old.Completion.ContinueWith(_ =>
		{
			old.Store.Dispose();
			old.Dispose();
		}, TaskScheduler.Default);
	}

	/// <summary>Synchronous variant for closing: waits (briefly) for the worker so the file is closed before the process ends.</summary>
	private void ShutdownRun()
	{
		SimulationRun? old = Run;
		if (old == null)
		{
			return;
		}
		Run = null;
		old.Stop();
		if (!old.Completion.Wait(TimeSpan.FromSeconds(5)))
		{
			Logger.Warn("The run did not stop in time; its cache file is deleted by the system when the process ends");
		}
		// not awaited: the process exit closes the handles and deletes the file in any case
		Task.Run(() =>
		{
			old.Store.Dispose();
			old.Dispose();
		});
	}
}
