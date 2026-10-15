using System.ComponentModel;
using System.Globalization;

namespace WaveDudeNext.Rendering;

/// <summary>
/// One 8-bit RGB colour. Replaces <c>System.Drawing.Color</c> in the library so that nothing here
/// depends on GDI+. Written and read as <c>#RRGGBB</c> (the form every colour takes in XML and in
/// the editors, 2026-10-13); <see cref="TryParse"/> also accepts <c>RRGGBB</c>, <c>#RGB</c> and the
/// author's JSON triplet <c>[r,g,b]</c>. The <see cref="TypeConverter"/> lets WPF bind a text box
/// straight to an <see cref="Rgb"/>.
/// </summary>
[TypeConverter(typeof(RgbConverter))]
public readonly record struct Rgb(byte R, byte G, byte B)
{
	/// <summary>Packed <c>0xFFRRGGBB</c>, which is also BGRA32 memory order on a little-endian machine.</summary>
	public int ToArgb() => unchecked((int)0xFF000000) | (R << 16) | (G << 8) | B;

	/// <summary>The colour of a packed <c>0xAARRGGBB</c> value (alpha ignored).</summary>
	public static Rgb FromArgb(int argb) => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

	/// <summary>Builds a colour from integer channels, each clamped to 0..255.</summary>
	public static Rgb Clamped(int r, int g, int b) => new((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));

	/// <summary><c>#RRGGBB</c>, upper case.</summary>
	public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

	/// <summary>Parses <c>#RRGGBB</c>, <c>RRGGBB</c>, <c>#RGB</c> or <c>[r,g,b]</c>.</summary>
	/// <exception cref="FormatException">The text is none of those.</exception>
	public static Rgb Parse(string text) =>
		TryParse(text, out Rgb color) ? color : throw new FormatException($"'{text}' is not a colour; #RRGGBB expected.");

	/// <summary>Tries to parse <c>#RRGGBB</c>, <c>RRGGBB</c>, <c>#RGB</c> or <c>[r,g,b]</c>; false for anything else.</summary>
	public static bool TryParse(string? text, out Rgb color)
	{
		color = default;
		if (text == null)
		{
			return false;
		}
		string s = text.Trim();
		if (s.StartsWith('['))
		{
			string[] parts = s.Trim('[', ']').Split(',');
			if (parts.Length == 3 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)
				&& int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int g)
				&& int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b))
			{
				color = Clamped(r, g, b);
				return true;
			}
			return false;
		}
		if (s.StartsWith('#'))
		{
			s = s[1..];
		}
		if (s.Length == 3)
		{
			s = new string([s[0], s[0], s[1], s[1], s[2], s[2]]);
		}
		if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int packed))
		{
			return false;
		}
		color = FromArgb(packed);
		return true;
	}
}

/// <summary>Converts between <see cref="Rgb"/> and its <c>#RRGGBB</c> text for bindings and property editors.</summary>
public sealed class RgbConverter : TypeConverter
{
	/// <inheritdoc/>
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
		sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

	/// <inheritdoc/>
	public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
		destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

	/// <inheritdoc/>
	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
		value is string text ? Rgb.Parse(text) : base.ConvertFrom(context, culture, value);

	/// <inheritdoc/>
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
		destinationType == typeof(string) && value is Rgb color ? color.ToString() : base.ConvertTo(context, culture, value, destinationType);
}
