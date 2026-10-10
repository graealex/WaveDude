namespace WaveDude.Rendering;

/// <summary>An opaque 8-bit RGB colour, the unit of a <see cref="ColorScheme"/>; a value type usable from any thread.</summary>
/// <param name="R">Red 0..255.</param>
/// <param name="G">Green 0..255.</param>
/// <param name="B">Blue 0..255.</param>
public readonly record struct Rgb(byte R, byte G, byte B)
{
	/// <summary>The colour packed as opaque ARGB (<c>0xFFRRGGBB</c>), which is also the in-memory byte order of a BGRA32 pixel.</summary>
	public int ToArgb() => unchecked((int)0xFF000000) | (R << 16) | (G << 8) | B;

	/// <summary>The colour of a packed ARGB value; the alpha byte is ignored.</summary>
	public static Rgb FromArgb(int argb) => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

	/// <summary>Creates a colour from channel values clamped to 0..255.</summary>
	public static Rgb Clamped(int r, int g, int b) => new((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));

	/// <summary><c>[r,g,b]</c>, the JSON triplet form of the scheme files.</summary>
	public override string ToString() => $"[{R},{G},{B}]";
}
