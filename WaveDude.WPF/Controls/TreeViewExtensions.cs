using System.Windows.Controls;

namespace WaveDude.WPF.Controls;

/// <summary>Selection helpers for data-bound <see cref="TreeView"/>s.</summary>
public static class TreeViewExtensions
{
	/// <summary>
	/// Selects and focuses the container of a data item anywhere in the tree, optionally
	/// expanding it. Only realised containers are searched, so call it once the tree has
	/// generated its items. Returns false when the item was not found.
	/// </summary>
	public static bool SetSelectedItem(this TreeView tree, object item, bool expand = false) =>
		SetSelected(tree, item, expand);

	private static bool SetSelected(ItemsControl? parent, object item, bool expand)
	{
		if (parent == null)
		{
			return false;
		}
		if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem container)
		{
			container.Focus();
			if (expand)
			{
				container.IsExpanded = true;
			}
			container.IsSelected = true;
			return true;
		}
		foreach (object child in parent.Items)
		{
			if (SetSelected(parent.ItemContainerGenerator.ContainerFromItem(child) as ItemsControl, item, expand))
			{
				return true;
			}
		}
		return false;
	}
}
