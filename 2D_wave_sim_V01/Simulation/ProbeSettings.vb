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

Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization

''' <summary>The simulation picture the probe and its graph box are drawn on.</summary>
Public Enum ProbePlaceOn
    Field = 0
    Intensity = 1
End Enum

''' <summary>What a probe measures.</summary>
Public Enum ProbeMeasurement
    ''' <summary>The field value (only on the field picture).</summary>
    Amplitude = 0
    ''' <summary>The averaged intensity (only on the intensity picture).</summary>
    Intensity = 1
    ''' <summary>The intensity summed over time (the total that has passed the point, or each point of the line, so far).</summary>
    IntegratedIntensity = 2
End Enum

Public Enum ProbeKind
    ''' <summary>One pixel, position A. The graph shows the value against the simulation step.</summary>
    Point = 0
    ''' <summary>The pixels on the line from A to B. The graph shows the value against the position on the line, at the current step.</summary>
    Line = 1
End Enum

Public Enum ProbeBoxSize
    Tiny = 0
    Small = 1
    Medium = 2
    Large = 3
    ExtraLarge = 4
End Enum

Public Enum ProbeScaleMode
    ''' <summary>The graph shows the last steps and scrolls along.</summary>
    Running = 0
    ''' <summary>The graph shows the whole run.</summary>
    Full = 1
End Enum

''' <summary>
''' One probe: it measures the field or the intensity during the simulation and shows the result in a little graph box
''' in the field or intensity picture. A point probe (A) shows its value against the simulation step; a line probe
''' (A-B) shows the values along the line, against the position on the line, at the current step.
''' The probes are stored in the section "probes" of configuration.json as a list "items".
''' </summary>
Public NotInheritable Class ProbeSettings

    Public Const SectionName As String = "probes"
    Public Const DefaultRunningWindow As Integer = 500
    Public Const MaxRunningWindow As Integer = 1000000

    Public Property PlaceOn As ProbePlaceOn = ProbePlaceOn.Field
    Public Property Measurement As ProbeMeasurement = ProbeMeasurement.Amplitude
    Public Property Kind As ProbeKind = ProbeKind.Point

    ''' <summary>Position A and position B in simulation pixels (0, 0 = top left).</summary>
    Public Property PosAX As Integer = 100
    Public Property PosAY As Integer = 100
    Public Property PosBX As Integer = 300
    Public Property PosBY As Integer = 100

    Public Property BoxSize As ProbeBoxSize = ProbeBoxSize.Medium

    ''' <summary>The top left corner of the graph box in simulation pixels (picked in the picture).</summary>
    Public Property BoxX As Integer = 450
    Public Property BoxY As Integer = 20

    ''' <summary>When False, the probe is switched off: not sampled or shown in a simulation, but its settings are
    ''' kept ("On" checkbox). Off by default (this property's own default, used for both probes of a new project -
    ''' see NewProbeList, Form1.Probes.vb). A probe loaded from a project saved before "active" was written for it
    ''' still comes back On (FromDictionary's own separate, hardcoded fallback below), so that project keeps its
    ''' exact previous behavior instead of silently picking up the new-project default.</summary>
    Public Property Active As Boolean = False

    Public Property ScaleMode As ProbeScaleMode = ProbeScaleMode.Running
    Public Property AutoScaleX As Boolean = True
    Public Property AutoScaleY As Boolean = True

    ''' <summary>The value axis when it is not scaled automatically.</summary>
    Public Property YMin As Double = -1.0
    Public Property YMax As Double = 1.0

    ''' <summary>The number of steps that a running graph shows.</summary>
    Public Property RunningWindow As Integer = DefaultRunningWindow

    ''' <summary>When True, the graph shows no data from before step StartAfterStep (0 = from the start).</summary>
    Public Property UseStartAfterStep As Boolean = False
    Public Property StartAfterStep As Integer = 0

    Public Function Clone() As ProbeSettings
        Return DirectCast(MemberwiseClone(), ProbeSettings)
    End Function

    ''' <summary>Takes over the settings that only change how the graph looks (not what is measured), so that they can be changed during a run.</summary>
    Public Sub CopyDisplayFrom(other As ProbeSettings)
        Active = other.Active
        BoxSize = other.BoxSize
        BoxX = other.BoxX
        BoxY = other.BoxY
        ScaleMode = other.ScaleMode
        AutoScaleX = other.AutoScaleX
        AutoScaleY = other.AutoScaleY
        YMin = other.YMin
        YMax = other.YMax
        RunningWindow = other.RunningWindow
        UseStartAfterStep = other.UseStartAfterStep
        StartAfterStep = other.StartAfterStep
    End Sub

    ''' <summary>Makes the measurement fit the picture: amplitude on the field, intensity or integrated intensity on the intensity.</summary>
    Public Sub Normalize()
        If PlaceOn = ProbePlaceOn.Field Then
            Measurement = ProbeMeasurement.Amplitude
        ElseIf Measurement = ProbeMeasurement.Amplitude Then
            Measurement = ProbeMeasurement.Intensity
        End If
        If Not (YMax > YMin) Then
            YMin = -1.0
            YMax = 1.0
        End If
        If RunningWindow < 10 Then RunningWindow = 10
        If RunningWindow > MaxRunningWindow Then RunningWindow = MaxRunningWindow
        If StartAfterStep < 0 Then StartAfterStep = 0
    End Sub

    Public Function MeasurementText() As String
        Select Case Measurement
            Case ProbeMeasurement.Amplitude
                Return "Amplitude"
            Case ProbeMeasurement.IntegratedIntensity
                Return "Integrated intensity"
            Case Else
                Return "Intensity"
        End Select
    End Function

    ''' <summary>The title of the graph, for example "Probe 1 - Amplitude". The number is also drawn at the probe in the picture.</summary>
    Public Function Title(index As Integer) As String
        Return "Probe " & (index + 1).ToString(CultureInfo.InvariantCulture) & " - " & MeasurementText()
    End Function

    ' ---------------------------------------------------------------- configuration file

    ''' <summary>The probes of a project configuration. There is always at least one (a default probe when there is none).</summary>
    Public Shared Function ListFromConfig(config As ProjectConfig) As List(Of ProbeSettings)
        Dim result As New List(Of ProbeSettings)()

        If config.ContainsKey(SectionName) Then
            Dim section As Dictionary(Of String, Object) = config.GetSection(SectionName)
            Dim items As Object = Nothing
            If section.TryGetValue("items", items) Then
                Dim list As IEnumerable = TryCast(items, IEnumerable)
                If list IsNot Nothing AndAlso Not TypeOf items Is String Then
                    For Each item As Object In list
                        Dim d As Dictionary(Of String, Object) = TryCast(item, Dictionary(Of String, Object))
                        If d IsNot Nothing Then result.Add(FromDictionary(d))
                    Next
                End If
            End If
        End If

        If result.Count = 0 Then result.Add(New ProbeSettings())
        Return result
    End Function

    Public Shared Sub ListToConfig(probes As List(Of ProbeSettings), config As ProjectConfig)
        Dim section As Dictionary(Of String, Object) = config.GetSection(SectionName)
        Dim items As New List(Of Object)()
        For Each p As ProbeSettings In probes
            items.Add(p.ToDictionary())
        Next
        section("count") = items.Count
        section("items") = items
    End Sub

    Private Function ToDictionary() As Dictionary(Of String, Object)
        Dim d As New Dictionary(Of String, Object)()
        d("active") = Active
        d("place_on") = If(PlaceOn = ProbePlaceOn.Field, "field", "intensity")
        Select Case Measurement
            Case ProbeMeasurement.Amplitude
                d("measurement") = "amplitude"
            Case ProbeMeasurement.IntegratedIntensity
                d("measurement") = "integrated_intensity"
            Case Else
                d("measurement") = "intensity"
        End Select
        d("probe_type") = If(Kind = ProbeKind.Line, "line", "point")
        d("ax") = PosAX
        d("ay") = PosAY
        d("bx") = PosBX
        d("by") = PosBY
        Select Case BoxSize
            Case ProbeBoxSize.Tiny
                d("box_size") = "tiny"
            Case ProbeBoxSize.Small
                d("box_size") = "small"
            Case ProbeBoxSize.Large
                d("box_size") = "large"
            Case ProbeBoxSize.ExtraLarge
                d("box_size") = "extra_large"
            Case Else
                d("box_size") = "medium"
        End Select
        d("box_x") = BoxX
        d("box_y") = BoxY
        d("scale_mode") = If(ScaleMode = ProbeScaleMode.Full, "full", "running")
        d("auto_scale_x") = AutoScaleX
        d("auto_scale_y") = AutoScaleY
        d("y_min") = YMin
        d("y_max") = YMax
        d("running_window") = RunningWindow
        d("use_start_after_step") = UseStartAfterStep
        d("start_after_step") = StartAfterStep
        Return d
    End Function

    Private Shared Function FromDictionary(d As Dictionary(Of String, Object)) As ProbeSettings
        Dim p As New ProbeSettings()

        p.Active = ReadBool(d, "active", True)
        p.PlaceOn = If(ReadText(d, "place_on", "field") = "intensity", ProbePlaceOn.Intensity, ProbePlaceOn.Field)
        Select Case ReadText(d, "measurement", "amplitude")
            Case "intensity"
                p.Measurement = ProbeMeasurement.Intensity
            Case "integrated_intensity"
                p.Measurement = ProbeMeasurement.IntegratedIntensity
            Case Else
                p.Measurement = ProbeMeasurement.Amplitude
        End Select
        p.Kind = If(ReadText(d, "probe_type", "point") = "line", ProbeKind.Line, ProbeKind.Point)
        p.PosAX = ReadInt(d, "ax", 100, 0, 100000)
        p.PosAY = ReadInt(d, "ay", 100, 0, 100000)
        p.PosBX = ReadInt(d, "bx", 300, 0, 100000)
        p.PosBY = ReadInt(d, "by", 100, 0, 100000)
        Select Case ReadText(d, "box_size", "medium")
            Case "tiny"
                p.BoxSize = ProbeBoxSize.Tiny
            Case "small"
                p.BoxSize = ProbeBoxSize.Small
            Case "large"
                p.BoxSize = ProbeBoxSize.Large
            Case "extra_large"
                p.BoxSize = ProbeBoxSize.ExtraLarge
            Case Else
                p.BoxSize = ProbeBoxSize.Medium
        End Select
        p.BoxX = ReadInt(d, "box_x", 450, 0, 100000)
        p.BoxY = ReadInt(d, "box_y", 20, 0, 100000)
        p.ScaleMode = If(ReadText(d, "scale_mode", "running") = "full", ProbeScaleMode.Full, ProbeScaleMode.Running)
        p.AutoScaleX = ReadBool(d, "auto_scale_x", True)
        p.AutoScaleY = ReadBool(d, "auto_scale_y", True)
        p.YMin = ReadDouble(d, "y_min", -1.0, -1.0E+30, 1.0E+30)
        p.YMax = ReadDouble(d, "y_max", 1.0, -1.0E+30, 1.0E+30)
        p.RunningWindow = ReadInt(d, "running_window", DefaultRunningWindow, 10, MaxRunningWindow)
        p.UseStartAfterStep = ReadBool(d, "use_start_after_step", False)
        p.StartAfterStep = ReadInt(d, "start_after_step", 0, 0, 1000000000)
        p.Normalize()
        Return p
    End Function

    Private Shared Function ReadText(d As Dictionary(Of String, Object), key As String, fallback As String) As String
        Dim v As Object = Nothing
        If d.TryGetValue(key, v) AndAlso v IsNot Nothing Then Return Convert.ToString(v, CultureInfo.InvariantCulture)
        Return fallback
    End Function

    Private Shared Function ReadBool(d As Dictionary(Of String, Object), key As String, fallback As Boolean) As Boolean
        Dim v As Object = Nothing
        If d.TryGetValue(key, v) AndAlso TypeOf v Is Boolean Then Return Convert.ToBoolean(v)
        Return fallback
    End Function

    Private Shared Function ReadInt(d As Dictionary(Of String, Object), key As String, fallback As Integer,
                                    minValue As Integer, maxValue As Integer) As Integer
        Dim v As Object = Nothing
        If d.TryGetValue(key, v) AndAlso v IsNot Nothing Then
            Try
                Dim n As Integer = Convert.ToInt32(v, CultureInfo.InvariantCulture)
                If n >= minValue AndAlso n <= maxValue Then Return n
            Catch ex As Exception When TypeOf ex Is FormatException OrElse TypeOf ex Is InvalidCastException OrElse TypeOf ex Is OverflowException
                ' use the fallback
            End Try
        End If
        Return fallback
    End Function

    Private Shared Function ReadDouble(d As Dictionary(Of String, Object), key As String, fallback As Double,
                                       minValue As Double, maxValue As Double) As Double
        Dim v As Object = Nothing
        If d.TryGetValue(key, v) AndAlso v IsNot Nothing Then
            Try
                Dim x As Double = Convert.ToDouble(v, CultureInfo.InvariantCulture)
                If x >= minValue AndAlso x <= maxValue Then Return x
            Catch ex As Exception When TypeOf ex Is FormatException OrElse TypeOf ex Is InvalidCastException OrElse TypeOf ex Is OverflowException
                ' use the fallback
            End Try
        End If
        Return fallback
    End Function

End Class
