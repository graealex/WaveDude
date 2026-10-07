using System;
using System.Runtime.InteropServices;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Hand-written COM import of Media Foundation's <c>IMFAttributes</c> (IID 2CD2D921-...), used by
	/// <see cref="MfVideoWriter"/> to set media-type attributes. The method order mirrors the native vtable and
	/// must not change; only the three <c>Set*</c> members that are actually called carry real signatures, the rest
	/// are parameterless placeholders that keep the slots aligned (never call them). All methods return an HRESULT.
	/// </summary>
	[Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	internal interface IMFAttributesNative
	{
		/// <summary>Slot for <c>IMFAttributes::GetItem</c> (placeholder).</summary>
		[PreserveSig]
		int GetItem();

		/// <summary>Slot for <c>IMFAttributes::GetItemType</c> (placeholder).</summary>
		[PreserveSig]
		int GetItemType();

		/// <summary>Slot for <c>IMFAttributes::CompareItem</c> (placeholder).</summary>
		[PreserveSig]
		int CompareItem();

		/// <summary>Slot for <c>IMFAttributes::Compare</c> (placeholder).</summary>
		[PreserveSig]
		int Compare();

		/// <summary>Slot for <c>IMFAttributes::GetUINT32</c> (placeholder).</summary>
		[PreserveSig]
		int GetUINT32();

		/// <summary>Slot for <c>IMFAttributes::GetUINT64</c> (placeholder).</summary>
		[PreserveSig]
		int GetUINT64();

		/// <summary>Slot for <c>IMFAttributes::GetDouble</c> (placeholder).</summary>
		[PreserveSig]
		int GetDouble();

		/// <summary>Slot for <c>IMFAttributes::GetGUID</c> (placeholder).</summary>
		[PreserveSig]
		int GetGUID();

		/// <summary>Slot for <c>IMFAttributes::GetStringLength</c> (placeholder).</summary>
		[PreserveSig]
		int GetStringLength();

		/// <summary>Slot for <c>IMFAttributes::GetString</c> (placeholder).</summary>
		[PreserveSig]
		int GetString();

		/// <summary>Slot for <c>IMFAttributes::GetAllocatedString</c> (placeholder).</summary>
		[PreserveSig]
		int GetAllocatedString();

		/// <summary>Slot for <c>IMFAttributes::GetBlobSize</c> (placeholder).</summary>
		[PreserveSig]
		int GetBlobSize();

		/// <summary>Slot for <c>IMFAttributes::GetBlob</c> (placeholder).</summary>
		[PreserveSig]
		int GetBlob();

		/// <summary>Slot for <c>IMFAttributes::GetAllocatedBlob</c> (placeholder).</summary>
		[PreserveSig]
		int GetAllocatedBlob();

		/// <summary>Slot for <c>IMFAttributes::GetUnknown</c> (placeholder).</summary>
		[PreserveSig]
		int GetUnknown();

		/// <summary>Slot for <c>IMFAttributes::SetItem</c> (placeholder).</summary>
		[PreserveSig]
		int SetItem();

		/// <summary>Slot for <c>IMFAttributes::DeleteItem</c> (placeholder).</summary>
		[PreserveSig]
		int DeleteItem();

		/// <summary>Slot for <c>IMFAttributes::DeleteAllItems</c> (placeholder).</summary>
		[PreserveSig]
		int DeleteAllItems();

		/// <summary><c>IMFAttributes::SetUINT32</c>: stores a 32-bit value under <paramref name="guidKey"/> (the GUID is passed by pointer).</summary>
		[PreserveSig]
		int SetUINT32([MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, int value);

		/// <summary><c>IMFAttributes::SetUINT64</c>: stores a 64-bit value under <paramref name="guidKey"/>.</summary>
		[PreserveSig]
		int SetUINT64([MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong value);

		/// <summary>Slot for <c>IMFAttributes::SetDouble</c> (placeholder).</summary>
		[PreserveSig]
		int SetDouble();

		/// <summary><c>IMFAttributes::SetGUID</c>: stores a GUID value under <paramref name="guidKey"/>.</summary>
		[PreserveSig]
		int SetGUID([MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [MarshalAs(UnmanagedType.LPStruct)] Guid value);

		/// <summary>Slot for <c>IMFAttributes::SetString</c> (placeholder).</summary>
		[PreserveSig]
		int SetString();

		/// <summary>Slot for <c>IMFAttributes::SetBlob</c> (placeholder).</summary>
		[PreserveSig]
		int SetBlob();

		/// <summary>Slot for <c>IMFAttributes::SetUnknown</c> (placeholder).</summary>
		[PreserveSig]
		int SetUnknown();

		/// <summary>Slot for <c>IMFAttributes::LockStore</c> (placeholder).</summary>
		[PreserveSig]
		int LockStore();

		/// <summary>Slot for <c>IMFAttributes::UnlockStore</c> (placeholder).</summary>
		[PreserveSig]
		int UnlockStore();

		/// <summary>Slot for <c>IMFAttributes::GetCount</c> (placeholder).</summary>
		[PreserveSig]
		int GetCount();

		/// <summary>Slot for <c>IMFAttributes::GetItemByIndex</c> (placeholder).</summary>
		[PreserveSig]
		int GetItemByIndex();

		/// <summary>Slot for <c>IMFAttributes::CopyAllItems</c> (placeholder).</summary>
		[PreserveSig]
		int CopyAllItems();
	}
}
