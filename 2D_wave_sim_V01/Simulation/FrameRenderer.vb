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
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Threading.Tasks

''' <summary>
''' Turns the field or the intensity of a wave engine (CPU or GPU) into pixels, like WaveVisualizer.render_field and
''' render_intensity: the value is converted to a table index (see ColorLut) and the index to a color.
''' Without a color table (Nothing) the index itself is used as gray level.
''' The picture can be smaller than the simulation (a preview): every picture pixel then takes the nearest
''' simulation pixel.
''' </summary>
Friend NotInheritable Class FrameRenderer

    Private Sub New()
    End Sub

    ''' <summary>Renders the field into pixels (packed ARGB, row by row, dw x dh).</summary>
    Public Shared Sub RenderField(sim As IWaveEngine, lut As Integer(), brightness As Single, pixels As Integer(), dw As Integer, dh As Integer)
        Dim u As Single() = sim.FieldData
        Dim stride As Integer = sim.Stride
        Dim sw As Integer = sim.Width
        Dim sh As Integer = sim.Height

        Parallel.For(0, dh,
            Sub(y As Integer)
                Dim srcRow As Integer = (CInt(CLng(y) * sh \ dh) + 1) * stride + 1
                Dim dst As Integer = y * dw
                For x As Integer = 0 To dw - 1
                    Dim v As Single = u(srcRow + CInt(CLng(x) * sw \ dw))
                    pixels(dst + x) = Colorize(ColorLut.FieldIndex(v, brightness), lut)
                Next
            End Sub)
    End Sub

    ''' <summary>Renders the intensity (exponent 0.5, as in the Python code) into pixels (packed ARGB, dw x dh).</summary>
    Public Shared Sub RenderIntensity(sim As IWaveEngine, lut As Integer(), brightness As Single, pixels As Integer(), dw As Integer, dh As Integer)
        Dim inten As Single() = sim.IntensityData
        Dim stride As Integer = sim.Stride
        Dim sw As Integer = sim.Width
        Dim sh As Integer = sim.Height

        Parallel.For(0, dh,
            Sub(y As Integer)
                Dim srcRow As Integer = (CInt(CLng(y) * sh \ dh) + 1) * stride + 1
                Dim dst As Integer = y * dw
                For x As Integer = 0 To dw - 1
                    Dim v As Single = inten(srcRow + CInt(CLng(x) * sw \ dw))
                    Dim index As Integer = 0
                    If v > 0.0F Then
                        Dim s As Double = Math.Sqrt(v) * brightness
                        If s > 1.0 Then s = 1.0
                        index = CInt(Math.Truncate(s * 254.0))
                    End If
                    pixels(dst + x) = Colorize(index, lut)
                Next
            End Sub)
    End Sub

    Private Shared Function Colorize(index As Integer, lut As Integer()) As Integer
        If lut IsNot Nothing Then Return lut(index)
        Return &HFF000000 Or (index << 16) Or (index << 8) Or index
    End Function

    ''' <summary>Reads bmp into a flat array of R,G,B byte triplets, row by row, resampled to exactly w x h (bmp is
    ''' expected to already be that size in the one place this is used - SimulationJob.GlowOverlay in
    ''' SimulationRunner - so this is just a format normalization, not a real resize). Used to additively blend
    ''' Form1's "glowing edges" preview overlay into exported video frames - see AddOverlay.</summary>
    Public Shared Function ToRgbBytes(bmp As Bitmap, w As Integer, h As Integer) As Byte()
        Dim result(w * h * 3 - 1) As Byte
        Using normalized As New Bitmap(w, h, PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(normalized)
                g.Clear(Color.Black)
                g.DrawImageUnscaled(bmp, 0, 0)
            End Using
            Dim rect As New Rectangle(0, 0, w, h)
            Dim data As BitmapData = normalized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            Try
                Dim stride As Integer = data.Stride
                Dim raw(stride * h - 1) As Byte
                Marshal.Copy(data.Scan0, raw, 0, raw.Length)
                For y As Integer = 0 To h - 1
                    Dim rowSrc As Integer = y * stride
                    Dim rowDst As Integer = y * w * 3
                    For x As Integer = 0 To w - 1
                        Dim s As Integer = rowSrc + x * 3
                        Dim d As Integer = rowDst + x * 3
                        result(d) = raw(s)
                        result(d + 1) = raw(s + 1)
                        result(d + 2) = raw(s + 2)
                    Next
                Next
            Finally
                normalized.UnlockBits(data)
            End Try
        End Using
        Return result
    End Function

    ''' <summary>
    ''' overlayRgb (ToRgbBytes) with gain already multiplied into every byte and clamped to 255, so that
    ''' AddOverlay's per-frame, per-pixel work is a plain add instead of repeating a floating-point multiply and
    ''' a CInt conversion on the same bytes for every single exported frame - gain is fixed for the whole run
    ''' (frozen at Start Simulation, like everything else in SimulationJob), so there is no reason to redo this
    ''' more than once. Returns Nothing (AddOverlay then does nothing) when gain is 0 or below or there is no
    ''' overlay to scale.
    ''' </summary>
    Public Shared Function ScaleOverlayRgb(overlayRgb As Byte(), gain As Double) As Byte()
        If overlayRgb Is Nothing OrElse gain <= 0.0 Then Return Nothing

        Dim scaled(overlayRgb.Length - 1) As Byte
        For i As Integer = 0 To overlayRgb.Length - 1
            scaled(i) = CByte(Math.Min(255.0, overlayRgb(i) * gain))
        Next
        Return scaled
    End Function

    ''' <summary>
    ''' Additively blends a gain-prescaled overlay (ScaleOverlayRgb - B,G,R triplets, same w x h as pixels) into
    ''' pixels (packed 0xAARRGGBB, as RenderField/RenderIntensity/Colorize produce), clamped to 255 per channel:
    ''' never darkens, never touches the alpha byte, and a black overlay pixel (no edge there) leaves pixels
    ''' untouched. Does nothing when there is no (prescaled) overlay. Parallelized by row like RenderField/
    ''' RenderIntensity, since this runs once per exported video frame at full simulation resolution and was the
    ''' actual cost of the glow-edges overlay on a video export - the live preview (Form1's own
    ''' ComposeGlowOverlay, same multiply-then-add idea, kept separate since it runs on the UI thread against a
    ''' Bitmap) is comparatively small and only runs a few times a second, so it was never the bottleneck.
    ''' </summary>
    Public Shared Sub AddOverlay(pixels As Integer(), scaledOverlayRgb As Byte(), w As Integer, h As Integer)
        If scaledOverlayRgb Is Nothing Then Return

        Parallel.For(0, h,
            Sub(y As Integer)
                Dim rowPix As Integer = y * w
                Dim rowRgb As Integer = y * w * 3
                For x As Integer = 0 To w - 1
                    Dim o As Integer = rowRgb + x * 3
                    Dim ob As Integer = scaledOverlayRgb(o)
                    Dim og As Integer = scaledOverlayRgb(o + 1)
                    Dim orr As Integer = scaledOverlayRgb(o + 2)
                    If ob = 0 AndAlso og = 0 AndAlso orr = 0 Then Continue For

                    Dim i As Integer = rowPix + x
                    Dim argb As Integer = pixels(i)
                    Dim a As Integer = argb And &HFF000000
                    Dim r As Integer = (argb >> 16) And &HFF
                    Dim gr As Integer = (argb >> 8) And &HFF
                    Dim b As Integer = argb And &HFF

                    r = Math.Min(255, r + orr)
                    gr = Math.Min(255, gr + og)
                    b = Math.Min(255, b + ob)

                    pixels(i) = a Or (r << 16) Or (gr << 8) Or b
                Next
            End Sub)
    End Sub

    ''' <summary>Makes a bitmap from packed ARGB pixels. The caller disposes the bitmap.</summary>
    Public Shared Function ToBitmap(pixels As Integer(), w As Integer, h As Integer) As Bitmap
        Dim bmp As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Dim data As BitmapData = bmp.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            For y As Integer = 0 To h - 1
                Marshal.Copy(pixels, y * w, IntPtr.Add(data.Scan0, y * data.Stride), w)
            Next
        Finally
            bmp.UnlockBits(data)
        End Try
        Return bmp
    End Function

End Class
