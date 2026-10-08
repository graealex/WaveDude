using System.Globalization;
using System.Windows.Data;

namespace WaveDude.WPF.Converters;

/// <summary>
/// Binds one radio button to one value of an enum property: checked when the property equals
/// the converter parameter, and checking it writes the parameter back. Unchecking (which the
/// group does when another button is chosen) writes nothing.
/// </summary>
public sealed class EnumBooleanConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		Equals(value, parameter);

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is true && parameter != null ? parameter : Binding.DoNothing;
}
