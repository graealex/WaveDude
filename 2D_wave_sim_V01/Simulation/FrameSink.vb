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

Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO

''' <summary>A video file that receives rendered frames (packed ARGB pixels, row by row).</summary>
Friend Interface IFrameSink
    Inherits IDisposable

    ''' <summary>Adds one frame of the size the sink was opened with.</summary>
    Sub AddPixels(pixels As Integer())

    ''' <summary>Finishes the file so that it can be played.</summary>
    Sub Close()

    ''' <summary>The full path of the file.</summary>
    ReadOnly Property FilePath As String
End Interface

''' <summary>
''' Video as an AVI file with Motion JPEG frames (see AviMjpegWriter). Needs no codec on this computer,
''' but Windows' own players do not play it; VLC does.
''' </summary>
Friend NotInheritable Class AviFrameSink
    Implements IFrameSink

    Private Shared ReadOnly JpegCodec As ImageCodecInfo = FindJpegCodec()

    Private ReadOnly _jpegQuality As Long
    Private ReadOnly _path As String
    Private ReadOnly _writer As AviMjpegWriter
    Private ReadOnly _width As Integer
    Private ReadOnly _height As Integer
    Private ReadOnly _buffer As New MemoryStream()

    Public Sub New(filePath As String, width As Integer, height As Integer, framesPerSecond As Integer, quality As Integer)
        _jpegQuality = Math.Max(1, Math.Min(100, quality))
        _path = filePath
        _width = width
        _height = height
        _writer = New AviMjpegWriter(filePath, width, height, framesPerSecond)
    End Sub

    Public ReadOnly Property FilePath As String Implements IFrameSink.FilePath
        Get
            Return _path
        End Get
    End Property

    Public Sub AddPixels(pixels As Integer()) Implements IFrameSink.AddPixels
        Using bmp As Bitmap = FrameRenderer.ToBitmap(pixels, _width, _height)
            _buffer.SetLength(0)
            Using parameters As New EncoderParameters(1)
                parameters.Param(0) = New EncoderParameter(System.Drawing.Imaging.Encoder.Quality, _jpegQuality)
                bmp.Save(_buffer, JpegCodec, parameters)
            End Using
            _writer.AddFrame(_buffer.GetBuffer(), CInt(_buffer.Length))
        End Using
    End Sub

    Public Sub Close() Implements IFrameSink.Close
        _writer.Close()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        _writer.Close()
        _buffer.Dispose()
    End Sub

    Private Shared Function FindJpegCodec() As ImageCodecInfo
        For Each codec As ImageCodecInfo In ImageCodecInfo.GetImageEncoders()
            If codec.FormatID = ImageFormat.Jpeg.Guid Then Return codec
        Next
        Throw New InvalidOperationException("The JPEG encoder is not available on this computer.")
    End Function

End Class
