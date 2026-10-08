using System.Globalization;
using System.Windows.Data;

namespace WaveDude.WPF.Converters;

/// <summary>
/// Multi-value converter that yields true when all bound values are equal. Used to drive
/// check marks on menu items generated from a list of choices.
/// </summary>
public sealed class EqualityConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		for (int i = 1; i < values.Length; i++)
		{
			if (!Equals(values[0], values[i]))
			{
				return false;
			}
		}
		return values.Length > 0;
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
