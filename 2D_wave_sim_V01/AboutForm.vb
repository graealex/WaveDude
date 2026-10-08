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

Imports System.Drawing

''' <summary>
''' The Info > About window: program name, version and a one-paragraph description. Designer-editable - the
''' layout lives in AboutForm.Designer.vb, open with the Windows Forms Designer to move/resize things. This file
''' only fills in the two pieces of text that aren't fixed at design time (the copyright string, read from the
''' assembly) and loads the application icon. Opened from Form1's AboutToolStripMenuItem_Click.
''' </summary>
Public Class AboutForm

    ''' <summary>Shown as the version - kept in sync by hand with Form1's title bar ("Wave Maker 2D   V0.25"),
    ''' since AssemblyInfo.vb's AssemblyVersion is left at the generic "1.0.0.0" and does not track it.</summary>
    Private Const DisplayVersion As String = "0.27"

    Public Sub New()
        InitializeComponent()

        lblVersion.Text = "Version " & DisplayVersion
        lblCopyright.Text = My.Application.Info.Copyright

        Try
            Using ic As Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                If ic IsNot Nothing Then picIcon.Image = ic.ToBitmap()
            End Using
        Catch
            ' no icon available (e.g. running outside a normal exe) - the picture box just stays empty
        End Try
    End Sub

    Private Sub AboutForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub


End Class
