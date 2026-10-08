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
Imports System.Text

''' <summary>
''' The 2D wave equation on the graphics card (through OpenCL), the same algorithm as WaveSimulator:
'''   new u = u + (u - previous u) * d + laplacian * c^2     (3x3 kernel 0.066 / 0.184 / -1)
'''   sources: u = u * 0.9 + sin(frequency * t) * amplitude * 0.1
'''   intensity: two cascaded one-pole filters, I1 = I1 * alpha + u^2 * (1 - alpha), I2 = I2 * alpha + I1 * (1 - alpha),
'''   blended (IntensityBlend) into what is shown/exported: Iout = (I1 * (1 - blend) + I2 * blend) * scale. alpha
'''   is derived once per simulation - see WaveSimulator's class summary (why two stages and the blend, not the
'''   single, fixed-0.98 filter shown above) and SimulationSettings.TimeDecayFactor's comment (why alpha); scale is
'''   SimulationSettings.IntensityScale (why it exists)
''' The field, the previous field, c^2, d and the intensity stay in the memory of the graphics card; only when a
''' frame is drawn (PrepareFrame) the field and/or intensity are copied back. The arrays have the same layout as
''' those of the CPU engine (a border of one cell), so the same renderer draws both.
'''
''' Optionally (use4thOrderLaplacian), the wave_step kernel instead computes the Laplacian with a 4th order
''' accurate, 5-point "cross" stencil wherever there is room for it, the same as WaveSimulator.UpdateField on the
''' CPU (see that class's summary for why). Which form to use is baked into the generated kernel source as a
''' compile-time #define, not a runtime branch, so there is no extra cost when it is off.
'''
''' The 4th order stencil needs the same time sub-stepping as the CPU engine to stay stable, for the same reason
''' (see WaveSimulator's class summary): AdvanceOneStep launches the wave_step kernel _subSteps times instead of
''' once when _subSteps > 1, with c2 scaled by a c2Scale kernel argument (1.0F / _subSteps^2) and the d buffer
''' pre-scaled (on the host, once, at Build time) to its 1 / _subSteps-th root, so _subSteps kernel launches move
''' the field by exactly the same physical time and damping as one ordinary launch. The intensity buffer is only
''' updated (via the kernel's "track" argument) on the last of the _subSteps launches, so its own exponential
''' averaging still runs once per AdvanceOneStep call, matching the CPU engine and its own earlier calibration.
''' _subSteps is 1 (c2Scale exactly 1.0F, an exact no-op multiply, and the d buffer unchanged) whenever the 3x3
''' kernel is used or this scene's highest c^2 does not need sub-stepping - see ComputeSubSteps.
''' </summary>
Friend NotInheritable Class GpuWaveSimulator
    Implements IWaveEngine

    Private ReadOnly _description As String
    Private ReadOnly _width As Integer
    Private ReadOnly _height As Integer
    Private ReadOnly _stride As Integer
    Private ReadOnly _cells As Integer
    Private ReadOnly _track As Boolean
    Private ReadOnly _sourceCount As Integer
    Private ReadOnly _amplitude As Single
    Private ReadOnly _use4thOrderLaplacian As Boolean
    ' see the class summary; 1 (no sub-stepping) unless Use4thOrderLaplacian needs otherwise for this scene's c^2.
    ' Set once, in Build (called from New) - not itself ReadOnly because VB.NET only allows a ReadOnly field to be
    ' assigned directly inside the declaring class's constructor, not in a method (Build) that constructor calls.
    Private _subSteps As Integer
    ' 1.0F / _subSteps^2, or exactly 1.0F when _subSteps = 1 (a bit-for-bit no-op multiply in the kernel); see _subSteps
    Private _c2Mult As Single
    ' the intensity's exponential-average decay factor for this simulation (replaces the old fixed 0.98f in the
    ' kernel), set once in Build - see WaveSimulator._intensityAlpha (CPU engine) for the identical computation
    Private _intensityAlpha As Single
    ' SimulationSettings.IntensityScale - see that property's comment and WaveSimulator._intensityScale (CPU engine)
    Private _intensityScale As Single

    Private _context As IntPtr
    Private _queue As IntPtr
    Private _program As IntPtr

    ' Two field buffers that take turns as "current field" and "previous field"; there is a kernel for each turn
    Private ReadOnly _field(1) As IntPtr
    Private ReadOnly _stepKernel(1) As IntPtr
    Private ReadOnly _sourceKernel(1) As IntPtr
    Private _current As Integer

    Private _c2Buffer As IntPtr
    Private _dBuffer As IntPtr
    ' the second cascaded filter stage's recursive state - see the class summary and WaveSimulator._intensity (CPU
    ' engine); used only inside wave_step, never read back - what is read back/probed is _intensityOutBuffer
    Private _intensityBuffer As IntPtr
    ' the first of the two cascaded exponential-filter stages that make up the intensity - see the class summary
    ' and WaveSimulator._intensityStage1 (CPU engine); used only inside wave_step, never read back
    Private _intensityStage1Buffer As IntPtr
    ' the blend of _intensityStage1Buffer and _intensityBuffer (IntensityBlend) that is actually read back/probed -
    ' see the class summary and WaveSimulator._intensityOut (CPU engine)
    Private _intensityOutBuffer As IntPtr
    Private _indexBuffer As IntPtr
    Private _levelBuffer As IntPtr
    Private _frequencyBuffer As IntPtr

    ' object sources (Sources tab): the flat list of generated point-source elements that are within the canvas,
    ' one entry per element; the frequency table above (_frequencyBuffer, indexed by Level) is shared with the
    ' image sources since both use the same level-to-frequency scale
    Private ReadOnly _objSourceCount As Integer
    Private ReadOnly _objSourceKernel(1) As IntPtr
    Private _objIndexBuffer As IntPtr
    Private _objLevelBuffer As IntPtr
    Private _objAmplitudeBuffer As IntPtr
    Private _objPhaseBuffer As IntPtr
    Private _objModeBuffer As IntPtr
    Private _objPulseShapeBuffer As IntPtr
    Private _objOnWidthBuffer As IntPtr
    ' the off-width currently in effect for each element, in cycles; read-write on the GPU (see apply_object_sources)
    ' since an element with RandomizeOffWidthAmount > 0 redraws it there each time it completes a cycle
    Private _objOffWidthBuffer As IntPtr
    ' "detach source": the step at which each element stops driving the field (releasing its hold on it), or -1 to never
    Private _objDetachStepBuffer As IntPtr
    ' runtime state for off-width re-randomization (see SourceElement.RandomizeOffWidthAmount): the un-randomized
    ' anchor each redraw is centered on, the per-element +/- spread (0 = never redraw), the cycle-clock value at
    ' which each element's current off+on cycle started, and a per-element xorshift32 PRNG seed (kernel-side
    ' reinterpreted as uint - only the entropy bits matter, not signedness)
    Private _objOffWidthBaseBuffer As IntPtr
    Private _objRandomizeOffWidthAmountBuffer As IntPtr
    Private _objCycleAnchorBuffer As IntPtr
    Private _objRngStateBuffer As IntPtr
    Private ReadOnly _objGrid1D(0) As IntPtr

    Private ReadOnly _grid2D(1) As IntPtr
    Private ReadOnly _grid1D(0) As IntPtr

    Private _hostField As Single()
    Private _hostIntensity As Single()

    Private _time As Double
    Private _sinceFinish As Integer
    Private _released As Boolean

    ' the probes: a second small program measures them on the card.
    ' Point probes: one value per step; the values wait in a history buffer until the next TakeProbeSamples
    ' (or until the buffer is full).
    Private Const ProbeRows As Integer = 2048
    Private _deviceId As IntPtr
    Private _probeSampler As ProbeSampler
    Private _probeProgram As IntPtr
    Private ReadOnly _probeKernel(1) As IntPtr
    Private _pixelBuffer As IntPtr
    Private _infoBuffer As IntPtr
    Private _historyBuffer As IntPtr
    Private _probeRow As Integer
    Private ReadOnly _probeGrid(0) As IntPtr
    Private ReadOnly _probeValues As New List(Of List(Of Single))()

    ' Line probes: the pixels of all lines in one list; the totals (integrated intensity) are kept on the card,
    ' the profiles are gathered into a small buffer and copied back when they are asked for
    Private ReadOnly _gatherKernel(1) As IntPtr
    Private _accumulateKernel As IntPtr
    Private _profilePixelBuffer As IntPtr
    Private _profileModeBuffer As IntPtr
    ' "start recording at step #" for the integrated line probes: one entry per pixel (same layout as _profilePixelBuffer/
    ' _profileModeBuffer), so profile_accumulate can skip a pixel's contribution before its probe's start step
    Private _profileStartFlagBuffer As IntPtr
    Private _profileStartStepBuffer As IntPtr
    Private _profileTotalBuffer As IntPtr
    Private _profileOutBuffer As IntPtr
    Private _profileCount As Integer
    Private _profileIntegrates As Boolean
    Private _profileStart As Integer()
    Private ReadOnly _profileGrid(0) As IntPtr

    ''' <summary>
    ''' Tries the GPUs one after the other, the best first. Returns Nothing when none works; <paramref name="problems"/>
    ''' then tells why (or, when a later GPU did work, why the earlier ones did not).
    ''' Throws DllNotFoundException when there is no OpenCL driver.
    ''' </summary>
    ''' <param name="use4thOrderLaplacian">See the class summary.</param>
    ''' <param name="timeDecayFactor">SimulationSettings.TimeDecayFactor - see that property's comment.</param>
    ''' <param name="intensityScale">SimulationSettings.IntensityScale - see that property's comment.</param>
    ''' <param name="objectSources">The generated elements of the object sources (Sources tab), or Nothing/empty for none.</param>
    Public Shared Function TryCreate(scene As SceneData, sourceFrequencyScale As Double, sourceAmplitude As Double,
                                     trackIntensity As Boolean, use4thOrderLaplacian As Boolean, timeDecayFactor As Double,
                                     intensityScale As Double,
                                     ByRef problems As String,
                                     Optional objectSources As List(Of SourceElement) = Nothing) As GpuWaveSimulator
        problems = ""
        Dim devices As List(Of GpuDeviceInfo) = GpuDeviceInfo.FindAll()
        If devices.Count = 0 Then
            problems = "no GPU found through OpenCL" & If(IntPtr.Size = 4, " (this is a 32-bit process; run the 64-bit build)", "")
            Return Nothing
        End If

        Dim messages As New StringBuilder()
        For Each dev As GpuDeviceInfo In devices
            Try
                Return New GpuWaveSimulator(dev, scene, sourceFrequencyScale, sourceAmplitude, trackIntensity, use4thOrderLaplacian, timeDecayFactor, intensityScale, objectSources)
            Catch ex As Exception
                If messages.Length > 0 Then messages.Append("; ")
                messages.Append(dev.Name).Append(": ").Append(ex.Message)
            End Try
        Next
        problems = messages.ToString()
        Return Nothing
    End Function

    Private Sub New(device As GpuDeviceInfo, scene As SceneData, sourceFrequencyScale As Double,
                    sourceAmplitude As Double, trackIntensity As Boolean, use4thOrderLaplacian As Boolean,
                    timeDecayFactor As Double, intensityScale As Double,
                    Optional objectSources As List(Of SourceElement) = Nothing)
        _description = "GPU: " & device.Name & " (OpenCL)"
        _width = scene.Width
        _height = scene.Height
        _stride = _width + 2
        _cells = _stride * (_height + 2)
        _track = trackIntensity
        _sourceCount = scene.SourceCount
        _amplitude = CSng(sourceAmplitude)
        _use4thOrderLaplacian = use4thOrderLaplacian

        If device.MaxAllocation > 0 AndAlso CLng(_cells) * 4L > device.MaxAllocation Then
            Throw New InvalidOperationException("the scene is too large for this GPU")
        End If

        ' object sources: keep only the elements whose position is inside the canvas (a randomized composite
        ' element can fall outside), same filtering as the CPU engine (WaveSimulator)
        Dim keptObjSources As New List(Of SourceElement)()
        If objectSources IsNot Nothing Then
            For Each el As SourceElement In objectSources
                If el.PosX >= 0 AndAlso el.PosX < _width AndAlso el.PosY >= 0 AndAlso el.PosY < _height Then
                    keptObjSources.Add(el)
                End If
            Next
        End If
        _objSourceCount = keptObjSources.Count

        Try
            Build(device, scene, sourceFrequencyScale, timeDecayFactor, intensityScale, keptObjSources)
        Catch ex As Exception
            ReleaseAll()
            Throw
        End Try
    End Sub

    Private Sub Build(device As GpuDeviceInfo, scene As SceneData, sourceFrequencyScale As Double, timeDecayFactor As Double,
                      intensityScale As Double, objectSources As List(Of SourceElement))
        Dim code As Integer = 0
        _deviceId = device.DeviceId
        Dim props() As IntPtr = {New IntPtr(OpenClApi.ContextPlatformProperty), device.PlatformId, IntPtr.Zero}
        Dim devs() As IntPtr = {device.DeviceId}

        _context = OpenClApi.clCreateContext(props, 1UI, devs, IntPtr.Zero, IntPtr.Zero, code)
        OpenClApi.Check(code, "clCreateContext")
        _queue = OpenClApi.clCreateCommandQueue(_context, device.DeviceId, 0UL, code)
        OpenClApi.Check(code, "clCreateCommandQueue")

        Dim sources() As String = {KernelSource(_use4thOrderLaplacian)}
        _program = OpenClApi.clCreateProgramWithSource(_context, 1UI, sources, Nothing, code)
        OpenClApi.Check(code, "clCreateProgramWithSource")
        code = OpenClApi.clBuildProgram(_program, 1UI, devs, "", IntPtr.Zero, IntPtr.Zero)
        If code <> 0 Then
            Dim log As String = OpenClApi.BuildLog(_program, device.DeviceId)
            Throw New InvalidOperationException("the program for the GPU could not be built (" & OpenClApi.ErrorName(code) & ")" &
                                                If(log.Length > 0, ": " & log, ""))
        End If

        ' the scene, with a border of one cell like the CPU engine
        Dim c2 As Single() = New Single(_cells - 1) {}
        Dim d As Single() = New Single(_cells - 1) {}
        For y As Integer = 0 To _height - 1
            Array.Copy(scene.C2, y * _width, c2, (y + 1) * _stride + 1, _width)
            Array.Copy(scene.D, y * _width, d, (y + 1) * _stride + 1, _width)
        Next

        ' see the class summary: sub-stepping keeps the 4th order stencil stable without changing the physical
        ' wave speed or damping - _subSteps stays 1 (no sub-stepping) unless the 4th order stencil is in use and
        ' this scene's highest c^2 actually needs it. The d buffer uploaded below is pre-scaled to match (its
        ' 1 / _subSteps-th root, or unchanged when _subSteps = 1), same as WaveSimulator's _dEffective on the CPU.
        Dim maxC2 As Single = 0.0F
        If _use4thOrderLaplacian Then
            For i As Integer = 0 To c2.Length - 1
                If c2(i) > maxC2 Then maxC2 = c2(i)
            Next
        End If
        _subSteps = If(_use4thOrderLaplacian, ComputeSubSteps(maxC2), 1)

        Dim dUpload As Single() = d
        If _subSteps > 1 Then
            Dim dEff As Single() = New Single(d.Length - 1) {}
            Dim invN As Double = 1.0 / _subSteps
            For i As Integer = 0 To d.Length - 1
                dEff(i) = CSng(Math.Pow(CDbl(d(i)), invN))
            Next
            dUpload = dEff
            _c2Mult = 1.0F / CSng(_subSteps * _subSteps)
        Else
            _c2Mult = 1.0F
        End If

        _c2Buffer = CreateFloatBuffer(c2)
        _dBuffer = CreateFloatBuffer(dUpload)

        Dim zeros As Single() = New Single(_cells - 1) {}
        _field(0) = CreateFloatBuffer(zeros)
        _field(1) = CreateFloatBuffer(zeros)
        If _track Then
            _intensityBuffer = CreateFloatBuffer(zeros)
            _intensityStage1Buffer = CreateFloatBuffer(zeros)
            _intensityOutBuffer = CreateFloatBuffer(zeros)
        Else
            _intensityBuffer = CreateFloatBuffer(New Single(0) {})
            _intensityStage1Buffer = CreateFloatBuffer(New Single(0) {})
            _intensityOutBuffer = CreateFloatBuffer(New Single(0) {})
        End If

        ' the level-to-frequency table (level 0-255 -> frequency), shared by the image sources and the object sources
        If _sourceCount > 0 OrElse _objSourceCount > 0 Then
            Dim frequency(255) As Single
            For l As Integer = 0 To 255
                frequency(l) = CSng(l / 255.0 * 0.5 * sourceFrequencyScale)
            Next
            _frequencyBuffer = CreateFloatBuffer(frequency)
        End If

        ' the intensity's averaging time constant scales inversely with the frequency of the slowest active source
        ' (image or object; a level of 0 means "no source" and is excluded) - see WaveSimulator's identical
        ' computation (CPU engine) and TimeDecayFactor's comment (SimulationSettings.vb) for why
        Dim minActiveLevel As Integer = 0
        For i As Integer = 0 To _sourceCount - 1
            Dim lv As Integer = CInt(scene.SourceLevel(i))
            If lv > 0 AndAlso (minActiveLevel = 0 OrElse lv < minActiveLevel) Then minActiveLevel = lv
        Next
        For Each el As SourceElement In objectSources
            If el.Level > 0 AndAlso (minActiveLevel = 0 OrElse el.Level < minActiveLevel) Then minActiveLevel = el.Level
        Next
        Const NominalDecayLevel As Integer = 100  ' see WaveSimulator.NominalDecayLevel (CPU engine)
        If minActiveLevel = 0 Then minActiveLevel = NominalDecayLevel
        Dim tauSteps As Double = Math.Max(1.0, timeDecayFactor * NominalDecayLevel / minActiveLevel)
        _intensityAlpha = CSng(Math.Exp(-1.0 / tauSteps))
        _intensityScale = CSng(intensityScale)

        ' the image sources (from the green channel of the scene image)
        If _sourceCount > 0 Then
            Dim index(_sourceCount - 1) As Integer
            Dim level(_sourceCount - 1) As Integer
            For i As Integer = 0 To _sourceCount - 1
                index(i) = (scene.SourceY(i) + 1) * _stride + scene.SourceX(i) + 1
                level(i) = CInt(scene.SourceLevel(i))
            Next
            _indexBuffer = CreateIntBuffer(index)
            _levelBuffer = CreateIntBuffer(level)
        End If

        ' the object sources (Sources tab)
        If _objSourceCount > 0 Then
            Dim objIndex(_objSourceCount - 1) As Integer
            Dim objLevel(_objSourceCount - 1) As Integer
            Dim objAmplitude(_objSourceCount - 1) As Single
            Dim objPhase(_objSourceCount - 1) As Single
            Dim objMode(_objSourceCount - 1) As Integer
            Dim objPulseShape(_objSourceCount - 1) As Integer
            Dim objOnWidth(_objSourceCount - 1) As Integer
            Dim objOffWidth(_objSourceCount - 1) As Integer
            Dim objDetachStep(_objSourceCount - 1) As Integer
            Dim objOffWidthBase(_objSourceCount - 1) As Integer
            Dim objRandomizeOffWidthAmount(_objSourceCount - 1) As Integer
            Dim objCycleAnchor(_objSourceCount - 1) As Single
            Dim objRngState(_objSourceCount - 1) As Integer
            ' a local, host-side RNG just to seed each element's on-GPU xorshift32 generator - xorshift needs a
            ' nonzero starting state, so 0 is never allowed
            Dim seedRng As New Random()
            For i As Integer = 0 To _objSourceCount - 1
                Dim el As SourceElement = objectSources(i)
                objIndex(i) = (el.PosY + 1) * _stride + el.PosX + 1
                objLevel(i) = el.Level
                objAmplitude(i) = CSng(el.Amplitude)
                objPhase(i) = CSng(el.PhaseRad)
                objMode(i) = If(el.Mode = SourceMode.Pulsed, 1, 0)
                objPulseShape(i) = If(el.PulseShape = SourcePulseShape.SmoothedSquare, 1, 0)
                objOnWidth(i) = el.OnWidthCycles
                objOffWidth(i) = el.OffWidthCycles
                objDetachStep(i) = el.DetachAfterStep
                objOffWidthBase(i) = el.OffWidthBase
                objRandomizeOffWidthAmount(i) = el.RandomizeOffWidthAmount
                objCycleAnchor(i) = 0.0F
                Dim seed As Integer = seedRng.Next(Integer.MinValue, Integer.MaxValue)
                If seed = 0 Then seed = 1
                objRngState(i) = seed
            Next
            _objIndexBuffer = CreateIntBuffer(objIndex)
            _objLevelBuffer = CreateIntBuffer(objLevel)
            _objAmplitudeBuffer = CreateFloatBuffer(objAmplitude)
            _objPhaseBuffer = CreateFloatBuffer(objPhase)
            _objModeBuffer = CreateIntBuffer(objMode)
            _objPulseShapeBuffer = CreateIntBuffer(objPulseShape)
            _objOnWidthBuffer = CreateIntBuffer(objOnWidth)
            _objOffWidthBuffer = CreateIntBuffer(objOffWidth)
            _objDetachStepBuffer = CreateIntBuffer(objDetachStep)
            _objOffWidthBaseBuffer = CreateIntBuffer(objOffWidthBase)
            _objRandomizeOffWidthAmountBuffer = CreateIntBuffer(objRandomizeOffWidthAmount)
            _objCycleAnchorBuffer = CreateFloatBuffer(objCycleAnchor)
            _objRngStateBuffer = CreateIntBuffer(objRngState)
        End If

        ' the kernels; kernel k reads field buffer k and writes into buffer 1 - k
        For k As Integer = 0 To 1
            _stepKernel(k) = CreateKernel("wave_step")
            SetMem(_stepKernel(k), 0UI, _field(k))
            SetMem(_stepKernel(k), 1UI, _field(1 - k))
            SetMem(_stepKernel(k), 2UI, _c2Buffer)
            SetMem(_stepKernel(k), 3UI, _dBuffer)
            SetMem(_stepKernel(k), 4UI, _intensityBuffer)
            SetInt(_stepKernel(k), 5UI, _width)
            SetInt(_stepKernel(k), 6UI, _height)
            SetInt(_stepKernel(k), 7UI, _stride)
            SetInt(_stepKernel(k), 8UI, If(_track, 1, 0))
            SetFloat(_stepKernel(k), 9UI, _c2Mult)
            SetFloat(_stepKernel(k), 10UI, _intensityAlpha)
            SetMem(_stepKernel(k), 11UI, _intensityStage1Buffer)
            SetMem(_stepKernel(k), 12UI, _intensityOutBuffer)
            SetFloat(_stepKernel(k), 13UI, _intensityScale)

            If _sourceCount > 0 Then
                _sourceKernel(k) = CreateKernel("apply_sources")
                SetMem(_sourceKernel(k), 0UI, _field(k))
                SetMem(_sourceKernel(k), 1UI, _indexBuffer)
                SetMem(_sourceKernel(k), 2UI, _levelBuffer)
                SetMem(_sourceKernel(k), 3UI, _frequencyBuffer)
                SetFloat(_sourceKernel(k), 5UI, _amplitude)
                SetInt(_sourceKernel(k), 6UI, _sourceCount)
            End If

            If _objSourceCount > 0 Then
                _objSourceKernel(k) = CreateKernel("apply_object_sources")
                SetMem(_objSourceKernel(k), 0UI, _field(k))
                SetMem(_objSourceKernel(k), 1UI, _objIndexBuffer)
                SetMem(_objSourceKernel(k), 2UI, _objLevelBuffer)
                SetMem(_objSourceKernel(k), 3UI, _objAmplitudeBuffer)
                SetMem(_objSourceKernel(k), 4UI, _objPhaseBuffer)
                SetMem(_objSourceKernel(k), 5UI, _objModeBuffer)
                SetMem(_objSourceKernel(k), 6UI, _objPulseShapeBuffer)
                SetMem(_objSourceKernel(k), 7UI, _objOnWidthBuffer)
                SetMem(_objSourceKernel(k), 8UI, _objOffWidthBuffer)
                SetMem(_objSourceKernel(k), 9UI, _objDetachStepBuffer)
                SetMem(_objSourceKernel(k), 10UI, _frequencyBuffer)
                SetInt(_objSourceKernel(k), 12UI, _objSourceCount)
                ' appended at the end rather than renumbering 0-12, so every SetMem/SetInt call above (and the
                ' per-call SetFloat for t at index 11 in AdvanceOneStep) keeps its existing, already-correct index
                SetMem(_objSourceKernel(k), 13UI, _objOffWidthBaseBuffer)
                SetMem(_objSourceKernel(k), 14UI, _objRandomizeOffWidthAmountBuffer)
                SetMem(_objSourceKernel(k), 15UI, _objCycleAnchorBuffer)
                SetMem(_objSourceKernel(k), 16UI, _objRngStateBuffer)
            End If
        Next

        _grid2D(0) = New IntPtr(_width)
        _grid2D(1) = New IntPtr(_height)
        _grid1D(0) = New IntPtr(Math.Max(1, _sourceCount))
        _objGrid1D(0) = New IntPtr(Math.Max(1, _objSourceCount))

        _hostField = New Single(_cells - 1) {}
        If _track Then _hostIntensity = New Single(_cells - 1) {}
        _current = 0
    End Sub

    ''' <summary>
    ''' The number of smaller time steps (see the class summary) that keep c2 * |lambda max| of the 4th order
    ''' stencil under the same target margin as WaveSimulator.ComputeSubSteps on the CPU (identical formula);
    ''' 1 when that is already true for the given (highest, in this scene) c^2.
    ''' </summary>
    Private Shared Function ComputeSubSteps(maxC2 As Single) As Integer
        ' the 4th order stencil's largest |eigenvalue| (32/3) and the same stability safety margin (2.0) as
        ' WaveSimulator.FourthOrderLambdaMax / SubStepTargetLimit on the CPU - see that class's comments
        Const LambdaMax4th As Single = 32.0F / 3.0F
        Const TargetLimit As Single = 2.0F
        If maxC2 <= 0.0F Then Return 1
        Dim n As Double = Math.Ceiling(Math.Sqrt(CDbl(maxC2) * CDbl(LambdaMax4th) / CDbl(TargetLimit)))
        Return Math.Max(1, CInt(n))
    End Function

    ''' <summary>
    ''' The OpenCL C code of the two kernels. use4thOrderLaplacian selects, at compile time (a #define, not a
    ''' runtime branch), which form of the Laplacian wave_step uses - see the class summary and
    ''' WaveSimulator.UpdateField, whose CPU-side stencils and boundary logic this mirrors exactly. wave_step's
    ''' c2Scale argument is the sub-stepping scale (see the class summary), and its intensityAlpha argument is the
    ''' intensity's exponential-average decay factor (_intensityAlpha) - both are runtime kernel arguments rather
    ''' than a #define, so they need no text formatting/locale handling here.
    ''' </summary>
    Private Shared Function KernelSource(use4thOrderLaplacian As Boolean) As String
        Dim s As New StringBuilder()
        s.AppendLine("#define USE_4TH_ORDER " & If(use4thOrderLaplacian, "1", "0"))
        s.AppendLine()
        s.AppendLine("__kernel void wave_step(__global const float* u, __global float* up, __global const float* c2,")
        s.AppendLine("                        __global const float* d, __global float* inten,")
        s.AppendLine("                        const int w, const int h, const int stride, const int track, const float c2Scale,")
        s.AppendLine("                        const float intensityAlpha, __global float* inten1, __global float* intenOut,")
        s.AppendLine("                        const float intensityScale)")
        s.AppendLine("{")
        s.AppendLine("    int x = get_global_id(0);")
        s.AppendLine("    int y = get_global_id(1);")
        s.AppendLine("    if (x >= w || y >= h) return;")
        s.AppendLine("    int i = (y + 1) * stride + x + 1;")
        s.AppendLine("    float uc = u[i];")
        s.AppendLine("    float lap;")
        s.AppendLine("#if USE_4TH_ORDER")
        ' the 4th order 5-point "cross" stencil (reaches 2 cells away along x and along y), only where the domain
        ' has that much room - the outermost ring (x = 0, x = w - 1, y = 0, y = h - 1) falls back to the 3x3
        ' kernel below, the same boundary rule as WaveSimulator.UpdateField on the CPU
        s.AppendLine("    if (x >= 1 && x <= w - 2 && y >= 1 && y <= h - 2) {")
        s.AppendLine("        lap = (-1.0f / 12.0f) * (u[i - 2] + u[i + 2] + u[i - 2 * stride] + u[i + 2 * stride])")
        s.AppendLine("            + (4.0f / 3.0f) * (u[i - 1] + u[i + 1] + u[i - stride] + u[i + stride])")
        s.AppendLine("            - 5.0f * uc;")
        s.AppendLine("    } else {")
        s.AppendLine("        lap = 0.066f * (u[i - stride - 1] + u[i - stride + 1] + u[i + stride - 1] + u[i + stride + 1])")
        s.AppendLine("            + 0.184f * (u[i - 1] + u[i + 1] + u[i - stride] + u[i + stride]) - uc;")
        s.AppendLine("    }")
        s.AppendLine("#else")
        s.AppendLine("    lap = 0.066f * (u[i - stride - 1] + u[i - stride + 1] + u[i + stride - 1] + u[i + stride + 1])")
        s.AppendLine("        + 0.184f * (u[i - 1] + u[i + 1] + u[i - stride] + u[i + stride]) - uc;")
        s.AppendLine("#endif")
        s.AppendLine("    float nv = uc + (uc - up[i]) * d[i] + lap * c2[i] * c2Scale;")
        s.AppendLine("    up[i] = nv;")
        ' two cascaded one-pole stages, blended (0.5f - see WaveSimulator.IntensityBlend, CPU engine) into what is
        ' read back/probed (intenOut) instead of using the full 2-stage cascade outright - see the class summary
        s.AppendLine("    if (track != 0) {")
        s.AppendLine("        float s1 = inten1[i] * intensityAlpha + nv * nv * (1.0f - intensityAlpha);")
        s.AppendLine("        inten1[i] = s1;")
        s.AppendLine("        float s2 = inten[i] * intensityAlpha + s1 * (1.0f - intensityAlpha);")
        s.AppendLine("        inten[i] = s2;")
        s.AppendLine("        intenOut[i] = (s1 * 0.5f + s2 * 0.5f) * intensityScale;")
        s.AppendLine("    }")
        s.AppendLine("}")
        s.AppendLine()
        s.AppendLine("__kernel void apply_sources(__global float* u, __global const int* idx, __global const int* level,")
        s.AppendLine("                            __global const float* freq, const float t, const float amp, const int n)")
        s.AppendLine("{")
        s.AppendLine("    int k = get_global_id(0);")
        s.AppendLine("    if (k >= n) return;")
        s.AppendLine("    int p = idx[k];")
        s.AppendLine("    u[p] = u[p] * 0.9f + sin(freq[level[k]] * t) * amp * 0.1f;")
        s.AppendLine("}")
        s.AppendLine()
        ' xorshift32: OpenCL has no built-in RNG, so object sources that re-randomize their off-width (see
        ' SourceElement.RandomizeOffWidthAmount) carry their own per-element state (rngState, seeded host-side,
        ' nonzero) through this. Matches WaveSimulator's use of SourceSettings.RandomInt only in spirit (a uniform
        ' draw in [-amount, amount]), not bit-for-bit - the two engines are not expected to reproduce each other.
        s.AppendLine("inline uint xorshift32(uint x)")
        s.AppendLine("{")
        s.AppendLine("    x ^= x << 13;")
        s.AppendLine("    x ^= x >> 17;")
        s.AppendLine("    x ^= x << 5;")
        s.AppendLine("    return x;")
        s.AppendLine("}")
        s.AppendLine()
        ' object sources (Sources tab): same math as WaveSimulator.ApplySources/ComputePulsePosition/PulseEnvelope
        ' on the CPU, one element per work item; mode 0 = continuous (envelope always 1), 1 = pulsed (Gaussian or
        ' smoothed-square envelope, "off" preceding "on" within each onWidth/offWidth cycle of the element's own
        ' frequency). offWidth is read-write: for an element with offWidthRandomizeAmount[k] > 0 this kernel redraws
        ' it (uniformly within +/- offWidthRandomizeAmount of offWidthBase) every time that element completes a full
        ' off+on cycle, advancing its own cycleAnchor[k] to mark where the new cycle started - so, unlike the other
        ' object-source buffers, offWidth/cycleAnchor/rngState are genuinely mutated by the GPU across steps.
        s.AppendLine("__kernel void apply_object_sources(__global float* u, __global const int* idx, __global const int* level,")
        s.AppendLine("                                   __global const float* amp, __global const float* phase, __global const int* mode,")
        s.AppendLine("                                   __global const int* pulseShape, __global const int* onWidth, __global int* offWidth,")
        s.AppendLine("                                   __global const int* detachStep,")
        s.AppendLine("                                   __global const float* freq, const float t, const int n,")
        s.AppendLine("                                   __global const int* offWidthBase, __global const int* offWidthRandomizeAmount,")
        s.AppendLine("                                   __global float* cycleAnchor, __global int* rngState)")
        s.AppendLine("{")
        s.AppendLine("    int k = get_global_id(0);")
        s.AppendLine("    if (k >= n) return;")
        ' "detach source": once the run reaches the element's detach step, its drive is faded out linearly over the
        ' next DetachRampSteps steps (rather than cut instantly), then it stops driving the field entirely (not even
        ' writing towards 0) so it releases its hold and stops diffracting waves through that point - matching
        ' WaveSimulator.ApplySources exactly (SourceElement.DetachRampSteps is the single shared constant).
        s.AppendLine("    float detachFade = 1.0f;")
        s.AppendLine("    if (detachStep[k] >= 0) {")
        s.AppendLine("        float stepsSinceDetach = t - (float)detachStep[k];")
        s.AppendLine("        if (stepsSinceDetach >= " & SourceElement.DetachRampSteps.ToString() & ".0f) return;")
        s.AppendLine("        if (stepsSinceDetach > 0.0f) detachFade = 1.0f - stepsSinceDetach / " & SourceElement.DetachRampSteps.ToString() & ".0f;")
        s.AppendLine("    }")
        s.AppendLine("    float f = freq[level[k]];")
        s.AppendLine("    float envelope = 1.0f;")
        s.AppendLine("    if (mode[k] != 0) {")
        s.AppendLine("        int ow = offWidth[k];")
        s.AppendLine("        int period = onWidth[k] + ow;")
        s.AppendLine("        if (period > 0) {")
        s.AppendLine("            float cycleClock = f * t / (2.0f * M_PI_F);")
        s.AppendLine("            float pos;")
        s.AppendLine("            int isFirstCycle;")
        s.AppendLine("            if (offWidthRandomizeAmount[k] <= 0) {")
        ' shifted so t = 0 lands at pos = ow (the start of the "on" span), not pos = 0 (the start of an "off" span) -
        ' a single/non-randomized source starting silent for its off-width was never the intent (see
        ' WaveSimulator.ComputePulsePosition, kept in exact agreement with this kernel)
        s.AppendLine("                float adjustedClock = cycleClock + (float)ow;")
        s.AppendLine("                isFirstCycle = (floor(adjustedClock / (float)period) == 0.0f) ? 1 : 0;")
        s.AppendLine("                pos = adjustedClock - period * floor(adjustedClock / (float)period);")
        s.AppendLine("            } else {")
        s.AppendLine("                pos = cycleClock - cycleAnchor[k];")
        s.AppendLine("                int guard = 0;")
        s.AppendLine("                while (pos >= (float)period && guard < 1000) {")
        s.AppendLine("                    cycleAnchor[k] += (float)period;")
        s.AppendLine("                    pos -= (float)period;")
        s.AppendLine("                    uint state = xorshift32((uint)rngState[k]);")
        s.AppendLine("                    rngState[k] = (int)state;")
        s.AppendLine("                    int amt = offWidthRandomizeAmount[k];")
        s.AppendLine("                    int span = 2 * amt + 1;")
        s.AppendLine("                    int r = (int)(state % (uint)span) - amt;")
        s.AppendLine("                    ow = offWidthBase[k] + r;")
        s.AppendLine("                    if (ow < 1) ow = 1;")
        s.AppendLine("                    offWidth[k] = ow;")
        s.AppendLine("                    period = onWidth[k] + ow;")
        s.AppendLine("                    guard++;")
        s.AppendLine("                }")
        s.AppendLine("                if (pos < 0.0f) pos = 0.0f;")
        ' the anchor only ever advances (above) once a cycle has actually completed, so it still reading its initial
        ' 0 means no cycle has completed yet - we are still in the element's very first (off-first) cycle
        s.AppendLine("                isFirstCycle = (cycleAnchor[k] == 0.0f) ? 1 : 0;")
        s.AppendLine("            }")
        s.AppendLine("            if (pulseShape[k] == 0) {")
        s.AppendLine("                if (pos < (float)ow) {")
        s.AppendLine("                    envelope = 0.0f;")
        s.AppendLine("                } else {")
        s.AppendLine("                    float onPos = pos - (float)ow;")
        s.AppendLine("                    float center = onWidth[k] / 2.0f;")
        s.AppendLine("                    float sigma = max(onWidth[k] / 6.0f, 1.0e-6f);")
        s.AppendLine("                    float dd = (onPos - center) / sigma;")
        s.AppendLine("                    envelope = exp(-0.5f * dd * dd);")
        s.AppendLine("                }")
        s.AppendLine("            } else {")
        s.AppendLine("                // a flat-topped pulse (envelope 1 while on, 0 while off) with a short, FIXED-width")
        s.AppendLine("                // (in cycles) smoothed edge at the rise and the fall - fixed, so a long off span")
        s.AppendLine("                // sits at (very close to) 0 for nearly all of it instead of lingering. The math is")
        s.AppendLine("                // exactly the on-first formula, fed a coordinate shifted so the on span starts at 0.")
        s.AppendLine("                float shifted = pos - (float)ow;")
        ' wrapping shifted forward by a full period makes it continue the falling tail of the on-span that JUST
        ' ended - correct from the second off-span onward, where that on-span really happened. The very first
        ' off-span (isFirstCycle) has no real preceding on-emission to continue from, so wrapping there would
        ' fabricate one: pos = 0 would land exactly on that phantom falling edge and start the run at envelope ~0.5
        ' instead of ~0, emitting a spurious one-cycle "blip" before the source actually goes quiet. Left un-wrapped
        ' (deeply negative) instead, dRise/dFall below correctly place it far from both edges, keeping the envelope
        ' at (very close to) 0 until the real rising edge - matching WaveSimulator.PulseEnvelope exactly.
        s.AppendLine("                if (shifted < 0.0f && !isFirstCycle) shifted += (float)period;")
        s.AppendLine("                float edgeWidth = 0.35f;")
        s.AppendLine("                float dFall = shifted - (float)onWidth[k];")
        s.AppendLine("                float dRise = shifted;")
        s.AppendLine("                if (dRise > (float)period / 2.0f) dRise -= (float)period;")
        s.AppendLine("                float rising = 0.5f + atan(dRise / edgeWidth) / M_PI_F;")
        s.AppendLine("                float falling = 0.5f - atan(dFall / edgeWidth) / M_PI_F;")
        s.AppendLine("                envelope = rising * falling;")
        s.AppendLine("            }")
        s.AppendLine("        }")
        s.AppendLine("    }")
        s.AppendLine("    float v = sin(phase[k] + f * t) * amp[k] * envelope * detachFade;")
        s.AppendLine("    int p = idx[k];")
        s.AppendLine("    u[p] = u[p] * 0.9f + v * 0.1f;")
        s.AppendLine("}")
        Return s.ToString()
    End Function

    ' ---- small OpenCL helpers

    Private Function CreateFloatBuffer(data As Single()) As IntPtr
        Dim bytes As New IntPtr(CLng(data.Length) * 4L)
        Dim code As Integer = 0
        Dim buffer As IntPtr = OpenClApi.clCreateBuffer(_context, OpenClApi.MemReadWrite, bytes, IntPtr.Zero, code)
        OpenClApi.Check(code, "clCreateBuffer")
        OpenClApi.Check(OpenClApi.clEnqueueWriteBufferFloat(_queue, buffer, 1UI, IntPtr.Zero, bytes, data, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueWriteBuffer")
        Return buffer
    End Function

    Private Function CreateIntBuffer(data As Integer()) As IntPtr
        Dim bytes As New IntPtr(CLng(data.Length) * 4L)
        Dim code As Integer = 0
        Dim buffer As IntPtr = OpenClApi.clCreateBuffer(_context, OpenClApi.MemReadWrite, bytes, IntPtr.Zero, code)
        OpenClApi.Check(code, "clCreateBuffer")
        OpenClApi.Check(OpenClApi.clEnqueueWriteBufferInt(_queue, buffer, 1UI, IntPtr.Zero, bytes, data, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueWriteBuffer")
        Return buffer
    End Function

    Private Function CreateKernel(name As String) As IntPtr
        Return CreateKernelFrom(_program, name)
    End Function

    Private Function CreateKernelFrom(program As IntPtr, name As String) As IntPtr
        Dim code As Integer = 0
        Dim kernel As IntPtr = OpenClApi.clCreateKernel(program, name, code)
        OpenClApi.Check(code, "clCreateKernel " & name)
        Return kernel
    End Function

    Private Shared Sub SetMem(kernel As IntPtr, index As UInteger, value As IntPtr)
        OpenClApi.Check(OpenClApi.clSetKernelArgMem(kernel, index, New IntPtr(IntPtr.Size), value), "clSetKernelArg")
    End Sub

    Private Shared Sub SetInt(kernel As IntPtr, index As UInteger, value As Integer)
        OpenClApi.Check(OpenClApi.clSetKernelArgInt(kernel, index, New IntPtr(4), value), "clSetKernelArg")
    End Sub

    Private Shared Sub SetFloat(kernel As IntPtr, index As UInteger, value As Single)
        OpenClApi.Check(OpenClApi.clSetKernelArgFloat(kernel, index, New IntPtr(4), value), "clSetKernelArg")
    End Sub

    ' ---- IWaveEngine

    Public ReadOnly Property Description As String Implements IWaveEngine.Description
        Get
            Return _description
        End Get
    End Property

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

    Public ReadOnly Property Stride As Integer Implements IWaveEngine.Stride
        Get
            Return _stride
        End Get
    End Property

    Public ReadOnly Property FieldData As Single() Implements IWaveEngine.FieldData
        Get
            Return _hostField
        End Get
    End Property

    Public ReadOnly Property IntensityData As Single() Implements IWaveEngine.IntensityData
        Get
            Return _hostIntensity
        End Get
    End Property

    Public Sub AdvanceOneStep() Implements IWaveEngine.AdvanceOneStep
        Dim k As Integer = _current

        If _sourceCount > 0 Then
            SetFloat(_sourceKernel(k), 4UI, CSng(_time))
            OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(_queue, _sourceKernel(k), 1UI, Nothing, _grid1D, Nothing, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (sources)")
        End If
        If _objSourceCount > 0 Then
            SetFloat(_objSourceKernel(k), 11UI, CSng(_time))
            OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(_queue, _objSourceKernel(k), 1UI, Nothing, _objGrid1D, Nothing, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (object sources)")
        End If

        If _subSteps <= 1 Then
            ' unchanged single-launch path: the "track" argument was bound once, at Build time, to If(_track, 1, 0)
            OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(_queue, _stepKernel(k), 2UI, Nothing, _grid2D, Nothing, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (step)")
            k = 1 - k
        Else
            ' _subSteps smaller launches (see the class summary); only the last one updates the intensity buffer,
            ' so its own exponential averaging still runs once per AdvanceOneStep call, not _subSteps times
            For subStep As Integer = 1 To _subSteps
                Dim trackNow As Integer = If(subStep = _subSteps AndAlso _track, 1, 0)
                SetInt(_stepKernel(k), 8UI, trackNow)
                OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(_queue, _stepKernel(k), 2UI, Nothing, _grid2D, Nothing, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (step)")
                k = 1 - k
            Next
        End If

        ' the buffer that received the new values is now the current field
        _current = k
        _time += 1.0

        ' now and then wait for the card, so that the commands do not pile up and errors show up in time
        _sinceFinish += 1
        If _sinceFinish >= 16 Then
            _sinceFinish = 0
            OpenClApi.Check(OpenClApi.clFinish(_queue), "clFinish")
        End If
    End Sub

    Public Sub PrepareFrame(needField As Boolean, needIntensity As Boolean) Implements IWaveEngine.PrepareFrame
        Dim bytes As New IntPtr(CLng(_cells) * 4L)
        If needField Then
            OpenClApi.Check(OpenClApi.clEnqueueReadBufferFloat(_queue, _field(_current), 1UI, IntPtr.Zero, bytes, _hostField, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueReadBuffer")
        End If
        If needIntensity AndAlso _track Then
            OpenClApi.Check(OpenClApi.clEnqueueReadBufferFloat(_queue, _intensityOutBuffer, 1UI, IntPtr.Zero, bytes, _hostIntensity, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueReadBuffer")
        End If
        _sinceFinish = 0
    End Sub

    ' ---- probes

    Private Shared Function ProbeKernelSource() As String
        Dim s As New StringBuilder()
        ' point probes: the value of the pixel of every probe, into row "row" of the history
        s.AppendLine("__kernel void probe_sample(__global const float* u, __global const float* inten, __global const int* pix,")
        s.AppendLine("                           __global const int* info, __global float* hist, const int row, const int n)")
        s.AppendLine("{")
        s.AppendLine("    int p = get_global_id(0);")
        s.AppendLine("    if (p >= n) return;")
        s.AppendLine("    int start = info[p * 4];")
        s.AppendLine("    int count = info[p * 4 + 1];")
        s.AppendLine("    int useI = info[p * 4 + 2];")
        s.AppendLine("    int mean = info[p * 4 + 3];")
        s.AppendLine("    float s = 0.0f;")
        s.AppendLine("    if (useI != 0) {")
        s.AppendLine("        for (int k = 0; k < count; k++) s += inten[pix[start + k]];")
        s.AppendLine("    } else {")
        s.AppendLine("        for (int k = 0; k < count; k++) s += u[pix[start + k]];")
        s.AppendLine("    }")
        s.AppendLine("    if (mean != 0 && count > 0) s = s / (float)count;")
        s.AppendLine("    hist[row * n + p] = s;")
        s.AppendLine("}")
        s.AppendLine()
        ' line probes: mode 0 = field, 1 = intensity, 2 = intensity totals over the steps; pStartFlag/pStartStep are the
        ' probe's "start recording at step #" (0/1 and the step): before that step the total is left at zero, so
        ' intensity that arrived earlier is purged rather than baked into the total once recording starts
        s.AppendLine("__kernel void profile_accumulate(__global const float* inten, __global const int* ppix,")
        s.AppendLine("                                 __global const int* pmode, __global const int* pStartFlag, __global const int* pStartStep,")
        s.AppendLine("                                 __global float* total, const int step, const int n)")
        s.AppendLine("{")
        s.AppendLine("    int k = get_global_id(0);")
        s.AppendLine("    if (k >= n) return;")
        s.AppendLine("    if (pmode[k] != 2) return;")
        s.AppendLine("    if (pStartFlag[k] != 0 && step < pStartStep[k]) return;")
        s.AppendLine("    total[k] += inten[ppix[k]];")
        s.AppendLine("}")
        s.AppendLine()
        s.AppendLine("__kernel void profile_gather(__global const float* u, __global const float* inten, __global const int* ppix,")
        s.AppendLine("                             __global const int* pmode, __global const float* total, __global float* outv, const int n)")
        s.AppendLine("{")
        s.AppendLine("    int k = get_global_id(0);")
        s.AppendLine("    if (k >= n) return;")
        s.AppendLine("    int m = pmode[k];")
        s.AppendLine("    float v;")
        s.AppendLine("    if (m == 2) v = total[k];")
        s.AppendLine("    else if (m == 1) v = inten[ppix[k]];")
        s.AppendLine("    else v = u[ppix[k]];")
        s.AppendLine("    outv[k] = v;")
        s.AppendLine("}")
        Return s.ToString()
    End Function

    Public Sub SetProbes(sampler As ProbeSampler) Implements IWaveEngine.SetProbes
        If sampler Is Nothing OrElse sampler.Count = 0 Then Return

        Try
            If sampler.NeedsIntensity AndAlso Not _track Then
                Throw New InvalidOperationException("the intensity is not tracked")
            End If

            Dim code As Integer = 0
            Dim devs() As IntPtr = {_deviceId}

            Dim sources() As String = {ProbeKernelSource()}
            _probeProgram = OpenClApi.clCreateProgramWithSource(_context, 1UI, sources, Nothing, code)
            OpenClApi.Check(code, "clCreateProgramWithSource (probes)")
            code = OpenClApi.clBuildProgram(_probeProgram, 1UI, devs, "", IntPtr.Zero, IntPtr.Zero)
            If code <> 0 Then
                Dim log As String = OpenClApi.BuildLog(_probeProgram, _deviceId)
                Throw New InvalidOperationException("the probe program for the GPU could not be built (" & OpenClApi.ErrorName(code) & ")" &
                                                    If(log.Length > 0, ": " & log, ""))
            End If

            If sampler.HasSeries Then SetUpSeriesProbes(sampler)
            If sampler.HasProfiles Then SetUpProfileProbes(sampler)
            _probeSampler = sampler
        Catch ex As Exception
            Throw New InvalidOperationException("the probes could not be set up on the GPU: " & ex.Message, ex)
        End Try
    End Sub

    Private Sub SetUpSeriesProbes(sampler As ProbeSampler)
        Dim n As Integer = sampler.Count
        Dim code As Integer = 0

        ' the pixels of the point probes in one list; info = start, count, uses intensity, average (not sum) per probe
        Dim total As Integer = 0
        For p As Integer = 0 To n - 1
            If Not sampler.IsProfile(p) Then total += sampler.Pixels(p).Length
        Next
        Dim pix(Math.Max(total, 1) - 1) As Integer
        Dim info(n * 4 - 1) As Integer
        Dim position As Integer = 0
        For p As Integer = 0 To n - 1
            If sampler.IsProfile(p) Then Continue For
            Dim px As Integer() = sampler.Pixels(p)
            Array.Copy(px, 0, pix, position, px.Length)
            info(p * 4) = position
            info(p * 4 + 1) = px.Length
            info(p * 4 + 2) = If(sampler.UseIntensity(p), 1, 0)
            info(p * 4 + 3) = 1
            position += px.Length
        Next
        _pixelBuffer = CreateIntBuffer(pix)
        _infoBuffer = CreateIntBuffer(info)

        Dim historyBytes As New IntPtr(CLng(ProbeRows) * n * 4L)
        _historyBuffer = OpenClApi.clCreateBuffer(_context, OpenClApi.MemReadWrite, historyBytes, IntPtr.Zero, code)
        OpenClApi.Check(code, "clCreateBuffer (probes)")

        For k As Integer = 0 To 1
            _probeKernel(k) = CreateKernelFrom(_probeProgram, "probe_sample")
            SetMem(_probeKernel(k), 0UI, _field(k))
            SetMem(_probeKernel(k), 1UI, _intensityOutBuffer)
            SetMem(_probeKernel(k), 2UI, _pixelBuffer)
            SetMem(_probeKernel(k), 3UI, _infoBuffer)
            SetMem(_probeKernel(k), 4UI, _historyBuffer)
            SetInt(_probeKernel(k), 5UI, 0)
            SetInt(_probeKernel(k), 6UI, n)
        Next

        _probeGrid(0) = New IntPtr(n)
        _probeValues.Clear()
        For i As Integer = 0 To n - 1
            _probeValues.Add(New List(Of Single)())
        Next
        _probeRow = 0
    End Sub

    Private Sub SetUpProfileProbes(sampler As ProbeSampler)
        Dim n As Integer = sampler.Count

        Dim total As Integer = 0
        For p As Integer = 0 To n - 1
            If sampler.IsProfile(p) Then total += sampler.Pixels(p).Length
        Next
        Dim pix(total - 1) As Integer
        Dim mode(total - 1) As Integer
        Dim startFlag(total - 1) As Integer
        Dim startStep(total - 1) As Integer
        _profileStart = New Integer(n - 1) {}
        _profileIntegrates = False

        Dim position As Integer = 0
        For p As Integer = 0 To n - 1
            _profileStart(p) = position
            If Not sampler.IsProfile(p) Then Continue For

            Dim px As Integer() = sampler.Pixels(p)
            Dim m As Integer = If(sampler.Integrate(p), 2, If(sampler.UseIntensity(p), 1, 0))
            If m = 2 Then _profileIntegrates = True
            Dim useStart As Integer = If(sampler.UseStartAfterStep(p), 1, 0)
            Dim afterStep As Integer = sampler.StartAfterStep(p)
            For k As Integer = 0 To px.Length - 1
                pix(position + k) = px(k)
                mode(position + k) = m
                startFlag(position + k) = useStart
                startStep(position + k) = afterStep
            Next
            position += px.Length
        Next
        _profileCount = total

        _profilePixelBuffer = CreateIntBuffer(pix)
        _profileModeBuffer = CreateIntBuffer(mode)
        _profileTotalBuffer = CreateFloatBuffer(New Single(total - 1) {})
        _profileOutBuffer = CreateFloatBuffer(New Single(total - 1) {})

        For k As Integer = 0 To 1
            _gatherKernel(k) = CreateKernelFrom(_probeProgram, "profile_gather")
            SetMem(_gatherKernel(k), 0UI, _field(k))
            SetMem(_gatherKernel(k), 1UI, _intensityOutBuffer)
            SetMem(_gatherKernel(k), 2UI, _profilePixelBuffer)
            SetMem(_gatherKernel(k), 3UI, _profileModeBuffer)
            SetMem(_gatherKernel(k), 4UI, _profileTotalBuffer)
            SetMem(_gatherKernel(k), 5UI, _profileOutBuffer)
            SetInt(_gatherKernel(k), 6UI, total)
        Next

        If _profileIntegrates Then
            _profileStartFlagBuffer = CreateIntBuffer(startFlag)
            _profileStartStepBuffer = CreateIntBuffer(startStep)
            _accumulateKernel = CreateKernelFrom(_probeProgram, "profile_accumulate")
            SetMem(_accumulateKernel, 0UI, _intensityOutBuffer)
            SetMem(_accumulateKernel, 1UI, _profilePixelBuffer)
            SetMem(_accumulateKernel, 2UI, _profileModeBuffer)
            SetMem(_accumulateKernel, 3UI, _profileStartFlagBuffer)
            SetMem(_accumulateKernel, 4UI, _profileStartStepBuffer)
            SetMem(_accumulateKernel, 5UI, _profileTotalBuffer)
            SetInt(_accumulateKernel, 7UI, total)
        End If

        _profileGrid(0) = New IntPtr(total)
    End Sub

    Public Sub SampleProbes() Implements IWaveEngine.SampleProbes
        If _probeSampler Is Nothing Then Return

        Dim k As Integer = _current
        If _probeSampler.HasSeries Then
            If _probeRow >= ProbeRows Then ReadProbeHistory()
            SetInt(_probeKernel(k), 5UI, _probeRow)
            OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(_queue, _probeKernel(k), 1UI, Nothing, _probeGrid, Nothing, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (probes)")
            _probeRow += 1
        End If

        ' the intensity totals of the lines that are integrated: add the intensity of this step (unless it is before
        ' that probe's "start recording at step #", in which case the kernel leaves the total at zero)
        If _profileIntegrates Then
            SetInt(_accumulateKernel, 6UI, CInt(_time))
            OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(_queue, _accumulateKernel, 1UI, Nothing, _profileGrid, Nothing, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (totals)")
        End If
    End Sub

    ''' <summary>Copies the measured values from the graphics memory into the lists.</summary>
    Private Sub ReadProbeHistory()
        If _probeSampler Is Nothing OrElse _probeRow = 0 Then Return

        Dim n As Integer = _probeSampler.Count
        Dim values(_probeRow * n - 1) As Single
        OpenClApi.Check(OpenClApi.clEnqueueReadBufferFloat(_queue, _historyBuffer, 1UI, IntPtr.Zero, New IntPtr(CLng(values.Length) * 4L), values, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueReadBuffer (probes)")
        For r As Integer = 0 To _probeRow - 1
            For p As Integer = 0 To n - 1
                If Not _probeSampler.IsProfile(p) Then _probeValues(p).Add(values(r * n + p))
            Next
        Next
        _probeRow = 0
        _sinceFinish = 0
    End Sub

    Public Function TakeProbeSamples() As Single()() Implements IWaveEngine.TakeProbeSamples
        If _probeSampler Is Nothing Then Return New Single()() {}

        Dim n As Integer = _probeSampler.Count
        Dim result As Single()() = New Single(n - 1)() {}
        If _probeSampler.HasSeries Then ReadProbeHistory()
        For p As Integer = 0 To n - 1
            If _probeSampler.IsProfile(p) Then
                result(p) = New Single(-1) {}
            Else
                result(p) = _probeValues(p).ToArray()
                _probeValues(p).Clear()
            End If
        Next
        Return result
    End Function

    Public Function ReadProbeProfiles() As Single()() Implements IWaveEngine.ReadProbeProfiles
        If _probeSampler Is Nothing Then Return New Single()() {}

        Dim n As Integer = _probeSampler.Count
        Dim result As Single()() = New Single(n - 1)() {}
        If Not _probeSampler.HasProfiles Then Return result

        Dim k As Integer = _current
        OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(_queue, _gatherKernel(k), 1UI, Nothing, _profileGrid, Nothing, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (profiles)")

        Dim values As Single() = New Single(_profileCount - 1) {}
        OpenClApi.Check(OpenClApi.clEnqueueReadBufferFloat(_queue, _profileOutBuffer, 1UI, IntPtr.Zero, New IntPtr(CLng(_profileCount) * 4L), values, 0UI, IntPtr.Zero, IntPtr.Zero), "clEnqueueReadBuffer (profiles)")
        _sinceFinish = 0

        For p As Integer = 0 To n - 1
            If Not _probeSampler.IsProfile(p) Then Continue For
            Dim length As Integer = _probeSampler.Pixels(p).Length
            Dim profile As Single() = New Single(length - 1) {}
            Array.Copy(values, _profileStart(p), profile, 0, length)
            result(p) = profile
        Next
        Return result
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        ReleaseAll()
    End Sub

    Private Sub ReleaseAll()
        If _released Then Return
        _released = True

        Try
            If _queue <> IntPtr.Zero Then OpenClApi.clFinish(_queue)
        Catch ex As Exception
            ' the driver is gone or in trouble; release what can be released
        End Try

        For k As Integer = 0 To 1
            ReleaseKernel(_stepKernel(k))
            ReleaseKernel(_sourceKernel(k))
            ReleaseKernel(_objSourceKernel(k))
            ReleaseKernel(_probeKernel(k))
            ReleaseKernel(_gatherKernel(k))
            ReleaseMem(_field(k))
        Next
        ReleaseKernel(_accumulateKernel)
        ReleaseMem(_pixelBuffer)
        ReleaseMem(_infoBuffer)
        ReleaseMem(_historyBuffer)
        ReleaseMem(_profilePixelBuffer)
        ReleaseMem(_profileModeBuffer)
        ReleaseMem(_profileStartFlagBuffer)
        ReleaseMem(_profileStartStepBuffer)
        ReleaseMem(_profileTotalBuffer)
        ReleaseMem(_profileOutBuffer)
        ReleaseMem(_c2Buffer)
        ReleaseMem(_dBuffer)
        ReleaseMem(_intensityBuffer)
        ReleaseMem(_intensityStage1Buffer)
        ReleaseMem(_intensityOutBuffer)
        ReleaseMem(_indexBuffer)
        ReleaseMem(_levelBuffer)
        ReleaseMem(_frequencyBuffer)
        ReleaseMem(_objIndexBuffer)
        ReleaseMem(_objLevelBuffer)
        ReleaseMem(_objAmplitudeBuffer)
        ReleaseMem(_objPhaseBuffer)
        ReleaseMem(_objModeBuffer)
        ReleaseMem(_objPulseShapeBuffer)
        ReleaseMem(_objOnWidthBuffer)
        ReleaseMem(_objOffWidthBuffer)
        ReleaseMem(_objDetachStepBuffer)
        ReleaseMem(_objOffWidthBaseBuffer)
        ReleaseMem(_objRandomizeOffWidthAmountBuffer)
        ReleaseMem(_objCycleAnchorBuffer)
        ReleaseMem(_objRngStateBuffer)

        Try
            If _probeProgram <> IntPtr.Zero Then OpenClApi.clReleaseProgram(_probeProgram)
            If _program <> IntPtr.Zero Then OpenClApi.clReleaseProgram(_program)
            If _queue <> IntPtr.Zero Then OpenClApi.clReleaseCommandQueue(_queue)
            If _context <> IntPtr.Zero Then OpenClApi.clReleaseContext(_context)
        Catch ex As Exception
            ' nothing more can be done
        End Try
        _probeProgram = IntPtr.Zero
        _program = IntPtr.Zero
        _queue = IntPtr.Zero
        _context = IntPtr.Zero
    End Sub

    Private Shared Sub ReleaseMem(ByRef handle As IntPtr)
        Try
            If handle <> IntPtr.Zero Then OpenClApi.clReleaseMemObject(handle)
        Catch ex As Exception
            ' nothing more can be done
        End Try
        handle = IntPtr.Zero
    End Sub

    Private Shared Sub ReleaseKernel(ByRef handle As IntPtr)
        Try
            If handle <> IntPtr.Zero Then OpenClApi.clReleaseKernel(handle)
        Catch ex As Exception
            ' nothing more can be done
        End Try
        handle = IntPtr.Zero
    End Sub

End Class
