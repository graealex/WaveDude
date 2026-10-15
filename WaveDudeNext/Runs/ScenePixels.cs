namespace WaveDudeNext.Runs;

/// <summary>
/// A decoded scene image as 32-bit BGRA pixels, the UI-independent input of <see cref="RunSpec"/>: the
/// application decodes the file (WPF with <c>BitmapDecoder</c>) and hands the bytes over; the run turns them
/// into <see cref="Engine.SceneData"/> on its worker. Immutable; the array must not be modified after
/// construction.
/// </summary>
/// <param name="Width">Image width in pixels (= canvas width).</param>
/// <param name="Height">Image height in pixels (= canvas height).</param>
/// <param name="StrideBytes">Bytes per row, at least 4 × <paramref name="Width"/>.</param>
/// <param name="Bgra">Pixel bytes, B, G, R, A per pixel, <paramref name="Height"/> rows of <paramref name="StrideBytes"/>.</param>
public sealed record ScenePixels(int Width, int Height, int StrideBytes, byte[] Bgra);
