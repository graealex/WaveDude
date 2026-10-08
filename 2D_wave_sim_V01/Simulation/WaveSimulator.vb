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
Imports System.Threading.Tasks

''' <summary>
''' The 2D wave equation on the CPU, a port of the Python class WaveSimulator2D (which runs on the GPU with CuPy):
'''   laplacian = 3x3 convolution with [[0.066, 0.184, 0.066], [0.184, -1, 0.184], [0.066, 0.184, 0.066]]
'''   new u = u + (u - previous u) * d + laplacian * c^2
''' with the field outside the domain fixed at 0. The sources are applied before each step as in
''' update_scene / StaticImageScene.update_field:
'''   u = u * 0.9 + sin(frequency * t) * 0.1
''' The intensity is no longer a single exponential average of WaveVisualizer.update (I = I * 0.98 + u^2 * 0.02):
''' a single-pole filter like that only rolls off at 6 dB/octave, so it never fully removes the ripple at the
''' source's own oscillation frequency, just delays it - lengthening its time constant mostly adds trail without
''' buying much extra ripple suppression. Instead the same one-pole filter (I = I * alpha + x * (1 - alpha)) is run
''' twice in a row (_intensityStage1, then _intensity), a critically-damped 2-pole low-pass that rolls off at
''' 12 dB/octave, so for the same responsiveness it suppresses the ripple far more without needing to know the
''' local frequency at every pixel. Two full stages turned out to add more delay than wanted at low frequencies
''' (a short pulse can come and go before the second stage has caught up, understating its peak) for not that
''' much extra benefit over one, so what is actually shown/exported (_intensityOut) is a blend of the one-stage
''' and two-stage outputs (IntensityBlend) - roughly the middle ground between the two (about 9, not 6 or 12,
''' dB/octave), still well ahead of a single stage at rejecting the ripple but reacting closer to as fast.
''' alpha (_intensityAlpha) is derived once per simulation so the time constant of each stage scales inversely
''' with the frequency of the slowest active source, times the user's TimeDecayFactor - see that property's
''' comment (SimulationSettings.vb) for why.
'''
''' The arrays have a border of one cell (which always stays 0) so that the step needs no edge tests.
''' The rows are divided over all processor cores.
'''
''' Optionally (Use4thOrderLaplacian), the Laplacian is instead computed with a 4th order accurate, 5-point
''' "cross" stencil (reaching 2 cells away along x and along y) wherever there is room for it - see UpdateField.
''' This reduces numerical dispersion (short, under-resolved wavelengths otherwise travel slower than long ones -
''' see the SimulationSettings.WaveSpeedC comment) at roughly twice the memory reads per step. The outermost ring
''' of the domain still uses the original 3x3 kernel, since it only has the usual one cell of border to read from,
''' not the two the wider stencil needs.
'''
''' The 4th order stencil's coefficients (-1/12, 4/3, -5) reach much further from zero than the 3x3 kernel's
''' (0.066, 0.184, -1) - about 7 times further at the highest spatial frequency the grid can represent - so the
''' same c^2 that is safely stable with the 3x3 kernel can be wildly unstable with the 4th order one (the field
''' blows up: everywhere saturates except at object edges, which is what an invalid/unstable Laplacian looks like).
''' To stay stable without changing the physical wave speed, AdvanceOneStep instead takes _subSteps smaller time
''' steps per call when the 4th order stencil is used: c^2 is scaled by 1 / _subSteps^2 (matching a time step
''' _subSteps times smaller) and the damping factor d by its _subSteps-th root (so _subSteps applications of it
''' compound back to the original d), so the net effect after _subSteps steps is exactly the same physical
''' propagation and damping as one ordinary step - just spread over more, individually stable, time steps.
''' _subSteps is computed once per scene from its highest c^2 (ComputeSubSteps) and is 1 (no sub-stepping, and a
''' multiply by the exact literal 1.0F) whenever the 3x3 kernel is used or the scene's c^2 does not need it.
''' </summary>
Friend NotInheritable Class WaveSimulator
    Implements IWaveEngine

    Private Const KernelCorner As Single = 0.066F
    Private Const KernelEdge As Single = 0.184F
    Private Const SourceOpacity As Single = 0.9F
    ' nominal source level (of 255) that TimeDecayFactor's steps count is calibrated against - see the property's
    ' comment (SimulationSettings.vb) and _intensityAlpha's computation below
    Private Const NominalDecayLevel As Integer = 100
    ' how much of the fully cascaded (2-stage, 12 dB/octave) intensity is blended into the shown/exported value
    ' versus the 1-stage (6 dB/octave) intensity - see the class summary; 0.5 is the middle ground, about 9 dB/octave
    Private Const IntensityBlend As Single = 0.5F

    ' the 4th order "cross" stencil's coefficients: (-f(-2) + 16f(-1) - 30f(0) + 16f(+1) - f(+2)) / 12 per axis,
    ' the two axes' center terms combined into one (-60/12 = -5) since both apply to the same center pixel
    Private Const FourthOrderNear As Single = 4.0F / 3.0F      ' 16 / 12
    Private Const FourthOrderFar As Single = -1.0F / 12.0F
    Private Const FourthOrderCenter As Single = -5.0F          ' -60 / 12

    ' the 4th order stencil's largest |eigenvalue| (at the grid's highest spatial frequency, kx = ky = pi):
    ' 2 * (-1/12 * cos(2k) + 4/3 * cos(k) - 5/2)|k=pi per axis, summed over x and y = -32/3
    Private Const FourthOrderLambdaMax As Single = 32.0F / 3.0F
    ' how far below the hard stability limit (c^2 * |lambda max| <= 4) sub-stepping aims to stay - chosen to give
    ' roughly the same safety margin the 3x3 kernel already has in practice (its own c^2 * |lambda max| is about
    ' 1.8 at the highest c^2 the app allows, i.e. about 2.2 times under the limit of 4)
    Private Const SubStepTargetLimit As Single = 2.0F

    Private ReadOnly _width As Integer
    Private ReadOnly _height As Integer
    Private ReadOnly _stride As Integer
    Private ReadOnly _use4thOrderLaplacian As Boolean
    ' see the class summary; 1 (no sub-stepping) unless Use4thOrderLaplacian needs otherwise for this scene's c^2
    Private ReadOnly _subSteps As Integer
    ' the d array used by UpdateField: the original _d itself when _subSteps = 1 (same reference, so multiplying by
    ' it is bit-for-bit identical to before), otherwise a separate array of _d raised to the power 1 / _subSteps
    Private ReadOnly _dEffective As Single()
    ' 1.0F / _subSteps^2 (or exactly 1.0F when _subSteps = 1, a bit-for-bit no-op multiply)
    Private ReadOnly _c2Mult As Single
    ' the intensity's exponential-average decay factor for this simulation (replaces the old fixed 0.98F) - see
    ' TimeDecayFactor's comment (SimulationSettings.vb) and its computation at the end of the constructor
    Private ReadOnly _intensityAlpha As Single
    ' SimulationSettings.IntensityScale - see that property's comment
    Private ReadOnly _intensityScale As Single

    Private _u As Single()
    Private _uPrev As Single()
    Private ReadOnly _c2 As Single()
    Private ReadOnly _d As Single()
    ' the 2-stage cascade's internal recursive state (12 dB/octave) - no longer what is shown/exported, see
    ' _intensityOut below and the class summary
    Private ReadOnly _intensity As Single()
    ' the first of the two cascaded exponential-filter stages that make up the intensity (see the class summary);
    ' Nothing when the intensity is not tracked, same as _intensity
    Private ReadOnly _intensityStage1 As Single()
    ' the blend of _intensityStage1 and _intensity (IntensityBlend) that is actually shown/exported - see the class
    ' summary; Nothing when the intensity is not tracked, same as _intensity
    Private ReadOnly _intensityOut As Single()

    Private ReadOnly _sourceIndex As Integer()
    Private ReadOnly _sourceLevel As Byte()
    Private ReadOnly _levelFrequency(255) As Double
    Private ReadOnly _levelUsed(255) As Boolean
    Private ReadOnly _levelValue(255) As Single
    Private ReadOnly _sourceAmplitude As Double

    ' object sources (Sources tab): the flat list of generated point-source elements that are within the canvas
    Private ReadOnly _objSources As SourceElement()
    Private ReadOnly _objIndex As Integer()

    ' per-object-source runtime state for pulsed sources whose off-width is re-randomized at runtime (see
    ' SourceElement.RandomizeOffWidthAmount): _objCycleAnchor(i) is the cycle-clock value (in cycles of the
    ' element's own frequency) at which its current off+on cycle started, and _objCurrentOffWidth(i) is the
    ' off-width (in cycles) currently in effect for it. Both are parallel arrays to _objSources/_objIndex.
    ' Elements that never re-randomize (RandomizeOffWidthAmount <= 0, the common case) never touch this state:
    ' their position within the cycle is still computed as a pure function of _time, exactly as before.
    Private ReadOnly _objRandom As New Random()
    Private ReadOnly _objCycleAnchor As Double()
    Private ReadOnly _objCurrentOffWidth As Integer()

    ''' <summary>Safety cap on how many off+on cycles ComputePulsePosition will advance through in a single call, so a
    ''' pathological configuration (e.g. an off-width collapsing towards 0) can never turn into an infinite loop.</summary>
    Private Const CycleAdvanceGuard As Integer = 1000

    Private _time As Double

    ' the probes: the values of the point probes per step, the intensity totals (per pixel of the line) of the integrating line probes
    Private _sampler As ProbeSampler
    Private ReadOnly _probeValues As New List(Of List(Of Single))()
    Private _profileTotals As Double()()

    ''' <param name="scene">The scene (its arrays are copied).</param>
    ''' <param name="sourceFrequencyScale">Frequency of a source = green value / 255 * 0.5 * this value.</param>
    ''' <param name="sourceAmplitude">Amplitude of the sources (1 = the value of the Python code).</param>
    ''' <param name="trackIntensity">True when the intensity is needed (it costs extra time in every step).</param>
    ''' <param name="use4thOrderLaplacian">See the class summary (Use4thOrderLaplacian).</param>
    ''' <param name="timeDecayFactor">SimulationSettings.TimeDecayFactor - see that property's comment.</param>
    ''' <param name="intensityScale">SimulationSettings.IntensityScale - see that property's comment.</param>
    ''' <param name="objectSources">The generated elements of the object sources (Sources tab), or Nothing/empty for none.</param>
    Public Sub New(scene As SceneData, sourceFrequencyScale As Double, sourceAmplitude As Double, trackIntensity As Boolean,
                   use4thOrderLaplacian As Boolean, timeDecayFactor As Double, intensityScale As Double,
                   Optional objectSources As List(Of SourceElement) = Nothing)
        _sourceAmplitude = sourceAmplitude
        _use4thOrderLaplacian = use4thOrderLaplacian
        _intensityScale = CSng(intensityScale)
        _width = scene.Width
        _height = scene.Height
        _stride = _width + 2
        Dim size As Integer = _stride * (_height + 2)

        _u = New Single(size - 1) {}
        _uPrev = New Single(size - 1) {}
        _c2 = New Single(size - 1) {}
        _d = New Single(size - 1) {}
        If trackIntensity Then
            _intensity = New Single(size - 1) {}
            _intensityStage1 = New Single(size - 1) {}
            _intensityOut = New Single(size - 1) {}
        End If

        For y As Integer = 0 To _height - 1
            Array.Copy(scene.C2, y * _width, _c2, (y + 1) * _stride + 1, _width)
            Array.Copy(scene.D, y * _width, _d, (y + 1) * _stride + 1, _width)
        Next

        ' see the class summary: sub-stepping keeps the 4th order stencil stable without changing the physical
        ' wave speed or damping - _subSteps stays 1 (no sub-stepping) unless the 4th order stencil is in use and
        ' this scene's highest c^2 actually needs it
        If use4thOrderLaplacian Then
            Dim maxC2 As Single = 0.0F
            For i As Integer = 0 To _c2.Length - 1
                If _c2(i) > maxC2 Then maxC2 = _c2(i)
            Next
            _subSteps = ComputeSubSteps(maxC2)
        Else
            _subSteps = 1
        End If

        If _subSteps > 1 Then
            Dim dEff As Single() = New Single(_d.Length - 1) {}
            Dim invN As Double = 1.0 / _subSteps
            For i As Integer = 0 To _d.Length - 1
                dEff(i) = CSng(Math.Pow(CDbl(_d(i)), invN))
            Next
            _dEffective = dEff
            _c2Mult = 1.0F / CSng(_subSteps * _subSteps)
        Else
            _dEffective = _d
            _c2Mult = 1.0F
        End If

        Dim n As Integer = scene.SourceCount
        _sourceIndex = New Integer(n - 1) {}
        _sourceLevel = New Byte(n - 1) {}
        For i As Integer = 0 To n - 1
            _sourceIndex(i) = (scene.SourceY(i) + 1) * _stride + scene.SourceX(i) + 1
            _sourceLevel(i) = scene.SourceLevel(i)
            _levelUsed(scene.SourceLevel(i)) = True
        Next
        For level As Integer = 0 To 255
            _levelFrequency(level) = level / 255.0 * 0.5 * sourceFrequencyScale
        Next

        ' object sources: keep only the elements whose position is inside the canvas (a randomized composite element can fall outside)
        If objectSources Is Nothing Then
            _objSources = Array.Empty(Of SourceElement)()
            _objIndex = Array.Empty(Of Integer)()
        Else
            Dim keptEl As New List(Of SourceElement)()
            Dim keptIdx As New List(Of Integer)()
            For Each el As SourceElement In objectSources
                If el.PosX >= 0 AndAlso el.PosX < _width AndAlso el.PosY >= 0 AndAlso el.PosY < _height Then
                    keptEl.Add(el)
                    keptIdx.Add((el.PosY + 1) * _stride + el.PosX + 1)
                End If
            Next
            _objSources = keptEl.ToArray()
            _objIndex = keptIdx.ToArray()
        End If

        ' the intensity's averaging time constant scales inversely with the frequency of the slowest active
        ' source (image or object; a level of 0 means "no source" and is excluded) - see TimeDecayFactor's comment
        ' (SimulationSettings.vb) for why, and NominalDecayLevel's comment for the calibration this reproduces
        Dim minActiveLevel As Integer = 0
        For level As Integer = 1 To 255
            If _levelUsed(level) Then
                minActiveLevel = level
                Exit For
            End If
        Next
        For Each el As SourceElement In _objSources
            If el.Level > 0 AndAlso (minActiveLevel = 0 OrElse el.Level < minActiveLevel) Then minActiveLevel = el.Level
        Next
        If minActiveLevel = 0 Then minActiveLevel = NominalDecayLevel
        Dim tauSteps As Double = Math.Max(1.0, timeDecayFactor * NominalDecayLevel / minActiveLevel)
        _intensityAlpha = CSng(Math.Exp(-1.0 / tauSteps))

        _objCycleAnchor = New Double(_objSources.Length - 1) {}
        _objCurrentOffWidth = New Integer(_objSources.Length - 1) {}
        For i As Integer = 0 To _objSources.Length - 1
            ' the very first cycle uses the off-width already rolled once at "Generate composite source" time;
            ' later cycles (for elements that opt into it) redraw from OffWidthBase - see ComputePulsePosition
            _objCurrentOffWidth(i) = _objSources(i).OffWidthCycles
        Next
    End Sub

    Public ReadOnly Property Width As Integer Implements IWaveEngine.Width
        Get
            Return _width
        End Get
    End Property

    Public ReadOnly Property Height As Integer Implements IWaveEngine.Height
        Get
            Return _height
        End Get
    End Property

    ''' <summary>Distance between two rows in FieldData and IntensityData. Pixel (x, y) is at (y + 1) * Stride + x + 1.</summary>
    Public ReadOnly Property Stride As Integer Implements IWaveEngine.Stride
        Get
            Return _stride
        End Get
    End Property

    ''' <summary>The field of the last completed step (with a border of one cell). Do not change.</summary>
    Public ReadOnly Property FieldData As Single() Implements IWaveEngine.FieldData
        Get
            Return _u
        End Get
    End Property

    ''' <summary>The averaged intensity (with a border of one cell), or Nothing when it is not tracked. Do not change.</summary>
    Public ReadOnly Property IntensityData As Single() Implements IWaveEngine.IntensityData
        Get
            Return _intensityOut
        End Get
    End Property

    Public ReadOnly Property Description As String Implements IWaveEngine.Description
        Get
            Return "CPU (" & Environment.ProcessorCount.ToString(System.Globalization.CultureInfo.InvariantCulture) & " threads)"
        End Get
    End Property

    ''' <summary>Nothing to do: the field and the intensity are always in memory.</summary>
    Public Sub PrepareFrame(needField As Boolean, needIntensity As Boolean) Implements IWaveEngine.PrepareFrame
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Sub SetProbes(sampler As ProbeSampler) Implements IWaveEngine.SetProbes
        _probeValues.Clear()
        _profileTotals = Nothing
        If sampler Is Nothing OrElse sampler.Count = 0 Then
            _sampler = Nothing
            Return
        End If
        If sampler.NeedsIntensity AndAlso _intensityOut Is Nothing Then
            Throw New InvalidOperationException("the intensity is not tracked")
        End If

        _sampler = sampler
        _profileTotals = New Double(sampler.Count - 1)() {}
        For i As Integer = 0 To sampler.Count - 1
            _probeValues.Add(New List(Of Single)())
            If sampler.Integrate(i) Then _profileTotals(i) = New Double(sampler.Pixels(i).Length - 1) {}
        Next
    End Sub

    Public Sub SampleProbes() Implements IWaveEngine.SampleProbes
        If _sampler Is Nothing Then Return
        Dim currentStep As Integer = CInt(_time)
        For p As Integer = 0 To _sampler.Count - 1
            If Not _sampler.IsProfile(p) Then
                Dim data As Single() = If(_sampler.UseIntensity(p), _intensityOut, _u)
                _probeValues(p).Add(_sampler.Compute(p, data))
            ElseIf _sampler.Integrate(p) Then
                ' before "start after step" the totals are simply never added to, so they stay at zero: any intensity
                ' that arrived earlier is purged rather than being baked into the total once recording starts
                If _sampler.UseStartAfterStep(p) AndAlso currentStep < _sampler.StartAfterStep(p) Then Continue For

                ' the intensity that has passed every point of the line, added up over the steps
                Dim px As Integer() = _sampler.Pixels(p)
                Dim totals As Double() = _profileTotals(p)
                Dim inten As Single() = _intensityOut
                For k As Integer = 0 To px.Length - 1
                    totals(k) += inten(px(k))
                Next
            End If
        Next
    End Sub

    Public Function TakeProbeSamples() As Single()() Implements IWaveEngine.TakeProbeSamples
        If _sampler Is Nothing Then Return New Single()() {}
        Dim result As Single()() = New Single(_sampler.Count - 1)() {}
        For p As Integer = 0 To _sampler.Count - 1
            result(p) = _probeValues(p).ToArray()
            _probeValues(p).Clear()
        Next
        Return result
    End Function

    Public Function ReadProbeProfiles() As Single()() Implements IWaveEngine.ReadProbeProfiles
        If _sampler Is Nothing Then Return New Single()() {}
        Dim result As Single()() = New Single(_sampler.Count - 1)() {}
        For p As Integer = 0 To _sampler.Count - 1
            If Not _sampler.IsProfile(p) Then Continue For

            Dim px As Integer() = _sampler.Pixels(p)
            Dim values As Single() = New Single(px.Length - 1) {}
            If _sampler.Integrate(p) Then
                Dim totals As Double() = _profileTotals(p)
                For k As Integer = 0 To px.Length - 1
                    values(k) = CSng(totals(k))
                Next
            Else
                Dim data As Single() = If(_sampler.UseIntensity(p), _intensityOut, _u)
                For k As Integer = 0 To px.Length - 1
                    values(k) = data(px(k))
                Next
            End If
            result(p) = values
        Next
        Return result
    End Function

    ''' <summary>The number of steps done so far (the time in steps).</summary>
    Public ReadOnly Property Time As Double
        Get
            Return _time
        End Get
    End Property

    ''' <summary>
    ''' The number of smaller time steps (see the class summary) that keep c2 * |lambda max| of the 4th order
    ''' stencil under SubStepTargetLimit for the given (highest, in this scene) c^2; 1 when that is already true.
    ''' </summary>
    Private Shared Function ComputeSubSteps(maxC2 As Single) As Integer
        If maxC2 <= 0.0F Then Return 1
        Dim n As Double = Math.Ceiling(Math.Sqrt(CDbl(maxC2) * CDbl(FourthOrderLambdaMax) / CDbl(SubStepTargetLimit)))
        Return Math.Max(1, CInt(n))
    End Function

    ''' <summary>One time step: apply the sources, then update the field (and the intensity) _subSteps times.</summary>
    Public Sub AdvanceOneStep() Implements IWaveEngine.AdvanceOneStep
        ApplySources()
        For subStep As Integer = 1 To _subSteps
            UpdateField(_dEffective, _c2Mult, subStep = _subSteps)
        Next
        _time += 1.0
    End Sub

    Private Sub ApplySources()
        Dim u As Single() = _u

        If _sourceIndex.Length > 0 Then
            ' sin(phase + frequency * t) * amplitude is the same for all sources with the same green value
            For level As Integer = 1 To 255
                If _levelUsed(level) Then
                    _levelValue(level) = CSng(Math.Sin(_levelFrequency(level) * _time) * _sourceAmplitude)
                End If
            Next

            For i As Integer = 0 To _sourceIndex.Length - 1
                Dim idx As Integer = _sourceIndex(i)
                u(idx) = u(idx) * SourceOpacity + _levelValue(_sourceLevel(i)) * (1.0F - SourceOpacity)
            Next
        End If

        For i As Integer = 0 To _objSources.Length - 1
            Dim el As SourceElement = _objSources(i)
            ' "detach source": once the run reaches the source's detach step, its drive is faded out linearly over
            ' the next DetachRampSteps steps (rather than cut instantly), then it stops driving the field entirely
            ' (not even towards 0) so it releases its hold and stops diffracting waves through that point. A hard
            ' cutoff would otherwise leave a small kink in the forcing right at the detach step, which can ring.
            Dim detachFade As Single = 1.0F
            If el.DetachAfterStep >= 0 Then
                Dim stepsSinceDetach As Double = _time - el.DetachAfterStep
                If stepsSinceDetach >= SourceElement.DetachRampSteps Then Continue For
                If stepsSinceDetach > 0.0 Then
                    detachFade = CSng(1.0 - stepsSinceDetach / SourceElement.DetachRampSteps)
                End If
            End If
            Dim freq As Double = _levelFrequency(el.Level)
            Dim envelope As Double
            If el.Mode = SourceMode.Continuous Then
                envelope = 1.0
            Else
                Dim offWidth As Integer
                Dim isFirstCycle As Boolean
                Dim pos As Double = ComputePulsePosition(i, el, freq, offWidth, isFirstCycle)
                envelope = PulseEnvelope(el, pos, offWidth, isFirstCycle)
            End If
            Dim v As Single = CSng(Math.Sin(el.PhaseRad + freq * _time) * el.Amplitude * envelope) * detachFade
            Dim idx As Integer = _objIndex(i)
            u(idx) = u(idx) * SourceOpacity + v * (1.0F - SourceOpacity)
        Next
    End Sub

    ''' <summary>
    ''' Returns the element's position within its current off+on cycle (0 = the very start of the "off" span, up to
    ''' but not including its current period), and reports the off-width in effect for that cycle via offWidth.
    ''' For most elements (RandomizeOffWidthAmount &lt;= 0) the off-width never changes, so this is a pure, stateless
    ''' function of _time - except that the cycle clock is shifted by the off-width, so the run starts already at the
    ''' beginning of the "on" span (pos = offWidth) instead of at the start of an off span: a single/non-randomized
    ''' source sitting silent for its off-width at the very start of a run was never the intent, only ever a side
    ''' effect of the cycle position formula happening to start at 0. An element with RandomizeOffWidthAmount &gt; 0
    ''' (only ever a composite source with "Randomize off-width" ticked) keeps the original start-in-off-state
    ''' behavior instead, since there the elements are meant to desynchronize from a common starting point rather
    ''' than all begin already mid-pulse. For an element that opted into runtime re-randomization, this also owns
    ''' that element's runtime state: each time _time has advanced far enough to complete the current cycle, it
    ''' advances the element's cycle anchor and redraws a fresh off-width (uniformly within +/- RandomizeOffWidthAmount
    ''' of OffWidthBase) before computing the position - so every source keeps its own, independently-drifting rhythm
    ''' instead of staying locked to whatever it rolled once at generate time.
    ''' isFirstCycle reports whether pos falls within the element's very first off+on cycle since the run began -
    ''' see PulseEnvelope for why that matters for the SmoothedSquare pulse shape.
    ''' </summary>
    Private Function ComputePulsePosition(i As Integer, el As SourceElement, freq As Double, ByRef offWidth As Integer, ByRef isFirstCycle As Boolean) As Double
        ' cycles of the source's own frequency elapsed since t = 0 (not folding in the phase, so the on/off timing
        ' does not shift when only the phase of the wave itself is changed)
        Dim cycleClock As Double = freq * _time / (2.0 * Math.PI)

        If el.RandomizeOffWidthAmount <= 0 Then
            offWidth = el.OffWidthCycles
            Dim period As Integer = el.OnWidthCycles + offWidth
            If period <= 0 Then
                isFirstCycle = True
                Return 0.0
            End If
            ' shifted so cycleClock = 0 lands at pos = offWidth (the start of the "on" span), not pos = 0 (the start
            ' of an "off" span) - see the summary above
            Dim adjustedClock As Double = cycleClock + offWidth
            isFirstCycle = (Math.Floor(adjustedClock / period) = 0.0)
            Return adjustedClock - period * Math.Floor(adjustedClock / period)   ' 0 .. period
        End If

        offWidth = _objCurrentOffWidth(i)
        Dim dynPeriod As Integer = el.OnWidthCycles + offWidth
        If dynPeriod <= 0 Then
            isFirstCycle = True
            Return 0.0
        End If

        Dim pos As Double = cycleClock - _objCycleAnchor(i)
        Dim guard As Integer = 0
        While pos >= dynPeriod AndAlso guard < CycleAdvanceGuard
            _objCycleAnchor(i) += dynPeriod
            pos -= dynPeriod
            offWidth = Math.Max(1, el.OffWidthBase + SourceSettings.RandomInt(_objRandom, el.RandomizeOffWidthAmount))
            _objCurrentOffWidth(i) = offWidth
            dynPeriod = el.OnWidthCycles + offWidth
            guard += 1
        End While
        If pos < 0.0 Then pos = 0.0   ' should not happen (_time only increases), guarded for safety
        ' the anchor only ever advances (above) once a cycle has actually completed, so it still reading its initial
        ' 0 means no cycle has completed yet - we are still in the element's very first (off-first) cycle
        isFirstCycle = (_objCycleAnchor(i) = 0.0)
        Return pos
    End Function

    ''' <summary>
    ''' For a pulsed source, the source is "off" for offWidth cycles of its own frequency, then "on" for
    ''' OnWidthCycles cycles, repeating (off precedes on so that, at the start of a run, every source is silent for
    ''' its own off-width before its first pulse, rather than every source firing simultaneously at t = 0). Within
    ''' the "on" span the amplitude is shaped by PulseShape: Gaussian tapers smoothly to (about) 0 at both ends of the
    ''' "on" span, and SmoothedSquare rises and falls quickly (an atan-smoothed square wave, avoiding a hard on/off
    ''' step that would ring the field). pos and offWidth come from ComputePulsePosition, as does isFirstCycle - see
    ''' the SmoothedSquare branch below for why that one matters.
    ''' </summary>
    Private Function PulseEnvelope(el As SourceElement, pos As Double, offWidth As Integer, isFirstCycle As Boolean) As Double
        Dim onWidth As Integer = el.OnWidthCycles
        Dim period As Integer = onWidth + offWidth
        If period <= 0 Then Return 1.0

        If el.PulseShape = SourcePulseShape.Gaussian Then
            If pos < offWidth Then Return 0.0
            Dim onPos As Double = pos - offWidth                  ' 0 .. onWidth, position within the "on" span
            Dim center As Double = onWidth / 2.0
            Dim sigma As Double = Math.Max(onWidth / 6.0, 1.0E-6)
            Dim d As Double = (onPos - center) / sigma
            Return Math.Exp(-0.5 * d * d)
        Else
            ' a flat-topped pulse (envelope 1 while "on", 0 while "off") with a short, FIXED-width (in cycles, not a
            ' fraction of the on/off span) smoothed edge at the rise and the fall (see the on-first version's comment
            ' for why a fixed edge width matters for a large off-width). The math below is exactly the original
            ' on-first formula, just fed a coordinate shifted so the "on" span starts at 0 (folding around the period
            ' the same way the original did), which keeps the already-verified edge-smoothing behavior unchanged.
            Dim shifted As Double = pos - offWidth
            If shifted < 0.0 Then
                ' wrapping shifted forward by a full period makes it continue the falling tail of the on-span that
                ' JUST ended - correct from the second off-span onward, where that on-span really happened. A
                ' source's very first off-span (isFirstCycle) has no real preceding on-emission to continue from, so
                ' wrapping there would fabricate one: pos = 0 would land exactly on that phantom falling edge and
                ' start the run at envelope ~0.5 instead of ~0, emitting a spurious one-cycle "blip" before the
                ' source actually goes quiet. Left un-wrapped (deeply negative) instead, dRise/dFall below correctly
                ' place it far from both edges, so the envelope is (very close to) 0 until the real rising edge.
                If Not isFirstCycle Then shifted += period
            End If
            Const EdgeWidth As Double = 0.35
            Dim dFall As Double = shifted - onWidth                ' 0 at the falling edge, grows past it
            Dim dRise As Double = shifted                          ' 0 at the rising edge
            If dRise > period / 2.0 Then dRise -= period           ' signed distance to the *nearest* rising edge

            Dim rising As Double = 0.5 + Math.Atan(dRise / EdgeWidth) / Math.PI
            Dim falling As Double = 0.5 - Math.Atan(dFall / EdgeWidth) / Math.PI
            Return rising * falling
        End If
    End Function

    ''' <summary>
    ''' One time step of size 1 / _subSteps: dArray is _d itself (_subSteps = 1) or its 1 / _subSteps-th root
    ''' (see the class summary); c2Mult is 1.0F (_subSteps = 1, an exact no-op) or 1.0F / _subSteps^2; the
    ''' intensity (if tracked) is only updated when updateIntensity is True, so that its own exponential
    ''' averaging still runs once per AdvanceOneStep call, from the field after all of its sub-steps, matching
    ''' its calibration (_intensityAlpha) regardless of how many sub-steps that call takes.
    ''' </summary>
    Private Sub UpdateField(dArray As Single(), c2Mult As Single, updateIntensity As Boolean)
        Dim u As Single() = _u
        Dim uPrev As Single() = _uPrev
        Dim c2 As Single() = _c2
        Dim d As Single() = dArray
        Dim intensity As Single() = _intensity
        Dim stage1 As Single() = _intensityStage1
        Dim intensityOut As Single() = _intensityOut
        Dim track As Boolean = (intensity IsNot Nothing) AndAlso updateIntensity
        Dim stride As Integer = _stride
        Dim w As Integer = _width
        Dim h As Integer = _height
        Dim use4th As Boolean = _use4thOrderLaplacian
        Dim mult As Single = c2Mult
        Dim alpha As Single = _intensityAlpha
        Dim blend As Single = IntensityBlend
        Dim scale As Single = _intensityScale

        Dim bands As Integer = Math.Min(h, Environment.ProcessorCount * 4)
        Parallel.For(0, bands,
            Sub(band As Integer)
                Dim y0 As Integer = CInt(CLng(h) * band \ bands)
                Dim y1 As Integer = CInt(CLng(h) * (band + 1) \ bands) - 1
                For y As Integer = y0 To y1
                    Dim idx As Integer = (y + 1) * stride + 1
                    ' the 4th order stencil reaches 2 cells away, so it is only safe for domain rows/columns that
                    ' have that much room before the (1 cell wide) border - the outermost ring of the domain (y = 0,
                    ' y = h - 1, and, per column below, x = 0, x = w - 1) falls back to the original 3x3 kernel
                    Dim rowInterior As Boolean = use4th AndAlso y >= 1 AndAlso y <= h - 2
                    For x As Integer = 0 To w - 1
                        Dim uc As Single = u(idx)
                        Dim laplacian As Single
                        If rowInterior AndAlso x >= 1 AndAlso x <= w - 2 Then
                            laplacian = FourthOrderFar * (u(idx - 2) + u(idx + 2) + u(idx - 2 * stride) + u(idx + 2 * stride)) +
                                       FourthOrderNear * (u(idx - 1) + u(idx + 1) + u(idx - stride) + u(idx + stride)) +
                                       FourthOrderCenter * uc
                        Else
                            laplacian = KernelCorner * (u(idx - stride - 1) + u(idx - stride + 1) + u(idx + stride - 1) + u(idx + stride + 1)) +
                                       KernelEdge * (u(idx - 1) + u(idx + 1) + u(idx - stride) + u(idx + stride)) - uc
                        End If
                        ' the new value replaces the previous one (each cell only reads its own previous value)
                        Dim nv As Single = uc + (uc - uPrev(idx)) * d(idx) + laplacian * c2(idx) * mult
                        uPrev(idx) = nv
                        If track Then
                            ' two cascaded one-pole stages (see the class summary), blended with the 1-stage
                            ' result (IntensityBlend) for the shown/exported intensity rather than using the full
                            ' 2-stage cascade outright
                            Dim s1 As Single = stage1(idx) * alpha + nv * nv * (1.0F - alpha)
                            stage1(idx) = s1
                            Dim s2 As Single = intensity(idx) * alpha + s1 * (1.0F - alpha)
                            intensity(idx) = s2
                            intensityOut(idx) = (s1 * (1.0F - blend) + s2 * blend) * scale
                        End If
                        idx += 1
                    Next
                Next
            End Sub)

        ' the array that received the new values is now the current field
        _uPrev = u
        _u = uPrev
    End Sub

End Class
