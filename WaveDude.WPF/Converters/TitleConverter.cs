using System.Globalization;
using System.Windows.Data;

namespace WaveDude.WPF.Converters;

/// <summary>
/// Joins the non-empty parts of a window title with " - ", for example the application name
/// and the open file, so that a missing file leaves just the name.
/// </summary>
public sealed class TitleConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
		string.Join(" - ", values.Select(v => v?.ToString()).Where(s => !string.IsNullOrEmpty(s)));

	/// <inheritdoc/>
	public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
