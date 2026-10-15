using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WaveDudeNext.WPF.Docking;

/// <summary>
/// A tabbed set of <see cref="DockItem"/>s sharing one pane; a leaf of the layout tree.
/// </summary>
public sealed class DockGroup : DockNode, INotifyPropertyChanged
{
	private DockItem? _activeItem;

	public DockGroup()
	{
		Items.CollectionChanged += OnItemsChanged;
	}

	/// <summary>Items in tab order.</summary>
	public ObservableCollection<DockItem> Items { get; } = new();

	/// <summary>The item whose content is shown. Null only while the group is empty.</summary>
	public DockItem? ActiveItem
	{
		get => _activeItem;
		set
		{
			if (!ReferenceEquals(_activeItem, value) && (value == null || Items.Contains(value)))
			{
				_activeItem = value;
				OnPropertyChanged();
			}
		}
	}

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems != null)
		{
			foreach (DockItem item in e.OldItems)
			{
				if (ReferenceEquals(item.Group, this))
				{
					item.Group = null;
				}
			}
		}
		if (e.NewItems != null)
		{
			foreach (DockItem item in e.NewItems)
			{
				item.Group = this;
			}
		}
		if (_activeItem == null || !Items.Contains(_activeItem))
		{
			ActiveItem = Items.Count > 0 ? Items[^1] : null;
		}
	}

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
