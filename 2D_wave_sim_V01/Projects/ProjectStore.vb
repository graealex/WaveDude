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
Imports System.Linq

''' <summary>
''' Creates and opens projects. A project is a folder inside the root folder; the folder name is the
''' project name. The main folder of a project contains configuration.json and these subfolders:
'''   assets, output_frames, output_video, scene_images
''' (the configuration is entirely in configuration.json; assets is instead where the user can keep any
''' third-party files used by the project, such as the original Photoshop file of a scene image.)
''' </summary>
Public NotInheritable Class ProjectStore

    Public Const AssetsFolderName As String = "assets"
    Public Const OutputFramesFolderName As String = "output_frames"
    Public Const OutputVideoFolderName As String = "output_video"
    Public Const SceneImageFolderName As String = "scene_images"

    Private Const MaxNameLength As Integer = 100

    Private Shared ReadOnly SubFolderNames As String() = {
        AssetsFolderName, OutputFramesFolderName, OutputVideoFolderName, SceneImageFolderName}

    ' names that Windows does not allow for a file or folder
    Private Shared ReadOnly ReservedNames As String() = {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"}

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Checks a project name. Returns an empty string when the name can be used as a folder name,
    ''' otherwise a message that says what is wrong. The name is never changed silently.
    ''' </summary>
    Public Shared Function ValidateName(projectName As String) As String
        If String.IsNullOrWhiteSpace(projectName) Then Return "Enter a project name."
        If projectName <> projectName.Trim() Then Return "The project name must not start or end with a space."
        If projectName.EndsWith(".", StringComparison.Ordinal) Then Return "The project name must not end with a dot."
        If projectName.Length > MaxNameLength Then
            Return "The project name is too long (at most " & MaxNameLength.ToString() & " characters)."
        End If

        Dim invalid As Char() = Path.GetInvalidFileNameChars()
        For Each ch As Char In projectName
            If Array.IndexOf(invalid, ch) >= 0 Then
                If AscW(ch) < 32 Then Return "The project name contains a control character."
                Return "The project name contains the character '" & ch & "', which is not allowed in a folder name."
            End If
        Next

        Dim baseName As String = projectName.Split("."c)(0).ToUpperInvariant()
        If Array.IndexOf(ReservedNames, baseName) >= 0 Then
            Return "'" & projectName & "' is a reserved name in Windows and cannot be used as a folder name."
        End If
        Return ""
    End Function

    ''' <summary>The full path of the folder of a project with the given name in the root folder.</summary>
    Public Shared Function FolderPathFor(rootFolder As String, projectName As String) As String
        Return Path.Combine(NormalizeFolder(rootFolder), projectName)
    End Function

    ''' <summary>True when the folder is a project folder (it has configuration.json or the old empty Configuration.txt).</summary>
    Public Shared Function IsProjectFolder(folderPath As String) As Boolean
        If String.IsNullOrWhiteSpace(folderPath) OrElse Not Directory.Exists(folderPath) Then Return False
        Return File.Exists(Path.Combine(folderPath, ProjectConfig.FileName)) OrElse
               File.Exists(Path.Combine(folderPath, ProjectConfig.LegacyFileName))
    End Function

    ''' <summary>
    ''' Opens an existing project folder. Throws (with a readable message) when the folder is not a project
    ''' or its configuration cannot be read. A project that still has the old empty Configuration.txt is
    ''' upgraded: configuration.json is written and the empty placeholder is removed.
    ''' </summary>
    Public Shared Function Open(folderPath As String) As Project
        Dim folder As String = NormalizeFolder(folderPath)
        If Not Directory.Exists(folder) Then
            Throw New DirectoryNotFoundException("The folder '" & folder & "' does not exist.")
        End If

        Dim jsonPath As String = Path.Combine(folder, ProjectConfig.FileName)
        Dim legacyPath As String = Path.Combine(folder, ProjectConfig.LegacyFileName)
        Dim opened As Project

        If File.Exists(jsonPath) Then
            opened = New Project(folder, ProjectConfig.Load(jsonPath))
        ElseIf File.Exists(legacyPath) Then
            If File.ReadAllText(legacyPath).Trim().Length > 0 Then
                Throw New InvalidDataException("The file " & ProjectConfig.LegacyFileName &
                                               " is not empty and has no known format, so it was not replaced.")
            End If
            opened = New Project(folder, New ProjectConfig(Path.GetFileName(folder)))
            opened.Save()
            File.Delete(legacyPath)
        Else
            Throw New InvalidDataException("The folder '" & Path.GetFileName(folder) & "' is not a project folder: it contains no " &
                                           ProjectConfig.FileName & ".")
        End If

        EnsureSubFolders(folder)
        Return opened
    End Function

    ''' <summary>
    ''' Creates a new project: the project folder (and the root folder when needed), the standard subfolders
    ''' and configuration.json. Throws (with a readable message) when the name is not valid, the project already
    ''' exists or the folder already exists and is not empty.
    ''' </summary>
    Public Shared Function Create(rootFolder As String, projectName As String) As Project
        Dim problem As String = ValidateName(projectName)
        If problem.Length > 0 Then Throw New ArgumentException(problem, "projectName")
        If String.IsNullOrWhiteSpace(rootFolder) Then Throw New ArgumentException("Enter a root folder.", "rootFolder")

        Dim folder As String = FolderPathFor(rootFolder, projectName)
        If IsProjectFolder(folder) Then
            Throw New IOException("A project named '" & projectName & "' already exists in this root folder. Use 'Open existing' to open it.")
        End If
        If Directory.Exists(folder) AndAlso Directory.EnumerateFileSystemEntries(folder).Any() Then
            Throw New IOException("The folder '" & folder & "' already exists and is not empty, so it cannot be used for a new project.")
        End If

        Directory.CreateDirectory(folder)
        EnsureSubFolders(folder)

        Dim created As New Project(folder, New ProjectConfig(projectName))
        created.Save()
        Return created
    End Function

    ''' <summary>The names of every project folder directly inside the root folder, sorted alphabetically
    ''' (case-insensitive). A subfolder that is not a project folder (IsProjectFolder) is left out. Returns an
    ''' empty list when the root folder does not exist, or cannot be listed (removed drive, no permission, ...).</summary>
    Public Shared Function ListProjects(rootFolder As String) As List(Of String)
        Dim names As New List(Of String)()
        If String.IsNullOrWhiteSpace(rootFolder) OrElse Not Directory.Exists(rootFolder) Then Return names

        Try
            For Each folder As String In Directory.GetDirectories(rootFolder)
                If IsProjectFolder(folder) Then names.Add(Path.GetFileName(folder))
            Next
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Return New List(Of String)()
        End Try
        names.Sort(Function(a, b) String.Compare(a, b, StringComparison.OrdinalIgnoreCase))
        Return names
    End Function

    ''' <summary>
    ''' Permanently deletes a project folder and everything in it. Throws (with a readable message) when the
    ''' folder is not a project folder, so a wrong or unrelated folder can never be removed by mistake.
    ''' </summary>
    Public Shared Sub Delete(folderPath As String)
        Dim folder As String = NormalizeFolder(folderPath)
        If Not IsProjectFolder(folder) Then
            Throw New InvalidDataException("The folder '" & Path.GetFileName(folder) & "' is not a project folder, so it was not deleted.")
        End If
        Directory.Delete(folder, True)
    End Sub

    ''' <summary>
    ''' Renames a project by moving its folder to a new name in the same root folder. Throws (with a readable
    ''' message) when the new name is not valid or a project (or any other file/folder) with that name already
    ''' exists there. Returns the new folder path.
    ''' </summary>
    Public Shared Function Rename(folderPath As String, newProjectName As String) As String
        Dim folder As String = NormalizeFolder(folderPath)
        If Not IsProjectFolder(folder) Then
            Throw New InvalidDataException("The folder '" & Path.GetFileName(folder) & "' is not a project folder, so it was not renamed.")
        End If

        Dim problem As String = ValidateName(newProjectName)
        If problem.Length > 0 Then Throw New ArgumentException(problem, "newProjectName")

        Dim rootFolder As String = Path.GetDirectoryName(folder)
        Dim newFolder As String = FolderPathFor(rootFolder, newProjectName)
        If String.Equals(folder, newFolder, StringComparison.OrdinalIgnoreCase) Then Return folder ' only a case change, or unchanged

        If Directory.Exists(newFolder) OrElse File.Exists(newFolder) Then
            Throw New IOException("'" & newProjectName & "' already exists in this root folder. Choose a different name.")
        End If

        Directory.Move(folder, newFolder)
        Return newFolder
    End Function

    ' ---------------------------------------------------------------- helpers

    Private Shared Sub EnsureSubFolders(folder As String)
        Try
            For Each subName As String In SubFolderNames
                Directory.CreateDirectory(Path.Combine(folder, subName))
            Next
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ' not fatal: a folder that cannot be created is reported when it is needed
        End Try
    End Sub

    ''' <summary>Full path without a trailing separator (except for a drive root such as C:\).</summary>
    Friend Shared Function NormalizeFolder(folderPath As String) As String
        Dim full As String = Path.GetFullPath(folderPath.Trim())
        Dim pathRoot As String = Path.GetPathRoot(full)
        If full.Length > pathRoot.Length Then
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        End If
        Return full
    End Function

End Class
