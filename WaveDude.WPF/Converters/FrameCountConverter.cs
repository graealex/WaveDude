using System.Globalization;
using System.Windows.Data;

namespace WaveDude.WPF.Converters;

/// <summary>
/// Number of frames a run produces: the step count divided by the visualisation interval,
/// rounded up, at least 1. The inputs are the two integers in that order; anything missing
/// (no project) gives 1.
/// </summary>
public sealed class FrameCountConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Length == 2 && values[0] is int steps && values[1] is int every && steps > 0 && every > 0)
		{
			return (steps + every - 1) / every;
		}
		return 1;
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
