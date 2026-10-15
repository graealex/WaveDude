using System.Windows.Input;

namespace WaveDudeNext.WPF.Commands;

/// <summary>
/// Commands that edit the project structure. Bound once on the main window; the parameter is
/// the tree node to act on, or null to act on the current selection. Exposed through the
/// Project pane toolbar, the tree's row buttons, its context menu and its key bindings.
/// </summary>
public static class ProjectCommands
{
	/// <summary>Appends a new source.</summary>
	public static readonly RoutedUICommand AddSource = new("Add source", nameof(AddSource), typeof(ProjectCommands));

	/// <summary>Appends a new probe.</summary>
	public static readonly RoutedUICommand AddProbe = new("Add probe", nameof(AddProbe), typeof(ProjectCommands));

	/// <summary>Appends a new item to the folder node given as parameter.</summary>
	public static readonly RoutedUICommand Add = new("Add", nameof(Add), typeof(ProjectCommands));

	/// <summary>Inserts a copy of a source or probe after the original.</summary>
	public static readonly RoutedUICommand Duplicate = new("Duplicate", nameof(Duplicate), typeof(ProjectCommands));

	/// <summary>Removes a source or probe.</summary>
	public static readonly RoutedUICommand Remove = new("Remove", nameof(Remove), typeof(ProjectCommands));

	/// <summary>Swaps a source or probe with its predecessor.</summary>
	public static readonly RoutedUICommand MoveUp = new("Move up", nameof(MoveUp), typeof(ProjectCommands));

	/// <summary>Swaps a source or probe with its successor.</summary>
	public static readonly RoutedUICommand MoveDown = new("Move down", nameof(MoveDown), typeof(ProjectCommands));

	/// <summary>Flips a source's Enabled or a probe's Active (the tree's check boxes; Space on the selected row).</summary>
	public static readonly RoutedUICommand ToggleEnabled = new("Enable or disable", nameof(ToggleEnabled), typeof(ProjectCommands));

	/// <summary>Picks an image file and embeds it as the scene image.</summary>
	public static readonly RoutedUICommand BrowseSceneImage = new("Browse for a scene image", nameof(BrowseSceneImage), typeof(ProjectCommands));
}
