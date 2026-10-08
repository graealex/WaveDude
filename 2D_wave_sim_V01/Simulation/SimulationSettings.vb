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
Imports System.Globalization

''' <summary>
''' The simulation settings of a project (Configuration tab). They are stored in the section "simulation" of
''' configuration.json:
'''   steps, visualize_every            number of time steps, and a frame is shown or exported every N steps
'''   export_field, export_intensity    write a video of the field / of the intensity
'''   preview_field, preview_intensity  show the field / the intensity in the Preview tab while simulating
'''   field_color_scheme, intensity_color_scheme   names of the color schemes to use (empty: gray levels)
''' and a few values that have no control on the screen yet but can be edited in the file:
'''   field_brightness, intensity_brightness, field_black_level, intensity_black_level,
'''   source_frequency_scale, video_fps
'''   video_quality                     1 to 100, quality of the exported videos (higher = larger files)
'''   use_gpu                           simulate on the graphics card (OpenCL) instead of the processor
'''   wave_speed_c                      "Set c-value" (File menu); see the WaveSpeedC property below
'''   time_decay_factor                 "Intensity decay factor" (Configuration tab); see TimeDecayFactor below
'''   intensity_scale                   "Intensity scale" (Configuration tab); see IntensityScale below
''' The defaults are those of the Python example (main_curr.py).
''' </summary>
Public NotInheritable Class SimulationSettings

    Public Const SectionName As String = "simulation"

    ' DefaultSteps is only the fallback for an existing project saved before "steps" existed (FromConfig below) -
    ' Steps itself has no software-chosen default of its own any more, the same as TimeDecayFactor: a new project
    ' starts at whatever numSimulationSteps' own Designer value is (see StartNewProject's "New SimulationSettings()").
    Public Const DefaultSteps As Integer = 2500
    Public Const MaxSteps As Integer = 10000000
    Public Const MaxVisualizeEvery As Integer = 1000
    Public Const MaxVideoFps As Integer = 240
    Public Const DefaultVideoQuality As Integer = 70

    Public Property Steps As Integer
    Public Property VisualizeEvery As Integer = 2

    Public Property ExportField As Boolean = False
    Public Property ExportIntensity As Boolean = False
    Public Property PreviewField As Boolean = True
    Public Property PreviewIntensity As Boolean = True

    ''' <summary>The scheme names a new project starts with (FillSchemeCombo, Form1.vb) - selected only when a
    ''' scheme with that exact name (case-insensitive) and matching SchemeType exists; otherwise the combo just
    ''' shows nothing selected, same as for any other scheme name that is not found.</summary>
    Public Property FieldColorScheme As String = "Default_Field"
    Public Property IntensityColorScheme As String = "Default_intensity"

    ''' <summary>Brightness scale of the field view (render_field(7) in the Python example; 30.0 here is high
    ''' enough to see a single source in the simulation, which is what numFieldScaling's own Designer default
    ''' now also shows for a new source).</summary>
    Public Property FieldBrightness As Double = 30.0

    ''' <summary>Brightness scale of the intensity view (render_intensity(7) in the Python example; see FieldBrightness).</summary>
    Public Property IntensityBrightness As Double = 30.0

    Public Property FieldBlackLevel As Double = -0.05
    Public Property IntensityBlackLevel As Double = 0.0

    ''' <summary>Multiplies the frequency of the sources that come from the green channel of the scene image.</summary>
    Public Property SourceFrequencyScale As Double = 2.0

    Public Property VideoFps As Integer = 60

    ''' <summary>Quality of the exported videos, 1 (small files) to 100 (best picture, large files).</summary>
    Public Property VideoQuality As Integer = DefaultVideoQuality

    ''' <summary>Run the simulation on the graphics card. Falls back to the processor when that is not possible.</summary>
    Public Property UseGpu As Boolean = False

    ''' <summary>
    ''' Use a 4th order accurate discrete Laplacian (a 5-point "cross" in each of x and y, reaching 2 cells away)
    ''' instead of the standard 3x3 kernel, everywhere it is safe to (see WaveSimulator.UpdateField/
    ''' GpuWaveSimulator.KernelSource - the outermost ring of the domain still falls back to the 3x3 kernel, since
    ''' it does not have 2 cells of margin). This reduces numerical dispersion (short wavelengths, under-resolved by
    ''' the grid, are otherwise slowed down relative to long ones - see the "Set c-value" comment on WaveSpeedC,
    ''' above, whose default is itself a symptom of this) at the cost of roughly twice the memory reads per step.
    ''' On by default for a new project (this property's own default, used by StartNewProject's
    ''' "New SimulationSettings()"). An existing project saved before this property existed still loads as Off
    ''' (FromConfig's own separate, hardcoded fallback below), so it keeps its exact previous behavior instead of
    ''' silently picking up the new project default.
    ''' </summary>
    Public Property Use4thOrderLaplacian As Boolean = True

    ''' <summary>
    ''' The propagation speed of waves through the medium, in simulation pixels per step (lambda = c / frequency).
    ''' Set from the "Set c-value" entry on the File menu. This is only used to estimate and display a source's
    ''' wavelength (lblLambdaIs on the Sources tab) - it does not affect the simulation itself, which derives the
    ''' real, per-pixel wave speed from the scene's refractive index (the red channel of the scene image; see
    ''' SceneData.C2) instead, and can vary from pixel to pixel while this single value cannot.
    ''' A new project starts at 1.0, the solver's own default speed for a uniform medium (this property's own
    ''' default, used by StartNewProject's "New SimulationSettings()"). An existing project saved before this
    ''' field existed still loads at 0.563 instead (FromConfig's own separate, hardcoded fallback below) - that
    ''' value was found experimentally to be the correct one at around a source frequency of 100, and is kept only
    ''' so such a project's displayed wavelength estimate does not change out from under it. Like every other
    ''' property here, this belongs to one project's SimulationSettings and is saved/loaded with it (wave_speed_c,
    ''' below) - it is not a global/universal setting, so different projects can each keep their own value.
    ''' </summary>
    Public Property WaveSpeedC As Double = 1.0

    ''' <summary>
    ''' "Intensity decay factor" (Configuration tab, numTimeDecayFactor): the intensity shown/exported is not the
    ''' raw Sqr(field), it is an exponential running average of it (see WaveSimulator.UpdateField /
    ''' GpuWaveSimulator.KernelSource), because a source oscillates every step and the raw squared amplitude would
    ''' flicker at twice the source frequency instead of showing a steady intensity. The averaging needs a time
    ''' constant of at least a few periods of the slowest active source, or that flicker "prints through"; a fixed
    ''' time constant long enough for a low frequency source is unnecessarily long for a high frequency one and
    ''' shows as a visible trail behind anything that moves. Both engines now scale the time constant inversely
    ''' with the frequency of the slowest active source (image or object) instead of using one fixed value, and
    ''' this property is the number of steps that time constant works out to at a nominal medium source (green
    ''' level 100). Raising it lengthens the trail on every source (helps a low frequency source avoid
    ''' print-through); lowering it shortens the trail (helps a high frequency source look crisp) at the cost of
    ''' print-through if a low frequency source is also present in the same scene. This class does not itself pick
    ''' a number for a new project - that would just be a second, competing default fighting the one already set on
    ''' numTimeDecayFactor (Configuration tab) in the Designer - so StartNewProject (Form1.vb) instead reads the
    ''' control's own value right after creating a new SimulationSettings, and that is the only place a new
    ''' project's starting value is decided; changing it means changing numTimeDecayFactor's Value in the Designer.
    ''' </summary>
    Public Property TimeDecayFactor As Double

    ''' <summary>
    ''' A plain multiplier on the tracked intensity itself (not just how it is displayed): both engines apply it to
    ''' every intensity value they compute, so it changes the actual numbers in the preview, the exported videos,
    ''' the probe data and any exported files - unlike FieldBrightness/IntensityBrightness above, which only scale
    ''' how an image is drawn from the numbers, leaving the numbers themselves alone. It exists because, with the
    ''' filtering TimeDecayFactor controls (see its comment), the raw intensity ends up landing around 1e-5 for
    ''' typical scenes - an inconvenient scale for an axis or a data file - and the right multiplier to bring that
    ''' into a convenient range depends on the source amplitudes and pulse shapes actually used, which this class
    ''' has no way to predict. The default of 1.0 leaves the raw numbers exactly as computed; numIntensityScale
    ''' (Configuration tab) lets the value be tuned by eye once the actual scale in use is visible.
    ''' </summary>
    Public Property IntensityScale As Double = 1.0

    ''' <summary>A copy that a running simulation can keep while the user goes on editing.</summary>
    Public Function Clone() As SimulationSettings
        Return DirectCast(MemberwiseClone(), SimulationSettings)
    End Function

    ' ---------------------------------------------------------------- configuration file

    Public Shared Function FromConfig(config As ProjectConfig) As SimulationSettings
        Dim s As New SimulationSettings()
        If Not config.ContainsKey(SectionName) Then Return s

        Dim section As Dictionary(Of String, Object) = config.GetSection(SectionName)
        s.Steps = ReadInt(section, "steps", DefaultSteps, 1, MaxSteps)
        s.VisualizeEvery = ReadInt(section, "visualize_every", 2, 1, MaxVisualizeEvery)
        s.ExportField = ReadBool(section, "export_field", False)
        s.ExportIntensity = ReadBool(section, "export_intensity", False)
        s.PreviewField = ReadBool(section, "preview_field", True)
        s.PreviewIntensity = ReadBool(section, "preview_intensity", True)
        s.FieldColorScheme = ReadText(section, "field_color_scheme", "Default_Field")
        s.IntensityColorScheme = ReadText(section, "intensity_color_scheme", "Default_intensity")
        s.FieldBrightness = ReadDouble(section, "field_brightness", 30.0, 0.0001, 100000.0)
        s.IntensityBrightness = ReadDouble(section, "intensity_brightness", 30.0, 0.0001, 100000.0)
        s.FieldBlackLevel = ReadDouble(section, "field_black_level", -0.05, -10.0, 1.0)
        s.IntensityBlackLevel = ReadDouble(section, "intensity_black_level", 0.0, -10.0, 1.0)
        s.SourceFrequencyScale = ReadDouble(section, "source_frequency_scale", 2.0, 0.0, 1000.0)
        s.VideoFps = ReadInt(section, "video_fps", 60, 1, MaxVideoFps)
        s.VideoQuality = ReadInt(section, "video_quality", DefaultVideoQuality, 1, 100)
        s.UseGpu = ReadBool(section, "use_gpu", False)
        s.Use4thOrderLaplacian = ReadBool(section, "use_4th_order_laplacian", False)
        s.WaveSpeedC = ReadDouble(section, "wave_speed_c", 0.563, 0.000001, 1000000.0)
        s.TimeDecayFactor = ReadDouble(section, "time_decay_factor", 10.0, 1.0, 100.0)
        s.IntensityScale = ReadDouble(section, "intensity_scale", 1.0, 0.0000001, 1000000.0)
        Return s
    End Function

    Public Sub ToConfig(config As ProjectConfig)
        Dim section As Dictionary(Of String, Object) = config.GetSection(SectionName)
        section("steps") = Steps
        section("visualize_every") = VisualizeEvery
        section("export_field") = ExportField
        section("export_intensity") = ExportIntensity
        section("preview_field") = PreviewField
        section("preview_intensity") = PreviewIntensity
        section("field_color_scheme") = FieldColorScheme
        section("intensity_color_scheme") = IntensityColorScheme
        section("field_brightness") = FieldBrightness
        section("intensity_brightness") = IntensityBrightness
        section("field_black_level") = FieldBlackLevel
        section("intensity_black_level") = IntensityBlackLevel
        section("source_frequency_scale") = SourceFrequencyScale
        section("video_fps") = VideoFps
        section("video_quality") = VideoQuality
        section("use_gpu") = UseGpu
        section("use_4th_order_laplacian") = Use4thOrderLaplacian
        section("wave_speed_c") = WaveSpeedC
        section("time_decay_factor") = TimeDecayFactor
        section("intensity_scale") = IntensityScale
    End Sub

    ' ---------------------------------------------------------------- helpers

    Private Shared Function ReadText(section As Dictionary(Of String, Object), key As String, fallback As String) As String
        Dim v As Object = Nothing
        If section.TryGetValue(key, v) AndAlso v IsNot Nothing Then Return Convert.ToString(v, CultureInfo.InvariantCulture)
        Return fallback
    End Function

    Private Shared Function ReadBool(section As Dictionary(Of String, Object), key As String, fallback As Boolean) As Boolean
        Dim v As Object = Nothing
        If section.TryGetValue(key, v) AndAlso TypeOf v Is Boolean Then Return Convert.ToBoolean(v)
        Return fallback
    End Function

    Private Shared Function ReadInt(section As Dictionary(Of String, Object), key As String, fallback As Integer,
                                    minValue As Integer, maxValue As Integer) As Integer
        Dim v As Object = Nothing
        If section.TryGetValue(key, v) AndAlso v IsNot Nothing Then
            Try
                Dim n As Integer = Convert.ToInt32(v, CultureInfo.InvariantCulture)
                If n >= minValue AndAlso n <= maxValue Then Return n
            Catch ex As Exception When TypeOf ex Is FormatException OrElse TypeOf ex Is InvalidCastException OrElse TypeOf ex Is OverflowException
                ' use the fallback
            End Try
        End If
        Return fallback
    End Function

    Private Shared Function ReadDouble(section As Dictionary(Of String, Object), key As String, fallback As Double,
                                       minValue As Double, maxValue As Double) As Double
        Dim v As Object = Nothing
        If section.TryGetValue(key, v) AndAlso v IsNot Nothing Then
            Try
                Dim d As Double = Convert.ToDouble(v, CultureInfo.InvariantCulture)
                If d >= minValue AndAlso d <= maxValue Then Return d
            Catch ex As Exception When TypeOf ex Is FormatException OrElse TypeOf ex Is InvalidCastException OrElse TypeOf ex Is OverflowException
                ' use the fallback
            End Try
        End If
        Return fallback
    End Function

End Class
