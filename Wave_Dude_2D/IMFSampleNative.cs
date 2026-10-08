using System;
using System.Runtime.InteropServices;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Hand-written COM import of Media Foundation's <c>IMFSample</c> (IID C40A00F2-...), one time-stamped video frame
	/// handed to the sink writer by <see cref="MfVideoWriter.AddPixels"/>. <c>IMFSample</c> derives from <c>IMFAttributes</c>,
	/// so the first 30 slots (<c>A01</c>..<c>A30</c>) are placeholders for the inherited attribute methods, followed by the
	/// sample's own methods. They are repeated here rather than taken from <see cref="IMFAttributesNative"/> because .NET
	/// does not inherit base-interface methods in COM interfaces: a derived <c>ComImport</c> interface's own methods are
	/// numbered from the slot after <c>IUnknown</c>, not after the base interface's methods. Vtable order is frozen;
	/// all methods return an HRESULT.
	/// </summary>
	[Guid("C40A00F2-B93A-4D80-AE8C-5A1C634F58E4")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	internal interface IMFSampleNative
	{
		/// <summary>Slot for inherited <c>IMFAttributes::GetItem</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A01();

		/// <summary>Slot for inherited <c>IMFAttributes::GetItemType</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A02();

		/// <summary>Slot for inherited <c>IMFAttributes::CompareItem</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A03();

		/// <summary>Slot for inherited <c>IMFAttributes::Compare</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A04();

		/// <summary>Slot for inherited <c>IMFAttributes::GetUINT32</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A05();

		/// <summary>Slot for inherited <c>IMFAttributes::GetUINT64</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A06();

		/// <summary>Slot for inherited <c>IMFAttributes::GetDouble</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A07();

		/// <summary>Slot for inherited <c>IMFAttributes::GetGUID</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A08();

		/// <summary>Slot for inherited <c>IMFAttributes::GetStringLength</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A09();

		/// <summary>Slot for inherited <c>IMFAttributes::GetString</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A10();

		/// <summary>Slot for inherited <c>IMFAttributes::GetAllocatedString</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A11();

		/// <summary>Slot for inherited <c>IMFAttributes::GetBlobSize</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A12();

		/// <summary>Slot for inherited <c>IMFAttributes::GetBlob</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A13();

		/// <summary>Slot for inherited <c>IMFAttributes::GetAllocatedBlob</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A14();

		/// <summary>Slot for inherited <c>IMFAttributes::GetUnknown</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A15();

		/// <summary>Slot for inherited <c>IMFAttributes::SetItem</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A16();

		/// <summary>Slot for inherited <c>IMFAttributes::DeleteItem</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A17();

		/// <summary>Slot for inherited <c>IMFAttributes::DeleteAllItems</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A18();

		/// <summary>Slot for inherited <c>IMFAttributes::SetUINT32</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A19();

		/// <summary>Slot for inherited <c>IMFAttributes::SetUINT64</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A20();

		/// <summary>Slot for inherited <c>IMFAttributes::SetDouble</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A21();

		/// <summary>Slot for inherited <c>IMFAttributes::SetGUID</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A22();

		/// <summary>Slot for inherited <c>IMFAttributes::SetString</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A23();

		/// <summary>Slot for inherited <c>IMFAttributes::SetBlob</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A24();

		/// <summary>Slot for inherited <c>IMFAttributes::SetUnknown</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A25();

		/// <summary>Slot for inherited <c>IMFAttributes::LockStore</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A26();

		/// <summary>Slot for inherited <c>IMFAttributes::UnlockStore</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A27();

		/// <summary>Slot for inherited <c>IMFAttributes::GetCount</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A28();

		/// <summary>Slot for inherited <c>IMFAttributes::GetItemByIndex</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A29();

		/// <summary>Slot for inherited <c>IMFAttributes::CopyAllItems</c> (placeholder, not called).</summary>
		[PreserveSig]
		int A30();

		/// <summary>Slot for <c>IMFSample::GetSampleFlags</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetSampleFlags();

		/// <summary>Slot for <c>IMFSample::SetSampleFlags</c> (placeholder, not called).</summary>
		[PreserveSig]
		int SetSampleFlags();

		/// <summary>Slot for <c>IMFSample::GetSampleTime</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetSampleTime();

		/// <summary><c>IMFSample::SetSampleTime</c>: presentation time in 100-nanosecond units.</summary>
		[PreserveSig]
		int SetSampleTime(long sampleTime);

		/// <summary>Slot for <c>IMFSample::GetSampleDuration</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetSampleDuration();

		/// <summary><c>IMFSample::SetSampleDuration</c>: duration in 100-nanosecond units.</summary>
		[PreserveSig]
		int SetSampleDuration(long duration);

		/// <summary>Slot for <c>IMFSample::GetBufferCount</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetBufferCount();

		/// <summary>Slot for <c>IMFSample::GetBufferByIndex</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetBufferByIndex();

		/// <summary>Slot for <c>IMFSample::ConvertToContiguousBuffer</c> (placeholder, not called).</summary>
		[PreserveSig]
		int ConvertToContiguousBuffer();

		/// <summary><c>IMFSample::AddBuffer</c>: appends an <c>IMFMediaBuffer</c> (raw pointer; the sample AddRefs it).</summary>
		[PreserveSig]
		int AddBuffer(IntPtr buffer);

		/// <summary>Slot for <c>IMFSample::RemoveBufferByIndex</c> (placeholder, not called).</summary>
		[PreserveSig]
		int RemoveBufferByIndex();

		/// <summary>Slot for <c>IMFSample::RemoveAllBuffers</c> (placeholder, not called).</summary>
		[PreserveSig]
		int RemoveAllBuffers();

		/// <summary>Slot for <c>IMFSample::GetTotalLength</c> (placeholder, not called).</summary>
		[PreserveSig]
		int GetTotalLength();

		/// <summary>Slot for <c>IMFSample::CopyToBuffer</c> (placeholder, not called).</summary>
		[PreserveSig]
		int CopyToBuffer();
	}
}
