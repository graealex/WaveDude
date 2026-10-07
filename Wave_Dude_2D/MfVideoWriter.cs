using System;
using System.Runtime.InteropServices;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// <see cref="IFrameSink"/> that encodes H.264 MP4 through the Media Foundation sink writer
	/// (<c>mfplat.dll</c> / <c>mfreadwrite.dll</c>, talked to via the hand-written <c>IMF*Native</c> COM
	/// interfaces). Created by <see cref="SimulationRunner"/>; a failing constructor means
	/// "no H.264 on this machine" and triggers the AVI fallback. Used from the worker thread only.
	/// Media Foundation is started in the constructor and shut down in <see cref="Cleanup"/>.
	/// </summary>
	internal sealed class MfVideoWriter : IFrameSink
	{
		/// <summary>Native <c>MFStartup</c>: initializes Media Foundation for this process.</summary>
		[DllImport("mfplat.dll", ExactSpelling = true)]
		private static extern int MFStartup(int version, int flags);

		/// <summary>Native <c>MFShutdown</c>: counterpart of <see cref="MFStartup"/>.</summary>
		[DllImport("mfplat.dll", ExactSpelling = true)]
		private static extern int MFShutdown();

		/// <summary>Native <c>MFCreateMediaType</c>: returns a new, empty <c>IMFMediaType</c> (ref count 1) in <paramref name="mediaType"/>.</summary>
		[DllImport("mfplat.dll", ExactSpelling = true)]
		private static extern int MFCreateMediaType(ref IntPtr mediaType);

		/// <summary>Native <c>MFCreateSample</c>: returns a new, empty <c>IMFSample</c> in <paramref name="sample"/>.</summary>
		[DllImport("mfplat.dll", ExactSpelling = true)]
		private static extern int MFCreateSample(ref IntPtr sample);

		/// <summary>Native <c>MFCreateMemoryBuffer</c>: allocates a system-memory <c>IMFMediaBuffer</c> of <paramref name="maxLength"/> bytes.</summary>
		[DllImport("mfplat.dll", ExactSpelling = true)]
		private static extern int MFCreateMemoryBuffer(int maxLength, ref IntPtr buffer);

		/// <summary>Native <c>MFCreateSinkWriterFromURL</c>: creates an <c>IMFSinkWriter</c> for the file at <paramref name="url"/> (container chosen by extension).</summary>
		[DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
		private static extern int MFCreateSinkWriterFromURL(string url, IntPtr byteStream, IntPtr attributes, ref IntPtr writer);

		/// <summary>
		/// Validates the size, starts Media Foundation, creates the sink writer with one H.264 output stream and an
		/// RGB32 input stream, and calls <c>BeginWriting</c>. Width and height are rounded down to even numbers
		/// (H.264 4:2:0 needs that); when that changes the size, frames are cropped to the top-left even region.
		/// The target bitrate is <c>width * height * fps * bpp</c> with <c>bpp = 0.01 * 50^(quality/100)</c>
		/// (0.01 .. 0.5 bits per pixel), clamped to 300 kbit/s .. 200 Mbit/s. On any failure everything is released
		/// (including <c>MFShutdown</c>) before the exception propagates.
		/// </summary>
		/// <param name="filePath">Output .mp4 path.</param>
		/// <param name="width">Width of the frames that will be passed to <see cref="AddPixels"/>.</param>
		/// <param name="height">Height of the frames that will be passed to <see cref="AddPixels"/>.</param>
		/// <param name="framesPerSecond">Playback frame rate.</param>
		/// <param name="quality">1..100 (clamped); controls the H.264 bitrate only.</param>
		/// <exception cref="NotSupportedException">The even-rounded size is below 16x16 or above 9,437,184 pixels (4096x2304), the limit of the Windows H.264 encoder.</exception>
		/// <exception cref="COMException">Media Foundation returned a failure HRESULT (no encoder, bad path, ...).</exception>
		public MfVideoWriter(string filePath, int width, int height, int framesPerSecond, int quality)
		{
			this._path = filePath;
			this._srcWidth = width;
			this._srcHeight = height;
			this._width = width & -2;
			this._height = height & -2;
			this._fps = framesPerSecond;
			if (this._width < 16 || this._height < 16)
			{
				throw new NotSupportedException("The canvas is too small for H.264 video.");
			}
			if ((long)this._width * this._height > 9437184L)
			{
				throw new NotSupportedException("The canvas is too large for the H.264 encoder of Windows.");
			}
			if (this._width != this._srcWidth || this._height != this._srcHeight)
			{
				this._cropped = new int[this._width * this._height];
			}
			MfVideoWriter.Check(MfVideoWriter.MFStartup(MfVersion, 0));
			this._started = true;
			try
			{
				MfVideoWriter.Check(MfVideoWriter.MFCreateSinkWriterFromURL(filePath, IntPtr.Zero, IntPtr.Zero, ref this._writerPtr));
				this._writer = (IMFSinkWriterNative)Marshal.GetObjectForIUnknown(this._writerPtr);
				int clampedQuality = Math.Max(1, Math.Min(100, quality));
				double bitsPerPixel = 0.01 * Math.Pow(50.0, clampedQuality / 100.0);
				long bitrate = (long)Math.Round((double)this._width * this._height * this._fps * bitsPerPixel);
				bitrate = Math.Max(300000L, Math.Min(200000000L, bitrate));
				IntPtr outputType = this.MakeType(MfVideoWriter.H264Format, (int)bitrate, false);
				int streamIndex = 0;
				try
				{
					MfVideoWriter.Check(this._writer.AddStream(outputType, ref streamIndex));
				}
				finally
				{
					Marshal.Release(outputType);
				}
				IntPtr inputType = this.MakeType(MfVideoWriter.Rgb32Format, 0, true);
				try
				{
					MfVideoWriter.Check(this._writer.SetInputMediaType(streamIndex, inputType, IntPtr.Zero));
				}
				finally
				{
					Marshal.Release(inputType);
				}
				MfVideoWriter.Check(this._writer.BeginWriting());
			}
			catch
			{
				this.Cleanup();
				throw;
			}
		}

		/// <summary>The MP4 path given to the constructor.</summary>
		public string FilePath
		{
			get
			{
				return this._path;
			}
		}

		/// <summary>
		/// Creates a progressive video <c>IMFMediaType</c> for <paramref name="format"/> at the writer's even size,
		/// <c>fps/1</c> frame rate and 1:1 pixel aspect ratio. The caller owns the returned reference and must
		/// <see cref="Marshal.Release"/> it. The RCW used for the attribute calls is released here.
		/// </summary>
		/// <param name="format">Subtype GUID: <see cref="H264Format"/> or <see cref="Rgb32Format"/>.</param>
		/// <param name="bitrate">Average bitrate in bits/s, written as <c>MF_MT_AVG_BITRATE</c> when &gt; 0.</param>
		/// <param name="topDown">When true, sets a positive <c>MF_MT_DEFAULT_STRIDE</c> of <c>width * 4</c> so the top-down ARGB rows are not flipped.</param>
		private IntPtr MakeType(Guid format, int bitrate, bool topDown)
		{
			IntPtr mediaType = IntPtr.Zero;
			MfVideoWriter.Check(MfVideoWriter.MFCreateMediaType(ref mediaType));
			try
			{
				IMFAttributesNative attributes = (IMFAttributesNative)Marshal.GetObjectForIUnknown(mediaType);
				try
				{
					MfVideoWriter.Check(attributes.SetGUID(MfVideoWriter.MajorType, MfVideoWriter.VideoType));
					MfVideoWriter.Check(attributes.SetGUID(MfVideoWriter.SubType, format));
					if (bitrate > 0)
					{
						MfVideoWriter.Check(attributes.SetUINT32(MfVideoWriter.AvgBitrate, bitrate));
					}
					MfVideoWriter.Check(attributes.SetUINT32(MfVideoWriter.InterlaceMode, 2)); // MFVideoInterlace_Progressive
					MfVideoWriter.Check(attributes.SetUINT64(MfVideoWriter.FrameSize, ((ulong)this._width << 32) | (ulong)this._height));
					MfVideoWriter.Check(attributes.SetUINT64(MfVideoWriter.FrameRate, ((ulong)this._fps << 32) | 1UL));
					MfVideoWriter.Check(attributes.SetUINT64(MfVideoWriter.PixelAspectRatio, 4294967297UL)); // 1:1 packed as (1 << 32) | 1
					if (topDown)
					{
						MfVideoWriter.Check(attributes.SetUINT32(MfVideoWriter.DefaultStride, this._width * 4));
					}
				}
				finally
				{
					Marshal.ReleaseComObject(attributes);
				}
			}
			catch
			{
				Marshal.Release(mediaType);
				throw;
			}
			return mediaType;
		}

		/// <summary>
		/// Copies the ARGB frame into a new Media Foundation memory buffer (cropping to the even size when needed),
		/// wraps it in a sample stamped with time and duration in 100-ns units derived from the frame index and fps,
		/// and hands it to stream 0 of the sink writer. Both RCW and raw reference of the buffer and sample are
		/// released afterwards; Media Foundation keeps its own reference while encoding.
		/// </summary>
		/// <param name="pixels">Row-major ARGB buffer of the original (uncropped) width * height.</param>
		/// <exception cref="InvalidOperationException">The writer has already been closed.</exception>
		/// <exception cref="COMException">A Media Foundation call failed.</exception>
		public void AddPixels(int[] pixels)
		{
			if (this._closed)
			{
				throw new InvalidOperationException("The video file is already closed.");
			}
			int[] source = pixels;
			if (this._cropped != null)
			{
				for (int y = 0; y < this._height; y++)
				{
					Array.Copy(pixels, y * this._srcWidth, this._cropped, y * this._width, this._width);
				}
				source = this._cropped;
			}
			int byteLength = this._width * this._height * 4;
			IntPtr bufferPtr = IntPtr.Zero;
			IntPtr samplePtr = IntPtr.Zero;
			IMFMediaBufferNative buffer = null;
			IMFSampleNative sample = null;
			try
			{
				MfVideoWriter.Check(MfVideoWriter.MFCreateMemoryBuffer(byteLength, ref bufferPtr));
				buffer = (IMFMediaBufferNative)Marshal.GetObjectForIUnknown(bufferPtr);
				IntPtr data = IntPtr.Zero;
				int maxLength = 0;
				int currentLength = 0;
				MfVideoWriter.Check(buffer.Lock(ref data, ref maxLength, ref currentLength));
				try
				{
					Marshal.Copy(source, 0, data, this._width * this._height);
				}
				finally
				{
					buffer.Unlock();
				}
				MfVideoWriter.Check(buffer.SetCurrentLength(byteLength));
				MfVideoWriter.Check(MfVideoWriter.MFCreateSample(ref samplePtr));
				sample = (IMFSampleNative)Marshal.GetObjectForIUnknown(samplePtr);
				MfVideoWriter.Check(sample.AddBuffer(bufferPtr));
				MfVideoWriter.Check(sample.SetSampleTime(this._frames * 10000000L / this._fps));
				MfVideoWriter.Check(sample.SetSampleDuration(10000000L / this._fps));
				MfVideoWriter.Check(this._writer.WriteSample(0, samplePtr));
				this._frames++;
			}
			finally
			{
				if (sample != null)
				{
					Marshal.ReleaseComObject(sample);
				}
				if (samplePtr != IntPtr.Zero)
				{
					Marshal.Release(samplePtr);
				}
				if (buffer != null)
				{
					Marshal.ReleaseComObject(buffer);
				}
				if (bufferPtr != IntPtr.Zero)
				{
					Marshal.Release(bufferPtr);
				}
			}
		}

		/// <summary>
		/// Finalizes the MP4 (writes the moov box) when at least one frame was written, then releases the sink
		/// writer and shuts Media Foundation down. Idempotent. Finalizing is skipped for an empty file because the
		/// sink writer rejects it; cleanup still runs.
		/// </summary>
		/// <exception cref="COMException"><c>Finalize</c> failed; cleanup has still been performed.</exception>
		public void Close()
		{
			if (this._closed)
			{
				return;
			}
			this._closed = true;
			try
			{
				if (this._writer != null && this._frames > 0)
				{
					MfVideoWriter.Check(this._writer.FinalizeWriting());
				}
			}
			finally
			{
				this.Cleanup();
			}
		}

		/// <summary>
		/// Releases the sink writer RCW and the raw reference obtained from <c>MFCreateSinkWriterFromURL</c>
		/// (both are needed: <see cref="Marshal.GetObjectForIUnknown"/> added its own reference), then calls
		/// <c>MFShutdown</c> once if startup succeeded. Safe to call repeatedly.
		/// </summary>
		private void Cleanup()
		{
			if (this._writer != null)
			{
				Marshal.ReleaseComObject(this._writer);
				this._writer = null;
			}
			if (this._writerPtr != IntPtr.Zero)
			{
				Marshal.Release(this._writerPtr);
				this._writerPtr = IntPtr.Zero;
			}
			if (this._started)
			{
				this._started = false;
				MfVideoWriter.MFShutdown();
			}
		}

		/// <summary>Same as <see cref="Close"/>.</summary>
		public void Dispose()
		{
			this.Close();
		}

		/// <summary>Throws the matching .NET exception for a failed HRESULT (negative value); succeeds silently otherwise.</summary>
		private static void Check(int hr)
		{
			if (hr < 0)
			{
				Marshal.ThrowExceptionForHR(hr);
			}
		}

		/// <summary><c>MF_VERSION</c> = 0x00020070 (SDK version 2, API version 0x70) passed to <c>MFStartup</c>.</summary>
		private const int MfVersion = 131184;

		/// <summary><c>MF_MT_MAJOR_TYPE</c> attribute key.</summary>
		private static readonly Guid MajorType = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");

		/// <summary><c>MF_MT_SUBTYPE</c> attribute key.</summary>
		private static readonly Guid SubType = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

		/// <summary><c>MF_MT_AVG_BITRATE</c> attribute key (bits per second).</summary>
		private static readonly Guid AvgBitrate = new Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e");

		/// <summary><c>MF_MT_INTERLACE_MODE</c> attribute key.</summary>
		private static readonly Guid InterlaceMode = new Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");

		/// <summary><c>MF_MT_FRAME_SIZE</c> attribute key (width in the high 32 bits, height in the low 32).</summary>
		private static readonly Guid FrameSize = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");

		/// <summary><c>MF_MT_FRAME_RATE</c> attribute key (numerator high 32 bits, denominator low 32).</summary>
		private static readonly Guid FrameRate = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");

		/// <summary><c>MF_MT_PIXEL_ASPECT_RATIO</c> attribute key (same packing as the frame rate).</summary>
		private static readonly Guid PixelAspectRatio = new Guid("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");

		/// <summary><c>MF_MT_DEFAULT_STRIDE</c> attribute key (bytes per row; positive = top-down).</summary>
		private static readonly Guid DefaultStride = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");

		/// <summary><c>MFMediaType_Video</c> major type ('vids').</summary>
		private static readonly Guid VideoType = new Guid("73646976-0000-0010-8000-00AA00389B71");

		/// <summary><c>MFVideoFormat_H264</c> subtype ('H264').</summary>
		private static readonly Guid H264Format = new Guid("34363248-0000-0010-8000-00AA00389B71");

		/// <summary><c>MFVideoFormat_RGB32</c> subtype (D3DFMT_X8R8G8B8 = 22); matches the ARGB int layout in memory.</summary>
		private static readonly Guid Rgb32Format = new Guid("00000016-0000-0010-8000-00AA00389B71");

		/// <summary>Output file path.</summary>
		private readonly string _path;

		/// <summary>Width of the frames passed to <see cref="AddPixels"/>, before rounding to even.</summary>
		private readonly int _srcWidth;

		/// <summary>Height of the frames passed to <see cref="AddPixels"/>, before rounding to even.</summary>
		private readonly int _srcHeight;

		/// <summary>Encoded width (source width rounded down to even).</summary>
		private readonly int _width;

		/// <summary>Encoded height (source height rounded down to even).</summary>
		private readonly int _height;

		/// <summary>Frames per second; also used to derive sample time/duration.</summary>
		private readonly int _fps;

		/// <summary>Scratch buffer of the even size when cropping is needed; null when source and encoded sizes match.</summary>
		private readonly int[] _cropped;

		/// <summary>True between a successful <c>MFStartup</c> and the matching <c>MFShutdown</c>.</summary>
		private bool _started;

		/// <summary>Raw <c>IMFSinkWriter</c> pointer from <c>MFCreateSinkWriterFromURL</c>; released separately from the RCW.</summary>
		private IntPtr _writerPtr;

		/// <summary>RCW over <see cref="_writerPtr"/>; null after <see cref="Cleanup"/>.</summary>
		private IMFSinkWriterNative _writer;

		/// <summary>Number of samples written; the next sample's time is <c>_frames / fps</c> seconds.</summary>
		private int _frames;

		/// <summary>True once <see cref="Close"/> has run.</summary>
		private bool _closed;
	}
}
