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

Imports System.Drawing

''' <summary>
''' A ready-to-use lookup table (255 colors) made from a color scheme plus the display options of the
''' Python function get_colormap_lut: invert, black level and make symmetric. Also converts field and
''' intensity values to table indices exactly like WaveVisualizer.render_field / render_intensity.
'''
''' The options belong to how a scheme is used in a simulation (field or intensity), not to the scheme
''' itself, so they are not saved in the scheme file.
''' </summary>
Public NotInheritable Class ColorLut

    ''' <summary>Number of entries (255).</summary>
    Public Const Size As Integer = ColorScheme.EntryCount

    Private ReadOnly _argb As Integer()

    Private Sub New(argb As Integer())
        _argb = argb
    End Sub

    ''' <summary>
    ''' Packed 32-bit ARGB values (alpha 255), ready to be copied into a Format32bppArgb bitmap.
    ''' Treat as read-only.
    ''' </summary>
    Public ReadOnly Property Argb As Integer()
        Get
            Return _argb
        End Get
    End Property

    Public Function ColorAt(index As Integer) As Color
        Return Color.FromArgb(_argb(index))
    End Function

    ''' <summary>
    ''' Builds the table. Order of operations, as in Python: reverse (new, optional), invert (1 - color),
    ''' make symmetric, black level (c * (1 - b) + b). Values are limited to 0..1 and converted to 8 bit
    ''' by truncation, like the Python code.
    ''' </summary>
    ''' <param name="invert">Replaces every color by its complement (1 - color), like the Python option.</param>
    ''' <param name="blackLevel">Lifts (positive) or lowers (negative) the darkest level. 0 = unchanged.</param>
    ''' <param name="makeSymmetric">Squeezes the whole scheme into each half and mirrors it around the centre.</param>
    ''' <param name="reverse">Reverses the order of the scheme (like matplotlib's _r maps).</param>
    Public Shared Function Build(scheme As ColorScheme,
                                 Optional invert As Boolean = False,
                                 Optional blackLevel As Double = 0.0,
                                 Optional makeSymmetric As Boolean = False,
                                 Optional reverse As Boolean = False) As ColorLut
        If scheme Is Nothing Then Throw New ArgumentNullException("scheme")
        Dim n As Integer = Size

        ' source values 0..1
        Dim src(n - 1, 2) As Double
        For i As Integer = 0 To n - 1
            Dim c As Color = scheme.GetEntry(If(reverse, n - 1 - i, i))
            src(i, 0) = c.R / 255.0
            src(i, 1) = c.G / 255.0
            src(i, 2) = c.B / 255.0
        Next

        If invert Then
            For i As Integer = 0 To n - 1
                For k As Integer = 0 To 2
                    src(i, k) = 1.0 - src(i, k)
                Next
            Next
        End If

        Dim argb(n - 1) As Integer
        For i As Integer = 0 To n - 1
            ' Python: entries 0..127 = src[0::2] and entries 127..254 = the same list mirrored,
            ' which is entry i = src(2 * min(i, 254 - i))
            Dim s As Integer = i
            If makeSymmetric Then s = 2 * Math.Min(i, n - 1 - i)

            Dim rgb(2) As Integer
            For k As Integer = 0 To 2
                Dim v As Double = src(s, k) * (1.0 - blackLevel) + blackLevel
                If v < 0.0 Then v = 0.0
                If v > 1.0 Then v = 1.0
                Dim b As Integer = CInt(Math.Truncate(v * 255.0))
                If b > 255 Then b = 255
                rgb(k) = b
            Next
            argb(i) = Color.FromArgb(255, rgb(0), rgb(1), rgb(2)).ToArgb()
        Next

        Return New ColorLut(argb)
    End Function

    ''' <summary>
    ''' Table index for a field value, like render_field: clip(value * brightness, -1, 1) * 127 + 127.
    ''' Zero field gives index 127 (the centre color); result is 0..254.
    ''' </summary>
    Public Shared Function FieldIndex(value As Single, brightness As Single) As Integer
        Dim x As Single = value * brightness
        If Single.IsNaN(x) Then Return 127
        If x > 1.0F Then x = 1.0F
        If x < -1.0F Then x = -1.0F
        Return CInt(Math.Truncate(x * 127.0F + 127.0F))
    End Function

    ''' <summary>
    ''' Table index for an intensity value, like render_intensity:
    ''' clip(intensity ^ exponent * brightness, 0, 1) * 254. The Python default exponent is 0.5.
    ''' </summary>
    Public Shared Function IntensityIndex(intensity As Single, brightness As Single, Optional exponent As Single = 0.5F) As Integer
        If Single.IsNaN(intensity) OrElse intensity <= 0.0F Then Return 0
        Dim x As Double = Math.Pow(intensity, exponent) * brightness
        If Double.IsNaN(x) OrElse x < 0.0 Then Return 0
        If x > 1.0 Then x = 1.0
        Return CInt(Math.Truncate(x * 254.0))
    End Function

End Class
