using System;
using System.Runtime.InteropServices;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Hand-written COM import of Media Foundation's <c>IMFMediaBuffer</c> (IID 045FA593-...), the byte buffer that
	/// <see cref="MfVideoWriter.AddPixels"/> fills with one RGB32 frame. Vtable order is frozen; all methods return
	/// an HRESULT. Only <see cref="Lock"/>, <see cref="Unlock"/> and <see cref="SetCurrentLength"/> are called.
	/// </summary>
	[Guid("045FA593-8799-42B8-BC8D-8968C6453507")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	internal interface IMFMediaBufferNative
	{
		/// <summary><c>IMFMediaBuffer::Lock</c>: returns the raw data pointer plus the buffer's maximum and current length in bytes; pair with <see cref="Unlock"/>.</summary>
		[PreserveSig]
		int Lock(ref IntPtr data, ref int maxLength, ref int currentLength);

		/// <summary><c>IMFMediaBuffer::Unlock</c>: releases the pointer obtained from <see cref="Lock"/>.</summary>
		[PreserveSig]
		int Unlock();

		/// <summary>Slot for <c>IMFMediaBuffer::GetCurrentLength</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetCurrentLength();

		/// <summary><c>IMFMediaBuffer::SetCurrentLength</c>: declares how many bytes of the buffer hold valid data.</summary>
		[PreserveSig]
		int SetCurrentLength(int length);

		/// <summary>Slot for <c>IMFMediaBuffer::GetMaxLength</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetMaxLength();
	}
}
