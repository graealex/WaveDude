using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WaveDude.Rendering;

namespace WaveDude.WPF.Converters;

/// <summary>One <see cref="Rgb"/> as a frozen solid brush, for the swatch beside a stop's text; anything else gives transparent.</summary>
public sealed class RgbBrushConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not Rgb c)
		{
			return Brushes.Transparent;
		}
		var brush = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
		brush.Freeze();
		return brush;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
