using System.Collections;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using WaveDude.Model;
using WaveDude.Rendering;
using WaveDude.Runs;
using WaveDude.WPF.Converters;
using WaveDude.WPF.Rendering;

namespace WaveDude.WPF;

/// <summary>
/// Handlers of <see cref="Commands.ProjectCommands"/>: structural edits of the open project.
/// A command parameter names the node to act on; without one the tree selection is used.
/// </summary>
public partial class MainWindow
{
	private object? Target(object? parameter) => parameter ?? SelectedItem;

	/// <summary>The project list a tree node stands for: a folder's list, or the list containing an item.</summary>
	private IList? ListOf(object? node) => node switch
	{
		NamedCollection folder when ReferenceEquals(folder.Items, Project.Sources) => Project.Sources,
		NamedCollection folder when ReferenceEquals(folder.Items, Project.Probes) => Project.Probes,
		NamedCollection folder when ReferenceEquals(folder.Items, Project.ColorSchemes) => Project.ColorSchemes,
		Source => Project.Sources,
		Probe => Project.Probes,
		ColorScheme => Project.ColorSchemes,
		_ => null,
	};

	private void OnAddSource(object sender, ExecutedRoutedEventArgs e) => Added(Project.AddSource());

	private void OnAddProbe(object sender, ExecutedRoutedEventArgs e) => Added(Project.AddProbe());

	private void CanAdd(object sender, CanExecuteRoutedEventArgs e) =>
		e.CanExecute = Target(e.Parameter) is NamedCollection folder && ListOf(folder) != null;

	private void OnAdd(object sender, ExecutedRoutedEventArgs e)
	{
		IList? list = ListOf(Target(e.Parameter) as NamedCollection);
		if (ReferenceEquals(list, Project.Sources))
		{
			Added(Project.AddSource());
		}
		else if (ReferenceEquals(list, Project.Probes))
		{
			Added(Project.AddProbe());
		}
		else if (ReferenceEquals(list, Project.ColorSchemes))
		{
			Added(Project.AddColorScheme());
		}
	}

	private void CanEditItem(object sender, CanExecuteRoutedEventArgs e) =>
		e.CanExecute = Target(e.Parameter) is Source or Probe or ColorScheme;

	private void CanToggleEnabled(object sender, CanExecuteRoutedEventArgs e) =>
		e.CanExecute = Target(e.Parameter) is Source or Probe;

	/// <summary>The tree's check boxes by keyboard: Space on a selected source or probe flips it (the author's point 11, 2026-10-13).</summary>
	private void OnToggleEnabled(object sender, ExecutedRoutedEventArgs e)
	{
		switch (Target(e.Parameter))
		{
			case Source source:
				source.Enabled = !source.Enabled;
				break;
			case Probe probe:
				probe.Active = !probe.Active;
				break;
		}
	}

	private void OnDuplicate(object sender, ExecutedRoutedEventArgs e)
	{
		switch (Target(e.Parameter))
		{
			case Source source:
				Added(Project.Duplicate(source));
				break;
			case Probe probe:
				Added(Project.Duplicate(probe));
				break;
			case ColorScheme scheme:
				Added(Project.Duplicate(scheme));
				break;
		}
	}

	private void OnRemove(object sender, ExecutedRoutedEventArgs e)
	{
		object? item = Target(e.Parameter);
		IList? list = ListOf(item);
		if (list == null)
		{
			return;
		}
		int index = list.IndexOf(item);
		switch (item)
		{
			case Source source:
				Project.Remove(source);
				break;
			case Probe probe:
				Project.Remove(probe);
				break;
			case ColorScheme scheme:
				Project.Remove(scheme);
				break;
		}
		// keep the user in place: the item that took the slot, else the folder
		if (list.Count > 0)
		{
			projectTree.Select(list[Math.Min(index, list.Count - 1)]!);
		}
		else
		{
			projectTree.SelectFolder(list);
		}
	}

	private void CanMoveUp(object sender, CanExecuteRoutedEventArgs e)
	{
		object? item = Target(e.Parameter);
		e.CanExecute = ListOf(item) is { } list && item is Source or Probe or ColorScheme && list.IndexOf(item) > 0;
	}

	private void CanMoveDown(object sender, CanExecuteRoutedEventArgs e)
	{
		object? item = Target(e.Parameter);
		e.CanExecute = ListOf(item) is { } list && item is Source or Probe or ColorScheme && list.IndexOf(item) < list.Count - 1;
	}

	private void OnMoveUp(object sender, ExecutedRoutedEventArgs e) => MoveBy(Target(e.Parameter), -1);

	private void OnMoveDown(object sender, ExecutedRoutedEventArgs e) => MoveBy(Target(e.Parameter), +1);

	private void MoveBy(object? item, int delta)
	{
		IList? list = ListOf(item);
		if (list == null)
		{
			return;
		}
		int index = list.IndexOf(item) + delta;
		bool moved = item switch
		{
			Source source => Project.Move(source, index),
			Probe probe => Project.Move(probe, index),
			ColorScheme scheme => Project.Move(scheme, index),
			_ => false,
		};
		if (moved)
		{
			projectTree.Select(item!);
		}
	}

	/// <summary>Selects a newly added item and puts the caret into its first editor (the name).</summary>
	private void Added(object item)
	{
		projectTree.Select(item);
		propertiesPane.FocusFirstEditor();
	}

	/// <summary>
	/// Scene > Browse: picks an image file and embeds its bytes in the project (2026-10-13; the file is
	/// decoded once to prove it is an image and to log its size). The scene switches to image mode and
	/// shows the file's name; the watcher treats the change like any scene edit.
	/// </summary>
	private void OnBrowseSceneImage(object sender, ExecutedRoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Filter = (string)FindResource("SceneImageFilter"),
			Title = (string)FindResource("TipBrowseSceneImage"),
		};
		if (dialog.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			byte[] bytes = File.ReadAllBytes(dialog.FileName);
			ScenePixels pixels = SceneImageLoader.Load(bytes);
			Project.Scene.ImageData = bytes;
			Project.Scene.Image = Path.GetFileName(dialog.FileName);
			Project.Scene.ImagePath = Scene.StoredImagePath(ProjectFolder, dialog.FileName);
			Project.Scene.Mode = SceneMode.Image;
			Logger.Info("Scene image '{Name}' embedded: {Width}×{Height}, {Size} KB", Project.Scene.Image, pixels.Width, pixels.Height, Math.Max(1, bytes.Length / 1024));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
		{
			Logger.Error(ex, "The image could not be loaded: {Message}", ex.Message);
		}
	}
}
