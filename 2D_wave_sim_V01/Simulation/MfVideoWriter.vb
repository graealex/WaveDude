' Wave Dude 2D - interactive 2D wave simulator
' Copyright (C) 2026 JJM Vleggaar (Huygens Optics)
' SPDX-License-Identifier: GPL-3.0-or-later
'
' This file is part of Wave Dude 2D.
'
' Wave Dude 2D is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' Wave Dude 2D is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
' GNU General Public License for more details.
'
' You should have received a copy of the GNU General Public License
' along with Wave Dude 2D. If not, see <https://www.gnu.org/licenses/>.

Option Strict On
Option Explicit On

Imports System.Runtime.InteropServices

' ---------------------------------------------------------------------------------------------------------------
' The few Media Foundation interfaces that MfVideoWriter needs. Only the order of the methods matters (it is the
' order of the entries in the COM method table); the methods that are not used are placeholders. Every interface
' repeats the methods of IMFAttributes because .NET does not inherit them in COM interfaces.
' ---------------------------------------------------------------------------------------------------------------

<ComImport(), Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
Friend Interface IMFAttributesNative
    <PreserveSig> Function GetItem() As Integer
    <PreserveSig> Function GetItemType() As Integer
    <PreserveSig> Function CompareItem() As Integer
    <PreserveSig> Function Compare() As Integer
    <PreserveSig> Function GetUINT32() As Integer
    <PreserveSig> Function GetUINT64() As Integer
    <PreserveSig> Function GetDouble() As Integer
    <PreserveSig> Function GetGUID() As Integer
    <PreserveSig> Function GetStringLength() As Integer
    <PreserveSig> Function GetString() As Integer
    <PreserveSig> Function GetAllocatedString() As Integer
    <PreserveSig> Function GetBlobSize() As Integer
    <PreserveSig> Function GetBlob() As Integer
    <PreserveSig> Function GetAllocatedBlob() As Integer
    <PreserveSig> Function GetUnknown() As Integer
    <PreserveSig> Function SetItem() As Integer
    <PreserveSig> Function DeleteItem() As Integer
    <PreserveSig> Function DeleteAllItems() As Integer
    <PreserveSig> Function SetUINT32(<MarshalAs(UnmanagedType.LPStruct)> guidKey As Guid, value As Integer) As Integer
    <PreserveSig> Function SetUINT64(<MarshalAs(UnmanagedType.LPStruct)> guidKey As Guid, value As ULong) As Integer
    <PreserveSig> Function SetDouble() As Integer
    <PreserveSig> Function SetGUID(<MarshalAs(UnmanagedType.LPStruct)> guidKey As Guid, <MarshalAs(UnmanagedType.LPStruct)> value As Guid) As Integer
    <PreserveSig> Function SetString() As Integer
    <PreserveSig> Function SetBlob() As Integer
    <PreserveSig> Function SetUnknown() As Integer
    <PreserveSig> Function LockStore() As Integer
    <PreserveSig> Function UnlockStore() As Integer
    <PreserveSig> Function GetCount() As Integer
    <PreserveSig> Function GetItemByIndex() As Integer
    <PreserveSig> Function CopyAllItems() As Integer
End Interface

