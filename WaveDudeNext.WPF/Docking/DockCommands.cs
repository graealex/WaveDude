using System.Windows.Input;

namespace WaveDudeNext.WPF.Docking;

/// <summary>
/// Routed commands handled by <see cref="DockGroupControl"/> for the group's active item.
/// </summary>
public static class DockCommands
{
	/// <summary>Moves the active item to the zone given as <see cref="DockSide"/> parameter.</summary>
	public static RoutedUICommand MoveToSide { get; } = new("Move to side", nameof(MoveToSide), typeof(DockCommands));

	/// <summary>Hides the active item.</summary>
	public static RoutedUICommand Close { get; } = new("Close", nameof(Close), typeof(DockCommands));
}
