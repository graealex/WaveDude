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

''' <summary>
''' A color scheme: a lookup table of exactly 255 RGB colors, the same table size the Python
''' simulator uses. A scheme can be used for the field view or for the intensity view.
'''
''' Schemes made in the stop editor also keep their anchor colors ("stops", StopCount of them) so they can be
''' edited again. The stops sit at fixed, equally spaced positions on the scale: stop 1 is the first entry,
''' the last stop is the last entry and, with an odd number of stops, the middle stop is exactly the centre
''' entry 127 (the zero-field color). With 9 stops that is Color 5.
''' All other entries are interpolated linearly in RGB between neighbouring stops.
''' Imported schemes (matplotlib, seaborn, Python tables) have no stops and are used as they are.
''' </summary>
Public Class ColorScheme

    ''' <summary>Number of entries in every scheme.</summary>
    Public Const EntryCount As Integer = 255

    ''' <summary>Number of fixed anchor colors in the stop editor (one Color button each) - 9 since the Color 8/9
    ''' buttons were added to the Designer (previously upgraded from 5 to 7 the same way).</summary>
    Public Const StopCount As Integer = 9

    Private _entries As Color()
    Private _stops As Color()

    ''' <summary>Scheme type "field" (signed values, centre = zero field) as stored in the file.</summary>
    Public Const SchemeTypeField As String = "field"

    ''' <summary>Scheme type "intensity" (values from zero upwards) as stored in the file.</summary>
    Public Const SchemeTypeIntensity As String = "intensity"

    Public Property Name As String
    Public Property Origin As String
    Public Property Category As String

    ''' <summary>
    ''' What the scheme is meant for: "field", "intensity" or an empty string when not specified.
    ''' This is only a label saved in the file; any scheme can technically be used for either.
    ''' </summary>
    Public Property SchemeType As String

    ''' <summary>Full path of the JSON file this scheme was loaded from or saved to (empty if never saved).</summary>
    Public Property FilePath As String

    Public Sub New(schemeName As String, colors As IList(Of Color))
        If colors Is Nothing Then Throw New ArgumentNullException("colors")
        Me.Name = schemeName
        Me.Origin = ""
        Me.Category = ""
        Me.SchemeType = ""
        Me.FilePath = ""
        _entries = Resample(colors, EntryCount)
    End Sub

    ' ---------------------------------------------------------------- reading

    ''' <summary>A copy of the 255 entries.</summary>
    Public ReadOnly Property Entries As Color()
        Get
            Return CType(_entries.Clone(), Color())
        End Get
    End Property

    ''' <summary>One entry, index 0 to 254.</summary>
    Public Function GetEntry(index As Integer) As Color
        Return _entries(index)
    End Function

    ''' <summary>True when the scheme has StopCount editable stops.</summary>
    Public ReadOnly Property HasStops As Boolean
        Get
            Return _stops IsNot Nothing
        End Get
    End Property

    ''' <summary>A copy of the stops, or Nothing when the scheme has none.</summary>
    Public ReadOnly Property Stops As Color()
        Get
            If _stops Is Nothing Then Return Nothing
            Return CType(_stops.Clone(), Color())
        End Get
    End Property

    ''' <summary>The color at a fractional position (0 to 254) on the scale, interpolated between entries.</summary>
    Public Function SampleAt(position As Double) As Color
        If position <= 0.0 Then Return _entries(0)
        If position >= EntryCount - 1 Then Return _entries(EntryCount - 1)
        Dim lo As Integer = CInt(Math.Floor(position))
        Return Lerp(_entries(lo), _entries(lo + 1), position - lo)
    End Function

    Public Overrides Function ToString() As String
        Return Name
    End Function

    ' ---------------------------------------------------------------- stops

    ''' <summary>
    ''' Creates a stop-based scheme. The stops must be given in scale order
    ''' (stop 1 first, the middle stop in the centre, the last stop last).
    ''' </summary>
    Public Shared Function FromStops(schemeName As String, stops As IList(Of Color)) As ColorScheme
        Dim scheme As New ColorScheme(schemeName, InterpolateStops(stops))
        scheme.AttachStops(stops)
        scheme.Origin = StopCount.ToString() & "-stop gradient made in the color scheme editor"
        scheme.Category = "custom"
        Return scheme
    End Function

    ''' <summary>Sets the stops of this scheme without recomputing the entries (used when loading a file).</summary>
    Friend Sub AttachStops(stops As IList(Of Color))
        If stops Is Nothing OrElse stops.Count <> StopCount Then
            Throw New ArgumentException("Exactly " & StopCount.ToString() & " stops are required.")
        End If
        Dim copy(StopCount - 1) As Color
        stops.CopyTo(copy, 0)
        _stops = copy
    End Sub

    ''' <summary>Changes one stop (0 to StopCount - 1) and recomputes all 255 entries. Only for schemes with stops.</summary>
    Public Sub SetStop(stopIndex As Integer, value As Color)
        If _stops Is Nothing Then
            Throw New InvalidOperationException("This scheme has no editable stops. Use ToStopScheme first.")
        End If
        If stopIndex < 0 OrElse stopIndex >= StopCount Then
            Throw New ArgumentOutOfRangeException("stopIndex")
        End If
        _stops(stopIndex) = Color.FromArgb(value.R, value.G, value.B)
        _entries = InterpolateStops(_stops)
    End Sub

    ''' <summary>
    ''' Makes an editable stop scheme from any scheme by sampling it at the stop positions.
    ''' This is lossy: the copy is a smooth approximation of the original.
    ''' </summary>
    Public Function ToStopScheme(newName As String) As ColorScheme
        Dim picked(StopCount - 1) As Color
        For k As Integer = 0 To StopCount - 1
            picked(k) = SampleAt(k * (EntryCount - 1) / CDbl(StopCount - 1))
        Next
        Dim scheme As ColorScheme = FromStops(newName, picked)
        scheme.Origin = StopCount.ToString() & "-stop copy of scheme '" & Name & "'"
        scheme.SchemeType = SchemeType
        Return scheme
    End Function

    ''' <summary>An independent copy under a new name (FilePath is not copied).</summary>
    Public Function Clone(newName As String) As ColorScheme
        Dim scheme As New ColorScheme(newName, _entries)
        scheme.Origin = Origin
        scheme.Category = Category
        scheme.SchemeType = SchemeType
        If _stops IsNot Nothing Then scheme.AttachStops(_stops)
        Return scheme
    End Function

    ''' <summary>Builds the 255 entries from the stops at fixed, equally spaced positions (linear in RGB).</summary>
    Public Shared Function InterpolateStops(stops As IList(Of Color)) As Color()
        If stops Is Nothing OrElse stops.Count <> StopCount Then
            Throw New ArgumentException("Exactly " & StopCount.ToString() & " stops are required.")
        End If
        Dim result(EntryCount - 1) As Color
        For i As Integer = 0 To EntryCount - 1
            ' position on the stop scale: 0.0 (first stop) to StopCount - 1 (last stop);
            ' with 9 stops entry 127 is exactly 4.0, the middle stop
            Dim pos As Double = i * (StopCount - 1) / CDbl(EntryCount - 1)
            Dim seg As Integer = CInt(Math.Floor(pos))
            If seg > StopCount - 2 Then seg = StopCount - 2
            result(i) = Lerp(stops(seg), stops(seg + 1), pos - seg)
        Next
        Return result
    End Function

    ' ---------------------------------------------------------------- helpers

    ''' <summary>Resamples a list of colors to the requested number of entries (linear in RGB).</summary>
    Public Shared Function Resample(source As IList(Of Color), count As Integer) As Color()
        If source Is Nothing OrElse source.Count < 2 Then
            Throw New ArgumentException("A color scheme needs at least 2 colors.")
        End If
        Dim result(count - 1) As Color
        If source.Count = count Then
            source.CopyTo(result, 0)
            Return result
        End If
        For i As Integer = 0 To count - 1
            Dim pos As Double = i * (source.Count - 1) / CDbl(count - 1)
            Dim lo As Integer = CInt(Math.Floor(pos))
            If lo >= source.Count - 1 Then
                result(i) = source(source.Count - 1)
            Else
                result(i) = Lerp(source(lo), source(lo + 1), pos - lo)
            End If
        Next
        Return result
    End Function

    Private Shared Function Lerp(a As Color, b As Color, t As Double) As Color
        Return Color.FromArgb(LerpChannel(a.R, b.R, t), LerpChannel(a.G, b.G, t), LerpChannel(a.B, b.B, t))
    End Function

    Private Shared Function LerpChannel(a As Integer, b As Integer, t As Double) As Integer
        Dim v As Integer = CInt(Math.Round(a + (b - a) * t, MidpointRounding.AwayFromZero))
        If v < 0 Then v = 0
        If v > 255 Then v = 255
        Return v
    End Function

End Class
