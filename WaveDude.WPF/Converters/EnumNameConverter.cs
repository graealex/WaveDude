using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace WaveDude.WPF.Converters;

/// <summary>Shows an enum value as words: <c>SmoothedSquare</c> becomes "Smoothed square".</summary>
public sealed partial class EnumNameConverter : IValueConverter
{
	[GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
	private static partial Regex WordBoundary();

	/// <inheritdoc/>
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return null;
		}
		string[] words = WordBoundary().Split(value.ToString() ?? "");
		return string.Join(' ', words.Select((w, i) => i == 0 ? w : w.ToLowerInvariant()));
	}

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
