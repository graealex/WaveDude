using System.Globalization;
using System.Windows.Data;

namespace WaveDude.WPF.Converters;

/// <summary>Whether an integer equals the converter parameter (a number as text): checks a menu item that stands for one value of a setting.</summary>
public sealed class IntEqualsConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is int number && parameter is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int wanted) && number == wanted;

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
