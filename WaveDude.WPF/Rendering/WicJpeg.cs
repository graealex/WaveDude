using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WaveDude.Export;

namespace WaveDude.WPF.Rendering;

/// <summary>
/// JPEG through the Windows Imaging Component (WIC), the codec that ships with Windows, reached through
/// WPF's <see cref="JpegBitmapEncoder"/>: a SIMD encoder on the CPU, single-threaded per picture (it does
/// not use the GPU). Installed at start-up as <see cref="ExportFormats.AlternativeJpeg"/>, so the export
/// uses it and falls back to the in-house <see cref="JpegEncoder"/> when it throws (2026-10-15, the user's
/// choice). The quality number is handed over unchanged: WIC scales the IJG tables like the in-house
/// encoder does, which the bench confirms by size and PSNR at the same number; its one quirk is that it
/// drops chroma subsampling above about 90, so files grow there, not a wrong picture. Any thread; the
/// export's sink thread is a single-threaded apartment, which WPF's encoder does not need but is happy with.
/// </summary>
public static class WicJpeg
{
	/// <summary>Makes the encoder for frames of one size and quality.</summary>
	/// <param name="width">Frame width in pixels.</param>
	/// <param name="height">Frame height in pixels.</param>
	/// <param name="quality">1..100, WPF's <see cref="JpegBitmapEncoder.QualityLevel"/>.</param>
	public static Action<Stream, ReadOnlyMemory<int>> Encoder(int width, int height, int quality)
	{
		int level = Math.Clamp(quality, 1, 100);
		return (stream, pixels) => Write(stream, pixels, width, height, level);
	}

	/// <summary>Writes one packed BGRA32 frame as a JPEG.</summary>
	public static void Write(Stream stream, ReadOnlyMemory<int> pixels, int width, int height, int quality)
	{
		// BitmapSource.Create takes an array; the sinks hand over heap arrays, so this is a view, not a copy
		if (!MemoryMarshal.TryGetArray(pixels, out ArraySegment<int> segment) || segment.Offset != 0)
		{
			segment = new ArraySegment<int>(pixels.ToArray());
		}
		BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, segment.Array!, width * 4);
		var encoder = new JpegBitmapEncoder { QualityLevel = quality };
		encoder.Frames.Add(BitmapFrame.Create(source));
		encoder.Save(stream);
	}
}
