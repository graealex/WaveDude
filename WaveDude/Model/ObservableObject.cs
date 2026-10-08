using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WaveDude.Model;

/// <summary>
/// Base of the model classes: raises <see cref="PropertyChanged"/> when a property value
/// changes, so bound views (tree headers, editors) follow edits without manual refreshes.
/// Not thread-safe; the model is edited on the UI thread only.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// Stores <paramref name="value"/> in <paramref name="field"/> and raises
	/// <see cref="PropertyChanged"/> when it differs; returns whether it did.
	/// </summary>
	protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(name);
		return true;
	}

	/// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="name"/>; use for derived properties.</summary>
	protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

	/// <summary>
	/// Memberwise copy that does not carry over the <see cref="PropertyChanged"/> subscribers
	/// of the original. Derived classes deep-copy their reference-typed members on top.
	/// </summary>
	protected T ShallowClone<T>() where T : ObservableObject
	{
		var copy = (T)MemberwiseClone();
		copy.PropertyChanged = null;
		return copy;
	}
}
