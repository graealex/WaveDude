using System.Windows.Controls;

namespace WaveDude.WPF.Controls;

/// <summary>Selection helpers for data-bound <see cref="TreeView"/>s.</summary>
public static class TreeViewExtensions
{
	/// <summary>
	/// Selects the container of a data item anywhere in the tree, optionally expanding it and
	/// giving it keyboard focus (not when the selection mirrors a handle on the preview, which
	/// keeps the focus, 2026-10-14). Only realised containers are searched, so call it once the
	/// tree has generated its items. Returns false when the item was not found.
	/// </summary>
	/// <param name="tree">The tree.</param>
	/// <param name="item">The data item whose container is wanted.</param>
	/// <param name="expand">Expand the container too.</param>
	/// <param name="focus">Give the container keyboard focus.</param>
	/// <param name="select">Select the container; false deselects it instead (nothing else is touched then).</param>
	public static bool SetSelectedItem(this TreeView tree, object item, bool expand = false, bool focus = true, bool select = true) =>
		SetSelected(tree, item, expand, focus, select);

	private static bool SetSelected(ItemsControl? parent, object item, bool expand, bool focus, bool select)
	{
		if (parent == null)
		{
			return false;
		}
		if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem container)
		{
			if (focus && select)
			{
				container.Focus();
			}
			if (expand && select)
			{
				container.IsExpanded = true;
			}
			container.IsSelected = select;
			return true;
		}
		foreach (object child in parent.Items)
		{
			if (SetSelected(parent.ItemContainerGenerator.ContainerFromItem(child) as ItemsControl, item, expand, focus, select))
			{
				return true;
			}
		}
		return false;
	}
}
