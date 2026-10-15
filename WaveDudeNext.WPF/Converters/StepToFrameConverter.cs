using System.Globalization;
using System.Windows.Data;

namespace WaveDudeNext.WPF.Converters;

/// <summary>
/// Turns a simulation step into the index of the frame that shows it. Inputs, in this order:
/// the step, the visualisation interval (steps per frame) and the last frame index. The result
/// is clamped to 0..last frame. With the parameter <c>"end"</c> a step of 0 means the end of
/// the run and gives the last frame, which is how <c>Display.ExportTo</c> encodes "no out point".
/// One way: the pane writes steps back through its commands.
/// </summary>
public sealed class StepToFrameConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Length != 3 || values[0] is not int step || values[1] is not int every || values[2] is not int lastFrame)
		{
			return 0;
		}
		if (step <= 0)
		{
			return Equals(parameter, "end") ? lastFrame : 0;
		}
		return Math.Clamp(every > 0 ? step / every : 0, 0, lastFrame);
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
