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

''' <summary>
''' Lets the user type or edit a free-form description of the current project. Opened modally from Form1's
''' btnDescription_Click, which sets DescriptionText before ShowDialog and reads it back (and saves it to
''' the project) only when the dialog returns DialogResult.OK (btnSaveDescription); btnCancelDescription
''' discards any typing.
''' </summary>
Public Class frmDescription

    ''' <summary>The text being edited. Form1 sets this before showing the dialog and reads it back after
    ''' a DialogResult.OK.</summary>
    Public Property DescriptionText As String
        Get
            Return TextBox1.Text
        End Get
        Set(value As String)
            TextBox1.Text = value
        End Set
    End Property

    Private Sub btnSaveDescription_Click(sender As Object, e As EventArgs) Handles btnSaveDescription.Click
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub btnCancelDescription_Click(sender As Object, e As EventArgs) Handles btnCancelDescription.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

End Class
