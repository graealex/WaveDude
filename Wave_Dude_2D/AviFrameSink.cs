using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// <see cref="IFrameSink"/> that JPEG-encodes each frame with the GDI+ encoder and appends it to a Motion-JPEG
	/// AVI through <see cref="AviMjpegWriter"/>. Fallback used by <see cref="SimulationRunner"/> when MP4 is not
	/// requested or Media Foundation is unavailable: needs no codec on this computer, but Windows' own players do not
	/// play it; VLC does. Used from the worker thread only.
	/// </summary>
	internal sealed class AviFrameSink : IFrameSink
	{
		/// <summary>Creates the AVI file immediately (header written); <paramref name="quality"/> is clamped to 1..100.</summary>
		/// <param name="filePath">Output .avi path (created or overwritten).</param>
		/// <param name="width">Frame width in pixels.</param>
		/// <param name="height">Frame height in pixels.</param>
		/// <param name="framesPerSecond">Playback frame rate.</param>
		/// <param name="quality">JPEG quality 1..100 passed to the GDI+ encoder.</param>
		public AviFrameSink(string filePath, int width, int height, int framesPerSecond, int quality)
		{
			_buffer = new MemoryStream();
			_jpegQuality = Math.Max(1, Math.Min(100, quality));
			_path = filePath;
			_width = width;
			_height = height;
			_writer = new AviMjpegWriter(filePath, width, height, framesPerSecond);
		}

		/// <summary>The AVI path given to the constructor.</summary>
		public string FilePath => _path;

		/// <summary>
		/// Wraps the ARGB buffer in a temporary bitmap, JPEG-encodes it into the reusable <see cref="_buffer"/>
		/// and appends the bytes as one AVI frame.
		/// </summary>
		public void AddPixels(int[] pixels)
		{
			using (Bitmap bitmap = FrameRenderer.ToBitmap(pixels, _width, _height))
			{
				_buffer.SetLength(0L);
				using (EncoderParameters encoderParameters = new EncoderParameters(1))
				{
					encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, _jpegQuality);
					bitmap.Save(_buffer, AviFrameSink.JpegCodec, encoderParameters);
				}
				_writer.AddFrame(_buffer.GetBuffer(), (int)_buffer.Length);
			}
		}

		/// <summary>Writes the AVI index and header patches and closes the file (idempotent).</summary>
		public void Close()
		{
			_writer.Close();
		}

		/// <summary>Closes the AVI (if not already) and releases the encode buffer.</summary>
		public void Dispose()
		{
			_writer.Close();
			_buffer.Dispose();
		}

		/// <summary>Finds the GDI+ JPEG encoder; runs once in the static initializer.</summary>
		/// <exception cref="InvalidOperationException">No JPEG encoder is installed.</exception>
		private static ImageCodecInfo FindJpegCodec()
		{
			foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
			{
				if (codec.FormatID == ImageFormat.Jpeg.Guid)
				{
					return codec;
				}
			}
			throw new InvalidOperationException("The JPEG encoder is not available on this computer.");
		}

		/// <summary>The GDI+ JPEG encoder, resolved once per process (a missing encoder surfaces as a type-initializer exception).</summary>
		private static readonly ImageCodecInfo JpegCodec = AviFrameSink.FindJpegCodec();

		/// <summary>JPEG quality 1..100 as a long, the type <see cref="EncoderParameter"/> expects.</summary>
		private readonly long _jpegQuality;

		/// <summary>Output file path.</summary>
		private readonly string _path;

		/// <summary>The underlying RIFF/AVI container writer; owns the file stream.</summary>
		private readonly AviMjpegWriter _writer;

		/// <summary>Frame width in pixels.</summary>
		private readonly int _width;

		/// <summary>Frame height in pixels.</summary>
		private readonly int _height;

		/// <summary>Reused in-memory target for each JPEG encode; its backing array is handed to the writer directly.</summary>
		private readonly MemoryStream _buffer;
	}
}
