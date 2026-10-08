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

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

''' <summary>
''' A small color picker: a saturation/brightness square, a hue bar, a new/old swatch, a hex box and a
''' button that opens the standard Windows color dialog. Raises ColorChanged while the user picks.
''' It is built completely in code, so it has no designer file.
''' </summary>
<DesignerCategory("Code")>
Public Class ColorPickerControl
    Inherits UserControl

    ''' <summary>Raised whenever the user changes the color (dragging, typing a hex code, dialog, old swatch).</summary>
    Public Event ColorChanged As EventHandler

    Private Const SvResolution As Integer = 150

    Private ReadOnly _svRect As New Rectangle(10, 10, 300, 300)
    Private ReadOnly _hueRect As New Rectangle(326, 10, 28, 300)
    Private ReadOnly _newRect As New Rectangle(10, 324, 150, 36)
    Private ReadOnly _oldRect As New Rectangle(160, 324, 150, 36)

    Private ReadOnly _lblHex As New Label()
    Private ReadOnly _txtHex As New TextBox()
    Private ReadOnly _lblRgb As New Label()
    Private ReadOnly _btnDialog As New Button()

    Private _h As Double            ' hue 0..360
    Private _s As Double            ' saturation 0..1
    Private _v As Double            ' brightness 0..1
    Private _color As Color = Color.Red
    Private _original As Color = Color.Gray

    Private _svBitmap As Bitmap
    Private _svHue As Double = -1.0
    Private _hueBitmap As Bitmap
    Private _drag As Integer        ' 0 = none, 1 = square, 2 = hue bar
    Private _updatingText As Boolean

    Public Sub New()
        MyBase.New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Me.Size = New Size(423, 411)

        _lblHex.Text = "Hex"
        _lblHex.AutoSize = True
        _lblHex.Location = New Point(10, 377)

        _txtHex.Location = New Point(48, 373)
        _txtHex.Width = 90
        _txtHex.MaxLength = 7

        _lblRgb.AutoSize = True
        _lblRgb.Location = New Point(152, 377)

        _btnDialog.Text = "More..."
        _btnDialog.Location = New Point(326, 324)
        _btnDialog.Size = New Size(90, 36)

        Me.Controls.Add(_lblHex)
        Me.Controls.Add(_txtHex)
        Me.Controls.Add(_lblRgb)
        Me.Controls.Add(_btnDialog)

        AddHandler _txtHex.TextChanged, AddressOf HexTextChanged
        AddHandler _btnDialog.Click, AddressOf DialogButtonClick

        ApplyColor(Color.Red, False, False)
    End Sub

    ' ---------------------------------------------------------------- public

    ''' <summary>The current color. Setting it does not raise ColorChanged.</summary>
    Public Property SelectedColor As Color
        Get
            Return _color
        End Get
        Set(value As Color)
            ApplyColor(value, False, False)
        End Set
    End Property

    ''' <summary>The color shown in the "Old" swatch; clicking that swatch restores it.</summary>
    Public Property OriginalColor As Color
        Get
            Return _original
        End Get
        Set(value As Color)
            _original = Color.FromArgb(value.R, value.G, value.B)
            Invalidate()
        End Set
    End Property

    ' ---------------------------------------------------------------- color logic

    Private Sub ApplyColor(c As Color, raiseChanged As Boolean, fromHexBox As Boolean)
        _color = Color.FromArgb(c.R, c.G, c.B)

        Dim hh, ss, vv As Double
        RgbToHsv(_color, hh, ss, vv)
        If ss > 0.0 AndAlso vv > 0.0 Then _h = hh   ' keep the old hue for grays and black
        _s = ss
        _v = vv

        UpdateTexts(fromHexBox)
        Invalidate()
        If raiseChanged Then RaiseEvent ColorChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub SyncFromHsv()
        _color = HsvToColor(_h, _s, _v)
        UpdateTexts(False)
        Invalidate()
        RaiseEvent ColorChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub UpdateTexts(skipHexBox As Boolean)
        _updatingText = True
        Try
            If Not skipHexBox Then
                _txtHex.Text = "#" & _color.R.ToString("X2", CultureInfo.InvariantCulture) &
                               _color.G.ToString("X2", CultureInfo.InvariantCulture) &
                               _color.B.ToString("X2", CultureInfo.InvariantCulture)
            End If
            _lblRgb.Text = String.Format(CultureInfo.InvariantCulture, "R {0}   G {1}   B {2}", _color.R, _color.G, _color.B)
        Finally
            _updatingText = False
        End Try
    End Sub

    Private Sub HexTextChanged(sender As Object, e As EventArgs)
        If _updatingText Then Return
        Dim t As String = _txtHex.Text.Trim().TrimStart("#"c)
        If t.Length <> 6 Then Return
        Dim rgb As Integer
        If Integer.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, rgb) Then
            ApplyColor(Color.FromArgb((rgb >> 16) And &HFF, (rgb >> 8) And &HFF, rgb And &HFF), True, True)
        End If
    End Sub

    Private Sub DialogButtonClick(sender As Object, e As EventArgs)
        Using dlg As New ColorDialog()
            dlg.FullOpen = True
            dlg.Color = _color
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                ApplyColor(dlg.Color, True, False)
            End If
        End Using
    End Sub

    ' ---------------------------------------------------------------- mouse

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left Then Return
        If _svRect.Contains(e.Location) Then
            _drag = 1
            UpdateFromMouse(e.Location)
        ElseIf _hueRect.Contains(e.Location) Then
            _drag = 2
            UpdateFromMouse(e.Location)
        ElseIf _oldRect.Contains(e.Location) Then
            ApplyColor(_original, True, False)
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _drag <> 0 Then UpdateFromMouse(e.Location)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _drag = 0
    End Sub

    Private Sub UpdateFromMouse(p As Point)
        If _drag = 1 Then
            _s = Clamp01((p.X - _svRect.X) / CDbl(_svRect.Width - 1))
            _v = 1.0 - Clamp01((p.Y - _svRect.Y) / CDbl(_svRect.Height - 1))
        ElseIf _drag = 2 Then
            _h = Clamp01((p.Y - _hueRect.Y) / CDbl(_hueRect.Height - 1)) * 360.0
        Else
            Return
        End If
        SyncFromHsv()
    End Sub

    ' ---------------------------------------------------------------- painting

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g As Graphics = e.Graphics
        g.Clear(Me.BackColor)

        ' saturation / brightness square
        EnsureSvBitmap()
        g.InterpolationMode = InterpolationMode.HighQualityBilinear
        g.PixelOffsetMode = PixelOffsetMode.Half
        g.DrawImage(_svBitmap, _svRect)
        g.DrawRectangle(Pens.Black, _svRect.X - 1, _svRect.Y - 1, _svRect.Width + 1, _svRect.Height + 1)

        ' hue bar
        EnsureHueBitmap()
        g.InterpolationMode = InterpolationMode.NearestNeighbor
        g.DrawImage(_hueBitmap, _hueRect)
        g.DrawRectangle(Pens.Black, _hueRect.X - 1, _hueRect.Y - 1, _hueRect.Width + 1, _hueRect.Height + 1)

        g.SmoothingMode = SmoothingMode.AntiAlias

        ' marker in the square
        Dim mx As Single = CSng(_svRect.X + _s * (_svRect.Width - 1))
        Dim my As Single = CSng(_svRect.Y + (1.0 - _v) * (_svRect.Height - 1))
        g.DrawEllipse(Pens.Black, mx - 7.0F, my - 7.0F, 14.0F, 14.0F)
        g.DrawEllipse(Pens.White, mx - 6.0F, my - 6.0F, 12.0F, 12.0F)

        ' marker on the hue bar
        Dim hy As Single = CSng(_hueRect.Y + (_h / 360.0) * (_hueRect.Height - 1))
        g.DrawRectangle(Pens.Black, _hueRect.X - 3, hy - 3.0F, _hueRect.Width + 5, 6.0F)
        g.DrawRectangle(Pens.White, _hueRect.X - 2, hy - 2.0F, _hueRect.Width + 3, 4.0F)

        g.SmoothingMode = SmoothingMode.Default

        ' swatches
        Using br As New SolidBrush(_color)
            g.FillRectangle(br, _newRect)
        End Using
        Using br As New SolidBrush(_original)
            g.FillRectangle(br, _oldRect)
        End Using
        g.DrawRectangle(Pens.Black, _newRect.X, _newRect.Y, _newRect.Width - 1, _newRect.Height - 1)
        g.DrawRectangle(Pens.Black, _oldRect.X, _oldRect.Y, _oldRect.Width - 1, _oldRect.Height - 1)
        TextRenderer.DrawText(g, "New", Me.Font, _newRect, ContrastColor(_color),
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
        TextRenderer.DrawText(g, "Old (click to restore)", Me.Font, _oldRect, ContrastColor(_original),
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)

        If Not Me.Enabled Then
            Using veil As New SolidBrush(Color.FromArgb(150, Me.BackColor))
                g.FillRectangle(veil, Me.ClientRectangle)
            End Using
        End If
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        Invalidate()
    End Sub

    Private Sub EnsureSvBitmap()
        If _svBitmap IsNot Nothing AndAlso Math.Abs(_svHue - _h) < 0.0001 Then Return
        If _svBitmap Is Nothing Then
            _svBitmap = New Bitmap(SvResolution, SvResolution, PixelFormat.Format32bppArgb)
        End If

        Dim baseColor As Color = HsvToColor(_h, 1.0, 1.0)
        Dim br As Double = baseColor.R / 255.0
        Dim bg As Double = baseColor.G / 255.0
        Dim bb As Double = baseColor.B / 255.0

        Dim pixels(SvResolution * SvResolution - 1) As Integer
        For y As Integer = 0 To SvResolution - 1
            Dim v As Double = 1.0 - y / CDbl(SvResolution - 1)
            For x As Integer = 0 To SvResolution - 1
                Dim s As Double = x / CDbl(SvResolution - 1)
                Dim r As Integer = CInt(Math.Round(255.0 * v * ((1.0 - s) + s * br)))
                Dim gg As Integer = CInt(Math.Round(255.0 * v * ((1.0 - s) + s * bg)))
                Dim b As Integer = CInt(Math.Round(255.0 * v * ((1.0 - s) + s * bb)))
                pixels(y * SvResolution + x) = Color.FromArgb(255, r, gg, b).ToArgb()
            Next
        Next

        Dim data As BitmapData = _svBitmap.LockBits(New Rectangle(0, 0, SvResolution, SvResolution),
                                                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)
        Finally
            _svBitmap.UnlockBits(data)
        End Try
        _svHue = _h
    End Sub

    Private Sub EnsureHueBitmap()
        If _hueBitmap IsNot Nothing Then Return
        _hueBitmap = New Bitmap(1, _hueRect.Height, PixelFormat.Format32bppArgb)
        For y As Integer = 0 To _hueRect.Height - 1
            _hueBitmap.SetPixel(0, y, HsvToColor(y / CDbl(_hueRect.Height - 1) * 360.0, 1.0, 1.0))
        Next
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If _svBitmap IsNot Nothing Then _svBitmap.Dispose()
            If _hueBitmap IsNot Nothing Then _hueBitmap.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    ' ---------------------------------------------------------------- helpers

    Private Shared Function Clamp01(x As Double) As Double
        If x < 0.0 Then Return 0.0
        If x > 1.0 Then Return 1.0
        Return x
    End Function

    Private Shared Function ContrastColor(c As Color) As Color
        Dim lum As Double = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B
        If lum > 140.0 Then Return Color.Black
        Return Color.White
    End Function

    ''' <summary>Converts hue (0..360), saturation and value (0..1) to an RGB color.</summary>
    Public Shared Function HsvToColor(h As Double, s As Double, v As Double) As Color
        Dim hue As Double = h Mod 360.0
        If hue < 0.0 Then hue += 360.0
        Dim c As Double = v * s
        Dim hp As Double = hue / 60.0
        Dim x As Double = c * (1.0 - Math.Abs((hp Mod 2.0) - 1.0))

        Dim r1, g1, b1 As Double
        Select Case CInt(Math.Floor(hp))
            Case 0
                r1 = c : g1 = x : b1 = 0.0
            Case 1
                r1 = x : g1 = c : b1 = 0.0
            Case 2
                r1 = 0.0 : g1 = c : b1 = x
            Case 3
                r1 = 0.0 : g1 = x : b1 = c
            Case 4
                r1 = x : g1 = 0.0 : b1 = c
            Case Else
                r1 = c : g1 = 0.0 : b1 = x
        End Select

        Dim m As Double = v - c
        Return Color.FromArgb(ToByte(r1 + m), ToByte(g1 + m), ToByte(b1 + m))
    End Function

    Private Shared Function ToByte(v As Double) As Integer
        Dim n As Integer = CInt(Math.Round(v * 255.0))
        If n < 0 Then n = 0
        If n > 255 Then n = 255
        Return n
    End Function

    Private Shared Sub RgbToHsv(c As Color, ByRef h As Double, ByRef s As Double, ByRef v As Double)
        Dim r As Double = c.R / 255.0
        Dim g As Double = c.G / 255.0
        Dim b As Double = c.B / 255.0
        Dim mx As Double = Math.Max(r, Math.Max(g, b))
        Dim mn As Double = Math.Min(r, Math.Min(g, b))
        Dim d As Double = mx - mn

        v = mx
        If mx <= 0.0 Then
            s = 0.0
        Else
            s = d / mx
        End If

        If d <= 0.0 Then
            h = 0.0
        ElseIf mx = r Then
            h = 60.0 * (((g - b) / d) Mod 6.0)
        ElseIf mx = g Then
            h = 60.0 * (((b - r) / d) + 2.0)
        Else
            h = 60.0 * (((r - g) / d) + 4.0)
        End If
        If h < 0.0 Then h += 360.0
    End Sub

End Class
