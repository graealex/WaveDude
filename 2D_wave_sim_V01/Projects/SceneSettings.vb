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
Imports System.IO

''' <summary>
''' The scene properties of a project (Configuration tab). They are stored in the section "scene" of
''' configuration.json:
'''   mode                  "image" (canvas size = size of the scene image) or "user_defined"
'''   image_file            the scene image, relative to the project folder when it lies inside it (forward slashes)
'''   user_width, user_height   canvas size in pixels for the user defined scene
'''   damping_edge_enabled, damping_edge_size_px
'''   use_sources (the sources in the scene image), image_source_scale (their amplitude factor),
'''   use_object_sources
''' The size of a scene image is not stored: it is read from the image itself. There is no scene-level "use probes"
''' switch: each probe has its own "On" checkbox (ProbeSettings.Active) instead.
''' </summary>
Public NotInheritable Class SceneSettings

    Public Const SectionName As String = "scene"

    Public Const ModeImage As String = "image"
    Public Const ModeUserDefined As String = "user_defined"

    ' the defaults are those of the Python example (1920 x 1080 canvas, 64 px damping edge)
    Public Const DefaultWidth As Integer = 1920
    Public Const DefaultHeight As Integer = 1080
    Public Const DefaultDampingEdgeSize As Integer = 64

    Public Const MinCanvasSize As Integer = 8
    Public Const MaxCanvasSize As Integer = 16384
    Public Const MaxDampingEdgeSize As Integer = 8192

    Public Const DefaultImageSourceScale As Double = 1.0
    Public Const MaxImageSourceScale As Double = 1000.0

    ''' <summary>The refractive index of a user defined (image-less) scene's background, 1.0 to 2.55 (a red channel value of 100 to 255, as in a scene image).</summary>
    Public Const DefaultBackgroundRefractiveIndex As Double = 1.0
    Public Const MinBackgroundRefractiveIndex As Double = 1.0
    Public Const MaxBackgroundRefractiveIndex As Double = 2.55

    ''' <summary>True: the canvas is the scene image. False: the canvas has the user defined size.</summary>
    Public Property UseImage As Boolean = False

    ''' <summary>The scene image as stored: relative to the project folder when inside it, otherwise a full path. Empty when none.</summary>
    Public Property ImageFile As String = ""

    Public Property UserWidth As Integer = DefaultWidth
    Public Property UserHeight As Integer = DefaultHeight

    ''' <summary>No software-chosen default of its own, the same as SimulationSettings.Steps/TimeDecayFactor - a
    ''' new project starts at whatever chkDampingEdge's own Designer Checked value is (see StartNewProject,
    ''' Form1.vb). The False below (used by FromConfig) is only the fallback for an existing project saved before
    ''' this setting existed.</summary>
    Public Property DampingEdgeEnabled As Boolean
    Public Property DampingEdgeSize As Integer = DefaultDampingEdgeSize

    ''' <summary>Use the sources in the green channel of the scene image.</summary>
    Public Property UseSources As Boolean = False

    ''' <summary>Amplitude factor of the image sources (source_amplitude of StaticImageScene in the Python code).</summary>
    Public Property ImageSourceScale As Double = DefaultImageSourceScale

    ''' <summary>Use the source objects of the source list (not implemented yet; only the setting is kept). No
    ''' software-chosen default of its own, the same as DampingEdgeEnabled above - a new project starts at
    ''' whatever chkUseObjectSources' own Designer Checked value is (see StartNewProject, Form1.vb).</summary>
    Public Property UseObjectSources As Boolean

    ''' <summary>The background refractive index of a user defined scene (a scene image has its own value per pixel, from its red channel).</summary>
    Public Property BackgroundRefractiveIndex As Double = DefaultBackgroundRefractiveIndex

    ' ---------------------------------------------------------------- configuration file

    ''' <summary>Reads the scene settings from a project configuration; missing or invalid values get their defaults.</summary>
    Public Shared Function FromConfig(config As ProjectConfig) As SceneSettings
        Dim scene As New SceneSettings()
        If Not config.ContainsKey(SectionName) Then Return scene

        Dim section As Dictionary(Of String, Object) = config.GetSection(SectionName)
        scene.UseImage = (ReadText(section, "mode", ModeUserDefined) = ModeImage)
        scene.ImageFile = ReadText(section, "image_file", "")
        scene.UserWidth = ReadInt(section, "user_width", DefaultWidth, MinCanvasSize, MaxCanvasSize)
        scene.UserHeight = ReadInt(section, "user_height", DefaultHeight, MinCanvasSize, MaxCanvasSize)
        scene.DampingEdgeEnabled = ReadBool(section, "damping_edge_enabled", False)
        scene.DampingEdgeSize = ReadInt(section, "damping_edge_size_px", DefaultDampingEdgeSize, 1, MaxDampingEdgeSize)
        scene.UseSources = ReadBool(section, "use_sources", False)
        scene.ImageSourceScale = ReadDouble(section, "image_source_scale", DefaultImageSourceScale, 0.0, MaxImageSourceScale)
        scene.UseObjectSources = ReadBool(section, "use_object_sources", False)
        scene.BackgroundRefractiveIndex = ReadDouble(section, "background_refractive_index", DefaultBackgroundRefractiveIndex,
                                                      MinBackgroundRefractiveIndex, MaxBackgroundRefractiveIndex)
        Return scene
    End Function

    ''' <summary>Puts the scene settings into a project configuration (call Project.Save to write the file).</summary>
    Public Sub ToConfig(config As ProjectConfig)
        Dim section As Dictionary(Of String, Object) = config.GetSection(SectionName)
        section("mode") = If(UseImage, ModeImage, ModeUserDefined)
        section("image_file") = ImageFile
        section("user_width") = UserWidth
        section("user_height") = UserHeight
        section("damping_edge_enabled") = DampingEdgeEnabled
        section("damping_edge_size_px") = DampingEdgeSize
        section("use_sources") = UseSources
        section("image_source_scale") = ImageSourceScale
        section("use_object_sources") = UseObjectSources
        section("background_refractive_index") = BackgroundRefractiveIndex
    End Sub

    ' ---------------------------------------------------------------- image paths

    ''' <summary>The path to store for an image: relative to the project folder when the image is inside it.</summary>
    Public Shared Function ToStoredPath(projectFolder As String, fullPath As String) As String
        If String.IsNullOrEmpty(projectFolder) Then Return fullPath
        Dim prefix As String = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        If fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            Return fullPath.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, "/"c)
        End If
        Return fullPath
    End Function

    ''' <summary>The full path of a stored image path.</summary>
    Public Shared Function ToFullPath(projectFolder As String, storedPath As String) As String
        If String.IsNullOrEmpty(storedPath) Then Return ""
        If Path.IsPathRooted(storedPath) Then Return storedPath
        Return Path.Combine(projectFolder, storedPath.Replace("/"c, Path.DirectorySeparatorChar))
    End Function

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

    Private Shared Function ReadDouble(section As Dictionary(Of String, Object), key As String, fallback As Double,
                                       minValue As Double, maxValue As Double) As Double
        Dim v As Object = Nothing
        If section.TryGetValue(key, v) AndAlso v IsNot Nothing Then
            Try
                Dim x As Double = Convert.ToDouble(v, CultureInfo.InvariantCulture)
                If x >= minValue AndAlso x <= maxValue Then Return x
            Catch ex As Exception When TypeOf ex Is FormatException OrElse TypeOf ex Is InvalidCastException OrElse TypeOf ex Is OverflowException
                ' use the fallback
            End Try
        End If
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

End Class
