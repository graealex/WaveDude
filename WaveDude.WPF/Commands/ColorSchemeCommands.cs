using System.Windows.Input;

namespace WaveDude.WPF.Commands;

/// <summary>Commands of the colour scheme editor (<c>Views/ColorSchemeView</c>), bound on the view itself.</summary>
public static class ColorSchemeCommands
{
	/// <summary>Turns a sampled 255-colour table into the author's 9 evenly spaced stops (<see cref="WaveDude.Rendering.ColorScheme.ReduceToStops"/>).</summary>
	public static readonly RoutedUICommand ReduceToStops = new("Reduce to stops", nameof(ReduceToStops), typeof(ColorSchemeCommands));

	/// <summary>Adds a stop in the widest gap (<see cref="WaveDude.Rendering.ColorScheme.AddStop"/>).</summary>
	public static readonly RoutedUICommand AddStop = new("Add stop", nameof(AddStop), typeof(ColorSchemeCommands));

	/// <summary>Removes the stop given as parameter; the last stop stays.</summary>
	public static readonly RoutedUICommand RemoveStop = new("Remove stop", nameof(RemoveStop), typeof(ColorSchemeCommands));
}
