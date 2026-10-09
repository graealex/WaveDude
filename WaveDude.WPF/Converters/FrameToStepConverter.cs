using System.Globalization;
using System.Windows.Data;

namespace WaveDude.WPF.Converters;

/// <summary>
/// The simulation step a frame shows: frame index × visualisation interval (the two inputs in
/// that order), as text in the given culture. One way, for readouts: a multi-binding does not
/// convert an int to a string target on its own.
/// </summary>
public sealed class FrameToStepConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
		(values.Length == 2 && values[0] is int frame && values[1] is int every ? frame * every : 0).ToString(culture);

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
