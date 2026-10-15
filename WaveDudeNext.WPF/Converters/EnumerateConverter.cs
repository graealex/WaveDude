using System.Globalization;
using System.Windows.Data;

namespace WaveDudeNext.WPF.Converters;

/// <summary>
/// Wraps a single object in a one-element sequence, or null in an empty one, so that a lone
/// root object can be the <c>ItemsSource</c> of a tree view.
/// </summary>
public sealed class EnumerateConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value == null ? Array.Empty<object>() : new[] { value };

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
