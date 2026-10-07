using System;
using System.Runtime.InteropServices;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Hand-written COM import of Media Foundation's <c>IMFSinkWriter</c> (IID 3137F1CD-...), the object that
	/// encodes and muxes the MP4 for <see cref="MfVideoWriter"/>. Vtable order is frozen; all methods return an
	/// HRESULT. Only the members with parameters (and <see cref="BeginWriting"/> / <see cref="FinalizeWriting"/>) are called.
	/// </summary>
	[Guid("3137F1CD-FE5E-4805-A5D8-FB477448CB3D")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	internal interface IMFSinkWriterNative
	{
		/// <summary><c>IMFSinkWriter::AddStream</c>: adds an output stream with the given encoded media type and returns its index.</summary>
		[PreserveSig]
		int AddStream(IntPtr mediaType, ref int streamIndex);

		/// <summary><c>IMFSinkWriter::SetInputMediaType</c>: sets the uncompressed input type for a stream (<paramref name="encodingParameters"/> may be null).</summary>
		[PreserveSig]
		int SetInputMediaType(int streamIndex, IntPtr mediaType, IntPtr encodingParameters);

		/// <summary><c>IMFSinkWriter::BeginWriting</c>: starts the encoder/muxer; must precede <see cref="WriteSample"/>.</summary>
		[PreserveSig]
		int BeginWriting();

		/// <summary><c>IMFSinkWriter::WriteSample</c>: delivers one time-stamped sample to a stream.</summary>
		[PreserveSig]
		int WriteSample(int streamIndex, IntPtr sample);

		/// <summary>Slot for <c>IMFSinkWriter::SendStreamTick</c> (placeholder, not called).</summary>
		[PreserveSig]
		int SendStreamTick();

		/// <summary>Slot for <c>IMFSinkWriter::PlaceMarker</c> (placeholder, not called).</summary>
		[PreserveSig]
		int PlaceMarker();

		/// <summary>Slot for <c>IMFSinkWriter::NotifyEndOfSegment</c> (placeholder, not called).</summary>
		[PreserveSig]
		int NotifyEndOfSegment();

		/// <summary>Slot for <c>IMFSinkWriter::Flush</c> (placeholder, not called).</summary>
		[PreserveSig]
		int Flush();

		/// <summary><c>IMFSinkWriter::Finalize</c>: flushes the encoder and writes the container trailer (named differently to avoid clashing with <see cref="object.Finalize"/>).</summary>
		[PreserveSig]
		int FinalizeWriting();

		/// <summary>Slot for <c>IMFSinkWriter::GetServiceForStream</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetServiceForStream();

		/// <summary>Slot for <c>IMFSinkWriter::GetStatistics</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetStatistics();
	}
}
