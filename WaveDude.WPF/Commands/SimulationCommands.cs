using System.Windows.Input;

namespace WaveDude.WPF.Commands;

/// <summary>
/// Commands that control the simulation run, bound on the main window (handlers in
/// <c>MainWindow.Simulation.cs</c>) and offered in the Simulation menu.
/// </summary>
public static class SimulationCommands
{
	/// <summary>Starts a run of the open project, replacing the previous run and its frames.</summary>
	public static readonly RoutedUICommand Run = new("Run", nameof(Run), typeof(SimulationCommands),
		[new KeyGesture(Key.F5)]);

	/// <summary>Pauses the run, or resumes a paused one.</summary>
	public static readonly RoutedUICommand TogglePause = new("Pause", nameof(TogglePause), typeof(SimulationCommands),
		[new KeyGesture(Key.F6)]);

	/// <summary>Makes sure the frames are coming: starts a run (keeping the preview's position) when there is none or it is stale; no gesture, the preview's Play issues it.</summary>
	public static readonly RoutedUICommand EnsureRun = new("Run if needed", nameof(EnsureRun), typeof(SimulationCommands));

	/// <summary>Stops the run; the frames computed so far stay in the preview. Shift+F5, or Escape (the usual "stop what you are doing" key, and reachable without a modifier).</summary>
	public static readonly RoutedUICommand Stop = new("Stop", nameof(Stop), typeof(SimulationCommands),
		[new KeyGesture(Key.F5, ModifierKeys.Shift), new KeyGesture(Key.Escape)]);
}
