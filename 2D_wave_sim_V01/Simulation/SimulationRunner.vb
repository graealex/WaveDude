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
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Threading

''' <summary>Everything a simulation run needs. Filled in by the form; the runner owns the job (and the image in it) afterwards.</summary>
Friend NotInheritable Class SimulationJob

    ''' <summary>The scene image (red = refractive index, green = sources, blue = damping). Nothing for an empty, user defined scene.</summary>
    Public Image As Bitmap

    ''' <summary>The canvas size of an empty scene (ignored when there is an image).</summary>
    Public Width As Integer
    Public Height As Integer

    ''' <summary>The background refractive index of an empty scene (ignored when there is an image).</summary>
    Public BackgroundRefractiveIndex As Double = 1.0

    Public UseSources As Boolean

    ''' <summary>Amplitude factor of the sources in the scene image.</summary>
    Public SourceAmplitude As Double = 1.0

    Public DampingEdge As Integer

    ''' <summary>A private copy of the simulation settings.</summary>
    Public Settings As SimulationSettings

    ''' <summary>Color tables (255 packed ARGB values), or Nothing for gray levels.</summary>
    Public FieldLut As Integer()
    Public IntensityLut As Integer()

    ''' <summary>Video files to write, normally .mp4; empty = no video of that kind. When H.264 is not available the extension becomes .avi.</summary>
    Public FieldVideoPath As String = ""
    Public IntensityVideoPath As String = ""

    ''' <summary>The probes to measure (copies), or Nothing for none.</summary>
    Public Probes As List(Of ProbeSettings)

    ''' <summary>The generated elements of the object sources to run (Sources tab), or Nothing/empty for none.</summary>
    Public ObjectSources As List(Of SourceElement)

    ''' <summary>The size (pixels) of the preview area; previews are made to fit it.</summary>
    Public PreviewWidth As Integer
    Public PreviewHeight As Integer

    ''' <summary>The Viewer tab's "glowing edges" overlay (Form1's _glowEdgesImage), exactly the size of the
    ''' simulation canvas - Nothing when there is none (no scene image), or when both gains below are 0 (the
    ''' overlay is switched off for both Field and Intensity). Frozen at the start of the run, the same as
    ''' Settings - the runner owns and disposes it. See FrameRenderer.AddOverlay.</summary>
    Public GlowOverlay As Bitmap

    ''' <summary>How strongly to blend GlowOverlay into the Field/Intensity video, frozen at the start of the run
    ''' from Form1's numBrightnessFieldOverlay/numBrightnessIntensityOverlay and chkDiplayOverlayInField/
    ''' chkDiplayOverlayInIntensity (0 when unchecked - see Form1.FieldOverlayGain/IntensityOverlayGain). Only
    ''' affects the exported videos - the live preview keeps blending its own copy of the overlay in on the UI
    ''' thread (Form1.ShowPreview/ComposeGlowOverlay), which can track the checkbox/brightness box live; a
    ''' running export cannot, so it uses whatever these were at the moment Start Simulation was pressed.</summary>
    Public FieldOverlayGain As Double
    Public IntensityOverlayGain As Double

End Class

Friend Enum SimulationStatus
    Completed
    Cancelled
    Failed
End Enum

Friend NotInheritable Class SimulationResult
    Public Status As SimulationStatus
    Public Message As String = ""
    Public StepsDone As Integer
    Public Elapsed As TimeSpan
    Public Files As New List(Of String)()

    ''' <summary>The video files that were written (empty when there is none of that kind).</summary>
    Public FieldFile As String = ""
    Public IntensityFile As String = ""

    ''' <summary>Where the simulation ran ("CPU (8 threads)", "GPU: NVIDIA GeForce MX130 (OpenCL)"), and why the GPU was not used when it was asked for.</summary>
    Public Engine As String = ""
    Public EngineNote As String = ""

    ''' <summary>A full simulation-resolution snapshot of the field/intensity, rendered right before the engine
    ''' was closed down - so a still export (the Preview tab's "Export current Field/Intensity frame" buttons) has
    ''' something to save even once the run has fully stopped and the live engine no longer exists. Nothing when
    ''' the run failed before an engine was created, or when intensity was not tracked for this run (LastIntensityFrame
    ''' only). The receiver owns the bitmap(s).</summary>
    Public LastFieldFrame As Bitmap
    Public LastIntensityFrame As Bitmap
