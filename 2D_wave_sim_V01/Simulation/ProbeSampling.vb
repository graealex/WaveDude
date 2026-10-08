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

''' <summary>The measured values of one point probe, one value per simulation step (value i belongs to step i + 1).</summary>
Friend NotInheritable Class ProbeSeries

    Private _values As Single() = New Single(1023) {}
    Private _count As Integer

    ''' <summary>The number of values.</summary>
    Public ReadOnly Property Count As Integer
        Get
            Return _count
        End Get
    End Property

    ''' <summary>The values; only the first Count entries are valid. Do not change.</summary>
    Public ReadOnly Property Values As Single()
        Get
            Return _values
        End Get
    End Property

    ''' <summary>The last value, or NaN when there is none yet.</summary>
    Public ReadOnly Property LastValue As Single
        Get
            If _count = 0 Then Return Single.NaN
            Return _values(_count - 1)
        End Get
    End Property

    Public Sub Add(items As Single())
        If items Is Nothing OrElse items.Length = 0 Then Return
        Dim needed As Integer = _count + items.Length
        If needed > _values.Length Then
            Dim bigger As Single() = New Single(Math.Max(needed, _values.Length * 2) - 1) {}
            Array.Copy(_values, bigger, _count)
            _values = bigger
        End If
        Array.Copy(items, 0, _values, _count, items.Length)
        _count = needed
    End Sub

    Public Sub Clear()
        _count = 0
    End Sub

End Class

''' <summary>
''' What the graph of one probe shows: for a point probe the series of values (one per step), for a line probe the
''' profile, the values along the line at the last update. The profile array is replaced, never changed, so it can be
''' passed from the simulation thread to the user interface.
''' </summary>
Friend NotInheritable Class ProbeTrace

    Private _profile As Single()
    Private _profileLength As Double

    Public ReadOnly Series As New ProbeSeries()

    ''' <summary>The simulation step of the last update (the last SampleProbes/collect that reached this trace). Used to hold
    ''' back the graph until "start after step" is reached.</summary>
    Public Property StepsSampled As Integer

    ''' <summary>The values along the line from A to B (one per pixel), or Nothing when there is none yet.</summary>
    Public ReadOnly Property Profile As Single()
        Get
            Return _profile
        End Get
    End Property

    ''' <summary>The length of the line in pixels (the profile is drawn from 0 to this length).</summary>
    Public ReadOnly Property ProfileLength As Double
        Get
            Return _profileLength
        End Get
    End Property

    Public Sub SetProfile(values As Single(), length As Double)
        _profile = values
        _profileLength = length
    End Sub

End Class

''' <summary>New measurements for the user interface: per probe the new values of a point probe and the last profile of a line probe.</summary>
Friend NotInheritable Class ProbeUpdate

    ''' <summary>The new values of the point probes (empty for a line probe).</summary>
    Public ReadOnly Samples As Single()()

    ''' <summary>The last profile of the line probes; Nothing for a point probe or when the profile did not change.</summary>
    Public ReadOnly Profiles As Single()()

    ''' <summary>The length in pixels of the line of every probe (0 for a point).</summary>
    Public ReadOnly Lengths As Double()

    ''' <summary>The simulation step this update was measured at.</summary>
    Public ReadOnly StepsDone As Integer

    Public Sub New(samples As Single()(), profiles As Single()(), lengths As Double(), stepsDone As Integer)
        Me.Samples = samples
        Me.Profiles = profiles
        Me.Lengths = lengths
        Me.StepsDone = stepsDone
    End Sub

End Class

