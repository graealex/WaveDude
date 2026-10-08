using System.Collections.ObjectModel;

namespace WaveDude.WPF.Docking;

/// <summary>
/// The registered items of a <see cref="DockHost"/>. Adding registers the item with the host;
/// items cannot be replaced or removed once registered, because saved layouts refer to them.
/// </summary>
public sealed class DockItemCollection : ObservableCollection<DockItem>
{
	private readonly Action<DockItem> _register;

	internal DockItemCollection(Action<DockItem> register)
	{
		_register = register;
	}

	/// <inheritdoc/>
	protected override void InsertItem(int index, DockItem item)
	{
		_register(item);
		base.InsertItem(index, item);
	}

	/// <inheritdoc/>
	protected override void SetItem(int index, DockItem item) =>
		throw new NotSupportedException("Dock items cannot be replaced.");

	/// <inheritdoc/>
	protected override void RemoveItem(int index) =>
		throw new NotSupportedException("Dock items cannot be unregistered.");

	/// <inheritdoc/>
	protected override void ClearItems() =>
		throw new NotSupportedException("Dock items cannot be unregistered.");
}
