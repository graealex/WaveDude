using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WaveDude.Runs;

namespace WaveDude.WPF.Rendering;

/// <summary>Decodes a scene image file into the BGRA bytes a run needs. UI thread (WPF imaging).</summary>
public static class SceneImageLoader
{
	/// <summary>Reads <paramref name="path"/> with the WPF decoders and converts the first frame to 32-bit BGRA.</summary>
	/// <exception cref="FileNotFoundException">The file does not exist.</exception>
	/// <exception cref="NotSupportedException">The file is not an image WPF can decode.</exception>
	public static ScenePixels Load(string path)
	{
		using FileStream stream = File.OpenRead(path);
		return Load(stream);
	}

	/// <summary>Decodes an image file's bytes (an embedded scene image).</summary>
	public static ScenePixels Load(byte[] bytes)
	{
		using var stream = new MemoryStream(bytes, writable: false);
		return Load(stream);
	}

	/// <summary>Decodes an image stream (any format WPF has a decoder for) into BGRA pixels.</summary>
	public static ScenePixels Load(Stream stream)
	{
		BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
		var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
		int width = converted.PixelWidth;
		int height = converted.PixelHeight;
		int stride = width * 4;
		var bgra = new byte[stride * height];
		converted.CopyPixels(bgra, stride, 0);
		return new ScenePixels(width, height, stride, bgra);
	}
}
