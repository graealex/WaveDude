using System.Globalization;
using System.Windows.Data;
using WaveDude.Model;
using WaveDude.Rendering;

namespace WaveDude.WPF.Converters;

/// <summary>
/// The automation id of a Project tree row from its node: "Tree:" plus the item's name (sources, probes,
/// colour schemes), a folder's label, or the type name of the fixed nodes ("Tree:Scene"), so a harness
/// can reach a row with Tab and act on it.
/// </summary>
public sealed class TreeIdConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => "Tree:" + value switch
	{
		Source source => source.Name,
		Probe probe => probe.Name,
		ColorScheme scheme => scheme.Name,
		NamedCollection folder => folder.Key,
		null => "",
		_ => value.GetType().Name,
	};

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
