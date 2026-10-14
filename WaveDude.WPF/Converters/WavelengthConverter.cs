using System.Globalization;
using System.Windows.Data;
using WaveDude.Model;

namespace WaveDude.WPF.Converters;

/// <summary>
/// The source's estimated wavelength as text: values are the source level, the simulation's wave speed and
/// source frequency scale, and whether the 4th-order Laplacian is on (<see cref="Simulation.Wavelength"/>).
/// "≈ 16,0 px" in the current culture; with the 9-point stencil "(nominal)" is appended, because that
/// stencil's speed varies with the frequency; "–" when the inputs do not give a wavelength.
/// </summary>
public sealed class WavelengthConverter : IMultiValueConverter
{
	public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Length < 4 || values[0] is not int level || values[1] is not double waveSpeed
			|| values[2] is not double scale || values[3] is not bool fourthOrder)
		{
			return "\u2013";
		}
		double wavelength = Simulation.Wavelength(level, waveSpeed, scale);
		if (double.IsNaN(wavelength) || double.IsInfinity(wavelength))
		{
			return "\u2013";
		}
		string text = string.Format(culture, "\u2248 {0:0.0} px", wavelength);
		return fourthOrder ? text : text + " (nominal)";
	}

	public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
