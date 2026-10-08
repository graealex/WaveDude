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

''' <summary>Whether a source is one point (or line, in a future version) or an X-Y array of points.</summary>
Public Enum SourceMainType
    ''' <summary>One point source. Named Simple (not Single) because Single is a reserved VB keyword (the floating-point type).</summary>
    Simple = 0
    Composite = 1
End Enum

''' <summary>Whether a source (or an element of a composite source) is always on, or switches on and off in cycles of its own frequency.</summary>
Public Enum SourceMode
    Continuous = 0
    Pulsed = 1
End Enum

''' <summary>The shape of the amplitude envelope of a pulsed source while it is "on".</summary>
Public Enum SourcePulseShape
    Gaussian = 0
    SmoothedSquare = 1
End Enum

''' <summary>How the elements of a composite source get their frequency/phase/off-width/position: all the same as the
''' single source, or randomized around it.</summary>
Public Enum SourceCompositeMode
    InheritFromSingle = 0
    InheritAndRandomize = 1
End Enum

''' <summary>
''' One concrete point source, as it is actually run by the simulation: the result of "Generate composite source" for
''' every element of the array (or the only element of a simple source). Generating rolls the randomization once, so
''' a run is reproducible: the engine never re-randomizes.
''' </summary>
Public NotInheritable Class SourceElement
    Public Property PosX As Integer
    Public Property PosY As Integer

    ''' <summary>The frequency, expressed the same way as an image source's green level (1-255): frequency = level / 255 * 0.5 * SourceFrequencyScale.</summary>
    Public Property Level As Integer = 100

    ''' <summary>0 (silent) to 1 (full scale).</summary>
    Public Property Amplitude As Double = 0.5

    Public Property PhaseRad As Double = 0.0

    Public Property Mode As SourceMode = SourceMode.Continuous
    Public Property PulseShape As SourcePulseShape = SourcePulseShape.Gaussian

    ''' <summary>The number of cycles (of the source's own frequency) that it is off, then on, then off again, ...</summary>
    Public Property OnWidthCycles As Integer = 10
    Public Property OffWidthCycles As Integer = 10

    ''' <summary>The un-randomized off-width (the composite source's own OffStateCycles at generate time), used as
    ''' the anchor every time OffWidthCycles is re-randomized at runtime. Equal to OffWidthCycles unless
    ''' RandomizeOffWidthAmount > 0.</summary>
    Public Property OffWidthBase As Integer = 10

    ''' <summary>When greater than 0, the engine redraws OffWidthCycles (uniformly within +/- this amount of
    ''' OffWidthBase) every time this element completes a full off+on cycle, so sources don't stay in lockstep or
    ''' permanently favor one extreme. 0 means the off-width set at generate time is never changed again.</summary>
    Public Property RandomizeOffWidthAmount As Integer = 0

    ''' <summary>The simulation step at which this element stops driving the field (releasing its hold on it, so it
    ''' no longer diffracts waves passing through its position), or -1 to never detach. Copied from the parent
    ''' SourceSettings' DetachSource/DetachAfterSteps at generate time.</summary>
    Public Property DetachAfterStep As Integer = -1

    ''' <summary>
    ''' The number of simulation steps over which a detaching element's drive is faded out (linearly, down to 0),
    ''' rather than cut instantly at DetachAfterStep - an abrupt cutoff otherwise leaves a small kink in the forcing
    ''' at that point, which can ring. Shared by both engines (WaveSimulator.ApplySources and the
    ''' apply_object_sources GPU kernel in GpuWaveSimulator), so the two stay in exact agreement.
    ''' </summary>
    Public Const DetachRampSteps As Integer = 30

    Public Function Clone() As SourceElement
        Return DirectCast(MemberwiseClone(), SourceElement)
    End Function
End Class

''' <summary>
''' One source of the project: either a single point source, or a composite source (an X-Y array of point sources
''' built from the same base parameters, optionally randomized). "Generate composite source" resolves the current
''' parameters (and, for a composite source, a fresh random draw) into a fixed list of SourceElement, which is what
''' the simulation actually runs; "Save source" keeps both the authoring parameters (so the source can be reopened
''' and edited or regenerated) and that generated list (so a saved project runs exactly what was generated, even
''' before it is regenerated again). The sources are stored in the section "object_sources" of configuration.json.
''' </summary>
Public NotInheritable Class SourceSettings

    Public Const SectionName As String = "object_sources"

    Public Property Name As String = "Source 1"
    Public Property MainType As SourceMainType = SourceMainType.Simple

    ''' <summary>The position of a simple source, or the center of a composite source's array, in simulation pixels.
    ''' PosX/PosY only hold a meaningful value once HasPosition is True (picked in the picture, or typed) - until
    ''' then they are just the unset default, and the Sources tab must not draw a marker for it.</summary>
    Public Property PosX As Integer = 100
    Public Property PosY As Integer = 100

    ''' <summary>Whether PosX/PosY have actually been assigned (by picking a position in the picture, or by typing
    ''' one) rather than still holding the default. Sources loaded from a project saved before this flag existed
    ''' default to True (see FromDictionary), since they were positioned under the old rules.</summary>
    Public Property HasPosition As Boolean = False

    ''' <summary>Whether the source interacts with the simulation at all. Off by default is never true - a new
    ''' source starts on. Switching it off keeps every parameter (it can still be edited, and switched back on)
    ''' but the source takes no part in a run: SourcesForRun leaves it out entirely, so it neither drives the
    ''' field nor gets in the way of waves passing through its position.</summary>
    Public Property SourceOn As Boolean = True

    ''' <summary>When True, the source stops driving the field after DetachAfterSteps simulation steps (it runs
    ''' normally up to that point). A source that keeps driving the field also holds it at a fixed value there,
    ''' which diffracts waves that later pass through that point; detaching releases that hold once the source
    ''' has done its job, so the field becomes free of it again for the rest of the run.</summary>
    Public Property DetachSource As Boolean = False

    ''' <summary>The simulation step (0-based) at which the source detaches, when DetachSource is True.</summary>
    Public Property DetachAfterSteps As Integer = 1000

    ' ---- single source (base) properties

    ''' <summary>1-255, the same scale as an image source's green level.</summary>
    Public Property Frequency As Integer = 100

    ''' <summary>0-100 (%) of the source amplitude; 100 matches numAmplitude's own Designer default for a new source.</summary>
    Public Property Amplitude As Integer = 100

    ''' <summary>-180 to 180 degrees.</summary>
    Public Property PhaseDeg As Integer = 0

    Public Property Mode As SourceMode = SourceMode.Continuous
    Public Property PulseShape As SourcePulseShape = SourcePulseShape.Gaussian
    Public Property PulseWidthCycles As Integer = 10
    Public Property OffStateCycles As Integer = 10

    ' ---- composite source properties

    Public Property ArraySizeX As Integer = 1
    Public Property ArraySizeY As Integer = 1
    Public Property SpacingX As Integer = 10
    Public Property SpacingY As Integer = 10
    Public Property CompositeMode As SourceCompositeMode = SourceCompositeMode.InheritFromSingle

    Public Property RandomizeFrequency As Boolean = False
    Public Property RandomizeFrequencyAmount As Integer = 0
    Public Property RandomizePhase As Boolean = False
    Public Property RandomizePhaseAmount As Integer = 0
    Public Property RandomizeOffWidth As Boolean = False
    Public Property RandomizeOffWidthAmount As Integer = 0
    Public Property RandomizePosition As Boolean = False
    Public Property RandomizePositionAmount As Integer = 0

    ''' <summary>The concrete elements produced by the last "Generate composite source" (one element for a simple source). Empty until generated.</summary>
    Public Property Elements As New List(Of SourceElement)()

    Public Function Clone() As SourceSettings
        Dim c As SourceSettings = DirectCast(MemberwiseClone(), SourceSettings)
        c.Elements = New List(Of SourceElement)()
        For Each e As SourceElement In Elements
            c.Elements.Add(e.Clone())
        Next
        Return c
    End Function

    Public Sub Normalize()
        If Frequency < 1 Then Frequency = 1
        If Frequency > 255 Then Frequency = 255
        If Amplitude < 0 Then Amplitude = 0
        If Amplitude > 100 Then Amplitude = 100
        If PhaseDeg < -180 Then PhaseDeg = -180
        If PhaseDeg > 180 Then PhaseDeg = 180
        If PulseWidthCycles < 1 Then PulseWidthCycles = 1
        If OffStateCycles < 1 Then OffStateCycles = 1
        If ArraySizeX < 1 Then ArraySizeX = 1
        If ArraySizeY < 1 Then ArraySizeY = 1
        If DetachAfterSteps < 0 Then DetachAfterSteps = 0
    End Sub

    ''' <summary>Rebuilds Elements from the current parameters: one element for a simple source, or an ArraySizeX x
    ''' ArraySizeY grid centered on (PosX, PosY) for a composite source, randomized once per element when
    ''' CompositeMode is InheritAndRandomize and the matching "Randomize" check box is on.</summary>
    Public Sub GenerateElements(rng As Random)
        Normalize()
        Dim result As New List(Of SourceElement)()

        If MainType = SourceMainType.Simple Then
            result.Add(MakeBaseElement(PosX, PosY))
        Else
            Dim cols As Integer = ArraySizeX
            Dim rows As Integer = ArraySizeY
            Dim randomize As Boolean = (CompositeMode = SourceCompositeMode.InheritAndRandomize)

            For row As Integer = 0 To rows - 1
                For col As Integer = 0 To cols - 1
                    ' the array is centered on (PosX, PosY), not anchored at a corner
                    Dim ex As Integer = PosX + CInt(Math.Round((col - (cols - 1) / 2.0) * SpacingX))
                    Dim ey As Integer = PosY + CInt(Math.Round((row - (rows - 1) / 2.0) * SpacingY))

                    If randomize AndAlso RandomizePosition AndAlso RandomizePositionAmount > 0 Then
                        ex += RandomInt(rng, RandomizePositionAmount)
                        ey += RandomInt(rng, RandomizePositionAmount)
                    End If

                    Dim el As SourceElement = MakeBaseElement(ex, ey)

                    If randomize AndAlso RandomizeFrequency AndAlso RandomizeFrequencyAmount > 0 Then
                        el.Level = Clamp(Frequency + RandomInt(rng, RandomizeFrequencyAmount), 1, 255)
                    End If
                    If randomize AndAlso RandomizePhase AndAlso RandomizePhaseAmount > 0 Then
                        Dim deg As Integer = Clamp(PhaseDeg + RandomInt(rng, RandomizePhaseAmount), -180, 180)
                        el.PhaseRad = deg * Math.PI / 180.0
                    End If
                    If randomize AndAlso RandomizeOffWidth AndAlso RandomizeOffWidthAmount > 0 Then
                        el.OffWidthCycles = Math.Max(1, OffStateCycles + RandomInt(rng, RandomizeOffWidthAmount))
                        el.RandomizeOffWidthAmount = RandomizeOffWidthAmount
                    End If

                    result.Add(el)
                Next
            Next
        End If

        Elements = result
    End Sub

    Private Function MakeBaseElement(x As Integer, y As Integer) As SourceElement
        Dim el As New SourceElement()
        el.PosX = x
        el.PosY = y
        el.Level = Frequency
        el.Amplitude = Amplitude / 100.0
        el.PhaseRad = PhaseDeg * Math.PI / 180.0
        el.Mode = Mode
        el.PulseShape = PulseShape
        el.OnWidthCycles = PulseWidthCycles
        el.OffWidthCycles = OffStateCycles
        el.OffWidthBase = OffStateCycles
        el.RandomizeOffWidthAmount = 0
        el.DetachAfterStep = If(DetachSource, DetachAfterSteps, -1)
        Return el
    End Function

    ''' <summary>A uniform random integer in [-amount, amount].</summary>
    Friend Shared Function RandomInt(rng As Random, amount As Integer) As Integer
        If amount <= 0 Then Return 0
        Return rng.Next(-amount, amount + 1)
    End Function

    Private Shared Function Clamp(value As Integer, minValue As Integer, maxValue As Integer) As Integer
        Return Math.Max(minValue, Math.Min(maxValue, value))
    End Function

    ' ---------------------------------------------------------------- configuration file

    Public Shared Function ListFromConfig(config As ProjectConfig) As List(Of SourceSettings)
        Dim result As New List(Of SourceSettings)()

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

        Return result
    End Function

    Public Shared Sub ListToConfig(sources As List(Of SourceSettings), config As ProjectConfig)
        Dim section As Dictionary(Of String, Object) = config.GetSection(SectionName)
        Dim items As New List(Of Object)()
        For Each s As SourceSettings In sources
            items.Add(s.ToDictionary())
        Next
        section("count") = items.Count
        section("items") = items
    End Sub

    Private Function ToDictionary() As Dictionary(Of String, Object)
        Dim d As New Dictionary(Of String, Object)()
        d("name") = Name
        d("main_type") = If(MainType = SourceMainType.Composite, "composite", "single")
        d("x") = PosX
        d("y") = PosY
        d("has_position") = HasPosition
        d("source_on") = SourceOn
        d("detach_source") = DetachSource
        d("detach_after_steps") = DetachAfterSteps
        d("frequency") = Frequency
        d("amplitude") = Amplitude
        d("phase_deg") = PhaseDeg
        d("mode") = If(Mode = SourceMode.Pulsed, "pulsed", "continuous")
        d("pulse_shape") = If(PulseShape = SourcePulseShape.SmoothedSquare, "smoothed_square", "gaussian")
        d("pulse_width_cycles") = PulseWidthCycles
        d("off_state_cycles") = OffStateCycles
        d("array_size_x") = ArraySizeX
        d("array_size_y") = ArraySizeY
        d("spacing_x") = SpacingX
        d("spacing_y") = SpacingY
        d("composite_mode") = If(CompositeMode = SourceCompositeMode.InheritAndRandomize, "randomize", "inherit")
        d("randomize_frequency") = RandomizeFrequency
        d("randomize_frequency_amount") = RandomizeFrequencyAmount
        d("randomize_phase") = RandomizePhase
        d("randomize_phase_amount") = RandomizePhaseAmount
        d("randomize_off_width") = RandomizeOffWidth
        d("randomize_off_width_amount") = RandomizeOffWidthAmount
        d("randomize_position") = RandomizePosition
        d("randomize_position_amount") = RandomizePositionAmount

        Dim elementList As New List(Of Object)()
        For Each el As SourceElement In Elements
            Dim ed As New Dictionary(Of String, Object)()
            ed("x") = el.PosX
            ed("y") = el.PosY
            ed("level") = el.Level
            ed("amplitude") = el.Amplitude
            ed("phase_rad") = el.PhaseRad
            ed("mode") = If(el.Mode = SourceMode.Pulsed, "pulsed", "continuous")
            ed("pulse_shape") = If(el.PulseShape = SourcePulseShape.SmoothedSquare, "smoothed_square", "gaussian")
            ed("on_width_cycles") = el.OnWidthCycles
            ed("off_width_cycles") = el.OffWidthCycles
            ed("off_width_base") = el.OffWidthBase
            ed("randomize_off_width_amount") = el.RandomizeOffWidthAmount
            ed("detach_after_step") = el.DetachAfterStep
            elementList.Add(ed)
        Next
        d("elements") = elementList

        Return d
    End Function

    Private Shared Function FromDictionary(d As Dictionary(Of String, Object)) As SourceSettings
        Dim s As New SourceSettings()

        s.Name = ReadText(d, "name", s.Name)
        s.MainType = If(ReadText(d, "main_type", "single") = "composite", SourceMainType.Composite, SourceMainType.Simple)
        s.PosX = ReadInt(d, "x", s.PosX, -1000000, 1000000)
        s.PosY = ReadInt(d, "y", s.PosY, -1000000, 1000000)
        ' a project saved before HasPosition existed has no "has_position" entry - default to True, since its
        ' sources were positioned under the old rules (there was no "unset" state back then)
        s.HasPosition = ReadBool(d, "has_position", True)
        ' a project saved before SourceOn/DetachSource existed has no entries for them either - default to the
        ' same behavior it always had: on, and never detaching
        s.SourceOn = ReadBool(d, "source_on", True)
        s.DetachSource = ReadBool(d, "detach_source", False)
        s.DetachAfterSteps = ReadInt(d, "detach_after_steps", s.DetachAfterSteps, 0, 1000000000)
        s.Frequency = ReadInt(d, "frequency", s.Frequency, 1, 255)
        s.Amplitude = ReadInt(d, "amplitude", s.Amplitude, 0, 100)
        s.PhaseDeg = ReadInt(d, "phase_deg", s.PhaseDeg, -180, 180)
        s.Mode = If(ReadText(d, "mode", "continuous") = "pulsed", SourceMode.Pulsed, SourceMode.Continuous)
        s.PulseShape = If(ReadText(d, "pulse_shape", "gaussian") = "smoothed_square", SourcePulseShape.SmoothedSquare, SourcePulseShape.Gaussian)
        s.PulseWidthCycles = ReadInt(d, "pulse_width_cycles", s.PulseWidthCycles, 1, 1000000)
        s.OffStateCycles = ReadInt(d, "off_state_cycles", s.OffStateCycles, 1, 1000000)
        s.ArraySizeX = ReadInt(d, "array_size_x", s.ArraySizeX, 1, 100000)
        s.ArraySizeY = ReadInt(d, "array_size_y", s.ArraySizeY, 1, 100000)
        s.SpacingX = ReadInt(d, "spacing_x", s.SpacingX, -1000000, 1000000)
        s.SpacingY = ReadInt(d, "spacing_y", s.SpacingY, -1000000, 1000000)
        s.CompositeMode = If(ReadText(d, "composite_mode", "inherit") = "randomize", SourceCompositeMode.InheritAndRandomize, SourceCompositeMode.InheritFromSingle)
        s.RandomizeFrequency = ReadBool(d, "randomize_frequency", False)
        s.RandomizeFrequencyAmount = ReadInt(d, "randomize_frequency_amount", 0, 0, 1000000)
        s.RandomizePhase = ReadBool(d, "randomize_phase", False)
        s.RandomizePhaseAmount = ReadInt(d, "randomize_phase_amount", 0, 0, 1000000)
        s.RandomizeOffWidth = ReadBool(d, "randomize_off_width", False)
        s.RandomizeOffWidthAmount = ReadInt(d, "randomize_off_width_amount", 0, 0, 1000000)
        s.RandomizePosition = ReadBool(d, "randomize_position", False)
        s.RandomizePositionAmount = ReadInt(d, "randomize_position_amount", 0, 0, 1000000)

        Dim elements As New List(Of SourceElement)()
        Dim elementsObj As Object = Nothing
        If d.TryGetValue("elements", elementsObj) Then
            Dim list As IEnumerable = TryCast(elementsObj, IEnumerable)
            If list IsNot Nothing AndAlso Not TypeOf elementsObj Is String Then
                For Each item As Object In list
                    Dim ed As Dictionary(Of String, Object) = TryCast(item, Dictionary(Of String, Object))
                    If ed Is Nothing Then Continue For
                    Dim el As New SourceElement()
                    el.PosX = ReadInt(ed, "x", 0, -1000000, 1000000)
                    el.PosY = ReadInt(ed, "y", 0, -1000000, 1000000)
                    el.Level = ReadInt(ed, "level", 100, 1, 255)
                    el.Amplitude = ReadDouble(ed, "amplitude", 0.5, 0.0, 1.0)
                    el.PhaseRad = ReadDouble(ed, "phase_rad", 0.0, -1000.0, 1000.0)
                    el.Mode = If(ReadText(ed, "mode", "continuous") = "pulsed", SourceMode.Pulsed, SourceMode.Continuous)
                    el.PulseShape = If(ReadText(ed, "pulse_shape", "gaussian") = "smoothed_square", SourcePulseShape.SmoothedSquare, SourcePulseShape.Gaussian)
                    el.OnWidthCycles = ReadInt(ed, "on_width_cycles", 10, 1, 1000000)
                    el.OffWidthCycles = ReadInt(ed, "off_width_cycles", 10, 1, 1000000)
                    ' a project saved before off-width re-randomization existed has no "off_width_base" entry -
                    ' default to this element's own off_width_cycles (already read above), which is exactly its
                    ' anchor under the old, always-static behavior
                    el.OffWidthBase = ReadInt(ed, "off_width_base", el.OffWidthCycles, 1, 1000000)
                    el.RandomizeOffWidthAmount = ReadInt(ed, "randomize_off_width_amount", 0, 0, 1000000)
                    el.DetachAfterStep = ReadInt(ed, "detach_after_step", -1, -1, 1000000000)
                    elements.Add(el)
                Next
            End If
        End If
        s.Elements = elements

        s.Normalize()
        Return s
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
