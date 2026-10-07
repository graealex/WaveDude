using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Minimal Motion-JPEG AVI (RIFF) writer, single video stream, no audio. The constructor writes the
	/// fixed-size header, <see cref="AddFrame"/> appends one <c>00dc</c> chunk per JPEG to the <c>movi</c>
	/// list and <see cref="Close"/> appends the <c>idx1</c> index and patches the size/count fields in the
	/// header. Used by <see cref="AviFrameSink"/>. All values are little-endian.
	/// </summary>
	/// <remarks>
	/// <para>Intended layout (byte offsets), which is what the <c>Pos*</c> constants encode:</para>
	/// <list type="table">
	/// <item><term>0</term><description><c>RIFF</c>, file size - 8 (patched at <see cref="PosRiffSize"/>), <c>AVI </c></description></item>
	/// <item><term>12</term><description><c>LIST</c> 192 <c>hdrl</c></description></item>
	/// <item><term>24</term><description><c>avih</c> 56: MainAVIHeader (microseconds/frame, flags = AVIF_HASINDEX, total frames at <see cref="PosTotalFrames"/>, 1 stream, suggested buffer at <see cref="PosBufferSize"/>, width, height)</description></item>
	/// <item><term>88</term><description><c>LIST</c> 116 <c>strl</c></description></item>
	/// <item><term>100</term><description><c>strh</c> 56: AVISTREAMHEADER (<c>vids</c>/<c>MJPG</c>, scale/rate, length at <see cref="PosStreamLength"/>, suggested buffer at <see cref="PosStreamBufferSize"/>, rcFrame)</description></item>
	/// <item><term>164</term><description><c>strf</c> 40: BITMAPINFOHEADER (24 bpp, compression <c>MJPG</c>)</description></item>
	/// <item><term>212</term><description><c>LIST</c> movi size (patched at <see cref="PosMoviSize"/>) <c>movi</c> at <see cref="PosMoviTag"/>, then the frame chunks</description></item>
	/// <item><term>end</term><description><c>idx1</c>: one 16-byte entry per frame (<c>00dc</c>, flags = AVIIF_KEYFRAME, offset relative to <see cref="PosMoviTag"/>, size)</description></item>
	/// </list>
	/// <para>Note (writes are deliberately left as they are): the constructor emits 236 header bytes rather
	/// than 224, because <c>strh</c> is written as 64 bytes (an extra zero DWORD before scale/rate, and
	/// rcFrame as two ints plus two ushorts) and <c>strf</c> as 44 bytes (planes and bit count as ints).
	/// The <c>Pos*</c> offsets therefore no longer line up with the bytes actually written: the <c>movi</c>
	/// tag is really at 232, <see cref="PosMoviSize"/> hits the BITMAPINFOHEADER and the real <c>movi</c>
	/// list size stays 0. Odd-length frames are padded with a 4-byte int, not a single byte.</para>
	/// </remarks>
	internal sealed class AviMjpegWriter : IDisposable
	{
		/// <summary>Creates (overwrites) the file and writes the complete header up to and including the <c>movi</c> tag.</summary>
		/// <param name="filePath">Output path; an existing file is truncated.</param>
		/// <param name="width">Frame width in pixels.</param>
		/// <param name="height">Frame height in pixels.</param>
		/// <param name="framesPerSecond">Playback rate; stored as microseconds per frame in <c>avih</c> and as the rate in <c>strh</c>.</param>
		public AviMjpegWriter(string filePath, int width, int height, int framesPerSecond)
		{
			this._offsets = new List<int>();
			this._sizes = new List<int>();
			this._stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
			this._writer = new BinaryWriter(this._stream);
			Encoding ascii = Encoding.ASCII;
			// RIFF header
			this._writer.Write(ascii.GetBytes("RIFF"));
			this._writer.Write(0U); // file size - 8, patched in Close
			this._writer.Write(ascii.GetBytes("AVI "));
			// LIST hdrl
			this._writer.Write(ascii.GetBytes("LIST"));
			this._writer.Write(192U);
			this._writer.Write(ascii.GetBytes("hdrl"));
			// avih (MainAVIHeader)
			this._writer.Write(ascii.GetBytes("avih"));
			this._writer.Write(56U);
			this._writer.Write((uint)(1000000 / framesPerSecond)); // dwMicroSecPerFrame
			this._writer.Write(0U); // dwMaxBytesPerSec
			this._writer.Write(0U); // dwPaddingGranularity
			this._writer.Write(16U); // dwFlags = AVIF_HASINDEX
			this._writer.Write(0U); // dwTotalFrames, patched in Close
			this._writer.Write(0U); // dwInitialFrames
			this._writer.Write(1U); // dwStreams
			this._writer.Write(0U); // dwSuggestedBufferSize, patched in Close
			this._writer.Write((uint)width);
			this._writer.Write((uint)height);
			for (int i = 0; i < 4; i++)
			{
				this._writer.Write(0U); // dwReserved[4]
			}
			// LIST strl
			this._writer.Write(ascii.GetBytes("LIST"));
			this._writer.Write(116U);
			this._writer.Write(ascii.GetBytes("strl"));
			// strh (AVISTREAMHEADER)
			this._writer.Write(ascii.GetBytes("strh"));
			this._writer.Write(56U);
			this._writer.Write(ascii.GetBytes("vids")); // fccType
			this._writer.Write(ascii.GetBytes("MJPG")); // fccHandler
			this._writer.Write(0U); // dwFlags
			this._writer.Write(0); // wPriority + wLanguage
			this._writer.Write(0); // dwInitialFrames
			this._writer.Write(0U); // extra DWORD (see class remarks); lands on dwScale
			this._writer.Write(1U); // intended dwScale; lands on dwRate
			this._writer.Write((uint)framesPerSecond); // intended dwRate; lands on dwStart
			this._writer.Write(0U); // dwLength, patched in Close
			this._writer.Write(0U); // dwSuggestedBufferSize, patched in Close
			this._writer.Write(0U); // dwQuality
			this._writer.Write(uint.MaxValue); // dwSampleSize (-1)
			this._writer.Write(0U); // rcFrame left/top
			this._writer.Write(0); // rcFrame right/bottom
			this._writer.Write(0); // extra DWORD (see class remarks)
			this._writer.Write((ushort)width); // extra; beyond the declared 56 bytes
			this._writer.Write((ushort)height);
			// strf (BITMAPINFOHEADER)
			this._writer.Write(ascii.GetBytes("strf"));
			this._writer.Write(40U);
			this._writer.Write(40U); // biSize
			this._writer.Write(width); // biWidth
			this._writer.Write(height); // biHeight
			this._writer.Write(1); // biPlanes (written as a DWORD, should be a WORD)
			this._writer.Write(24); // biBitCount (written as a DWORD, should be a WORD)
			this._writer.Write(ascii.GetBytes("MJPG")); // biCompression
			this._writer.Write((uint)(((long)width * height * 3L) & 0xFFFFFFFFL)); // biSizeImage
			this._writer.Write(0); // biXPelsPerMeter
			this._writer.Write(0); // biYPelsPerMeter
			this._writer.Write(0U); // biClrUsed
			this._writer.Write(0U); // biClrImportant
			// LIST movi; frames follow
			this._writer.Write(ascii.GetBytes("LIST"));
			this._writer.Write(0U); // movi size, patched in Close
			this._writer.Write(ascii.GetBytes("movi"));
		}

		/// <summary>Number of frames appended so far.</summary>
		public int FrameCount
		{
			get
			{
				return this._offsets.Count;
			}
		}

		/// <summary>
		/// Appends one JPEG frame as a <c>00dc</c> chunk and records its offset/size for the index. Odd lengths
		/// are followed by a pad (written as a 4-byte int, see class remarks).
		/// </summary>
		/// <param name="data">Buffer holding the JPEG; only the first <paramref name="length"/> bytes are used.</param>
		/// <param name="length">Number of JPEG bytes.</param>
		/// <exception cref="InvalidOperationException">The file is already closed.</exception>
		/// <exception cref="IOException">The frame plus the index that would follow would push the file past <see cref="MaxAviBytes"/>.</exception>
		public void AddFrame(byte[] data, int length)
		{
			if (this._closed)
			{
				throw new InvalidOperationException("The video file is already closed.");
			}
			// Reserve room for the idx1 that Close() will append (16 bytes per frame plus slack).
			if (this._stream.Position + length + 16L * (this._offsets.Count + 2) + 64L > MaxAviBytes)
			{
				throw new IOException("The video file would become larger than 4 GB, which the AVI format does not allow.");
			}
			this._offsets.Add((int)(this._stream.Position - PosMoviTag));
			this._sizes.Add(length);
			this._writer.Write(Encoding.ASCII.GetBytes("00dc"));
			this._writer.Write((uint)length);
			this._writer.Write(data, 0, length);
			if ((length & 1) == 1)
			{
				this._writer.Write(0); // 4-byte pad (int), not the 1-byte RIFF pad
			}
			if (length > this._largestFrame)
			{
				this._largestFrame = length;
			}
		}

		/// <summary>
		/// Appends the <c>idx1</c> chunk, patches the RIFF size, frame counts, buffer sizes and movi size in the
		/// header, then closes the stream. Idempotent; the stream is closed even if a write fails.
		/// </summary>
		public void Close()
		{
			if (this._closed)
			{
				return;
			}
			this._closed = true;
			try
			{
				long indexPosition = this._stream.Position;
				this._writer.Write(Encoding.ASCII.GetBytes("idx1"));
				this._writer.Write((uint)(16 * this._offsets.Count));
				for (int i = 0; i < this._offsets.Count; i++)
				{
					this._writer.Write(Encoding.ASCII.GetBytes("00dc"));
					this._writer.Write(16U); // AVIIF_KEYFRAME
					this._writer.Write((uint)this._offsets[i]);
					this._writer.Write((uint)this._sizes[i]);
				}
				long endPosition = this._stream.Position;
				this.Patch(PosRiffSize, (uint)(endPosition - 8L));
				this.Patch(PosTotalFrames, (uint)this._offsets.Count);
				this.Patch(PosBufferSize, (uint)this._largestFrame);
				this.Patch(PosStreamLength, (uint)this._offsets.Count);
				this.Patch(PosStreamBufferSize, (uint)this._largestFrame);
				this.Patch(PosMoviSize, (uint)(indexPosition - PosMoviSize));
				this._writer.Flush();
			}
			finally
			{
				this._writer.Close();
				this._stream.Dispose();
			}
		}

		/// <summary>Seeks to <paramref name="position"/> and overwrites one DWORD; leaves the stream positioned after it.</summary>
		private void Patch(long position, uint value)
		{
			this._stream.Seek(position, SeekOrigin.Begin);
			this._writer.Write(value);
		}

		/// <summary>Same as <see cref="Close"/>.</summary>
		public void Dispose()
		{
			this.Close();
		}

		/// <summary>Hard cap on the file size in bytes, kept safely below the 4 GB RIFF limit.</summary>
		private const long MaxAviBytes = 3900000000L;

		/// <summary>Unused duplicate of <see cref="MaxAviBytes"/>.</summary>
		private const long MaxFileSize = 3900000000L;

		/// <summary>Offset of the RIFF size DWORD (file size - 8).</summary>
		private const long PosRiffSize = 4L;

		/// <summary>Offset of avih.dwTotalFrames.</summary>
		private const long PosTotalFrames = 48L;

		/// <summary>Offset of avih.dwSuggestedBufferSize (largest frame).</summary>
		private const long PosBufferSize = 60L;

		/// <summary>Offset of strh.dwLength (frame count).</summary>
		private const long PosStreamLength = 140L;

		/// <summary>Offset of strh.dwSuggestedBufferSize (largest frame).</summary>
		private const long PosStreamBufferSize = 144L;

		/// <summary>Intended offset of the movi LIST size DWORD (actually written at 228, see class remarks).</summary>
		private const long PosMoviSize = 216L;

		/// <summary>Intended offset of the <c>movi</c> tag; idx1 offsets are relative to this (the tag is actually written at 232, see class remarks).</summary>
		private const long PosMoviTag = 220L;

		/// <summary>The output file; disposed by <see cref="Close"/>.</summary>
		private FileStream _stream;

		/// <summary>Little-endian writer over <see cref="_stream"/>.</summary>
		private BinaryWriter _writer;

		/// <summary>Per frame: chunk offset relative to <see cref="PosMoviTag"/>, for idx1.</summary>
		private readonly List<int> _offsets;

		/// <summary>Per frame: JPEG byte count, for idx1.</summary>
		private readonly List<int> _sizes;

		/// <summary>Largest JPEG so far, in bytes; becomes the suggested buffer size.</summary>
		private int _largestFrame;

		/// <summary>True once <see cref="Close"/> has run; further <see cref="AddFrame"/> calls throw.</summary>
		private bool _closed;
	}
}
