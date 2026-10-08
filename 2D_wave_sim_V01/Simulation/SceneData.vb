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
'
' Portions of this file are based on WaveSimulator2D by 0x23
' <https://github.com/0x23/WaveSimulator2D>.

Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

''' <summary>
''' The per-pixel description of a scene that the wave simulation needs, all row by row (index = y * Width + x):
'''   C2   the squared wave speed (1 / refractive index) ^ 2 (the time step is 1)
'''   D    the damping factor of the velocity, 1 = no damping, 0 = the wave is stopped
'''   sources: pixel positions and the value of the green channel, which sets the frequency
'''
''' The rules are those of the Python classes StaticImageScene, StaticRefractiveIndex and StaticDampening:
'''   red channel     refractive index times 100 (values are limited to 0.9 to 10)
'''   green channel   every pixel above 0 is a sinusoidal source; the value sets its frequency
'''   blue channel    damping: the damping factor is 1 - blue / 255
'''   damping edge    a border of the given thickness, damping factor sqrt(i / thickness) at i pixels from the edge
''' </summary>
Friend NotInheritable Class SceneData

    Public ReadOnly Width As Integer
    Public ReadOnly Height As Integer
    Public ReadOnly C2 As Single()
    Public ReadOnly D As Single()
    Public ReadOnly SourceX As Integer()
    Public ReadOnly SourceY As Integer()
    Public ReadOnly SourceLevel As Byte()

    Private Sub New(w As Integer, h As Integer, c2 As Single(), d As Single(),
                    srcX As Integer(), srcY As Integer(), srcLevel As Byte())
        Width = w
        Height = h
        Me.C2 = c2
        Me.D = d
        SourceX = srcX
        SourceY = srcY
        SourceLevel = srcLevel
    End Sub

    Public ReadOnly Property SourceCount As Integer
        Get
            Return SourceX.Length
        End Get
    End Property

    ''' <summary>A scene from an image (red = refractive index, green = sources, blue = damping).</summary>
    Public Shared Function FromImage(image As Bitmap, useSources As Boolean, dampingEdge As Integer) As SceneData
        Dim w As Integer = image.Width
        Dim h As Integer = image.Height
        Dim c2(w * h - 1) As Single
        Dim d(w * h - 1) As Single
        Dim sx As New List(Of Integer)()
        Dim sy As New List(Of Integer)()
        Dim sl As New List(Of Byte)()

        Dim data As BitmapData = image.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim row(w * 4 - 1) As Byte
            For y As Integer = 0 To h - 1
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, w * 4)
                For x As Integer = 0 To w - 1
                    Dim o As Integer = x * 4
                    ' Format32bppArgb is stored as B, G, R, A
                    Dim blue As Integer = row(o)
                    Dim green As Integer = row(o + 1)
                    Dim red As Integer = row(o + 2)

                    Dim n As Double = red / 100.0
                    If n < 0.9 Then n = 0.9
                    If n > 10.0 Then n = 10.0
                    Dim c As Double = 1.0 / n

                    Dim idx As Integer = y * w + x
                    c2(idx) = CSng(c * c)
                    d(idx) = CSng(1.0 - blue / 255.0)

                    If useSources AndAlso green > 0 Then
                        sx.Add(x)
                        sy.Add(y)
                        sl.Add(CByte(green))
                    End If
                Next
            Next
        Finally
            image.UnlockBits(data)
        End Try

        ApplyBorder(d, w, h, dampingEdge)
        Return New SceneData(w, h, c2, d, sx.ToArray(), sy.ToArray(), sl.ToArray())
    End Function

    ''' <summary>
    ''' An empty, user defined scene (no sources), optionally with a damping edge. The background gets the given
    ''' refractive index (1.0 by default, the same rule as a scene image's red channel: index = red / 100).
    ''' </summary>
    Public Shared Function Empty(w As Integer, h As Integer, dampingEdge As Integer, Optional refractiveIndex As Double = 1.0) As SceneData
        Dim n As Double = refractiveIndex
        If n < 0.9 Then n = 0.9
        If n > 10.0 Then n = 10.0
        Dim c As Single = CSng(1.0 / n)

        Dim c2(w * h - 1) As Single
        Dim d(w * h - 1) As Single
        For i As Integer = 0 To c2.Length - 1
            c2(i) = c * c
            d(i) = 1.0F
        Next
        ApplyBorder(d, w, h, dampingEdge)
        Return New SceneData(w, h, c2, d, New Integer() {}, New Integer() {}, New Byte() {})
    End Function

    ''' <summary>The number of source pixels (green channel above 0) in a scene image.</summary>
    Public Shared Function CountSources(image As Bitmap) As Integer
        Dim w As Integer = image.Width
        Dim h As Integer = image.Height
        Dim count As Integer = 0

        Dim data As BitmapData = image.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim row(w * 4 - 1) As Byte
            For y As Integer = 0 To h - 1
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, w * 4)
                For x As Integer = 0 To w - 1
                    If row(x * 4 + 1) > 0 Then count += 1
                Next
            Next
        Finally
            image.UnlockBits(data)
        End Try
        Return count
    End Function

    ''' <summary>
    ''' Damping edge, as in StaticDampening: the ring i pixels from the edge gets the factor sqrt(i / thickness),
    ''' so the outermost ring stops the wave completely and the factor rises to 1 inside.
    ''' </summary>
    Friend Shared Sub ApplyBorder(d As Single(), w As Integer, h As Integer, thickness As Integer)
        Dim t As Integer = Math.Min(thickness, Math.Min(w, h) \ 2)
        If t <= 0 Then Return

        For i As Integer = 0 To t - 1
            Dim v As Single = CSng(Math.Sqrt(i / CDbl(t)))
            For x As Integer = i To w - 1 - i
                d(i * w + x) = v
                d((h - 1 - i) * w + x) = v
            Next
            For y As Integer = i To h - 1 - i
                d(y * w + i) = v
                d(y * w + (w - 1 - i)) = v
            Next
        Next
    End Sub

End Class
