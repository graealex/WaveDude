using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WaveDudeNext.Runs;

namespace WaveDudeNext.WPF.Converters;

/// <summary>
/// The header of a Simulation > Frames kept in memory entry: "Auto" (the string table's
/// <c>MenuSimulationFrameMemoryAuto</c>) for <see cref="FrameMemory.Automatic"/>, else the percentage of the
/// RAM, "10 %" to "100 %". A multi-value converter whose first input is the option and whose second, the
/// current language, only serves to re-read the resource on a language switch (2026-10-15).
/// </summary>
public sealed class FrameMemoryOptionConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Length == 0 || values[0] is not int percent)
		{
			return null;
		}
		if (percent <= FrameMemory.Automatic)
		{
			return Application.Current?.TryFindResource("MenuSimulationFrameMemoryAuto") as string ?? "_Auto";
		}
		// the underscore of a mnemonic would be swallowed; a digit mnemonic is of no use anyway
		return string.Format(culture, "{0} %", percent);
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