<ComImport(), Guid("C40A00F2-B93A-4D80-AE8C-5A1C634F58E4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
Friend Interface IMFSampleNative
    <PreserveSig> Function A01() As Integer
    <PreserveSig> Function A02() As Integer
    <PreserveSig> Function A03() As Integer
    <PreserveSig> Function A04() As Integer
    <PreserveSig> Function A05() As Integer
    <PreserveSig> Function A06() As Integer
    <PreserveSig> Function A07() As Integer
    <PreserveSig> Function A08() As Integer
    <PreserveSig> Function A09() As Integer
    <PreserveSig> Function A10() As Integer
    <PreserveSig> Function A11() As Integer
    <PreserveSig> Function A12() As Integer
    <PreserveSig> Function A13() As Integer
    <PreserveSig> Function A14() As Integer
    <PreserveSig> Function A15() As Integer
    <PreserveSig> Function A16() As Integer
    <PreserveSig> Function A17() As Integer
    <PreserveSig> Function A18() As Integer
    <PreserveSig> Function A19() As Integer
    <PreserveSig> Function A20() As Integer
    <PreserveSig> Function A21() As Integer
    <PreserveSig> Function A22() As Integer
    <PreserveSig> Function A23() As Integer
    <PreserveSig> Function A24() As Integer
    <PreserveSig> Function A25() As Integer
    <PreserveSig> Function A26() As Integer
    <PreserveSig> Function A27() As Integer
    <PreserveSig> Function A28() As Integer
    <PreserveSig> Function A29() As Integer
    <PreserveSig> Function A30() As Integer
    <PreserveSig> Function GetSampleFlags() As Integer
    <PreserveSig> Function SetSampleFlags() As Integer
    <PreserveSig> Function GetSampleTime() As Integer
    <PreserveSig> Function SetSampleTime(sampleTime As Long) As Integer
    <PreserveSig> Function GetSampleDuration() As Integer
    <PreserveSig> Function SetSampleDuration(duration As Long) As Integer
    <PreserveSig> Function GetBufferCount() As Integer
    <PreserveSig> Function GetBufferByIndex() As Integer
    <PreserveSig> Function ConvertToContiguousBuffer() As Integer
    <PreserveSig> Function AddBuffer(buffer As IntPtr) As Integer
    <PreserveSig> Function RemoveBufferByIndex() As Integer
    <PreserveSig> Function RemoveAllBuffers() As Integer
    <PreserveSig> Function GetTotalLength() As Integer
    <PreserveSig> Function CopyToBuffer() As Integer
End Interface

<ComImport(), Guid("045FA593-8799-42B8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
Friend Interface IMFMediaBufferNative
    <PreserveSig> Function Lock(ByRef data As IntPtr, ByRef maxLength As Integer, ByRef currentLength As Integer) As Integer
    <PreserveSig> Function Unlock() As Integer
    <PreserveSig> Function GetCurrentLength() As Integer
    <PreserveSig> Function SetCurrentLength(length As Integer) As Integer
    <PreserveSig> Function GetMaxLength() As Integer
End Interface

<ComImport(), Guid("3137F1CD-FE5E-4805-A5D8-FB477448CB3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
Friend Interface IMFSinkWriterNative
    <PreserveSig> Function AddStream(mediaType As IntPtr, ByRef streamIndex As Integer) As Integer
    <PreserveSig> Function SetInputMediaType(streamIndex As Integer, mediaType As IntPtr, encodingParameters As IntPtr) As Integer
    <PreserveSig> Function BeginWriting() As Integer
    <PreserveSig> Function WriteSample(streamIndex As Integer, sample As IntPtr) As Integer
    <PreserveSig> Function SendStreamTick() As Integer
    <PreserveSig> Function PlaceMarker() As Integer
    <PreserveSig> Function NotifyEndOfSegment() As Integer
    <PreserveSig> Function Flush() As Integer
    <PreserveSig> Function FinalizeWriting() As Integer
    <PreserveSig> Function GetServiceForStream() As Integer
    <PreserveSig> Function GetStatistics() As Integer
End Interface

''' <summary>
''' Writes an H.264 video in an MP4 file with the Media Foundation of Windows (the encoder that is built into Windows),
''' so no codec or other program is needed and every Windows player opens the file.
''' The constructor throws when Media Foundation or its H.264 encoder is not available or the size is not supported;
''' the caller can then use another writer.
''' </summary>
Friend NotInheritable Class MfVideoWriter
    Implements IFrameSink

    ' the version of Media Foundation that is asked for (MF_VERSION of the Windows 7 and later SDK)
    Private Const MfVersion As Integer = &H20070

    Private Shared ReadOnly MajorType As New Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f")
    Private Shared ReadOnly SubType As New Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5")
    Private Shared ReadOnly AvgBitrate As New Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e")
    Private Shared ReadOnly InterlaceMode As New Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd")
    Private Shared ReadOnly FrameSize As New Guid("1652c33d-d6b2-4012-b834-72030849a37d")
    Private Shared ReadOnly FrameRate As New Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0")
    Private Shared ReadOnly PixelAspectRatio As New Guid("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6")
    Private Shared ReadOnly DefaultStride As New Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6")
    Private Shared ReadOnly VideoType As New Guid("73646976-0000-0010-8000-00AA00389B71")
    Private Shared ReadOnly H264Format As New Guid("34363248-0000-0010-8000-00AA00389B71")
    Private Shared ReadOnly Rgb32Format As New Guid("00000016-0000-0010-8000-00AA00389B71")

    <DllImport("mfplat.dll", ExactSpelling:=True)>
    Private Shared Function MFStartup(version As Integer, flags As Integer) As Integer
    End Function

    <DllImport("mfplat.dll", ExactSpelling:=True)>
    Private Shared Function MFShutdown() As Integer
    End Function

    <DllImport("mfplat.dll", ExactSpelling:=True)>
    Private Shared Function MFCreateMediaType(ByRef mediaType As IntPtr) As Integer
    End Function

    <DllImport("mfplat.dll", ExactSpelling:=True)>
    Private Shared Function MFCreateSample(ByRef sample As IntPtr) As Integer
    End Function

    <DllImport("mfplat.dll", ExactSpelling:=True)>
    Private Shared Function MFCreateMemoryBuffer(maxLength As Integer, ByRef buffer As IntPtr) As Integer
    End Function

    <DllImport("mfreadwrite.dll", ExactSpelling:=True, CharSet:=CharSet.Unicode)>
    Private Shared Function MFCreateSinkWriterFromURL(url As String, byteStream As IntPtr, attributes As IntPtr, ByRef writer As IntPtr) As Integer
    End Function

    Private ReadOnly _path As String
    Private ReadOnly _srcWidth As Integer
    Private ReadOnly _srcHeight As Integer
    Private ReadOnly _width As Integer          ' the encoded size: H.264 needs even sizes
    Private ReadOnly _height As Integer
    Private ReadOnly _fps As Integer
    Private ReadOnly _cropped As Integer()

    Private _started As Boolean
    Private _writerPtr As IntPtr
    Private _writer As IMFSinkWriterNative
    Private _frames As Integer
    Private _closed As Boolean

    Public Sub New(filePath As String, width As Integer, height As Integer, framesPerSecond As Integer, quality As Integer)
        _path = filePath
        _srcWidth = width
        _srcHeight = height
        _width = width And Not 1
        _height = height And Not 1
        _fps = framesPerSecond
        If _width < 16 OrElse _height < 16 Then Throw New NotSupportedException("The canvas is too small for H.264 video.")
        If CLng(_width) * _height > 4096L * 2304L Then Throw New NotSupportedException("The canvas is too large for the H.264 encoder of Windows.")
        If _width <> _srcWidth OrElse _height <> _srcHeight Then _cropped = New Integer(_width * _height - 1) {}

        Check(MFStartup(MfVersion, 0))
        _started = True
        Try
            Check(MFCreateSinkWriterFromURL(filePath, IntPtr.Zero, IntPtr.Zero, _writerPtr))
            _writer = DirectCast(Marshal.GetObjectForIUnknown(_writerPtr), IMFSinkWriterNative)

            ' the quality (1 to 100) sets the bits per pixel of every frame: 0.01 * 50 ^ (quality / 100),
            ' which is 0.01 at quality 1, 0.15 at 70 and 0.5 at 100 (1920 x 1080 at 60 frames/s: 1 to 62 Mbit/s)
            Dim q As Integer = Math.Max(1, Math.Min(100, quality))
            Dim bitsPerPixel As Double = 0.01 * Math.Pow(50.0, q / 100.0)
            Dim bitrate As Long = CLng(_width * CDbl(_height) * _fps * bitsPerPixel)
            bitrate = Math.Max(300000L, Math.Min(200000000L, bitrate))

            Dim outputType As IntPtr = MakeType(H264Format, CInt(bitrate), False)
            Dim streamIndex As Integer = 0
            Try
                Check(_writer.AddStream(outputType, streamIndex))
            Finally
                Marshal.Release(outputType)
            End Try

            ' the pictures are given as 32 bit pixels; the sink writer converts them for the encoder
            Dim inputType As IntPtr = MakeType(Rgb32Format, 0, True)
            Try
                Check(_writer.SetInputMediaType(streamIndex, inputType, IntPtr.Zero))
            Finally
                Marshal.Release(inputType)
            End Try

            Check(_writer.BeginWriting())
        Catch
            Cleanup()
            Throw
        End Try
    End Sub

    Public ReadOnly Property FilePath As String Implements IFrameSink.FilePath
        Get
            Return _path
        End Get
    End Property

    ''' <summary>Makes a video media type (an IMFMediaType) and returns its interface pointer. The caller releases it.</summary>
    Private Function MakeType(format As Guid, bitrate As Integer, topDown As Boolean) As IntPtr
        Dim ptr As IntPtr = IntPtr.Zero
        Check(MFCreateMediaType(ptr))
        Try
            Dim a As IMFAttributesNative = DirectCast(Marshal.GetObjectForIUnknown(ptr), IMFAttributesNative)
            Try
                Check(a.SetGUID(MajorType, VideoType))
                Check(a.SetGUID(SubType, format))
                If bitrate > 0 Then Check(a.SetUINT32(AvgBitrate, bitrate))
                Check(a.SetUINT32(InterlaceMode, 2))                                  ' progressive
                Check(a.SetUINT64(FrameSize, (CULng(_width) << 32) Or CULng(_height)))
                Check(a.SetUINT64(FrameRate, (CULng(_fps) << 32) Or 1UL))
                Check(a.SetUINT64(PixelAspectRatio, (1UL << 32) Or 1UL))
                If topDown Then Check(a.SetUINT32(DefaultStride, _width * 4))         ' positive stride: the first row is the top row
            Finally
                Marshal.ReleaseComObject(a)
            End Try
        Catch
            Marshal.Release(ptr)
            Throw
        End Try
        Return ptr
    End Function

    Public Sub AddPixels(pixels As Integer()) Implements IFrameSink.AddPixels
        If _closed Then Throw New InvalidOperationException("The video file is already closed.")

        Dim source As Integer() = pixels
        If _cropped IsNot Nothing Then
            For y As Integer = 0 To _height - 1
                Array.Copy(pixels, y * _srcWidth, _cropped, y * _width, _width)
            Next
            source = _cropped
        End If

        Dim byteCount As Integer = _width * _height * 4
        Dim bufferPtr As IntPtr = IntPtr.Zero
        Dim samplePtr As IntPtr = IntPtr.Zero
        Dim buffer As IMFMediaBufferNative = Nothing
        Dim sample As IMFSampleNative = Nothing
        Try
            Check(MFCreateMemoryBuffer(byteCount, bufferPtr))
            buffer = DirectCast(Marshal.GetObjectForIUnknown(bufferPtr), IMFMediaBufferNative)

            Dim data As IntPtr = IntPtr.Zero
            Dim maxLength As Integer = 0
            Dim currentLength As Integer = 0
            Check(buffer.Lock(data, maxLength, currentLength))
            Try
                Marshal.Copy(source, 0, data, _width * _height)
            Finally
                buffer.Unlock()
            End Try
            Check(buffer.SetCurrentLength(byteCount))

            Check(MFCreateSample(samplePtr))
            sample = DirectCast(Marshal.GetObjectForIUnknown(samplePtr), IMFSampleNative)
            Check(sample.AddBuffer(bufferPtr))

            ' times are in units of 100 nanoseconds
            Check(sample.SetSampleTime(CLng(_frames) * 10000000L \ _fps))
            Check(sample.SetSampleDuration(10000000L \ _fps))

            Check(_writer.WriteSample(0, samplePtr))
            _frames += 1
        Finally
            If sample IsNot Nothing Then Marshal.ReleaseComObject(sample)
            If samplePtr <> IntPtr.Zero Then Marshal.Release(samplePtr)
            If buffer IsNot Nothing Then Marshal.ReleaseComObject(buffer)
            If bufferPtr <> IntPtr.Zero Then Marshal.Release(bufferPtr)
        End Try
    End Sub

    Public Sub Close() Implements IFrameSink.Close
        If _closed Then Return
        _closed = True
        Try
            If _writer IsNot Nothing AndAlso _frames > 0 Then Check(_writer.FinalizeWriting())
        Finally
            Cleanup()
        End Try
    End Sub

    Private Sub Cleanup()
        If _writer IsNot Nothing Then
            Marshal.ReleaseComObject(_writer)
            _writer = Nothing
        End If
        If _writerPtr <> IntPtr.Zero Then
            Marshal.Release(_writerPtr)
            _writerPtr = IntPtr.Zero
        End If
        If _started Then
            _started = False
            MFShutdown()
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Close()
    End Sub

    Private Shared Sub Check(hr As Integer)
        If hr < 0 Then Marshal.ThrowExceptionForHR(hr)
    End Sub

End Class
