using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
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
		if (value is Enum member)
		{
			// the string table ("Enum.Type.Member") first, then a [Description] on the member (ProbeMeasurement.Intensity reads "Value")
			if (Application.Current?.TryFindResource($"Enum.{member.GetType().Name}.{member}") is string localized)
			{
				return localized;
			}
			if (member.GetType().GetField(member.ToString()) is { } field && field.GetCustomAttribute<DescriptionAttribute>() is { } description)
			{
				return description.Description;
			}
		}
		string[] words = WordBoundary().Split(value.ToString() ?? "");
		return string.Join(' ', words.Select((w, i) => i == 0 ? w : w.ToLowerInvariant()));
	}

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
