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
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Drawing.Text
Imports System.Globalization
Imports System.Runtime.InteropServices

''' <summary>
''' Draws the probe graphs (a little box with axes and the measured curve) and the probe markers. Used for the live
''' pictures on the screen (with a Graphics of the picture box) and for the exported videos (through ProbeOverlay).
'''   Point probe   value (Y) against the simulation step (X)
'''   Line probe    value (Y) against the position on the line A-B in pixels (X), at the current step
''' </summary>
Friend NotInheritable Class ProbeGraphPainter

    Private Sub New()
    End Sub

    ''' <summary>The nominal size of the box in scene pixels.</summary>
    Public Shared Function NominalBoxSize(boxSize As ProbeBoxSize) As Size
        Select Case boxSize
            Case ProbeBoxSize.Tiny
                Return New Size(200, 100)
            Case ProbeBoxSize.Small
                Return New Size(300, 150)
            Case ProbeBoxSize.Large
                Return New Size(500, 250)
            Case ProbeBoxSize.ExtraLarge
                Return New Size(700, 350)
            Case Else
                Return New Size(400, 200)
        End Select
    End Function

    ''' <summary>
    ''' The graph box in scene pixels of a canvas of w x h: the nominal size (smaller when the canvas is small: at most
    ''' half as wide as the canvas) with its top left corner where the probe says, moved when necessary so that the box stays on the canvas.
    ''' </summary>
    Public Shared Function BoxInCanvas(probe As ProbeSettings, w As Integer, h As Integer) As Rectangle
        Dim nominal As Size = NominalBoxSize(probe.BoxSize)
        Dim factor As Double = 1.0
        If nominal.Width > w / 2.0 Then factor = Math.Min(factor, (w / 2.0) / nominal.Width)
        If nominal.Height > h Then factor = Math.Min(factor, h / CDbl(nominal.Height))

        Dim boxW As Integer = Math.Max(1, CInt(nominal.Width * factor))
        Dim boxH As Integer = Math.Max(1, CInt(nominal.Height * factor))
        Dim x As Integer = Math.Max(0, Math.Min(w - boxW, probe.BoxX))
        Dim y As Integer = Math.Max(0, Math.Min(h - boxH, probe.BoxY))
        Return New Rectangle(x, y, boxW, boxH)
    End Function

    ' ---------------------------------------------------------------- the axes

    ''' <summary>
    ''' The ranges of the graph.
    ''' Point probe: X is the simulation step. With a running graph the last RunningWindow steps are shown; with a full
    ''' graph the whole run (0 to the last step so far when X is scaled automatically, otherwise 0 to the total number of steps).
    ''' Line probe: X is the position on the line, from 0 to the length of the line.
    ''' With automatic Y the range follows the values that are shown.
    ''' </summary>
    Public Shared Sub ComputeRanges(probe As ProbeSettings, trace As ProbeTrace, totalSteps As Integer,
                                    ByRef xMin As Double, ByRef xMax As Double, ByRef yMin As Double, ByRef yMax As Double)
        If probe.Kind = ProbeKind.Line Then
            ProfileRanges(probe, trace, xMin, xMax, yMin, yMax)
        Else
            Dim series As ProbeSeries = Nothing
            If trace IsNot Nothing Then series = trace.Series
            Dim startStep As Double = If(probe.UseStartAfterStep, Math.Max(0, probe.StartAfterStep), 0.0)
            SeriesRanges(probe, series, totalSteps, startStep, xMin, xMax, yMin, yMax)
        End If
    End Sub

    ''' <summary>
    ''' The ranges for a point probe. When "start after step" is used, the values from before that step are trimmed off:
    ''' xMin never goes below it, so the visible range starts there (with more room for the data that follows, especially
    ''' with the full scale, which would otherwise show mostly empty space before the data starts).
    ''' </summary>
    Private Shared Sub SeriesRanges(probe As ProbeSettings, series As ProbeSeries, totalSteps As Integer, startStep As Double,
                                    ByRef xMin As Double, ByRef xMax As Double, ByRef yMin As Double, ByRef yMax As Double)
        Dim count As Integer = If(series Is Nothing, 0, series.Count)
        Dim current As Double = count

        If probe.ScaleMode = ProbeScaleMode.Full Then
            xMin = startStep
            xMax = If(probe.AutoScaleX, Math.Max(current, startStep + 1.0), Math.Max(CDbl(totalSteps), startStep + 1.0))
        Else
            Dim window As Double = Math.Max(10, probe.RunningWindow)
            If probe.AutoScaleX Then
                xMax = Math.Max(current, startStep + 1.0)
                xMin = Math.Max(startStep, xMax - window)
            Else
                xMax = Math.Max(current, startStep + window)
                xMin = Math.Max(startStep, xMax - window)
            End If
        End If
        If xMax <= xMin Then xMax = xMin + 1.0

        yMin = probe.YMin
        yMax = probe.YMax
        If Not (yMax > yMin) Then
            yMin = -1.0
            yMax = 1.0
        End If

        If probe.AutoScaleY AndAlso count > 0 Then
            Dim i0 As Integer = Math.Max(0, CInt(Math.Floor(xMin)) - 1)
            Dim i1 As Integer = Math.Min(count - 1, CInt(Math.Ceiling(xMax)))
            Dim lo As Double = Double.MaxValue
            Dim hi As Double = Double.MinValue
            Dim values As Single() = series.Values
            For i As Integer = i0 To i1
                Dim v As Single = values(i)
                If Not (Single.IsNaN(v) OrElse Single.IsInfinity(v)) Then
                    If v < lo Then lo = v
                    If v > hi Then hi = v
                End If
            Next
            If lo <= hi Then FitRange(lo, hi, False, yMin, yMax)
        End If
    End Sub

    Private Shared Sub ProfileRanges(probe As ProbeSettings, trace As ProbeTrace,
                                     ByRef xMin As Double, ByRef xMax As Double, ByRef yMin As Double, ByRef yMax As Double)
        Dim length As Double = 0.0
        Dim profile As Single() = Nothing
        If trace IsNot Nothing Then
            length = trace.ProfileLength
            profile = trace.Profile
        End If

        xMin = 0.0
        xMax = If(length > 0.0, length, 1.0)

        yMin = probe.YMin
        yMax = probe.YMax
        If Not (yMax > yMin) Then
            yMin = -1.0
            yMax = 1.0
        End If

        If probe.AutoScaleY AndAlso profile IsNot Nothing Then
            Dim lo As Double = Double.MaxValue
            Dim hi As Double = Double.MinValue
            For i As Integer = 0 To profile.Length - 1
                Dim v As Single = profile(i)
                If Not (Single.IsNaN(v) OrElse Single.IsInfinity(v)) Then
                    If v < lo Then lo = v
                    If v > hi Then hi = v
                End If
            Next
            ' an intensity starts at zero
            If lo <= hi Then FitRange(lo, hi, probe.Measurement <> ProbeMeasurement.Amplitude AndAlso lo >= 0.0, yMin, yMax)
        End If
    End Sub

    ''' <summary>A value range with some room around the values (from zero when zeroBased).</summary>
    Private Shared Sub FitRange(lo As Double, hi As Double, zeroBased As Boolean, ByRef yMin As Double, ByRef yMax As Double)
        Dim span As Double = hi - lo
        Dim biggest As Double = Math.Max(Math.Abs(hi), Math.Abs(lo))
        Dim pad As Double = span * 0.08
        If span < biggest * 0.000000001 OrElse span < 1.0E-30 Then
            pad = Math.Max(biggest * 0.1, 0.000001)
        End If
        yMin = If(zeroBased, 0.0, lo - pad)
        yMax = hi + pad
    End Sub

    Private Shared Function NiceTicks(lo As Double, hi As Double, target As Integer) As List(Of Double)
        Dim ticks As New List(Of Double)()
        Dim span As Double = hi - lo
        If Not (span > 0.0) OrElse Double.IsInfinity(span) Then Return ticks

        Dim raw As Double = span / Math.Max(1, target)
        Dim magnitude As Double = Math.Pow(10.0, Math.Floor(Math.Log10(raw)))
        Dim normalized As Double = raw / magnitude
        Dim tickStep As Double
        If normalized < 1.5 Then
            tickStep = 1.0
        ElseIf normalized < 3.0 Then
            tickStep = 2.0
        ElseIf normalized < 7.0 Then
            tickStep = 5.0
        Else
            tickStep = 10.0
        End If
        tickStep *= magnitude

        Dim v As Double = Math.Ceiling(lo / tickStep) * tickStep
        Dim guard As Integer = 0
        While v <= hi + tickStep * 0.000000001 AndAlso guard < 40
            ticks.Add(If(Math.Abs(v) < tickStep * 0.000000001, 0.0, v))
            v += tickStep
            guard += 1
        End While
        Return ticks
    End Function

    Private Shared Function FormatTick(v As Double) As String
        If v = 0.0 Then Return "0"
        Return v.ToString("G3", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>A position on the step axis or on the line: a whole number, or with one decimal for a short line.</summary>
    Private Shared Function FormatPosition(v As Double, span As Double) As String
        If span >= 10.0 Then Return Math.Round(v).ToString("0", CultureInfo.InvariantCulture)
        Return v.ToString("0.#", CultureInfo.InvariantCulture)
    End Function

    ' ---------------------------------------------------------------- the graph

    ''' <summary>Draws the graph box (background, axes, curve, title and a value) in the given rectangle.</summary>
    Public Shared Sub DrawGraph(g As Graphics, bounds As Rectangle, title As String, probe As ProbeSettings,
                                trace As ProbeTrace, totalSteps As Integer)
        If bounds.Width < 40 OrElse bounds.Height < 30 Then Return

        Dim state As GraphicsState = g.Save()
        Try
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit

            Using background As New SolidBrush(Color.FromArgb(235, 14, 16, 22))
                g.FillRectangle(background, bounds)
            End Using
            Using border As New Pen(Color.FromArgb(170, 200, 200, 210), 1.0F)
                g.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1)
            End Using

            Dim fontPx As Single = Math.Max(9.0F, bounds.Height / 14.0F)
            Using font As New Font("Segoe UI", fontPx, FontStyle.Regular, GraphicsUnit.Pixel)
                Dim labelWidth As Integer = CInt(Math.Ceiling(fontPx * 4.4F))
                Dim topMargin As Integer = CInt(fontPx * 2.3F)
                Dim bottomMargin As Integer = CInt(fontPx * 1.9F)
                Dim plot As New Rectangle(bounds.X + labelWidth + 6, bounds.Y + topMargin,
                                          bounds.Width - labelWidth - 6 - 10, bounds.Height - topMargin - bottomMargin)
                If plot.Width < 20 OrElse plot.Height < 15 Then Return

                ' a line probe (a profile of the current instant) has nothing meaningful to show before the start step is
                ' reached, so it stays blank until then; a point probe (a series) is trimmed instead, see SeriesRanges
                Dim isLine As Boolean = (probe.Kind = ProbeKind.Line)
                Dim started As Boolean = Not probe.UseStartAfterStep OrElse trace Is Nothing OrElse trace.StepsSampled >= probe.StartAfterStep
                Dim shownTrace As ProbeTrace = If(started, trace, Nothing)

                ' a point probe's own range/autoscale must see the real trace (it is trimmed by "start after step" inside
                ' ComputeRanges/SeriesRanges, not hidden); only a line probe uses shownTrace, to stay blank until it starts
                Dim xMin, xMax, yMin, yMax As Double
                ComputeRanges(probe, If(isLine, shownTrace, trace), totalSteps, xMin, xMax, yMin, yMax)

                DrawAxes(g, font, fontPx, bounds, plot, xMin, xMax, yMin, yMax)
                Dim lineWidth As Single = Math.Max(1.2F, bounds.Height / 110.0F)
                If isLine Then
                    If shownTrace IsNot Nothing Then DrawProfile(g, plot, shownTrace.Profile, yMin, yMax, lineWidth)
                Else
                    ' a point probe is trimmed (xMin), not hidden: its curve is drawn up to the current value even before
                    ' the start step is reached (the range is then just very small)
                    If trace IsNot Nothing Then DrawCurve(g, plot, trace.Series, xMin, xMax, yMin, yMax, lineWidth)
                End If
                DrawTitle(g, font, bounds, title, ValueText(isLine, If(isLine, shownTrace, trace)), fontPx)
            End Using
        Finally
            g.Restore(state)
        End Try
    End Sub

    ''' <summary>The value at the top right of the box: the last value of a point probe, the highest value on the line of a line probe.</summary>
    Private Shared Function ValueText(isLine As Boolean, trace As ProbeTrace) As String
        If trace Is Nothing Then Return ""

        If isLine Then
            Dim profile As Single() = trace.Profile
            If profile Is Nothing OrElse profile.Length = 0 Then Return ""
            Dim hi As Single = Single.MinValue
            For i As Integer = 0 To profile.Length - 1
                Dim v As Single = profile(i)
                If Not (Single.IsNaN(v) OrElse Single.IsInfinity(v)) AndAlso v > hi Then hi = v
            Next
            If hi = Single.MinValue Then Return ""
            Return "max " & FormatTick(hi)
        End If

        If trace.Series.Count = 0 Then Return ""
        Dim last As Single = trace.Series.LastValue
        If Single.IsNaN(last) OrElse Single.IsInfinity(last) Then Return ""
        Return "= " & FormatTick(last)
    End Function

    Private Shared Sub DrawAxes(g As Graphics, font As Font, fontPx As Single, bounds As Rectangle, plot As Rectangle,
                                xMin As Double, xMax As Double, yMin As Double, yMax As Double)
        Using gridPen As New Pen(Color.FromArgb(46, 255, 255, 255), 1.0F)
            Using zeroPen As New Pen(Color.FromArgb(120, 255, 255, 255), 1.0F)
                Using textBrush As New SolidBrush(Color.FromArgb(225, 225, 225, 230))
                    Using rightAligned As New StringFormat()
                        Using centered As New StringFormat()
                            rightAligned.Alignment = StringAlignment.Far
                            rightAligned.LineAlignment = StringAlignment.Center
                            rightAligned.FormatFlags = StringFormatFlags.NoWrap
                            centered.Alignment = StringAlignment.Center
                            centered.FormatFlags = StringFormatFlags.NoWrap

                            ' value axis
                            For Each v As Double In NiceTicks(yMin, yMax, 4)
                                Dim y As Single = ToPixelY(v, plot, yMin, yMax)
                                g.DrawLine(If(v = 0.0, zeroPen, gridPen), plot.Left, y, plot.Right, y)
                                g.DrawString(FormatTick(v), font, textBrush,
                                             New RectangleF(bounds.X + 1, y - fontPx * 0.7F, plot.Left - bounds.X - 4, fontPx * 1.4F), rightAligned)
                            Next

                            ' step axis (point probe) or position on the line (line probe)
                            For Each v As Double In NiceTicks(xMin, xMax, 5)
                                Dim x As Single = CSng(plot.Left + (v - xMin) / (xMax - xMin) * plot.Width)
                                g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom)
                                g.DrawString(FormatPosition(v, xMax - xMin), font, textBrush,
                                             New RectangleF(x - fontPx * 2.5F, plot.Bottom + 2, fontPx * 5.0F, fontPx * 1.4F), centered)
                            Next
                        End Using
                    End Using
                End Using
            End Using

            Using framePen As New Pen(Color.FromArgb(130, 200, 200, 210), 1.0F)
                g.DrawRectangle(framePen, plot.X, plot.Y, plot.Width, plot.Height)
            End Using
        End Using
    End Sub

    Private Shared Function ToPixelY(v As Double, plot As Rectangle, yMin As Double, yMax As Double) As Single
        Dim y As Double = plot.Bottom - (v - yMin) / (yMax - yMin) * plot.Height
        If y < -30000.0 Then y = -30000.0
        If y > 30000.0 Then y = 30000.0
        Return CSng(y)
    End Function

    ''' <summary>The curve of a point probe: the value of every step.</summary>
    Private Shared Sub DrawCurve(g As Graphics, plot As Rectangle, series As ProbeSeries,
                                 xMin As Double, xMax As Double, yMin As Double, yMax As Double, lineWidth As Single)
        If series Is Nothing OrElse series.Count < 1 Then Return

        Dim values As Single() = series.Values
        Dim count As Integer = series.Count
        Dim i0 As Integer = Math.Max(0, CInt(Math.Floor(xMin)) - 1)
        Dim i1 As Integer = Math.Min(count - 1, CInt(Math.Ceiling(xMax)))
        If i1 < i0 Then Return

        Dim points As New List(Of PointF)()
        Dim visible As Integer = i1 - i0 + 1
        Dim xScale As Double = plot.Width / (xMax - xMin)

        If visible <= plot.Width * 2 Then
            ' every value gets its own point (value i belongs to step i + 1)
            For i As Integer = i0 To i1
                Dim v As Single = values(i)
                If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Continue For
                points.Add(New PointF(CSng(plot.Left + (i + 1 - xMin) * xScale), ToPixelY(v, plot, yMin, yMax)))
            Next
        Else
            ' more values than pixels: draw the lowest and the highest value of every pixel column
            For column As Integer = 0 To plot.Width - 1
                Dim a As Integer = i0 + CInt(CLng(column) * visible \ plot.Width)
                Dim b As Integer = i0 + CInt(CLng(column + 1) * visible \ plot.Width) - 1
                If b < a Then b = a
                Dim lo As Single = Single.MaxValue
                Dim hi As Single = Single.MinValue
                For i As Integer = a To Math.Min(b, i1)
                    Dim v As Single = values(i)
                    If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Continue For
                    If v < lo Then lo = v
                    If v > hi Then hi = v
                Next
                If lo <= hi Then
                    Dim x As Single = plot.Left + column + 0.5F
                    points.Add(New PointF(x, ToPixelY(lo, plot, yMin, yMax)))
                    points.Add(New PointF(x, ToPixelY(hi, plot, yMin, yMax)))
                End If
            Next
        End If

        StrokePoints(g, plot, points, lineWidth)
    End Sub

    ''' <summary>The curve of a line probe: the value at every pixel of the line, from A (left) to B (right).</summary>
    Private Shared Sub DrawProfile(g As Graphics, plot As Rectangle, profile As Single(), yMin As Double, yMax As Double, lineWidth As Single)
        If profile Is Nothing OrElse profile.Length < 1 Then Return

        Dim n As Integer = profile.Length
        Dim points As New List(Of PointF)()

        If n <= plot.Width * 2 Then
            For k As Integer = 0 To n - 1
                Dim v As Single = profile(k)
                If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Continue For
                Dim fraction As Double = If(n > 1, k / (n - 1.0), 0.0)
                points.Add(New PointF(CSng(plot.Left + fraction * plot.Width), ToPixelY(v, plot, yMin, yMax)))
            Next
        Else
            ' more pixels on the line than on the screen: the lowest and the highest value of every pixel column
            For column As Integer = 0 To plot.Width - 1
                Dim a As Integer = CInt(CLng(column) * n \ plot.Width)
                Dim b As Integer = CInt(CLng(column + 1) * n \ plot.Width) - 1
                If b < a Then b = a
                Dim lo As Single = Single.MaxValue
                Dim hi As Single = Single.MinValue
                For k As Integer = a To Math.Min(b, n - 1)
                    Dim v As Single = profile(k)
                    If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Continue For
                    If v < lo Then lo = v
                    If v > hi Then hi = v
                Next
                If lo <= hi Then
                    Dim x As Single = plot.Left + column + 0.5F
                    points.Add(New PointF(x, ToPixelY(lo, plot, yMin, yMax)))
                    points.Add(New PointF(x, ToPixelY(hi, plot, yMin, yMax)))
                End If
            Next
        End If

        StrokePoints(g, plot, points, lineWidth)
    End Sub

    Private Shared Sub StrokePoints(g As Graphics, plot As Rectangle, points As List(Of PointF), lineWidth As Single)
        If points.Count < 2 Then
            If points.Count = 1 Then
                Using dot As New SolidBrush(Color.FromArgb(255, 255, 208, 64))
                    g.FillEllipse(dot, points(0).X - 2.0F, points(0).Y - 2.0F, 4.0F, 4.0F)
                End Using
            End If
            Return
        End If

        Dim clip As Region = g.Clip
        Try
            g.SetClip(plot)
            Using pen As New Pen(Color.FromArgb(255, 255, 208, 64), lineWidth)
                pen.LineJoin = LineJoin.Round
                g.DrawLines(pen, points.ToArray())
            End Using
        Finally
            g.Clip = clip
            clip.Dispose()
        End Try
    End Sub

    Private Shared Sub DrawTitle(g As Graphics, font As Font, bounds As Rectangle, title As String,
                                 valueText As String, fontPx As Single)
        Using textBrush As New SolidBrush(Color.FromArgb(240, 240, 240, 245))
            Using valueBrush As New SolidBrush(Color.FromArgb(255, 255, 208, 64))
                Using farFormat As New StringFormat()
                    Using nearFormat As New StringFormat()
                        farFormat.Alignment = StringAlignment.Far
                        farFormat.FormatFlags = StringFormatFlags.NoWrap
                        nearFormat.Alignment = StringAlignment.Near
                        nearFormat.Trimming = StringTrimming.EllipsisCharacter
                        nearFormat.FormatFlags = StringFormatFlags.NoWrap

                        Dim valueWidth As Single = 0.0F
                        If valueText.Length > 0 Then
                            valueWidth = g.MeasureString(valueText, font).Width
                            g.DrawString(valueText, font, valueBrush,
                                         New RectangleF(bounds.Right - valueWidth - 8, bounds.Y + 3, valueWidth + 4, fontPx * 1.4F), farFormat)
                        End If
                        g.DrawString(title, font, textBrush,
                                     New RectangleF(bounds.X + 6, bounds.Y + 3, Math.Max(10.0F, bounds.Width - valueWidth - 16), fontPx * 1.4F), nearFormat)
                    End Using
                End Using
            End Using
        End Using
    End Sub

    ' ---------------------------------------------------------------- the markers

    ''' <summary>
    ''' Draws the probe position(s): a cross at A (and B) with the number of the probe, and the line between them.
    ''' A simulation pixel (x, y) is drawn at (originX + (x + 0.5) * scaleX, originY + (y + 0.5) * scaleY).
    ''' </summary>
    Public Shared Sub DrawMarkers(g As Graphics, probe As ProbeSettings, index As Integer, originX As Single, originY As Single,
                                  scaleX As Single, scaleY As Single)
        Dim ax As Single = originX + (probe.PosAX + 0.5F) * scaleX
        Dim ay As Single = originY + (probe.PosAY + 0.5F) * scaleY
        Dim bx As Single = originX + (probe.PosBX + 0.5F) * scaleX
        Dim by2 As Single = originY + (probe.PosBY + 0.5F) * scaleY
        Dim isLine As Boolean = (probe.Kind = ProbeKind.Line)
        Dim number As String = (index + 1).ToString(CultureInfo.InvariantCulture)

        Dim state As GraphicsState = g.Save()
        Try
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
            Using dark As New Pen(Color.FromArgb(210, 0, 0, 0), 3.5F)
                Using light As New Pen(Color.White, 1.5F)
                    If isLine Then
                        g.DrawLine(dark, ax, ay, bx, by2)
                        g.DrawLine(light, ax, ay, bx, by2)
                    End If
                    DrawCross(g, dark, light, ax, ay)
                    If isLine Then DrawCross(g, dark, light, bx, by2)
                End Using
            End Using

            Using font As New Font("Segoe UI", 13.0F, FontStyle.Bold, GraphicsUnit.Pixel)
                Using shadow As New SolidBrush(Color.FromArgb(220, 0, 0, 0))
                    Using textBrush As New SolidBrush(Color.White)
                        ' a line probe has two ends, so each is labelled with the probe number and which end it is
                        If isLine Then
                            DrawNumber(g, font, shadow, textBrush, number & "A", ax, ay)
                            DrawNumber(g, font, shadow, textBrush, number & "B", bx, by2)
                        Else
                            DrawNumber(g, font, shadow, textBrush, number, ax, ay)
                        End If
                    End Using
                End Using
            End Using
        Finally
            g.Restore(state)
        End Try
    End Sub

    Private Shared Sub DrawCross(g As Graphics, dark As Pen, light As Pen, x As Single, y As Single)
        Const arm As Single = 7.0F
        g.DrawLine(dark, x - arm, y, x + arm, y)
        g.DrawLine(dark, x, y - arm, x, y + arm)
        g.DrawLine(light, x - arm, y, x + arm, y)
        g.DrawLine(light, x, y - arm, x, y + arm)
    End Sub

    Private Shared Sub DrawNumber(g As Graphics, font As Font, shadow As Brush, textBrush As Brush, text As String, x As Single, y As Single)
        g.DrawString(text, font, shadow, x + 9.0F, y - 18.0F)
        g.DrawString(text, font, textBrush, x + 8.0F, y - 19.0F)
    End Sub