End Class

''' <summary>
''' Runs a simulation on a background thread, as the loop in main_curr.py: for every step update the scene
''' (sources), update the field and the intensity; every N steps render a frame for the previews and the videos.
''' The events are raised on the background thread; the form must pass them on to the user interface thread.
''' </summary>
Friend NotInheritable Class SimulationRunner

    ''' <summary>Steps done so far and the total, raised about ten times a second.</summary>
    Public Event ProgressChanged(stepsDone As Integer, totalSteps As Integer)

    ''' <summary>The engine that runs the simulation (CPU or GPU) was chosen. The note is empty, or tells why the GPU is not used.</summary>
    Public Event EngineReady(description As String, note As String)

    ''' <summary>New probe measurements: the new values of the point probes and the last profile of the line probes. The receiver owns the arrays.</summary>
    Public Event ProbesUpdated(update As ProbeUpdate)

    ''' <summary>A preview picture is ready. The receiver owns the bitmap and must call PreviewShown when it is done with it.</summary>
    Public Event PreviewReady(isField As Boolean, frame As Bitmap)

    ''' <summary>The run is over (completed, stopped or failed). This is the last event.</summary>
    Public Event Finished(result As SimulationResult)

    Private Const JpegQuality As Long = 90L

    ''' <summary>Minimum time (ms) between previews - see its use in Run() for why this exists: it keeps a GPU
    ''' engine's readback rate steady and independent of how fast the UI thread happens to be at displaying the
    ''' previous one (e.g. with/without the "display overlay" checkboxes), rather than implicitly riding on
    ''' _previewPending alone. ~30 fps is already faster than a picture box is usefully watchable at.</summary>
    Private Const PreviewIntervalMs As Long = 33L

    Private ReadOnly _job As SimulationJob
    Private _thread As Thread
    Private _cancel As Integer
    Private _previewPending As Integer

    ' open = running, closed = paused
    Private ReadOnly _gate As New ManualResetEventSlim(True)

    ' the live engine, set once at the start of Run() and cleared just before it is disposed at the end; read from
    ' the UI thread (RenderCurrentFrame) only while paused, when the background thread is parked at _gate.Wait()
    ' and is not touching it
    Private _sim As IWaveEngine

    ' the probe collector for this run (Nothing when there are no probes), set alongside _sim so RenderCurrentFrame
    ' can also burn the probe markers/graphs onto a still export - the video export loop in Run() already does
    ' this (ProbeOverlay.Burn) but RenderCurrentFrame did not, which left probe data out of exported still frames
    Private _collector As ProbeCollector

    Public Sub New(job As SimulationJob)
        _job = job
    End Sub

    Public Sub Start()
        _thread = New Thread(AddressOf Run)
        _thread.IsBackground = True
        _thread.Name = "Wave simulation"
        _thread.Start()
    End Sub

    ''' <summary>Asks the run to stop after the current step. The videos are closed properly.</summary>
    Public Sub Cancel()
        Interlocked.Exchange(_cancel, 1)
        _gate.Set()                 ' a paused run must wake up to stop
    End Sub

    ''' <summary>Holds the run before the next step. The videos stay open.</summary>
    Public Sub PauseRun()
        _gate.Reset()
    End Sub

    ''' <summary>Continues a paused run.</summary>
    Public Sub ResumeRun()
        _gate.Set()
    End Sub

    ''' <summary>Tells the runner that a preview picture was shown (or thrown away). When all pictures that were
    ''' announced are shown, the next preview is made.</summary>
    Public Sub PreviewShown()
        Interlocked.Decrement(_previewPending)
    End Sub

    ''' <summary>Waits until the thread has ended. Returns False when the time ran out.</summary>
    Public Function WaitForExit(milliseconds As Integer) As Boolean
        Dim t As Thread = _thread
        If t Is Nothing Then Return True
        Return t.Join(milliseconds)
    End Function

    ''' <summary>
    ''' Renders the field or the intensity at full simulation resolution, on demand (for a still-image export) -
    ''' only while the run is paused, since only then is the background thread guaranteed not to be touching the
    ''' engine's arrays. Returns Nothing when the run is not paused, when the engine is gone (not yet created, or
    ''' the run has finished), or when the intensity is asked for but was not tracked for this run.
    ''' </summary>
    Public Function RenderCurrentFrame(isField As Boolean, lut As Integer(), brightness As Single) As Bitmap
        If _gate.IsSet Then Return Nothing   ' running (not paused) - unsafe to read
        Dim sim As IWaveEngine = _sim
        If sim Is Nothing Then Return Nothing
        If Not isField AndAlso sim.IntensityData Is Nothing Then Return Nothing

        Dim w As Integer = sim.Width
        Dim h As Integer = sim.Height
        sim.PrepareFrame(isField, Not isField)
        Dim pixels As Integer() = New Integer(w * h - 1) {}
        If isField Then
            FrameRenderer.RenderField(sim, lut, brightness, pixels, w, h)
            If _collector IsNot Nothing Then ProbeOverlay.Burn(pixels, w, h, _job.Probes, _collector.Traces, ProbePlaceOn.Field, _job.Settings.Steps)
        Else
            FrameRenderer.RenderIntensity(sim, lut, brightness, pixels, w, h)
            If _collector IsNot Nothing Then ProbeOverlay.Burn(pixels, w, h, _job.Probes, _collector.Traces, ProbePlaceOn.Intensity, _job.Settings.Steps)
        End If
        Return FrameRenderer.ToBitmap(pixels, w, h)
    End Function

    Private Sub Run()
        Dim result As New SimulationResult()
        Dim clock As Stopwatch = Stopwatch.StartNew()
        Dim fieldWriter As IFrameSink = Nothing
        Dim intensityWriter As IFrameSink = Nothing
        Dim sim As IWaveEngine = Nothing
        ' declared here (not inside Try, unlike most locals below) because Finally also needs them, for the
        ' last-frame probe overlay - a Dim inside Try is a compile error in Finally (BC30451): unlike most VB
        ' locals, Try/Catch/Finally each have their own block scope
        Dim collector As ProbeCollector = Nothing
        Dim total As Integer = 0

        Try
            Dim settings As SimulationSettings = _job.Settings
            Dim exportField As Boolean = _job.FieldVideoPath.Length > 0
            Dim exportIntensity As Boolean = _job.IntensityVideoPath.Length > 0
            Dim needIntensity As Boolean = exportIntensity OrElse settings.PreviewIntensity

            ' the scene and the simulator
            Dim scene As SceneData
            If _job.Image IsNot Nothing Then
                scene = SceneData.FromImage(_job.Image, _job.UseSources, _job.DampingEdge)
                _job.Image.Dispose()
                _job.Image = Nothing
            Else
                scene = SceneData.Empty(_job.Width, _job.Height, _job.DampingEdge, _job.BackgroundRefractiveIndex)
            End If
            Dim w As Integer = scene.Width
            Dim h As Integer = scene.Height

            ' the probes
            Dim sampler As ProbeSampler = Nothing
            If _job.Probes IsNot Nothing AndAlso _job.Probes.Count > 0 Then
                sampler = ProbeSampler.Build(_job.Probes, w, h)
                collector = New ProbeCollector(sampler)
                If sampler.NeedsIntensity Then needIntensity = True
            End If
            Dim engineNote As String = ""
            sim = WaveEngineFactory.Create(scene, settings, _job.SourceAmplitude, needIntensity, engineNote, _job.ObjectSources)
            _sim = sim
            _collector = collector
            scene = Nothing
            result.Engine = sim.Description
            result.EngineNote = engineNote
            RaiseEvent EngineReady(sim.Description, engineNote)
            If sampler IsNot Nothing Then sim.SetProbes(sampler)

            ' videos
            If exportField Then
                fieldWriter = OpenSink(_job.FieldVideoPath, w, h, settings.VideoFps, settings.VideoQuality, result)
                result.Files.Add(fieldWriter.FilePath)
                result.FieldFile = fieldWriter.FilePath
            End If
            If exportIntensity Then
                intensityWriter = OpenSink(_job.IntensityVideoPath, w, h, settings.VideoFps, settings.VideoQuality, result)
                result.Files.Add(intensityWriter.FilePath)
                result.IntensityFile = intensityWriter.FilePath
            End If

            Dim fullPixels As Integer() = Nothing
            If exportField OrElse exportIntensity Then fullPixels = New Integer(w * h - 1) {}

            ' the "glowing edges" overlay for the videos (FrameRenderer.AddOverlay) - converted to a plain RGB
            ' byte array and gain-prescaled (FrameRenderer.ScaleOverlayRgb) once, up front, rather than redoing
            ' either on every single exported frame; this was the actual cost of the overlay on a video export
            Dim fieldOverlayScaled As Byte() = Nothing
            Dim intensityOverlayScaled As Byte() = Nothing
            If _job.GlowOverlay IsNot Nothing AndAlso (exportField OrElse exportIntensity) Then
                Dim overlayRgb As Byte() = FrameRenderer.ToRgbBytes(_job.GlowOverlay, w, h)
                If exportField Then fieldOverlayScaled = FrameRenderer.ScaleOverlayRgb(overlayRgb, _job.FieldOverlayGain)
                If exportIntensity Then intensityOverlayScaled = FrameRenderer.ScaleOverlayRgb(overlayRgb, _job.IntensityOverlayGain)
            End If
            If _job.GlowOverlay IsNot Nothing Then
                _job.GlowOverlay.Dispose()
                _job.GlowOverlay = Nothing
            End If

            ' previews are made small enough to fit the preview area
            Dim previewW As Integer = w
            Dim previewH As Integer = h
            Dim scale As Double = Math.Min(_job.PreviewWidth / CDbl(w), _job.PreviewHeight / CDbl(h))
            If scale < 1.0 Then
                previewW = Math.Max(1, CInt(Math.Floor(w * scale)))
                previewH = Math.Max(1, CInt(Math.Floor(h * scale)))
            End If
            Dim previewPixels As Integer() = Nothing
            If settings.PreviewField OrElse settings.PreviewIntensity Then previewPixels = New Integer(previewW * previewH - 1) {}

            Dim fieldBrightness As Single = CSng(settings.FieldBrightness)
            Dim intensityBrightness As Single = CSng(settings.IntensityBrightness)
            Dim every As Integer = Math.Max(1, settings.VisualizeEvery)
            total = settings.Steps

            Dim lastProgress As Long = 0
            Dim lastProbePost As Long = 0
            Dim lastPreview As Long = 0

            result.Status = SimulationStatus.Completed
            For i As Integer = 0 To total - 1
                If Not _gate.IsSet Then
                    ' paused: the time of the pause does not count
                    clock.Stop()
                    RaiseEvent ProgressChanged(result.StepsDone, total)
                    _gate.Wait()
                    clock.Start()
                End If

                If Thread.VolatileRead(_cancel) <> 0 Then
                    result.Status = SimulationStatus.Cancelled
                    Exit For
                End If

                sim.AdvanceOneStep()
                If sampler IsNot Nothing Then sim.SampleProbes()
                result.StepsDone = i + 1

                If (i Mod every) = 0 Then
                    ' the probe values up to this step (the graphs in the videos need them)
                    If collector IsNot Nothing Then
                        collector.Collect(sim, result.StepsDone)
                        If exportField OrElse exportIntensity Then collector.CollectProfiles(sim, result.StepsDone)
                    End If

                    ' previews are only made when the pictures of the last preview have all been shown, and not
                    ' more often than roughly PreviewIntervalMs. The second condition is the fix for a real bug:
                    ' without it, this was gated purely by _previewPending (whether the UI thread has finished
                    ' showing the last one, via ShowPreview/PreviewShown) - and on a GPU engine, PrepareFrame
                    ' below is where the actual cost is (a full-resolution GPU->host readback, not just the small
                    ' preview picture), so the GPU run's speed ended up riding on how fast the UI happened to be.
                    ' With the "display overlay" checkboxes off, SetPreviewFrame is cheap and _previewPending
                    ' clears almost immediately, so a readback fired on nearly every eligible step; with them on,
                    ' ComposeGlowOverlay's per-pixel blend made the UI side slow enough to skip most of those
                    ' readbacks "by accident", which is why turning the overlay on made the run go faster rather
                    ' than slower. Pacing previews by elapsed time keeps the readback rate steady and no longer
                    ' dependent on a checkbox that was never meant to affect simulation throughput. Exporting a
                    ' video already forces a readback every eligible step regardless (see exportField/
                    ' exportIntensity below), which is why the slowdown was never visible with video export on.
                    Dim previewDue As Boolean = clock.ElapsedMilliseconds - lastPreview >= PreviewIntervalMs
                    Dim doPreview As Boolean = previewPixels IsNot Nothing AndAlso previewDue AndAlso Thread.VolatileRead(_previewPending) <= 0
                    If doPreview Then lastPreview = clock.ElapsedMilliseconds

                    ' a GPU engine copies only the data that is going to be drawn
                    sim.PrepareFrame(exportField OrElse (doPreview AndAlso settings.PreviewField),
                                     exportIntensity OrElse (doPreview AndAlso settings.PreviewIntensity))

                    If exportField Then
                        FrameRenderer.RenderField(sim, _job.FieldLut, fieldBrightness, fullPixels, w, h)
                        FrameRenderer.AddOverlay(fullPixels, fieldOverlayScaled, w, h)
                        If collector IsNot Nothing Then ProbeOverlay.Burn(fullPixels, w, h, _job.Probes, collector.Traces, ProbePlaceOn.Field, total)
                        fieldWriter.AddPixels(fullPixels)
                    End If
                    If exportIntensity Then
                        FrameRenderer.RenderIntensity(sim, _job.IntensityLut, intensityBrightness, fullPixels, w, h)
                        FrameRenderer.AddOverlay(fullPixels, intensityOverlayScaled, w, h)
                        If collector IsNot Nothing Then ProbeOverlay.Burn(fullPixels, w, h, _job.Probes, collector.Traces, ProbePlaceOn.Intensity, total)
                        intensityWriter.AddPixels(fullPixels)
                    End If

                    If doPreview Then
                        Dim announced As Integer = If(settings.PreviewField, 1, 0) + If(settings.PreviewIntensity, 1, 0)
                        Interlocked.Exchange(_previewPending, announced)
                        If settings.PreviewField Then
                            FrameRenderer.RenderField(sim, _job.FieldLut, fieldBrightness, previewPixels, previewW, previewH)
                            RaiseEvent PreviewReady(True, FrameRenderer.ToBitmap(previewPixels, previewW, previewH))
                        End If
                        If settings.PreviewIntensity Then
                            FrameRenderer.RenderIntensity(sim, _job.IntensityLut, intensityBrightness, previewPixels, previewW, previewH)
                            RaiseEvent PreviewReady(False, FrameRenderer.ToBitmap(previewPixels, previewW, previewH))
                        End If
                    End If
                End If

                ' the probe values go to the user interface about 20 times a second
                If collector IsNot Nothing AndAlso clock.ElapsedMilliseconds - lastProbePost >= 50 Then
                    lastProbePost = clock.ElapsedMilliseconds
                    collector.Collect(sim, result.StepsDone)
                    collector.CollectProfiles(sim, result.StepsDone)
                    RaiseEvent ProbesUpdated(collector.TakeForUi())
                End If

                If clock.ElapsedMilliseconds - lastProgress >= 100 Then
                    lastProgress = clock.ElapsedMilliseconds
                    RaiseEvent ProgressChanged(i + 1, total)
                End If
            Next

            ' the last probe values
            If collector IsNot Nothing Then
                collector.Collect(sim, result.StepsDone)
                collector.CollectProfiles(sim, result.StepsDone)
                RaiseEvent ProbesUpdated(collector.TakeForUi())
            End If

        Catch ex As OutOfMemoryException
            result.Status = SimulationStatus.Failed
            result.Message = "There is not enough memory for a simulation of this size. Use a smaller canvas."
        Catch ex As Exception
            result.Status = SimulationStatus.Failed
            result.Message = ex.Message
        Finally
            ' close the videos, also after a stop or an error, so that the files can be played
            Try
                If fieldWriter IsNot Nothing Then fieldWriter.Close()
            Catch ex As Exception
                ' nothing more can be done
            End Try
            Try
                If intensityWriter IsNot Nothing Then intensityWriter.Close()
            Catch ex As Exception
                ' nothing more can be done
            End Try
            If sim IsNot Nothing Then
                ' one last full-resolution snapshot, so a still export still has something to save once the run
                ' has fully stopped and the engine itself is gone (see SimulationResult.LastFieldFrame/LastIntensityFrame)
                Try
                    Dim lastPixels As Integer() = New Integer(sim.Width * sim.Height - 1) {}
                    Dim needInten As Boolean = sim.IntensityData IsNot Nothing
                    sim.PrepareFrame(True, needInten)
                    FrameRenderer.RenderField(sim, _job.FieldLut, CSng(_job.Settings.FieldBrightness), lastPixels, sim.Width, sim.Height)
                    If collector IsNot Nothing Then ProbeOverlay.Burn(lastPixels, sim.Width, sim.Height, _job.Probes, collector.Traces, ProbePlaceOn.Field, total)
                    result.LastFieldFrame = FrameRenderer.ToBitmap(lastPixels, sim.Width, sim.Height)
                    If needInten Then
                        FrameRenderer.RenderIntensity(sim, _job.IntensityLut, CSng(_job.Settings.IntensityBrightness), lastPixels, sim.Width, sim.Height)
                        If collector IsNot Nothing Then ProbeOverlay.Burn(lastPixels, sim.Width, sim.Height, _job.Probes, collector.Traces, ProbePlaceOn.Intensity, total)
                        result.LastIntensityFrame = FrameRenderer.ToBitmap(lastPixels, sim.Width, sim.Height)
                    End If
                Catch ex As Exception
                    ' no still frame this time - not fatal, the export buttons just won't have anything to save
                End Try
                Try
                    sim.Dispose()
                Catch ex As Exception
                    ' nothing more can be done
                End Try
                _sim = Nothing
                _collector = Nothing
            End If
            If _job.Image IsNot Nothing Then _job.Image.Dispose()
        End Try

        clock.Stop()
        result.Elapsed = clock.Elapsed
        RaiseEvent ProgressChanged(result.StepsDone, _job.Settings.Steps)
        RaiseEvent Finished(result)
    End Sub

    ''' <summary>
    ''' Opens a video file. An .mp4 file is written as H.264 with the encoder of Windows; when that is not possible
    ''' (no Media Foundation, size not supported) an .avi file with Motion JPEG frames is written instead.
    ''' </summary>
    Private Shared Function OpenSink(filePath As String, w As Integer, h As Integer, fps As Integer, quality As Integer, result As SimulationResult) As IFrameSink
        If String.Equals(Path.GetExtension(filePath), ".mp4", StringComparison.OrdinalIgnoreCase) Then
            Try
                Return New MfVideoWriter(filePath, w, h, fps, quality)
            Catch ex As Exception
                If File.Exists(filePath) Then
                    Try
                        File.Delete(filePath)
                    Catch ex2 As Exception
                        ' an empty leftover file does no harm
                    End Try
                End If
                result.Message = "MP4 (H.264) video could not be made on this computer (" & ex.Message.Trim() &
                                 "). The videos were written as Motion JPEG .avi files instead; play them with VLC."
                filePath = Path.ChangeExtension(filePath, ".avi")
            End Try
        End If
        Return New AviFrameSink(filePath, w, h, fps, quality)
    End Function

End Class
