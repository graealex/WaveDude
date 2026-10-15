using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WaveDudeNext.WPF.Converters;

/// <summary>
/// A folder's item count as words with the right plural (2026-10-15, after "1 entries"): the string table's
/// <c>ItemCountZero</c> ("No entries"), <c>ItemCountOne</c> ("1 entry") or <c>ItemCountMany</c> ("{0} entries",
/// the count formatted in the current culture). A multi-value converter whose first input is the count and
/// whose second, the current language, only serves to re-read the words on a language switch.
/// </summary>
public sealed class ItemCountConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Length == 0 || values[0] is not int count)
		{
			return null;
		}
		string key = count switch
		{
			0 => "ItemCountZero",
			1 => "ItemCountOne",
			_ => "ItemCountMany",
		};
		string format = Application.Current?.TryFindResource(key) as string ?? "{0}";
		return string.Format(culture, format, count);
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
