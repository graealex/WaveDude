using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

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
				string name = i < names.Length ? names[i].Trim() : "";
				result.Add(new NamedCollection(name, items));
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
public sealed class NamedCollection : IEnumerable
{
	/// <param name="label">Header text of the folder node.</param>
	/// <param name="items">The wrapped collection; an observable one keeps the node in sync.</param>
	public NamedCollection(string label, IEnumerable items)
	{
		Label = label;
		Items = items;
	}

	/// <summary>Header text of the folder node.</summary>
	public string Label { get; }

	/// <summary>The wrapped collection.</summary>
	public IEnumerable Items { get; }

	/// <inheritdoc/>
	public IEnumerator GetEnumerator() => Items.GetEnumerator();

	/// <inheritdoc/>
	public override string ToString() => Label;
}