End Class

''' <summary>Draws the probes into the pixels of an exported video frame.</summary>
Friend NotInheritable Class ProbeOverlay

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Draws the markers and the graph boxes of the probes that belong on this kind of picture into a frame
    ''' (packed ARGB pixels, w x h). The frame is the whole scene, so the box is where the probe says (in scene pixels).
    ''' </summary>
    Public Shared Sub Burn(pixels As Integer(), w As Integer, h As Integer, probes As IList(Of ProbeSettings),
                           traces As ProbeTrace(), placeOn As ProbePlaceOn, totalSteps As Integer)
        For index As Integer = 0 To probes.Count - 1
            Dim probe As ProbeSettings = probes(index)
            If probe.PlaceOn <> placeOn OrElse Not probe.Active Then Continue For

            DrawMarkerPixels(pixels, w, h, probe, index)

            Dim rect As Rectangle = ProbeGraphPainter.BoxInCanvas(probe, w, h)
            If rect.Width < 60 OrElse rect.Height < 30 Then Continue For

            Using bmp As New Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppPArgb)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.Transparent)
                    ProbeGraphPainter.DrawGraph(g, New Rectangle(0, 0, rect.Width, rect.Height), probe.Title(index), probe, traces(index), totalSteps)
                End Using
                Blend(pixels, w, h, bmp, rect)
            End Using
        Next
    End Sub

    ''' <summary>Puts a picture with premultiplied alpha over a rectangle of the frame.</summary>
    Private Shared Sub Blend(pixels As Integer(), w As Integer, h As Integer, bmp As Bitmap, rect As Rectangle)
        Dim data As BitmapData = bmp.LockBits(New Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb)
        Try
            Dim row(bmp.Width - 1) As Integer
            For y As Integer = 0 To bmp.Height - 1
                Dim fy As Integer = rect.Y + y
                If fy < 0 OrElse fy >= h Then Continue For

                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, bmp.Width)
                For x As Integer = 0 To bmp.Width - 1
                    Dim fx As Integer = rect.X + x
                    If fx < 0 OrElse fx >= w Then Continue For

                    Dim src As Integer = row(x)
                    Dim a As Integer = (src >> 24) And &HFF
                    If a = 0 Then Continue For

                    Dim idx As Integer = fy * w + fx
                    If a = 255 Then
                        pixels(idx) = src Or &HFF000000
                    Else
                        Dim dst As Integer = pixels(idx)
                        Dim inv As Integer = 255 - a
                        Dim r As Integer = Math.Min(255, ((src >> 16) And &HFF) + (((dst >> 16) And &HFF) * inv + 127) \ 255)
                        Dim gr As Integer = Math.Min(255, ((src >> 8) And &HFF) + (((dst >> 8) And &HFF) * inv + 127) \ 255)
                        Dim b As Integer = Math.Min(255, (src And &HFF) + ((dst And &HFF) * inv + 127) \ 255)
                        pixels(idx) = &HFF000000 Or (r << 16) Or (gr << 8) Or b
                    End If
                Next
            Next
        Finally
            bmp.UnlockBits(data)
        End Try
    End Sub

    ' ---- markers, drawn pixel by pixel

    Private Shared Sub DrawMarkerPixels(pixels As Integer(), w As Integer, h As Integer, probe As ProbeSettings, index As Integer)
        Dim arm As Integer = Math.Max(6, h \ 120)
        Dim isLine As Boolean = (probe.Kind = ProbeKind.Line)

        ' black underneath, white on top
        For pass As Integer = 0 To 1
            Dim color As Integer = If(pass = 0, &HFF000000, &HFFFFFFFF)
            Dim radius As Integer = If(pass = 0, 2, 1)

            If isLine Then DrawThickLine(pixels, w, h, probe.PosAX, probe.PosAY, probe.PosBX, probe.PosBY, color, radius)
            DrawCrossPixels(pixels, w, h, probe.PosAX, probe.PosAY, arm, color, radius)
            If isLine Then DrawCrossPixels(pixels, w, h, probe.PosBX, probe.PosBY, arm, color, radius)
        Next

        ' the number of the probe next to the cross; a line probe's two ends are labelled with which end they are
        Dim number As String = (index + 1).ToString(CultureInfo.InvariantCulture)
        Dim fontPx As Single = Math.Max(12.0F, h / 60.0F)
        If isLine Then
            BurnLabel(pixels, w, h, number & "A", probe.PosAX + arm + 2, probe.PosAY - arm - CInt(fontPx * 1.4F), fontPx)
            BurnLabel(pixels, w, h, number & "B", probe.PosBX + arm + 2, probe.PosBY - arm - CInt(fontPx * 1.4F), fontPx)
        Else
            BurnLabel(pixels, w, h, number, probe.PosAX + arm + 2, probe.PosAY - arm - CInt(fontPx * 1.4F), fontPx)
        End If
    End Sub

    Private Shared Sub BurnLabel(pixels As Integer(), w As Integer, h As Integer, text As String, x As Integer, y As Integer, fontPx As Single)
        Using font As New Font("Segoe UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel)
            Dim labelWidth As Integer
            Dim labelHeight As Integer
            Using measure As New Bitmap(1, 1)
                Using g As Graphics = Graphics.FromImage(measure)
                    Dim m As SizeF = g.MeasureString(text, font)
                    labelWidth = CInt(Math.Ceiling(m.Width)) + 4
                    labelHeight = CInt(Math.Ceiling(m.Height)) + 4
                End Using
            End Using

            Using bmp As New Bitmap(labelWidth, labelHeight, PixelFormat.Format32bppPArgb)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.Transparent)
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
                    Using shadow As New SolidBrush(Color.FromArgb(220, 0, 0, 0))
                        Using white As New SolidBrush(Color.White)
                            g.DrawString(text, font, shadow, 3.0F, 3.0F)
                            g.DrawString(text, font, white, 2.0F, 2.0F)
                        End Using
                    End Using
                End Using
                Blend(pixels, w, h, bmp, New Rectangle(x, y, labelWidth, labelHeight))
            End Using
        End Using
    End Sub

    Private Shared Sub DrawCrossPixels(pixels As Integer(), w As Integer, h As Integer, x As Integer, y As Integer,
                                       arm As Integer, color As Integer, radius As Integer)
        DrawThickLine(pixels, w, h, x - arm, y, x + arm, y, color, radius)
        DrawThickLine(pixels, w, h, x, y - arm, x, y + arm, color, radius)
    End Sub

    Private Shared Sub DrawThickLine(pixels As Integer(), w As Integer, h As Integer, x0 As Integer, y0 As Integer,
                                     x1 As Integer, y1 As Integer, color As Integer, radius As Integer)
        Dim dx As Integer = Math.Abs(x1 - x0)
        Dim dy As Integer = -Math.Abs(y1 - y0)
        Dim sx As Integer = If(x0 < x1, 1, -1)
        Dim sy As Integer = If(y0 < y1, 1, -1)
        Dim diff As Integer = dx + dy
        Dim guard As Integer = 0

        Do
            For oy As Integer = -radius To radius
                For ox As Integer = -radius To radius
                    Dim px As Integer = x0 + ox
                    Dim py As Integer = y0 + oy
                    If px >= 0 AndAlso px < w AndAlso py >= 0 AndAlso py < h Then pixels(py * w + px) = color
                Next
            Next

            If (x0 = x1 AndAlso y0 = y1) OrElse guard > 100000 Then Exit Do
            guard += 1
            Dim e2 As Integer = 2 * diff
            If e2 >= dy Then
                diff += dy
                x0 += sx
            End If
            If e2 <= dx Then
                diff += dx
                y0 += sy
            End If
        Loop
    End Sub

End Class
