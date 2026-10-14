using System.Globalization;
using System.Windows.Data;
using WaveDude.Model;

namespace WaveDude.WPF.Converters;

/// <summary>A <see cref="ProbeLayer"/> to the measurements a probe on it can record (<see cref="Probe.MeasurementsFor"/>), for the measurement combo's items.</summary>
public sealed class MeasurementsForLayerConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is ProbeLayer layer ? Probe.MeasurementsFor(layer) : Enum.GetValues<ProbeMeasurement>();

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
