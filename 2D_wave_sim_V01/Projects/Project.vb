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

Imports System.IO

''' <summary>
''' An open project: a folder (the folder name is the project name) that contains configuration.json
''' and the standard subfolders. Projects are created and opened with ProjectStore.
''' </summary>
Public NotInheritable Class Project

    Private ReadOnly _folderPath As String
    Private ReadOnly _config As ProjectConfig

    Friend Sub New(folderPath As String, config As ProjectConfig)
        _folderPath = folderPath
        _config = config
        ' the folder name is the one and only project name
        _config.ProjectName = Name
    End Sub

    ''' <summary>The name of the project, which is the name of its folder.</summary>
    Public ReadOnly Property Name As String
        Get
            Return Path.GetFileName(_folderPath)
        End Get
    End Property

    ''' <summary>Full path of the main folder of the project.</summary>
    Public ReadOnly Property FolderPath As String
        Get
            Return _folderPath
        End Get
    End Property

    ''' <summary>The folder that contains the project folder (the root folder).</summary>
    Public ReadOnly Property RootFolder As String
        Get
            Dim parent As String = Path.GetDirectoryName(_folderPath)
            If parent Is Nothing Then Return ""
            Return parent
        End Get
    End Property

    ''' <summary>The configuration of the project. Changes are written to disk by Save.</summary>
    Public ReadOnly Property Config As ProjectConfig
        Get
            Return _config
        End Get
    End Property

    ''' <summary>Full path of configuration.json.</summary>
    Public ReadOnly Property ConfigFilePath As String
        Get
            Return Path.Combine(_folderPath, ProjectConfig.FileName)
        End Get
    End Property

    ''' <summary>Where the user can keep any third-party files used by the project (e.g. the original Photoshop
    ''' file of a scene image) - not read or written by the simulator itself.</summary>
    Public ReadOnly Property AssetsFolder As String
        Get
            Return Path.Combine(_folderPath, ProjectStore.AssetsFolderName)
        End Get
    End Property

    Public ReadOnly Property OutputFramesFolder As String
        Get
            Return Path.Combine(_folderPath, ProjectStore.OutputFramesFolderName)
        End Get
    End Property

    Public ReadOnly Property OutputVideoFolder As String
        Get
            Return Path.Combine(_folderPath, ProjectStore.OutputVideoFolderName)
        End Get
    End Property

    Public ReadOnly Property SceneImageFolder As String
        Get
            Return Path.Combine(_folderPath, ProjectStore.SceneImageFolderName)
        End Get
    End Property

    ''' <summary>Writes the configuration to configuration.json.</summary>
    Public Sub Save()
        _config.ProjectName = Name
        _config.Save(ConfigFilePath)
    End Sub

End Class
