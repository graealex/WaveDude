using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WaveDude.WPF.Localization;

namespace WaveDude.WPF.Converters;

/// <summary>
/// Builds the child list of a tree node from several bindings: a collection becomes a
/// <see cref="NamedCollection"/> folder node labelled by the matching entry of the
/// comma-separated converter parameter, any other non-null value is added as a node of its own.
/// </summary>
/// <example>
/// <c>ConverterParameter=",,Sources,Probes"</c> with bindings to Scene, Simulation, Sources
/// and Probes yields two plain nodes followed by two folders.
/// </example>
public sealed class NamedCollectionConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		string[] names = (parameter as string)?.Split(',') ?? [];
		var result = new List<object>(values.Length);
		for (int i = 0; i < values.Length; i++)
		{
			object? value = values[i];
			if (value == null || value == DependencyProperty.UnsetValue)
			{
				continue;
			}
			if (value is IEnumerable items && value is not string)
			{
				string key = i < names.Length ? names[i].Trim() : "";
				result.Add(new NamedCollection(key, items));
			}
			else
			{
				result.Add(value);
			}
		}
		return result;
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}

/// <summary>
/// A labelled folder node wrapping a collection, so that a list such as the project's sources
/// shows as "Sources" with the elements beneath it. Enumerates the wrapped items.
/// </summary>
public sealed class NamedCollection : IEnumerable, INotifyPropertyChanged
{
	/// <param name="key">The resource key of the label ("TreeSources").</param>
	/// <param name="items">The wrapped collection; an observable one keeps the node in sync.</param>
	public NamedCollection(string key, IEnumerable items)
	{
		Key = key;
		Items = items;
		// the label follows the language; a weak handler, since folders are recreated with every project
		PropertyChangedEventManager.AddHandler(LanguageController.Instance, OnLanguageChanged, nameof(LanguageController.Current));
	}

	/// <summary>The resource key of the label ("TreeSources"); language-independent, for automation ids.</summary>
	public string Key { get; }

	/// <summary>The label in the current language (the resource under <see cref="Key"/>, else the key itself).</summary>
	public string Label => Application.Current?.TryFindResource(Key) as string ?? Key;

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));

	/// <summary>The wrapped collection.</summary>
	public IEnumerable Items { get; }

	/// <inheritdoc/>
	public IEnumerator GetEnumerator() => Items.GetEnumerator();

	/// <inheritdoc/>
	public override string ToString() => Label;
}
