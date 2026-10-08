using System.Collections;
using System.Windows.Input;
using WaveDude.Model;
using WaveDude.WPF.Converters;

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
		Source => Project.Sources,
		Probe => Project.Probes,
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
	}

	private void CanEditItem(object sender, CanExecuteRoutedEventArgs e) =>
		e.CanExecute = Target(e.Parameter) is Source or Probe;

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
		e.CanExecute = ListOf(item) is { } list && item is Source or Probe && list.IndexOf(item) > 0;
	}

	private void CanMoveDown(object sender, CanExecuteRoutedEventArgs e)
	{
		object? item = Target(e.Parameter);
		e.CanExecute = ListOf(item) is { } list && item is Source or Probe && list.IndexOf(item) < list.Count - 1;
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
}
