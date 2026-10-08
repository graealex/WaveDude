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

Imports System.Collections.Generic
Imports System.IO
Imports System.Text

''' <summary>
''' Writes a video as an AVI file with Motion JPEG frames. Every frame is given as an ordinary JPEG image.
''' No codec or external program is needed. VLC and most video editors play the file; a program like
''' ffmpeg can convert it to MP4.
'''
''' File layout (all numbers little endian):
'''   RIFF 'AVI ' [ LIST 'hdrl' [ 'avih' (56)  LIST 'strl' [ 'strh' (56)  'strf' (40) ] ]
'''                 LIST 'movi' [ '00dc' frame ... ]   'idx1' index ]
''' The sizes and the frame count are written when the file is closed.
''' </summary>
Friend NotInheritable Class AviMjpegWriter
    Implements IDisposable

    ' AVI files are limited to 4 GB (unsigned 32 bit sizes); stop before that
    Private Const MaxFileSize As Long = 3900000000L

    ' positions of the numbers that are filled in at the end
    Private Const PosRiffSize As Long = 4
    Private Const PosTotalFrames As Long = 48
    Private Const PosBufferSize As Long = 60
    Private Const PosStreamLength As Long = 140
    Private Const PosStreamBufferSize As Long = 144
    Private Const PosMoviSize As Long = 216
    Private Const PosMoviTag As Long = 220

    Private _stream As FileStream
    Private _writer As BinaryWriter
    Private ReadOnly _offsets As New List(Of Integer)()
    Private ReadOnly _sizes As New List(Of Integer)()
    Private _largestFrame As Integer
    Private _closed As Boolean

    Public Sub New(filePath As String, width As Integer, height As Integer, framesPerSecond As Integer)
        _stream = New FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)
        _writer = New BinaryWriter(_stream)

        Dim ascii As Encoding = Encoding.ASCII

        _writer.Write(ascii.GetBytes("RIFF"))
        _writer.Write(0UI)                                  ' file size - 8, filled in at the end
        _writer.Write(ascii.GetBytes("AVI "))

        _writer.Write(ascii.GetBytes("LIST"))
        _writer.Write(192UI)                                ' 'hdrl' + avih chunk (64) + strl list chunk (124)
        _writer.Write(ascii.GetBytes("hdrl"))

        ' main header
        _writer.Write(ascii.GetBytes("avih"))
        _writer.Write(56UI)
        _writer.Write(CUInt(1000000 \ framesPerSecond))     ' microseconds per frame
        _writer.Write(0UI)                                  ' maximum bytes per second
        _writer.Write(0UI)                                  ' padding granularity
        _writer.Write(&H10UI)                               ' flags: has index
        _writer.Write(0UI)                                  ' total frames, filled in at the end
        _writer.Write(0UI)                                  ' initial frames
        _writer.Write(1UI)                                  ' streams
        _writer.Write(0UI)                                  ' suggested buffer size, filled in at the end
        _writer.Write(CUInt(width))
        _writer.Write(CUInt(height))
        For i As Integer = 0 To 3
            _writer.Write(0UI)                              ' reserved
        Next

        ' the video stream
        _writer.Write(ascii.GetBytes("LIST"))
        _writer.Write(116UI)                                ' 'strl' + strh chunk (64) + strf chunk (48)
        _writer.Write(ascii.GetBytes("strl"))

        _writer.Write(ascii.GetBytes("strh"))
        _writer.Write(56UI)
        _writer.Write(ascii.GetBytes("vids"))
        _writer.Write(ascii.GetBytes("MJPG"))
        _writer.Write(0UI)                                  ' flags
        _writer.Write(0US)                                  ' priority
        _writer.Write(0US)                                  ' language
        _writer.Write(0UI)                                  ' initial frames
        _writer.Write(1UI)                                  ' scale
        _writer.Write(CUInt(framesPerSecond))               ' rate: frames per second = rate / scale
        _writer.Write(0UI)                                  ' start
        _writer.Write(0UI)                                  ' length in frames, filled in at the end
        _writer.Write(0UI)                                  ' suggested buffer size, filled in at the end
        _writer.Write(&HFFFFFFFFUI)                         ' quality: default
        _writer.Write(0UI)                                  ' sample size
        _writer.Write(0US)                                  ' frame rectangle: left, top, right, bottom
        _writer.Write(0US)
        _writer.Write(CUShort(width))
        _writer.Write(CUShort(height))

        _writer.Write(ascii.GetBytes("strf"))               ' BITMAPINFOHEADER
        _writer.Write(40UI)
        _writer.Write(40UI)                                 ' structure size
        _writer.Write(width)
        _writer.Write(height)
        _writer.Write(1US)                                  ' planes
        _writer.Write(24US)                                 ' bits per pixel
        _writer.Write(ascii.GetBytes("MJPG"))
        _writer.Write(CUInt(CLng(width) * height * 3L And &HFFFFFFFFL))
        _writer.Write(0)                                    ' pixels per meter
        _writer.Write(0)
        _writer.Write(0UI)                                  ' colors used
        _writer.Write(0UI)                                  ' important colors

        _writer.Write(ascii.GetBytes("LIST"))
        _writer.Write(0UI)                                  ' size of the movie list, filled in at the end
        _writer.Write(ascii.GetBytes("movi"))
    End Sub

    ''' <summary>The number of frames written so far.</summary>
    Public ReadOnly Property FrameCount As Integer
        Get
            Return _offsets.Count
        End Get
    End Property

    ''' <summary>Adds one frame: the first length bytes of data are a complete JPEG image.</summary>
    Public Sub AddFrame(data As Byte(), length As Integer)
        If _closed Then Throw New InvalidOperationException("The video file is already closed.")

        ' room for this frame and the index (16 bytes per frame)
        If _stream.Position + length + 16L * (_offsets.Count + 2) + 64L > MaxFileSize Then
            Throw New IOException("The video file would become larger than 4 GB, which the AVI format does not allow.")
        End If

        _offsets.Add(CInt(_stream.Position - PosMoviTag))
        _sizes.Add(length)
        _writer.Write(Encoding.ASCII.GetBytes("00dc"))
        _writer.Write(CUInt(length))
        _writer.Write(data, 0, length)
        If (length And 1) = 1 Then _writer.Write(CByte(0))  ' chunks are padded to an even size
        If length > _largestFrame Then _largestFrame = length
    End Sub

    ''' <summary>Writes the index and the sizes and closes the file.</summary>
    Public Sub Close()
        If _closed Then Return
        _closed = True

        Try
            Dim moviEnd As Long = _stream.Position

            _writer.Write(Encoding.ASCII.GetBytes("idx1"))
            _writer.Write(CUInt(16 * _offsets.Count))
            For i As Integer = 0 To _offsets.Count - 1
                _writer.Write(Encoding.ASCII.GetBytes("00dc"))
                _writer.Write(&H10UI)                       ' key frame
                _writer.Write(CUInt(_offsets(i)))
                _writer.Write(CUInt(_sizes(i)))
            Next
            Dim fileEnd As Long = _stream.Position

            Patch(PosRiffSize, CUInt(fileEnd - 8L))
            Patch(PosTotalFrames, CUInt(_offsets.Count))
            Patch(PosBufferSize, CUInt(_largestFrame))
            Patch(PosStreamLength, CUInt(_offsets.Count))
            Patch(PosStreamBufferSize, CUInt(_largestFrame))
            Patch(PosMoviSize, CUInt(moviEnd - PosMoviSize))
            _writer.Flush()
        Finally
            _writer.Close()
            _stream.Dispose()
        End Try
    End Sub

    Private Sub Patch(position As Long, value As UInteger)
        _stream.Seek(position, SeekOrigin.Begin)
        _writer.Write(value)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Close()
    End Sub

End Class
