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
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Web.Script.Serialization

''' <summary>
''' Reads and writes color schemes as JSON files (one file per scheme, e.g. in the color_schemes folder).
'''
''' File format (the same as the files exported from Python):
'''   name, origin, category (optional), scheme_type (optional: "field" or "intensity"),
'''   bits_per_channel, entry_count, colors = 255 x [r,g,b]
''' and, only for schemes made in the stop editor:
'''   stops = 9 x [r,g,b] (ColorScheme.StopCount)
''' </summary>
Public NotInheritable Class ColorSchemeStore

    ''' <summary>Name of the folder with the color schemes that belongs to the program.</summary>
    Public Const SchemeFolderName As String = "ColorSchemeList"

    Private Sub New()
    End Sub

    ''' <summary>
    ''' The program's own color scheme folder. While developing (the exe runs from bin\Debug inside the
    ''' Visual Studio project) this is the ColorSchemeList folder of the project itself, so edits end up
    ''' in the project. In a deployed program it is the ColorSchemeList folder next to the exe.
    ''' The folder is created when it does not exist.
    ''' </summary>
    Public Shared Function FindDefaultFolder() As String
        Dim exeFolder As String = AppDomain.CurrentDomain.BaseDirectory
        Dim result As String = Path.Combine(exeFolder, SchemeFolderName)

        Try
            Dim probe As DirectoryInfo = New DirectoryInfo(exeFolder)
            For level As Integer = 0 To 4
                If probe Is Nothing Then Exit For
                If probe.GetFiles("*.vbproj").Length > 0 Then
                    Dim candidate As String = Path.Combine(probe.FullName, SchemeFolderName)
                    If Directory.Exists(candidate) Then result = candidate
                    Exit For
                End If
                probe = probe.Parent
            Next

            If Not Directory.Exists(result) Then Directory.CreateDirectory(result)
        Catch
            ' the caller checks whether the folder exists
        End Try
        Return result
    End Function

    ''' <summary>Loads one scheme file. Throws when the file cannot be read or is not a color scheme.</summary>
    Public Shared Function Load(filePath As String) As ColorScheme
        Dim text As String = File.ReadAllText(filePath, Encoding.UTF8)

        Dim serializer As New JavaScriptSerializer()
        serializer.MaxJsonLength = Integer.MaxValue
        Dim root As Dictionary(Of String, Object) = TryCast(serializer.DeserializeObject(text), Dictionary(Of String, Object))
        If root Is Nothing Then
            Throw New InvalidDataException("Not a color scheme file (a JSON object was expected).")
        End If

        Dim schemeName As String = GetString(root, "name", Path.GetFileNameWithoutExtension(filePath))

        Dim colors As List(Of Color) = Nothing
        If root.ContainsKey("colors") Then colors = ParseColorList(root("colors"))

        Dim stops As List(Of Color) = Nothing
        If root.ContainsKey("stops") Then stops = ParseColorList(root("stops"))
        If stops IsNot Nothing AndAlso stops.Count <> ColorScheme.StopCount Then stops = Nothing

        Dim scheme As ColorScheme
        If colors IsNot Nothing AndAlso colors.Count >= 2 Then
            scheme = New ColorScheme(schemeName, colors)
        ElseIf stops IsNot Nothing Then
            scheme = ColorScheme.FromStops(schemeName, stops)
        Else
            Throw New InvalidDataException("The file contains no usable 'colors' list.")
        End If

        If stops IsNot Nothing Then scheme.AttachStops(stops)
        scheme.Origin = GetString(root, "origin", scheme.Origin)
        scheme.Category = GetString(root, "category", scheme.Category)
        scheme.SchemeType = NormalizeSchemeType(GetString(root, "scheme_type", scheme.SchemeType))
        scheme.FilePath = filePath
        Return scheme
    End Function

    ''' <summary>
    ''' Loads every *.json file in a folder. Files that cannot be read are skipped and described in errors
    ''' (errors may be Nothing).
    ''' </summary>
    Public Shared Function LoadFolder(folder As String, errors As List(Of String)) As List(Of ColorScheme)
        Dim result As New List(Of ColorScheme)()
        If String.IsNullOrEmpty(folder) OrElse Not Directory.Exists(folder) Then Return result

        Dim files As New List(Of String)(Directory.GetFiles(folder, "*.json"))
        files.Sort(StringComparer.OrdinalIgnoreCase)

        For Each f As String In files
            Try
                result.Add(Load(f))
            Catch ex As Exception
                If errors IsNot Nothing Then errors.Add(Path.GetFileName(f) & ": " & ex.Message)
            End Try
        Next
        Return result
    End Function

    ''' <summary>Writes a scheme to a JSON file (overwriting an existing file) and sets scheme.FilePath.</summary>
    Public Shared Sub Save(scheme As ColorScheme, filePath As String)
        If scheme Is Nothing Then Throw New ArgumentNullException("scheme")

        Dim sb As New StringBuilder()
        sb.AppendLine("{")
        sb.AppendLine("  ""name"": " & Quote(scheme.Name) & ",")
        sb.AppendLine("  ""origin"": " & Quote(scheme.Origin) & ",")
        If Not String.IsNullOrEmpty(scheme.Category) Then
            sb.AppendLine("  ""category"": " & Quote(scheme.Category) & ",")
        End If
        If Not String.IsNullOrEmpty(scheme.SchemeType) Then
            sb.AppendLine("  ""scheme_type"": " & Quote(scheme.SchemeType) & ",")
        End If
        sb.AppendLine("  ""bits_per_channel"": 8,")
        sb.AppendLine("  ""entry_count"": " & ColorScheme.EntryCount.ToString(CultureInfo.InvariantCulture) & ",")

        If scheme.HasStops Then
            Dim stopParts As New List(Of String)()
            For Each c As Color In scheme.Stops
                stopParts.Add(FormatRgb(c))
            Next
            sb.AppendLine("  ""stops"": [" & String.Join(", ", stopParts) & "],")
        End If

        Dim entries As Color() = scheme.Entries
        sb.AppendLine("  ""colors"": [")
        For i As Integer = 0 To entries.Length - 1 Step 8
            Dim last As Integer = Math.Min(i + 7, entries.Length - 1)
            Dim parts As New List(Of String)()
            For j As Integer = i To last
                parts.Add(FormatRgb(entries(j)))
            Next
            sb.Append("    ")
            sb.Append(String.Join(", ", parts))
            If last < entries.Length - 1 Then sb.Append(",")
            sb.AppendLine()
        Next
        sb.AppendLine("  ]")
        sb.AppendLine("}")

        Dim folderPath As String = Path.GetDirectoryName(filePath)
        If Not String.IsNullOrEmpty(folderPath) Then Directory.CreateDirectory(folderPath)

        ' write to a temporary file first so a failed save never leaves a half-written scheme behind
        Dim tmp As String = filePath & ".tmp"
        File.WriteAllText(tmp, sb.ToString(), New UTF8Encoding(False))
        If File.Exists(filePath) Then File.Delete(filePath)
        File.Move(tmp, filePath)

        scheme.FilePath = filePath
    End Sub

    ''' <summary>A file name (without extension) that is safe on Windows for the given scheme name.</summary>
    Public Shared Function SafeFileName(schemeName As String) As String
        If String.IsNullOrEmpty(schemeName) Then Return "color_scheme"
        Dim invalid As Char() = Path.GetInvalidFileNameChars()
        Dim sb As New StringBuilder()
        For Each ch As Char In schemeName.Trim()
            If Array.IndexOf(invalid, ch) >= 0 Then
                sb.Append("_"c)
            Else
                sb.Append(ch)
            End If
        Next
        Dim s As String = sb.ToString().Trim().TrimEnd("."c)
        If s.Length = 0 Then s = "color_scheme"
        Return s
    End Function

    ''' <summary>Full path of the JSON file for a scheme name inside a folder.</summary>
    Public Shared Function PathFor(folder As String, schemeName As String) As String
        Return Path.Combine(folder, SafeFileName(schemeName) & ".json")
    End Function

    ' ---------------------------------------------------------------- helpers

    Private Shared Function NormalizeSchemeType(value As String) As String
        If value Is Nothing Then Return ""
        Dim v As String = value.Trim().ToLowerInvariant()
        If v = ColorScheme.SchemeTypeField OrElse v = ColorScheme.SchemeTypeIntensity Then Return v
        Return ""
    End Function

    Private Shared Function GetString(root As Dictionary(Of String, Object), key As String, fallback As String) As String
        Dim v As Object = Nothing
        If root.TryGetValue(key, v) AndAlso v IsNot Nothing Then
            Return Convert.ToString(v, CultureInfo.InvariantCulture)
        End If
        Return fallback
    End Function

    Private Shared Function ParseColorList(node As Object) As List(Of Color)
        Dim outer As IEnumerable = TryCast(node, IEnumerable)
        If outer Is Nothing OrElse TypeOf node Is String Then
            Throw New InvalidDataException("Expected a list of [r,g,b] triplets.")
        End If

        Dim result As New List(Of Color)()
        For Each item As Object In outer
            Dim inner As IEnumerable = TryCast(item, IEnumerable)
            If inner Is Nothing OrElse TypeOf item Is String Then
                Throw New InvalidDataException("Expected a list of [r,g,b] triplets.")
            End If
            Dim rgb As New List(Of Integer)()
            For Each v As Object In inner
                rgb.Add(Convert.ToInt32(v, CultureInfo.InvariantCulture))
            Next
            If rgb.Count < 3 Then
                Throw New InvalidDataException("A color needs three values [r,g,b].")
            End If
            result.Add(Color.FromArgb(Clamp255(rgb(0)), Clamp255(rgb(1)), Clamp255(rgb(2))))
        Next
        Return result
    End Function

    Private Shared Function Clamp255(v As Integer) As Integer
        If v < 0 Then Return 0
        If v > 255 Then Return 255
        Return v
    End Function

    Private Shared Function FormatRgb(c As Color) As String
        Return String.Format(CultureInfo.InvariantCulture, "[{0},{1},{2}]", c.R, c.G, c.B)
    End Function

    Private Shared Function Quote(text As String) As String
        If text Is Nothing Then Return """"""
        Dim sb As New StringBuilder()
        sb.Append(""""c)
        For Each ch As Char In text
            Select Case ch
                Case """"c
                    sb.Append("\""")
                Case "\"c
                    sb.Append("\\")
                Case ChrW(10)
                    sb.Append("\n")
                Case ChrW(13)
                    sb.Append("\r")
                Case ChrW(9)
                    sb.Append("\t")
                Case Else
                    If AscW(ch) < 32 Then
                        sb.Append("\u").Append(AscW(ch).ToString("x4", CultureInfo.InvariantCulture))
                    Else
                        sb.Append(ch)
                    End If
            End Select
        Next
        sb.Append(""""c)
        Return sb.ToString()
    End Function

End Class
