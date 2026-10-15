using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WaveDudeNext.WPF.Converters;

/// <summary>
/// Builds the window title from the application name, the open file and the dirty flag: the non-empty
/// parts joined with " - ", an asterisk after the file for unsaved edits, and "Untitled*" when an unsaved
/// project has no file yet (the word is looked up in the current language's strings on every conversion;
/// the window refreshes the binding on a language switch).
/// </summary>
public sealed class TitleConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
	{
		string title = values.Length > 0 ? values[0]?.ToString() ?? "" : "";
		string file = values.Length > 1 ? values[1]?.ToString() ?? "" : "";
		bool dirty = values.Length > 2 && values[2] is true;
		if (dirty)
		{
			if (file.Length == 0)
			{
				file = Application.Current?.TryFindResource("Untitled") as string ?? "Untitled";
			}
			file += "*";
		}
		return file.Length == 0 ? title : title + " - " + file;
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
