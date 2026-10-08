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

''' <summary>
''' A wave simulation engine: the CPU version (WaveSimulator) or the GPU version (GpuWaveSimulator).
''' Both keep the field and the intensity in arrays with a border of one cell, pixel (x, y) is at
''' (y + 1) * Stride + x + 1, and both are advanced one time step at a time.
''' </summary>
Friend Interface IWaveEngine
    Inherits IDisposable

    ''' <summary>Where the simulation runs, for example "CPU (8 threads)" or "GPU: NVIDIA GeForce MX130 (OpenCL)".</summary>
    ReadOnly Property Description As String

    ReadOnly Property Width As Integer
    ReadOnly Property Height As Integer

    ''' <summary>Distance between two rows in FieldData and IntensityData.</summary>
    ReadOnly Property Stride As Integer

    ''' <summary>The field of the last completed step, valid after PrepareFrame(True, ...). Do not change.</summary>
    ReadOnly Property FieldData As Single()

    ''' <summary>The averaged intensity, valid after PrepareFrame(..., True); Nothing when it is not tracked. Do not change.</summary>
    ReadOnly Property IntensityData As Single()

    ''' <summary>One time step: apply the sources, update the field, update the intensity.</summary>
    Sub AdvanceOneStep()

    ''' <summary>
    ''' Makes FieldData and/or IntensityData available for drawing. Does nothing on the CPU; on the GPU the data is
    ''' copied from the graphics memory, so only ask for what is going to be drawn.
    ''' </summary>
    Sub PrepareFrame(needField As Boolean, needIntensity As Boolean)

    ''' <summary>Tells the engine which values to measure for the probes (Nothing or no probes: nothing is measured).</summary>
    Sub SetProbes(sampler As ProbeSampler)

    ''' <summary>
    ''' Measures the probes in the state after the last AdvanceOneStep: one value per step for every point probe, and
    ''' the running totals of the line probes that integrate the intensity.
    ''' </summary>
    Sub SampleProbes()

    ''' <summary>The values measured for the point probes since the last call, per probe (an empty array for a line probe or when there is none).</summary>
    Function TakeProbeSamples() As Single()()

    ''' <summary>
    ''' The values along the line of every line probe in the current state (Nothing for a point probe): the field, the
    ''' intensity, or the intensity totals of the steps so far. On the GPU this waits for the card, so do not call it every step.
    ''' </summary>
    Function ReadProbeProfiles() As Single()()

End Interface

''' <summary>Makes the engine that a run asks for, with the CPU as the fall back.</summary>
Friend NotInheritable Class WaveEngineFactory

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Makes the GPU engine when the settings ask for it and that works, otherwise the CPU engine.
    ''' <paramref name="note"/> is empty, or tells why the GPU was not used (or that the first choice failed).
    ''' Object sources (the Sources tab) run on either engine, the same as the image sources.
    ''' </summary>
    Public Shared Function Create(scene As SceneData, settings As SimulationSettings, sourceAmplitude As Double,
                                  trackIntensity As Boolean, ByRef note As String,
                                  Optional objectSources As List(Of SourceElement) = Nothing) As IWaveEngine
        note = ""

        If settings.UseGpu Then
            Dim problems As String = ""
            Dim gpu As GpuWaveSimulator = Nothing
            Try
                gpu = GpuWaveSimulator.TryCreate(scene, settings.SourceFrequencyScale, sourceAmplitude, trackIntensity,
                                                 settings.Use4thOrderLaplacian, settings.TimeDecayFactor, settings.IntensityScale,
                                                 problems, objectSources)
            Catch ex As DllNotFoundException
                problems = "no OpenCL driver (OpenCL.dll) found; install the driver of the graphics card"
            Catch ex As BadImageFormatException
                problems = "the OpenCL driver does not match this 32/64-bit program"
            Catch ex As EntryPointNotFoundException
                problems = "the OpenCL driver is incomplete"
            Catch ex As Exception
                problems = ex.Message
            End Try

            If gpu IsNot Nothing Then
                If problems.Length > 0 Then note = "the first choice failed - " & problems
                Return gpu
            End If
            note = "GPU not used: " & problems
        End If

        Return New WaveSimulator(scene, settings.SourceFrequencyScale, sourceAmplitude, trackIntensity,
                                 settings.Use4thOrderLaplacian, settings.TimeDecayFactor, settings.IntensityScale, objectSources)
    End Function

End Class
