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
Imports System.IO
Imports System.Text
Imports System.Web.Script.Serialization

''' <summary>
''' Small per-user settings of the program itself (not of a project): the root folder and the project
''' that were used last. Stored in settings.json in the user's application data folder.
''' Reading and writing never throw; a problem simply means the defaults are used.
''' </summary>
Public NotInheritable Class AppSettings

    Private Const KeyRootFolder As String = "last_root_folder"
    Private Const KeyProjectName As String = "last_project_name"
    Private Const KeyProjectRoot As String = "project_root"

    Public Property LastRootFolder As String = ""
    Public Property LastProjectName As String = ""

    ''' <summary>The root folder browsed on the Configuration tab (lstAllProjects, File > Choose project root) -
    ''' distinct from LastRootFolder/LastProjectName, which remember the project that was actually open last, not
    ''' necessarily the folder being browsed for other projects to switch to.</summary>
    Public Property ProjectRoot As String = ""

    Private Shared Function SettingsFilePath() As String
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "2D_wave_sim_V01", "settings.json")
    End Function

    Public Shared Function Load() As AppSettings
        Dim settings As New AppSettings()
        Try
            Dim filePath As String = SettingsFilePath()
            If File.Exists(filePath) Then
                Dim serializer As New JavaScriptSerializer()
                Dim root As Dictionary(Of String, Object) = TryCast(serializer.DeserializeObject(File.ReadAllText(filePath, Encoding.UTF8)), Dictionary(Of String, Object))
                If root IsNot Nothing Then
                    settings.LastRootFolder = ReadText(root, KeyRootFolder)
                    settings.LastProjectName = ReadText(root, KeyProjectName)
                    settings.ProjectRoot = ReadText(root, KeyProjectRoot)
                End If
            End If
        Catch ex As Exception
            ' use the defaults
        End Try
        Return settings
    End Function

    Public Sub Save()
        Try
            Dim filePath As String = SettingsFilePath()
            Directory.CreateDirectory(Path.GetDirectoryName(filePath))
            Dim values As New Dictionary(Of String, String)()
            values(KeyRootFolder) = LastRootFolder
            values(KeyProjectName) = LastProjectName
            values(KeyProjectRoot) = ProjectRoot
            Dim serializer As New JavaScriptSerializer()
            File.WriteAllText(filePath, serializer.Serialize(values), New UTF8Encoding(False))
        Catch ex As Exception
            ' the settings are a convenience only
        End Try
    End Sub

    Private Shared Function ReadText(root As Dictionary(Of String, Object), key As String) As String
        Dim v As Object = Nothing
        If root.TryGetValue(key, v) AndAlso v IsNot Nothing Then Return Convert.ToString(v)
        Return ""
    End Function

End Class
