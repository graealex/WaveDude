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

	private void CanRun(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = Run?.IsActive != true;

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
	private void StartRun(bool automatic = false)
	{
		if (!Settings.Default.UseGpu)
		{
			Logger.Info("Running on the CPU: 'Use the GPU' is off in the Simulation menu");
		}
		RunSpec spec = RunSpec.Create(Project, LoadSceneImage(), Settings.Default.UseGpu);
		FrameCache.CheckSpace(FrameStore.SizeOf(spec.Width, spec.Height, spec.Layers, spec.FrameCount));
		DiscardRun();
		FrameStore store = FrameStore.Create(FrameCache.NewPath(), spec.Width, spec.Height, spec.Layers, spec.FrameCount, spec.VisualizeEvery);
		var run = new SimulationRun(spec, store, SynchronizationContext.Current, automatic);
		run.PropertyChanged += (_, args) =>
		{
			if (args.PropertyName == nameof(SimulationRun.State))
			{
				CommandManager.InvalidateRequerySuggested();
			}
		};
		run.Finished += (_, _) => OnRunFinished();
		Logger.Info("{Kind} started: {Steps} steps, {Frames} frames of {Width}×{Height}; edits made from now on do not affect it",
			automatic ? "Re-run" : "Run", spec.Steps, spec.FrameCount, spec.Width, spec.Height);
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

	/// <summary>Play pressed: a missing or stale run is started like an automatic one (position kept, not followed); errors go to the log and status bar.</summary>
	private void OnEnsureRun(object sender, ExecutedRoutedEventArgs e)
	{
		if (Run?.IsActive == true || (Run != null && !IsRunStale && !IsProbesStale))
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

	private void OnStop(object sender, ExecutedRoutedEventArgs e) => Run?.Stop();

	/// <summary>
	/// Drops the current run: stops it and disposes its store (deleting the cache file) once the worker has
	/// let go of it. Called before a new run, when the project is replaced and on closing.
	/// </summary>
	private void DiscardRun()
	{
		SimulationRun? old = Run;
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
