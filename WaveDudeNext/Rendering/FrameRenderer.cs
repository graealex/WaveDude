using System.Numerics.Tensors;

namespace WaveDudeNext.Rendering;

/// <summary>
/// Static helpers that turn a stored frame (the field or the intensity of one simulation step, width × height
/// values without padding) into packed BGRA32 pixels, like <c>WaveVisualizer.render_field</c> and
/// <c>render_intensity</c> in the Python code: the value is converted to a table index (see
/// <see cref="ColorLut"/>) and the index to a colour. Without a colour table (null) the index itself is used
/// as grey level. Row loops run in parallel (each worker converts its row to floats with the vectorised
/// <see cref="TensorPrimitives.ConvertToSingle"/> first), so the methods are CPU-bound but not thread-affine;
/// the caller owns the buffers and must not touch them while a call is in progress.
/// </summary>
public static class FrameRenderer
{
	/// <summary>
	/// Renders the wave field: every value is mapped by <see cref="ColorLut.FieldIndex"/>
	/// (<c>clip(value · brightness, -1, 1) · 127 + 127</c>) and coloured.
	/// </summary>
	/// <param name="values">Field values, row-major, <paramref name="width"/> × <paramref name="height"/>.</param>
	/// <param name="width">Frame width in pixels.</param>
	/// <param name="height">Frame height in pixels.</param>
	/// <param name="lut">ARGB palette indexed 0..254 (127 = zero field), or null for greyscale.</param>
	/// <param name="brightness">Gain applied before the value is clamped to -1..1.</param>
	/// <param name="pixels">Destination, at least <paramref name="width"/> × <paramref name="height"/> packed BGRA32 pixels, row-major.</param>
	public static void RenderField(ReadOnlyMemory<Half> values, int width, int height, int[]? lut, float brightness, Memory<int> pixels)
	{
		Parallel.For(0, height, () => new float[width], (y, _, row) =>
		{
			TensorPrimitives.ConvertToSingle(values.Span.Slice(y * width, width), row);
			Span<int> target = pixels.Span.Slice(y * width, width);
			for (int x = 0; x < width; x++)
			{
				target[x] = Colorize(ColorLut.FieldIndex(row[x], brightness), lut);
			}
			return row;
		}, _ => { });
	}

	/// <summary>
	/// Renders the accumulated intensity: each value is mapped as <c>sqrt(value) · brightness</c> (exponent 0.5,
	/// as in the Python code), clamped to 1, then scaled to palette index 0..254 (non-positive values map to 0).
	/// </summary>
	/// <param name="values">Intensity values, row-major, <paramref name="width"/> × <paramref name="height"/>.</param>
	/// <param name="width">Frame width in pixels.</param>
	/// <param name="height">Frame height in pixels.</param>
	/// <param name="lut">ARGB palette indexed 0..254, or null for greyscale.</param>
	/// <param name="brightness">Gain applied after the square-root compression.</param>
	/// <param name="pixels">Destination, at least <paramref name="width"/> × <paramref name="height"/> packed BGRA32 pixels, row-major.</param>
	public static void RenderIntensity(ReadOnlyMemory<Half> values, int width, int height, int[]? lut, float brightness, Memory<int> pixels)
	{
		Parallel.For(0, height, () => new float[width], (y, _, row) =>
		{
			TensorPrimitives.ConvertToSingle(values.Span.Slice(y * width, width), row);
			Span<int> target = pixels.Span.Slice(y * width, width);
			for (int x = 0; x < width; x++)
			{
				target[x] = Colorize(ColorLut.IntensityIndex(row[x], brightness), lut);
			}
			return row;
		}, _ => { });
	}

	/// <summary>
	/// Renders a plane of palette indices (an <see cref="Engine.FrameFormat.Index"/> frame, the brightness already
	/// applied when it was captured): every byte is looked up in the palette, nothing else. The cheapest picture there
	/// is, a table lookup per pixel.
	/// </summary>
	/// <param name="indices">Palette indices 0..254, row-major, <paramref name="width"/> × <paramref name="height"/>.</param>
	/// <param name="width">Frame width in pixels.</param>
	/// <param name="height">Frame height in pixels.</param>
	/// <param name="lut">ARGB palette indexed 0..254, or null for greyscale.</param>
	/// <param name="pixels">Destination, at least <paramref name="width"/> × <paramref name="height"/> packed BGRA32 pixels, row-major.</param>
	public static void RenderIndexed(ReadOnlyMemory<byte> indices, int width, int height, int[]? lut, Memory<int> pixels)
	{
		Parallel.For(0, height, y =>
		{
			ReadOnlySpan<byte> row = indices.Span.Slice(y * width, width);
			Span<int> target = pixels.Span.Slice(y * width, width);
			for (int x = 0; x < width; x++)
			{
				target[x] = Colorize(Math.Min((int)row[x], ColorLut.Size - 1), lut);
			}
		});
	}

	/// <summary>Looks up a palette index in <paramref name="lut"/>, or builds an opaque grey of that level when there is no LUT.</summary>
	public static int Colorize(int index, int[]? lut) =>
		lut != null ? lut[index] : unchecked((int)0xFF000000) | (index << 16) | (index << 8) | index;
}
