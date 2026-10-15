using System.Globalization;
using System.Windows.Data;

namespace WaveDudeNext.WPF.Converters;

/// <summary>
/// Binds the text of an editable field to a <see cref="double"/> without the binding engine's
/// exceptions: text that does not parse yet (empty, a lone minus, "1.") leaves the source alone
/// instead of logging a conversion error on every keystroke. Both directions use the culture
/// the binding passes in, which <c>App.OnStartup</c> makes the user's culture for every element,
/// so the text agrees with how the preset list shows the same values.
/// </summary>
public sealed class DoubleTextConverter : IValueConverter
{
	/// <inheritdoc/>
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is double d ? d.ToString(culture) : value?.ToString();

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		double.TryParse(value as string, NumberStyles.Float, culture, out double d) ? d : Binding.DoNothing;
}
