using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WaveDude.Rendering;

namespace WaveDude.WPF.Converters;

/// <summary>
/// A colour scheme (or its stop list, or a plain colour list) as a horizontal gradient brush, the preview
/// strip of the picker, the tree and the editor: every stop becomes a gradient stop at its position when
/// there are up to 64, else 64 samples of the interpolated table. Null gives a transparent brush. The
/// brush is frozen.
/// </summary>
public sealed class ColorSchemeBrushConverter : IValueConverter
{
	private const int MaxStops = 64;

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		IReadOnlyList<ColorStop>? stops = value switch
		{
			ColorScheme scheme => scheme.Stops,
			IReadOnlyList<ColorStop> list => list,
			IReadOnlyList<Rgb> colors => colors.Select((c, k) => new ColorStop(colors.Count == 1 ? 0.0 : k / (double)(colors.Count - 1), c)).ToList(),
			_ => null,
		};
		if (stops == null || stops.Count == 0)
		{
			return Brushes.Transparent;
		}
		var collection = new GradientStopCollection();
		if (stops.Count <= MaxStops)
		{
			foreach (ColorStop stop in stops.OrderBy(s => s.Position))
			{
				collection.Add(new GradientStop(Color.FromRgb(stop.Color.R, stop.Color.G, stop.Color.B), Math.Clamp(stop.Position, 0.0, 1.0)));
			}
		}
		else
		{
			Rgb[] samples = ColorScheme.Interpolate(stops, MaxStops);
			for (int i = 0; i < samples.Length; i++)
			{
				collection.Add(new GradientStop(Color.FromRgb(samples[i].R, samples[i].G, samples[i].B), i / (double)(samples.Length - 1)));
			}
		}
		var brush = new LinearGradientBrush(collection, 0.0);
		brush.Freeze();
		return brush;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
