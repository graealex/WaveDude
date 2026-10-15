using System.Windows.Input;

namespace WaveDudeNext.WPF.Commands;

/// <summary>
/// Commands of the View menu's preview quality entries (2026-10-15), bound on the main window (handlers in
/// <c>MainWindow.xaml.cs</c>): each sets one persisted user setting from its parameter, a number as text.
/// The settings describe the machine, not the project: the simulation is the same, only what the preview
/// captures and keeps changes, and an export always uses a full run.
/// </summary>
public static class ViewCommands
{
	/// <summary>Sets the preview resolution: the parameter is the divisor of the canvas along each axis (1, 2 or 4).</summary>
	public static readonly RoutedUICommand SetPreviewResolution = new("Preview resolution", nameof(SetPreviewResolution), typeof(ViewCommands));

	/// <summary>Sets the preview's frame share: the parameter multiplies the project's visualisation interval (1, 2 or 4).</summary>
	public static readonly RoutedUICommand SetPreviewFrames = new("Preview frames", nameof(SetPreviewFrames), typeof(ViewCommands));

	/// <summary>Sets the preview's sample size: the parameter is the bytes per sample (2 for halves, 1 for palette indices).</summary>
	public static readonly RoutedUICommand SetPreviewSamples = new("Preview samples", nameof(SetPreviewSamples), typeof(ViewCommands));
}
