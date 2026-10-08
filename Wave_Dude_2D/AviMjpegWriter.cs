using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Minimal Motion-JPEG AVI (RIFF) writer, single video stream, no audio. Every frame is given as an
	/// ordinary JPEG image, so no codec or external program is needed; VLC and most video editors play the
	/// file, and a program like ffmpeg can convert it to MP4. The constructor writes the fixed-size header,
	/// <see cref="AddFrame"/> appends one <c>00dc</c> chunk per JPEG to the <c>movi</c> list and
	/// <see cref="Close"/> appends the <c>idx1</c> index and patches the size/count fields in the header.
	/// Used by <see cref="AviFrameSink"/>. All values are little-endian.
	/// </summary>
	/// <remarks>
	/// Layout (byte offsets), which is what the <c>Pos*</c> constants encode:
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
			_stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
			_writer = new BinaryWriter(_stream);
			Encoding ascii = Encoding.ASCII;
			// RIFF header
			_writer.Write(ascii.GetBytes("RIFF"));
			_writer.Write(0U); // file size - 8, patched in Close
			_writer.Write(ascii.GetBytes("AVI "));
			// LIST hdrl
			_writer.Write(ascii.GetBytes("LIST"));
			_writer.Write(192U); // 'hdrl' + avih chunk (64) + strl list chunk (124)
			_writer.Write(ascii.GetBytes("hdrl"));
			// avih (MainAVIHeader, 56 bytes)
			_writer.Write(ascii.GetBytes("avih"));
			_writer.Write(56U);
			_writer.Write((uint)(1000000 / framesPerSecond)); // dwMicroSecPerFrame
			_writer.Write(0U); // dwMaxBytesPerSec
			_writer.Write(0U); // dwPaddingGranularity
			_writer.Write(AvifHasIndex); // dwFlags
			_writer.Write(0U); // dwTotalFrames, patched in Close
			_writer.Write(0U); // dwInitialFrames
			_writer.Write(1U); // dwStreams
			_writer.Write(0U); // dwSuggestedBufferSize, patched in Close
			_writer.Write((uint)width);
			_writer.Write((uint)height);
			for (int i = 0; i < 4; i++)
			{
				_writer.Write(0U); // dwReserved[4]
			}
			// LIST strl
			_writer.Write(ascii.GetBytes("LIST"));
			_writer.Write(116U); // 'strl' + strh chunk (64) + strf chunk (48)
			_writer.Write(ascii.GetBytes("strl"));
			// strh (AVISTREAMHEADER, 56 bytes)
			_writer.Write(ascii.GetBytes("strh"));
			_writer.Write(56U);
			_writer.Write(ascii.GetBytes("vids")); // fccType
			_writer.Write(ascii.GetBytes("MJPG")); // fccHandler
			_writer.Write(0U); // dwFlags
			_writer.Write((ushort)0); // wPriority
			_writer.Write((ushort)0); // wLanguage
			_writer.Write(0U); // dwInitialFrames
			_writer.Write(1U); // dwScale
			_writer.Write((uint)framesPerSecond); // dwRate (frames per second = dwRate / dwScale)
			_writer.Write(0U); // dwStart
			_writer.Write(0U); // dwLength, patched in Close
			_writer.Write(0U); // dwSuggestedBufferSize, patched in Close
			_writer.Write(0xFFFFFFFFU); // dwQuality: -1 = default
			_writer.Write(0U); // dwSampleSize (0 = variable, one frame per chunk)
			_writer.Write((ushort)0); // rcFrame.left
			_writer.Write((ushort)0); // rcFrame.top
			_writer.Write((ushort)width); // rcFrame.right
			_writer.Write((ushort)height); // rcFrame.bottom
			// strf (BITMAPINFOHEADER, 40 bytes)
			_writer.Write(ascii.GetBytes("strf"));
			_writer.Write(40U);
			_writer.Write(40U); // biSize
			_writer.Write(width); // biWidth
			_writer.Write(height); // biHeight
			_writer.Write((ushort)1); // biPlanes
			_writer.Write((ushort)24); // biBitCount
			_writer.Write(ascii.GetBytes("MJPG")); // biCompression
			_writer.Write((uint)(((long)width * height * 3L) & 0xFFFFFFFFL)); // biSizeImage
			_writer.Write(0); // biXPelsPerMeter
			_writer.Write(0); // biYPelsPerMeter
			_writer.Write(0U); // biClrUsed
			_writer.Write(0U); // biClrImportant
			// LIST movi; frames follow
			_writer.Write(ascii.GetBytes("LIST"));
			_writer.Write(0U); // movi size, patched in Close
			_writer.Write(ascii.GetBytes("movi"));
		}

		/// <summary>Number of frames appended so far.</summary>
		public int FrameCount => _offsets.Count;

		/// <summary>
		/// Appends one JPEG frame as a <c>00dc</c> chunk and records its offset/size for the index. Odd lengths
		/// are followed by one RIFF pad byte.
		/// </summary>
		/// <param name="data">Buffer holding the JPEG; only the first <paramref name="length"/> bytes are used.</param>
		/// <param name="length">Number of JPEG bytes.</param>
		/// <exception cref="InvalidOperationException">The file is already closed.</exception>
		/// <exception cref="IOException">The frame plus the index that would follow would push the file past <see cref="MaxAviBytes"/>.</exception>
		public void AddFrame(byte[] data, int length)
		{
			if (_closed)
			{
				throw new InvalidOperationException("The video file is already closed.");
			}
			// Reserve room for the idx1 that Close() will append (16 bytes per frame plus slack).
			if (_stream.Position + length + 16L * (_offsets.Count + 2) + 64L > MaxAviBytes)
			{
				throw new IOException("The video file would become larger than 4 GB, which the AVI format does not allow.");
			}
			_offsets.Add((int)(_stream.Position - PosMoviTag));
			_sizes.Add(length);
			_writer.Write(Encoding.ASCII.GetBytes("00dc"));
			_writer.Write((uint)length);
			_writer.Write(data, 0, length);
			if ((length & 1) == 1)
			{
				_writer.Write((byte)0); // RIFF chunks are word-aligned
			}
			if (length > _largestFrame)
			{
				_largestFrame = length;
			}
		}

		/// <summary>
		/// Appends the <c>idx1</c> chunk, patches the RIFF size, frame counts, buffer sizes and movi size in the
		/// header, then closes the stream. Idempotent; the stream is closed even if a write fails.
		/// </summary>
		public void Close()
		{
			if (_closed)
			{
				return;
			}
			_closed = true;
			try
			{
				long indexPosition = _stream.Position;
				_writer.Write(Encoding.ASCII.GetBytes("idx1"));
				_writer.Write((uint)(16 * _offsets.Count));
				for (int i = 0; i < _offsets.Count; i++)
				{
					_writer.Write(Encoding.ASCII.GetBytes("00dc"));
					_writer.Write(AviifKeyframe);
					_writer.Write((uint)_offsets[i]);
					_writer.Write((uint)_sizes[i]);
				}
				long endPosition = _stream.Position;
				Patch(PosRiffSize, (uint)(endPosition - 8L));
				Patch(PosTotalFrames, (uint)_offsets.Count);
				Patch(PosBufferSize, (uint)_largestFrame);
				Patch(PosStreamLength, (uint)_offsets.Count);
				Patch(PosStreamBufferSize, (uint)_largestFrame);
				Patch(PosMoviSize, (uint)(indexPosition - PosMoviTag)); // LIST size counts from its fourcc
				_writer.Flush();
			}
			finally
			{
				_writer.Close();
				_stream.Dispose();
			}
		}

		/// <summary>Seeks to <paramref name="position"/> and overwrites one DWORD; leaves the stream positioned after it.</summary>
		private void Patch(long position, uint value)
		{
			_stream.Seek(position, SeekOrigin.Begin);
			_writer.Write(value);
		}

		/// <summary>Same as <see cref="Close"/>.</summary>
		public void Dispose() => Close();

		/// <summary>Hard cap on the file size in bytes. AVI files are limited to 4 GB (unsigned 32 bit sizes); this stops safely before that.</summary>
		private const long MaxAviBytes = 3900000000L;

		/// <summary>avih.dwFlags: the file has an <c>idx1</c> index.</summary>
		private const uint AvifHasIndex = 0x10;

		/// <summary>idx1 entry flag: the chunk is a key frame (every MJPEG frame is).</summary>
		private const uint AviifKeyframe = 0x10;

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

		/// <summary>Offset of the movi LIST size DWORD.</summary>
		private const long PosMoviSize = 216L;

		/// <summary>Offset of the <c>movi</c> fourcc; idx1 chunk offsets are relative to this.</summary>
		private const long PosMoviTag = 220L;

		/// <summary>The output file; disposed by <see cref="Close"/>.</summary>
		private readonly FileStream _stream;

		/// <summary>Little-endian writer over <see cref="_stream"/>.</summary>
		private readonly BinaryWriter _writer;

		/// <summary>Per frame: chunk offset relative to <see cref="PosMoviTag"/>, for idx1.</summary>
		private readonly List<int> _offsets = new List<int>();

		/// <summary>Per frame: JPEG byte count, for idx1.</summary>
		private readonly List<int> _sizes = new List<int>();

		/// <summary>Largest JPEG so far, in bytes; becomes the suggested buffer size.</summary>
		private int _largestFrame;

		/// <summary>True once <see cref="Close"/> has run; further <see cref="AddFrame"/> calls throw.</summary>
		private bool _closed;
	}
}