''' <summary>
''' What a simulation engine has to measure for the probes. For every probe the pixels (as indices into the padded
''' field arrays, see IWaveEngine) and what is read there: the field or the intensity.
'''   Point probe                one pixel; the engine measures its value at every step (a series).
'''   Point, integrated          the same, but the values are added up over time (done by ProbeCollector).
'''   Line probe                 the pixels on the line A-B; the engine gives the values along the line when asked (a profile).
'''   Line, integrated           the engine adds the intensity of every pixel of the line up over the steps.
''' </summary>
Friend NotInheritable Class ProbeSampler

    Public ReadOnly Count As Integer
    Public ReadOnly Pixels As Integer()()
    Public ReadOnly IsProfile As Boolean()
    Public ReadOnly UseIntensity As Boolean()
    Public ReadOnly Cumulative As Boolean()
    Public ReadOnly Integrate As Boolean()
    Public ReadOnly Lengths As Double()
    Public ReadOnly NeedsIntensity As Boolean
    Public ReadOnly HasSeries As Boolean
    Public ReadOnly HasProfiles As Boolean

    ''' <summary>Per probe: whether "start recording at step #" is on, and that step. Used to purge (never accumulate)
    ''' the running total of an integrated (cumulative) probe from before that step, so the total shown once recording
    ''' starts is not inflated by intensity that arrived before it - see ProbeCollector.Collect, WaveSimulator.SampleProbes
    ''' and GpuWaveSimulator's profile_accumulate kernel.</summary>
    Public ReadOnly UseStartAfterStep As Boolean()
    Public ReadOnly StartAfterStep As Integer()

    Private Sub New(pixels As Integer()(), isProfile As Boolean(), useIntensity As Boolean(), cumulative As Boolean(),
                    integrate As Boolean(), lengths As Double(), useStartAfterStep As Boolean(), startAfterStep As Integer())
        Count = pixels.Length
        Me.Pixels = pixels
        Me.IsProfile = isProfile
        Me.UseIntensity = useIntensity
        Me.Cumulative = cumulative
        Me.Integrate = integrate
        Me.Lengths = lengths
        Me.UseStartAfterStep = useStartAfterStep
        Me.StartAfterStep = startAfterStep
        For i As Integer = 0 To Count - 1
            If useIntensity(i) Then NeedsIntensity = True
            If isProfile(i) Then
                HasProfiles = True
            Else
                HasSeries = True
            End If
        Next
    End Sub

    ''' <summary>Builds the sampler for a canvas of w x h pixels. Positions outside the canvas are moved onto it.</summary>
    Public Shared Function Build(probes As IList(Of ProbeSettings), w As Integer, h As Integer) As ProbeSampler
        Dim n As Integer = probes.Count
        Dim pixels As Integer()() = New Integer(n - 1)() {}
        Dim isProfile(n - 1) As Boolean
        Dim useIntensity(n - 1) As Boolean
        Dim cumulative(n - 1) As Boolean
        Dim integrate(n - 1) As Boolean
        Dim lengths(n - 1) As Double
        Dim useStartAfterStep(n - 1) As Boolean
        Dim startAfterStep(n - 1) As Integer
        Dim stride As Integer = w + 2

        For i As Integer = 0 To n - 1
            Dim p As ProbeSettings = probes(i)
            Dim ax As Integer = Clamp(p.PosAX, w - 1)
            Dim ay As Integer = Clamp(p.PosAY, h - 1)
            Dim integrated As Boolean = (p.Measurement = ProbeMeasurement.IntegratedIntensity)

            If p.Kind = ProbeKind.Line Then
                Dim endX As Integer = Clamp(p.PosBX, w - 1)
                Dim endY As Integer = Clamp(p.PosBY, h - 1)
                pixels(i) = LinePixels(ax, ay, endX, endY, stride)
                isProfile(i) = True
                integrate(i) = integrated
                Dim dx As Double = endX - ax
                Dim dy As Double = endY - ay
                lengths(i) = Math.Sqrt(dx * dx + dy * dy)
            Else
                pixels(i) = New Integer() {(ay + 1) * stride + ax + 1}
                cumulative(i) = integrated
            End If

            useIntensity(i) = (p.Measurement <> ProbeMeasurement.Amplitude)
            useStartAfterStep(i) = p.UseStartAfterStep
            startAfterStep(i) = p.StartAfterStep
        Next

        Return New ProbeSampler(pixels, isProfile, useIntensity, cumulative, integrate, lengths, useStartAfterStep, startAfterStep)
    End Function

    Private Shared Function Clamp(value As Integer, maxValue As Integer) As Integer
        If value < 0 Then Return 0
        If value > maxValue Then Return maxValue
        Return value
    End Function

    ''' <summary>The pixels on the line from (x0, y0) to (x1, y1), as indices into the padded arrays.</summary>
    Private Shared Function LinePixels(x0 As Integer, y0 As Integer, x1 As Integer, y1 As Integer, stride As Integer) As Integer()
        Dim result As New List(Of Integer)()
        Dim dx As Integer = Math.Abs(x1 - x0)
        Dim dy As Integer = -Math.Abs(y1 - y0)
        Dim sx As Integer = If(x0 < x1, 1, -1)
        Dim sy As Integer = If(y0 < y1, 1, -1)
        Dim diff As Integer = dx + dy

        Do
            result.Add((y0 + 1) * stride + x0 + 1)
            If x0 = x1 AndAlso y0 = y1 Then Exit Do
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
        Return result.ToArray()
    End Function

    ''' <summary>The value of a point probe in the given data (the field or the intensity array of an engine).</summary>
    Public Function Compute(probeIndex As Integer, data As Single()) As Single
        If data Is Nothing Then Return 0.0F
        Return data(Pixels(probeIndex)(0))
    End Function

End Class

''' <summary>
''' Collects the measurements of an engine: the series of the point probes and the profiles of the line probes
''' (for the graphs in the exported videos), and the new values for the user interface.
''' </summary>
Friend NotInheritable Class ProbeCollector

    Public ReadOnly Traces As ProbeTrace()

    Private ReadOnly _sampler As ProbeSampler
    Private ReadOnly _totals As Double()
    Private ReadOnly _pending As New List(Of List(Of Single))()
    Private ReadOnly _fresh As Boolean()
    Private _lastStepsDone As Integer

    Public Sub New(sampler As ProbeSampler)
        _sampler = sampler
        Traces = New ProbeTrace(sampler.Count - 1) {}
        _totals = New Double(sampler.Count - 1) {}
        _fresh = New Boolean(sampler.Count - 1) {}
        For i As Integer = 0 To sampler.Count - 1
            Traces(i) = New ProbeTrace()
            _pending.Add(New List(Of Single)())
        Next
    End Sub

    ''' <summary>Takes the values that the engine measured for the point probes since the last call.</summary>
    Public Sub Collect(engine As IWaveEngine, stepsDone As Integer)
        If Not _sampler.HasSeries Then Return

        Dim raw As Single()() = engine.TakeProbeSamples()
        ' the number of steps already processed before this batch, so the absolute step of values(k) can be worked
        ' out below (one value per step, in order) - needed to purge a cumulative total from before "start after step"
        Dim batchStartStep As Integer = _lastStepsDone
        For p As Integer = 0 To _sampler.Count - 1
            If _sampler.IsProfile(p) OrElse p >= raw.Length Then Continue For
            Dim values As Single() = raw(p)
            If values Is Nothing OrElse values.Length = 0 Then Continue For

            If _sampler.Cumulative(p) Then
                Dim useStart As Boolean = _sampler.UseStartAfterStep(p)
                Dim startStep As Integer = _sampler.StartAfterStep(p)
                For k As Integer = 0 To values.Length - 1
                    If useStart AndAlso (batchStartStep + k + 1) < startStep Then
                        ' before recording starts: keep the running total at zero, purging any intensity that
                        ' arrived earlier, so the total does not jump the moment recording starts
                        _totals(p) = 0.0
                        values(k) = 0.0F
                    Else
                        _totals(p) += values(k)
                        values(k) = CSng(_totals(p))
                    End If
                Next
            End If

            Traces(p).Series.Add(values)
            Traces(p).StepsSampled = stepsDone
            _pending(p).AddRange(values)
        Next
        _lastStepsDone = stepsDone
    End Sub

    ''' <summary>Takes the profiles of the line probes in the current state of the engine.</summary>
    Public Sub CollectProfiles(engine As IWaveEngine, stepsDone As Integer)
        If Not _sampler.HasProfiles Then Return

        Dim raw As Single()() = engine.ReadProbeProfiles()
        For p As Integer = 0 To _sampler.Count - 1
            If Not _sampler.IsProfile(p) OrElse p >= raw.Length OrElse raw(p) Is Nothing Then Continue For
            Traces(p).SetProfile(raw(p), _sampler.Lengths(p))
            Traces(p).StepsSampled = stepsDone
            _fresh(p) = True
        Next
        _lastStepsDone = stepsDone
    End Sub

    ''' <summary>What is new since the last call (for the user interface).</summary>
    Public Function TakeForUi() As ProbeUpdate
        Dim n As Integer = _sampler.Count
        Dim samples As Single()() = New Single(n - 1)() {}
        Dim profiles As Single()() = New Single(n - 1)() {}
        For p As Integer = 0 To n - 1
            samples(p) = _pending(p).ToArray()
            _pending(p).Clear()
            If _fresh(p) Then
                profiles(p) = Traces(p).Profile
                _fresh(p) = False
            End If
        Next
        Return New ProbeUpdate(samples, profiles, _sampler.Lengths, _lastStepsDone)
    End Function

End Class
