using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace WaveDude.WPF.Converters;

/// <summary>
/// The embedded scene image's bytes as "1920 × 1080, 312 KB" for the scene view; "" without an image,
/// "(unreadable)" when no decoder takes the bytes. Reads the header only (<see cref="BitmapCacheOption.None"/>).
/// </summary>
public sealed class ImageInfoConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not byte[] bytes)
		{
			return "";
		}
		try
		{
			using var stream = new MemoryStream(bytes, writable: false);
			BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
			BitmapFrame frame = decoder.Frames[0];
			return string.Format(culture, "{0} × {1}, {2:N0} KB", frame.PixelWidth, frame.PixelHeight, Math.Max(1, bytes.Length / 1024));
		}
		catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException)
		{
			return "(unreadable)";
		}
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
