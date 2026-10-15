using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Markup;

namespace WaveDudeNext.WPF.Docking;

/// <summary>
/// One tool window: a title and a piece of content that can live in any <see cref="DockGroup"/>.
/// Declarable in XAML inside <c>DockHost.Items</c>, with the content as the element body.
/// </summary>
[ContentProperty(nameof(Content))]
public sealed class DockItem : INotifyPropertyChanged
{
	private string _title = "";
	private DockGroup? _group;

	/// <summary>Creates an item whose <see cref="Id"/>, <see cref="Title"/> and <see cref="Content"/> are set afterwards (XAML).</summary>
	public DockItem()
	{
	}

	/// <param name="id">Stable identifier used in saved layouts.</param>
	/// <param name="title">Text shown in the title bar and tab.</param>
	/// <param name="content">The WPF content hosted in the pane.</param>
	public DockItem(string id, string title, object content)
	{
		Id = id;
		_title = title;
		Content = content;
	}

	/// <summary>Stable identifier used in saved layouts; fixed once the item is registered.</summary>
	public string Id { get; set; } = "";

	/// <summary>Text shown in the title bar and tab.</summary>
	public string Title
	{
		get => _title;
		set
		{
			if (_title != value)
			{
				_title = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>The WPF content hosted in the pane; set before the item is first shown.</summary>
	public object? Content { get; set; }

	/// <summary>Whether the pane shows a close button. Closing only hides the item.</summary>
	public bool CanClose { get; set; } = true;

	/// <summary>Edge the item is placed at when shown without a saved position.</summary>
	public DockSide DefaultSide { get; set; } = DockSide.Left;

	/// <summary>The group currently containing this item, or null while hidden.</summary>
	public DockGroup? Group
	{
		get => _group;
		internal set
		{
			if (!ReferenceEquals(_group, value))
			{
				bool wasShown = _group != null;
				_group = value;
				OnPropertyChanged();
				if (wasShown != (value != null))
				{
					OnPropertyChanged(nameof(IsShown));
				}
			}
		}
	}

	/// <summary>True while the item occupies a group.</summary>
	public bool IsShown => _group != null;

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
