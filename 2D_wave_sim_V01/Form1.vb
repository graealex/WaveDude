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
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Globalization
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Public Class Form1

    ' ------------------------------------------------------------------ state of the ColorScheme tab

    ' all schemes found in the chosen folder
    Private _schemes As New List(Of ColorScheme)()

    ' the scheme that is being edited (a working copy; Nothing until one has been loaded)
    Private _current As ColorScheme
    Private _dirty As Boolean

    ' the scheme that is selected in the list and only previewed (Nothing when no list item is selected)
    Private _previewScheme As ColorScheme

    ' the stop (0 to ColorScheme.StopCount - 1) that the picker is editing, -1 for none
    Private _selectedStop As Integer = -1

    ' True while the code itself changes the radio buttons (so their events are ignored)
    Private _updatingRadios As Boolean

    Private _stopButtons As Button()
    Private _picker As ColorPickerControl
    Private ReadOnly _thumbs As New ImageList()

    ' lstAllProjects's current sort (Configuration tab project list): which column, and which direction. Column 0
    ' is colProjectName, column 1 is colLastEdit. Defaults to "last edited, newest first", per column 1 descending.
    Private _projectSortColumn As Integer = 1
    Private _projectSortAscending As Boolean = False

    ' lstAllProjects3's own sort state (Outputs tab) - same idea, kept separate since it is an independent list.
    Private _outputProjectSortColumn As Integer = 1
    Private _outputProjectSortAscending As Boolean = False

    ' ListView1's own sort state (color scheme list, tabColorSchemes): which column, and which direction. Column 0
    ' is the "Scheme" (name) column, column 2 is "Type" (field/intensity); column 1 ("Kind") is not sortable.
    ' Defaults to "type, then name", ascending - the standard order asked for.
    Private _schemeSortColumn As Integer = 2
    Private _schemeSortAscending As Boolean = True

    ' The PictureBox that shows the selected scene/image in pnlPreview (Outputs tab) - built in code in Form1_Load
    ' the same way _picker is built for Panel2, since pnlPreview is a plain, empty Panel.
    Private _outputPreviewPic As PictureBox

    ' True while pnlPreview shows a video's thumbnail rather than an actual scene/output image - see
    ' SetOutputPreviewImage and UpdateShareWithEditorsButton (btnShareWithEditors is disabled for a video, since
    ' its thumbnail has a different size than the simulation and would not make sense on the Sources/Probes tabs).
    Private _outputPreviewIsVideo As Boolean

    ' lstScenes/lstImages/lstVideo's own sort state (Outputs tab): which column (0 = name, 1 = created on) and
    ' which direction, one pair per list since they sort independently. All three default to "created on, newest
    ' first", per column 1 descending - the same idea as _projectSortColumn/_outputProjectSortColumn above.
    Private _sceneSortColumn As Integer = 1
    Private _sceneSortAscending As Boolean = False
    Private _imageSortColumn As Integer = 1
    Private _imageSortAscending As Boolean = False
    Private _videoSortColumn As Integer = 1
    Private _videoSortAscending As Boolean = False

    ' The ImageList keeps using the bitmaps that were added to it (it re-creates its native image list from
    ' them, e.g. after Clear or when the ListView on a hidden tab is created). They must therefore stay
    ' alive until the list is rebuilt or the form is closed, and must not be disposed right after Add.
    Private ReadOnly _thumbBitmaps As New List(Of Bitmap)()

    Private Const ThumbWidth As Integer = 120
    Private Const ThumbHeight As Integer = 16

    ' ------------------------------------------------------------------ state of the project

    ' the open project (Nothing when none is open) and the settings of the program itself
    Private _project As Project
    Private _settings As New AppSettings()

    ' ------------------------------------------------------------------ form events

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' one button per stop, in scale order (Color 1 = first entry ... last button = last entry)
        _stopButtons = New Button() {btnColor1, btnCOlor2, btnColor3, btnColor4, btnColor5, btnColor6, btnColor7, btnColor8, btnColor9}
        Debug.Assert(_stopButtons.Length = ColorScheme.StopCount, "The number of Color buttons must equal ColorScheme.StopCount.")
        For Each btn As Button In _stopButtons
            btn.UseVisualStyleBackColor = False
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 1
            btn.FlatAppearance.BorderColor = Color.Black
        Next

        ' scheme list: thumbnail + name, grouped by category
        _thumbs.ImageSize = New Size(ThumbWidth, ThumbHeight)
        _thumbs.ColorDepth = ColorDepth.Depth32Bit
        ListView1.View = View.Details
        ListView1.FullRowSelect = True
        ListView1.MultiSelect = False
        ListView1.HideSelection = False
        ListView1.HeaderStyle = ColumnHeaderStyle.Clickable
        ListView1.SmallImageList = _thumbs
        ListView1.Columns.Clear()
        ListView1.Columns.Add("Scheme", 250)
        ListView1.Columns.Add("Kind", 55)
        ListView1.Columns.Add("Type", 62)
        UpdateSchemeListSortHeaders()

        ' scheme bar and picker
        pnlScheme.BorderStyle = BorderStyle.FixedSingle
        Panel2.BorderStyle = BorderStyle.FixedSingle
        _picker = New ColorPickerControl()
        _picker.Dock = DockStyle.Fill
        Panel2.Controls.Add(_picker)
        AddHandler _picker.ColorChanged, AddressOf Picker_ColorChanged

        txtCurrentColorScheme.ReadOnly = True

        ' tabMainFunctions: owner-drawn only so a disabled tab's own label can be shown in a lighter gray
        ' (WinForms does not do this by itself - a disabled TabPage still draws its label at full strength) -
        ' see tabMainFunctions_DrawItem.
        tabMainFunctions.DrawMode = TabDrawMode.OwnerDrawFixed

        ' lstAllProjects (Configuration tab project list): sortable by clicking colProjectName/colLastEdit, see
        ' lstAllProjects_ColumnClick. Show the initial sort direction in the header the same way a click would.
        UpdateProjectListSortHeaders()

        ' the program's own scheme folder (ColorSchemeList in the project, or next to the exe when deployed)
        txtSchemeFolder.Text = ColorSchemeStore.FindDefaultFolder()
        If Directory.Exists(txtSchemeFolder.Text) Then LoadSchemeList()

        RefreshEditorState()

        ' lblInfo (Settings tab): a clickable "hyperlink" style label - see lblInfo_Click for what it shows
        lblInfo.ForeColor = Color.Blue
        lblInfo.Cursor = Cursors.Hand

        InitializeSceneControls()
        InitializeSimulationControls()
        InitializeProbeControls()
        InitializeSourceControls()

        ' btnPickRoot must stay clickable even when no project root is known yet (that is the one situation it
        ' exists for), but the Designer put it inside grpProjects, and WinForms disables an enabled child along
        ' with a disabled parent - grpProjects.Enabled = False (UpdateProjectAccess, no root) would silently
        ' swallow its clicks right when they matter most. Move it onto tabConfiguration itself, in the same
        ' screen position, so it is never caught by that.
        Dim rootButtonSpot As Point = grpProjects.Location
        rootButtonSpot.Offset(btnPickRoot.Location)
        grpProjects.Controls.Remove(btnPickRoot)
        tabConfiguration.Controls.Add(btnPickRoot)
        btnPickRoot.Location = rootButtonSpot
        btnPickRoot.BringToFront()

        InitializeProject()
        InitializeOutputsTab()

        ' Enter in a NumericUpDown's edit box otherwise just beeps (Windows beeps on an unhandled Enter
        ' keystroke) and leaves the typed value sitting there uncommitted - HookNumericUpDownEnterKey wires
        ' every NumericUpDown on the form, wherever it lives, to NumericUpDown_KeyDown instead.
        HookNumericUpDownEnterKey(Me)
    End Sub

    ''' <summary>Wires every NumericUpDown found anywhere under root (recursing into GroupBox/TabPage/Panel/...
    ''' containers) to NumericUpDown_KeyDown, so Enter commits the typed value and moves on instead of beeping.
    ''' Called once, from Form1_Load, on the whole form - a plain loop over Controls rather than a fixed list, so
    ''' it also covers any NumericUpDown added later without needing to come back and wire it up by hand.</summary>
    Private Sub HookNumericUpDownEnterKey(root As Control)
        For Each child As Control In root.Controls
            Dim box As NumericUpDown = TryCast(child, NumericUpDown)
            If box IsNot Nothing Then AddHandler box.KeyDown, AddressOf NumericUpDown_KeyDown
            If child.HasChildren Then HookNumericUpDownEnterKey(child)
        Next
    End Sub

    ''' <summary>Enter in a NumericUpDown: stops the beep (e.SuppressKeyPress - Enter is not a character its
    ''' edit box accepts, which is what Windows was beeping about) and moves focus on to the next control, the
    ''' same as Tab would - which also commits whatever was typed, since a NumericUpDown parses/clamps its text
    ''' on losing focus regardless of how focus was lost.</summary>
    Private Sub NumericUpDown_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter Then Return
        e.SuppressKeyPress = True
        e.Handled = True
        Dim box As Control = TryCast(sender, Control)
        If box IsNot Nothing Then Me.SelectNextControl(box, True, True, True, True)
    End Sub

    Private Sub Form1_FormClosed(sender As Object, e As FormClosedEventArgs) Handles MyBase.FormClosed
        ClearPreviews()

        For Each thumb As Bitmap In _thumbBitmaps
            thumb.Dispose()
        Next
        _thumbBitmaps.Clear()

        ReplaceSceneImage(Nothing)
        DisposeEmptyPreview()
        ClearOutputPreview()
    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If Not ConfirmSaveProject() Then
            e.Cancel = True
            Return
        End If

        If _runner IsNot Nothing Then
            Dim running As DialogResult = MessageBox.Show(Me, "A simulation is running. Stop it and close the program?",
                "Simulation", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If running <> DialogResult.Yes Then
                e.Cancel = True
                Return
            End If
            ' the runner closes the video files before it ends
            _runner.Cancel()
            _runner.WaitForExit(15000)
        End If

        If _dirty Then
            Dim answer As DialogResult = MessageBox.Show(Me,
                "The current color scheme has unsaved changes. Close anyway?",
                "Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            If answer <> DialogResult.Yes Then e.Cancel = True
        End If
    End Sub

    ' ------------------------------------------------------------------ project name and folder
    '
    ' A project is a folder inside the root folder; the folder name is the project name (txtProjectName).
    ' The main folder of a project holds configuration.json and the standard subfolders (see ProjectStore).
    '
    ' The settings on the tabs are kept in memory and only written to configuration.json by "Save project"
    ' and "Save as". _project is Nothing for a new project that has not been saved yet.

    ' True when there are changes that are not saved in configuration.json
    Private _projectDirty As Boolean

    ' btnPickRoot (grpProjects) blinks light blue, slowly, while no project root is known yet - e.g. right after
    ' installing on a different computer, when AppSettings.ProjectRoot is still empty and nothing on the Sources/
    ' Probes/Viewer tabs works yet. UpdateProjectAccess starts/stops this alongside the rest of the "has a root"
    ' state; RootBlinkTimer_Tick does the actual blinking.
    Private WithEvents _rootBlinkTimer As New Timer() With {.Interval = 700}
    Private _rootBlinkOn As Boolean

    ''' <summary>Enables/disables everything that depends on a project root being chosen and/or an actual, named
    ''' project being the current one. grpProjects and the Outputs tab (its own project list, not necessarily the
    ''' one that is open) only need a root. The scene/simulation groups, the Sources/Probes/Viewer tabs, most of
    ''' "Quick navigate to" (all but btnGotoConfiguration, which jumps to the always-available Configuration tab),
    ''' "Simulation Control", "Share with source / probe" and Save all need a real project. The Configuration tab
    ''' (where the root and the project list themselves live) and the color scheme tab manage the application's
    ''' own settings, not a project's, so they are never restricted. Disabling
    ''' a TabPage here is not enough by itself to keep the user out of it - see tabMainFunctions_Selecting, which
    ''' refuses to switch to a disabled tab at all instead of just showing its (disabled) contents.</summary>
    Private Sub UpdateProjectAccess()
        Dim hasRoot As Boolean = _settings.ProjectRoot.Length > 0
        Dim hasProject As Boolean = _project IsNot Nothing

        grpProjects.Enabled = hasRoot
        tabOutput.Enabled = hasRoot

        ' btnPickRoot (moved onto tabConfiguration itself in Form1_Load, so grpProjects.Enabled above cannot
        ' disable it) blinks light blue, slowly, for exactly the situation it exists for: no project root known
        ' yet - a fresh install on another computer, before AppSettings.ProjectRoot is set and everything else
        ' on screen is still disabled.
        If hasRoot Then
            _rootBlinkTimer.Stop()
            _rootBlinkOn = False
            btnPickRoot.UseVisualStyleBackColor = True
        Else
            _rootBlinkTimer.Start()
        End If

        grpSceneProperties.Enabled = hasProject
        grpSimulationSettings.Enabled = hasProject
        tabSourceList.Enabled = hasProject
        tabProbeList.Enabled = hasProject
        tabViewer.Enabled = hasProject

        ' grpNavigateTabs itself stays enabled: btnGotoConfiguration jumps to the always-available Configuration
        ' tab, so only the other three "quick navigate" buttons need a real project.
        btnGotoPreview.Enabled = hasProject
        btnGotoProbes.Enabled = hasProject
        btnGotoSources.Enabled = hasProject

        grpSimulationControlButtons.Enabled = hasProject
        grpShareImage.Enabled = hasProject
        btnSaveConfiguration.Enabled = hasProject
        btnSaveConfiguration2.Enabled = hasProject
        btnSaveAs.Enabled = hasProject
        btnDescription.Enabled = hasProject

        ' Changing a TabPage's Enabled does not by itself repaint the tab strip, so a label's color
        ' (tabMainFunctions_DrawItem) would otherwise stay stuck at its old, disabled gray until something
        ' unrelated forced a redraw (resizing the window, switching tabs, ...) - force it here instead.
        tabMainFunctions.Invalidate()
    End Sub

    ''' <summary>Toggles btnPickRoot between light blue and its normal color (UpdateProjectAccess starts/stops
    ''' this timer, only while no project root is known yet).</summary>
    Private Sub RootBlinkTimer_Tick(sender As Object, e As EventArgs) Handles _rootBlinkTimer.Tick
        _rootBlinkOn = Not _rootBlinkOn
        btnPickRoot.UseVisualStyleBackColor = False
        btnPickRoot.BackColor = If(_rootBlinkOn, Color.LightBlue, SystemColors.Control)
    End Sub

    ''' <summary>A disabled TabPage (UpdateProjectAccess) still lets itself be clicked into and shows its
    ''' (disabled) contents unless refused here - WinForms' TabControl has no built-in notion of a locked tab.</summary>
    Private Sub tabMainFunctions_Selecting(sender As Object, e As TabControlCancelEventArgs) Handles tabMainFunctions.Selecting
        If e.TabPage.Enabled Then Return
        e.Cancel = True
        MessageBox.Show(Me, "Please open or create a project first.", "No project", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ''' <summary>Draws each tab's own label - needed only so a disabled tab's label can be shown in a lighter
    ''' gray than an enabled one's (Form1_Load sets tabMainFunctions.DrawMode to OwnerDrawFixed for this).</summary>
    Private Sub tabMainFunctions_DrawItem(sender As Object, e As DrawItemEventArgs) Handles tabMainFunctions.DrawItem
        Dim page As TabPage = tabMainFunctions.TabPages(e.Index)
        Dim tabRect As Rectangle = tabMainFunctions.GetTabRect(e.Index)

        ' the selected tab a shade lighter than the rest, so which one is open is still visible without the
        ' native visual style this drawing replaces
        Dim backColor As Color = If(e.Index = tabMainFunctions.SelectedIndex, SystemColors.Window, SystemColors.Control)
        Using backBrush As New SolidBrush(backColor)
            e.Graphics.FillRectangle(backBrush, tabRect)
        End Using

        Dim textColor As Color = If(page.Enabled, SystemColors.ControlText, Color.Silver)
        Dim flags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine
        TextRenderer.DrawText(e.Graphics, page.Text, tabMainFunctions.Font, tabRect, textColor, flags)
    End Sub

    ''' <summary>Startup: nothing from any previous project is filled into the form. Only the chosen project
    ''' root folder (AppSettings.ProjectRoot) is restored, if one was already chosen on an earlier run - everything
    ''' that needs a root or a real project stays disabled until then (UpdateProjectAccess).</summary>
    Private Sub InitializeProject()
        _settings = AppSettings.Load()

        txtRootFolder.Text = _settings.ProjectRoot
        txtProjectName.Text = ""

        UpdateProjectAccess()

        UpdateTitle()
        RefreshProjectList()
    End Sub

    ''' <summary>File > Open: browse directly to the main folder of a project and open it, without going through
    ''' the project list (browsing for the project ROOT folder instead is btnBrowseRoot / File > Choose project
    ''' root / double-clicking txtRootFolder - see ChooseProjectRootToolStripMenuItem_Click).</summary>
    Private Sub OpenProject_Click(sender As Object, e As EventArgs) Handles OpenToolStripMenuItem.Click
        Try
            Dim folder As String = BrowseForFolder("Choose the main folder of the project (the folder that contains configuration.json)",
                                                   txtRootFolder.Text.Trim(), False)
            If folder.Length = 0 Then Return
            If Not ConfirmSaveProject() Then Return

            SetCurrentProject(ProjectStore.Open(folder))
        Catch ex As Exception
            MessageBox.Show(Me, "The project could not be opened:" & vbCrLf & vbCrLf & ex.Message,
                            "Open project", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ''' <summary>Save project (buttons and File > Save): creates the project (folders and configuration file) when it is new, otherwise overwrites its configuration file.</summary>
    Private Sub SaveProject_Click(sender As Object, e As EventArgs) Handles btnSaveConfiguration.Click, SaveToolStripMenuItem.Click
        SaveProject()
    End Sub

    ''' <summary>
    ''' Save as: a simple name prompt (PromptForText, the same small dialog btnRenameProject uses), not a file
    ''' dialog - the new project always goes into the current root folder (txtRootFolder), never somewhere else,
    ''' and there is no file extension to worry about (a project is just a folder, see ProjectStore). Saving under
    ''' a name that already exists in the root is not refused up front: SaveProjectTo itself asks to overwrite in
    ''' that case, same as plain Save would for an existing project.
    ''' </summary>
    Private Sub SaveAs_Click(sender As Object, e As EventArgs) Handles btnSaveAs.Click
        Dim root As String = txtRootFolder.Text.Trim()
        If root.Length = 0 Then
            MessageBox.Show(Me, "Enter a root folder first (double-click the box to browse).", "Save project as",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim projectName As String = txtProjectName.Text.Trim()
        If Not PromptForText("Save project as", "New project name (saved in the current root folder):", projectName, projectName) Then Return
        projectName = projectName.Trim()

        Dim problem As String = ProjectStore.ValidateName(projectName)
        If problem.Length > 0 Then
            MessageBox.Show(Me, problem, "Save project as", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        SaveProjectTo(root, projectName)
    End Sub

    ''' <summary>Opens frmDescription so the user can add/edit a free-form description of the current project,
    ''' stored in the project's own configuration.json (ProjectConfig.Description) - saved to disk immediately
    ''' on OK, the same as btnRenameProject's rename takes effect right away rather than waiting for "Save
    ''' project". Only enabled while a real (saved) project is open (UpdateProjectAccess) - a new, unsaved
    ''' project has no configuration.json yet to hold it.</summary>
    Private Sub btnDescription_Click(sender As Object, e As EventArgs) Handles btnDescription.Click
        If _project Is Nothing Then Return
        Using dlg As New frmDescription()
            dlg.DescriptionText = _project.Config.Description
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                _project.Config.Description = dlg.DescriptionText
                _project.Save()
            End If
        End Using
    End Sub

    Private Sub ExitProgram_Click(sender As Object, e As EventArgs) Handles ExitToolStripMenuItem.Click
        Close()
    End Sub

    ' Info menu (added on tabMainFunctions's menu strip): Help opens a page describing the program's tabs and
    ' controls, About shows the program name/version. Both are plain, self-built forms (HelpForm.vb, AboutForm.vb).

    Private Sub HelpToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles HelpToolStripMenuItem.Click
        Using dlg As New HelpForm()
            dlg.ShowDialog(Me)
        End Using
    End Sub

    Private Sub AboutToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AboutToolStripMenuItem.Click
        Using dlg As New AboutForm()
            dlg.ShowDialog(Me)
        End Using
    End Sub

    ''' <summary>File > Choose project root, the btnBrowseRoot button, btnPickRoot (the blinking button that is
    ''' always reachable even with no root yet - see Form1_Load/UpdateProjectAccess), and double-clicking the
    ''' (read-only) root folder box: all four let the user pick the folder that contains the project folders
    ''' (txtRootFolder just displays it - AppSettings.ProjectRoot is the real, persisted value, restored again on
    ''' the next start). Once a root is chosen, grpProjects and the Outputs tab become usable and the project
    ''' list is (re)populated.</summary>
    Private Sub ChooseProjectRootToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ChooseProjectRootToolStripMenuItem.Click, btnBrowseRoot.Click, btnPickRoot.Click, txtRootFolder.DoubleClick
        Dim start As String = If(_settings.ProjectRoot.Length > 0, _settings.ProjectRoot, txtRootFolder.Text.Trim())
        Dim chosen As String = BrowseForFolder("Choose the root folder that contains your project folders", start, True)
        If chosen.Length = 0 Then Return

        _settings.ProjectRoot = chosen
        _settings.Save()
        txtRootFolder.Text = chosen
        UpdateProjectAccess()
        RefreshProjectList()
    End Sub

    ''' <summary>lblInfo (Settings tab): a clickable "hyperlink" style label (see Form1_Load for its Cursor/ForeColor)
    ''' that explains the 4th order Laplacian option and why it needs a different "Set c-value" (File menu).</summary>
    Private Sub lblInfo_Click(sender As Object, e As EventArgs) Handles lblInfo.Click
        MessageBox.Show(Me,
            "The standard 3rd order Laplacian processing inherently leads to large dispersion, meaning that longer " &
            "wavelengths propagate faster through the medium. Choosing 4th order processing (including sub-step sampling) " &
            "reduces this effect significantly. However, processing is somewhat slower. Another effect of 4th order " &
            "processing is that it requires a different propagation speed (c). The value can be specified per project and set in the File menu. " &
            "For 4th order processing choose 1.00 for c. For standard processing, this value can be quite a bit " &
            "different. For example, with standard processing for waves with a frequency of 100, c = 0.56, leading in " &
            "this case to a wavelength of only 9 pixels (instead of 15). Because of the dispersion present in standard " &
            "processing, the exact value of c is strongly dependent on the frequency. (lower frequencies -> higher c " &
            "value; higher frequencies -> lower c value).",
            "4th order Laplacian and wave speed (c)", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ''' <summary>File > Set c-value: shows the current wave propagation speed c (SimulationSettings.WaveSpeedC) and
    ''' lets it be changed. c is only used to estimate a source's wavelength (lblLambdaIs on the Sources tab).</summary>
    Private Sub SetCValueToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SetCValueToolStripMenuItem.Click
        Dim newValue As Double
        If PromptForCValue(_simulation.WaveSpeedC, newValue) Then
            If newValue <> _simulation.WaveSpeedC Then
                _simulation.WaveSpeedC = newValue
                MarkProjectChanged()
            End If
            UpdateLambdaLabel()
        End If
    End Sub

    ''' <summary>
    ''' A small modal dialog, built entirely in code (no Designer file needed), to view and change the wave
    ''' propagation speed c. Returns True (with the chosen value in result) when the user pressed OK.
    ''' </summary>
    Private Function PromptForCValue(current As Double, ByRef result As Double) As Boolean
        Using dlg As New Form()
            dlg.Text = "Set c-value"
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.MinimizeBox = False
            dlg.MaximizeBox = False
            dlg.ShowInTaskbar = False
            dlg.ClientSize = New Size(340, 116)

            Dim lbl As New Label()
            lbl.Text = "Wave propagation speed c (in pixels per step)" & vbCrLf &
                       "(lambda = c / frequency)." & vbCrLf &
                       "Only used to estimate a source's wavelength in screen pixels"
            lbl.SetBounds(12, 10, 316, 44)

            Dim num As New NumericUpDown()
            num.DecimalPlaces = 3
            num.Minimum = 0.001D
            num.Maximum = 1000000D
            num.Increment = 0.1D
            num.SetBounds(12, 60, 110, 22)
            Dim clamped As Double = Math.Max(CDbl(num.Minimum), Math.Min(CDbl(num.Maximum), current))
            num.Value = CDec(clamped)

            Dim btnOk As New Button()
            btnOk.Text = "OK"
            btnOk.DialogResult = DialogResult.OK
            btnOk.SetBounds(152, 80, 84, 26)

            Dim btnCancel As New Button()
            btnCancel.Text = "Cancel"
            btnCancel.DialogResult = DialogResult.Cancel
            btnCancel.SetBounds(244, 80, 84, 26)

            dlg.Controls.AddRange(New Control() {lbl, num, btnOk, btnCancel})
            dlg.AcceptButton = btnOk
            dlg.CancelButton = btnCancel

            If dlg.ShowDialog(Me) = DialogResult.OK Then
                result = CDbl(num.Value)
                Return True
            End If
        End Using
        result = current
        Return False
    End Function

    ''' <summary>
    ''' A small modal dialog, built entirely in code (no Designer file needed), that asks for one line of text -
    ''' used to rename a project. Returns True (with the typed text in result) when the user pressed OK.
    ''' </summary>
    Private Function PromptForText(title As String, prompt As String, defaultValue As String, ByRef result As String) As Boolean
        Using dlg As New Form()
            dlg.Text = title
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.MinimizeBox = False
            dlg.MaximizeBox = False
            dlg.ShowInTaskbar = False
            dlg.ClientSize = New Size(340, 110)

            Dim lbl As New Label()
            lbl.Text = prompt
            lbl.SetBounds(12, 10, 316, 20)

            Dim txt As New TextBox()
            txt.SetBounds(12, 34, 316, 22)
            txt.Text = defaultValue
            txt.SelectAll()

            Dim btnOk As New Button()
            btnOk.Text = "OK"
            btnOk.DialogResult = DialogResult.OK
            btnOk.SetBounds(152, 68, 84, 26)

            Dim btnCancel As New Button()
            btnCancel.Text = "Cancel"
            btnCancel.DialogResult = DialogResult.Cancel
            btnCancel.SetBounds(244, 68, 84, 26)

            dlg.Controls.AddRange(New Control() {lbl, txt, btnOk, btnCancel})
            dlg.AcceptButton = btnOk
            dlg.CancelButton = btnCancel
            dlg.ActiveControl = txt

            If dlg.ShowDialog(Me) = DialogResult.OK Then
                result = txt.Text
                Return True
            End If
        End Using
        result = defaultValue
        Return False
    End Function

    ''' <summary>Saves the project in the folder root\name shown in the boxes. Returns False when nothing was saved.</summary>
    Private Function SaveProject() As Boolean
        Dim root As String = txtRootFolder.Text.Trim()
        Dim projectName As String = txtProjectName.Text.Trim()

        If root.Length = 0 Then
            MessageBox.Show(Me, "Enter a root folder first (double-click the box to browse).", "Save project",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            txtRootFolder.Focus()
            Return False
        End If

        Dim problem As String = ProjectStore.ValidateName(projectName)
        If problem.Length > 0 Then
            MessageBox.Show(Me, problem, "Save project", MessageBoxButtons.OK, MessageBoxIcon.Information)
            txtProjectName.Focus()
            Return False
        End If

        Return SaveProjectTo(root, projectName)
    End Function

    ''' <summary>
    ''' Saves the current settings as the project root\projectName:
    ''' the open project itself: its configuration file is overwritten;
    ''' another existing project: after confirmation its configuration file is overwritten;
    ''' a folder that does not exist yet: the project folder, its subfolders and the configuration file are created.
    ''' </summary>
    Private Function SaveProjectTo(root As String, projectName As String) As Boolean
        Try
            Dim rootFull As String = ProjectStore.NormalizeFolder(root)
            Dim folder As String = Path.Combine(rootFull, projectName)
            Dim previous As Project = _project
            Dim target As Project

            If previous IsNot Nothing AndAlso String.Equals(folder, previous.FolderPath, StringComparison.OrdinalIgnoreCase) Then
                target = previous
            ElseIf ProjectStore.IsProjectFolder(folder) Then
                Dim answer As DialogResult = MessageBox.Show(Me,
                    "A project named '" & projectName & "' already exists in the root folder" & vbCrLf & rootFull & vbCrLf & vbCrLf &
                    "Overwrite its configuration with the current settings?",
                    "Save project", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If answer <> DialogResult.Yes Then Return False
                target = ProjectStore.Open(folder)
            Else
                If Not Directory.Exists(rootFull) Then
                    Dim answer As DialogResult = MessageBox.Show(Me,
                        "The root folder does not exist:" & vbCrLf & rootFull & vbCrLf & vbCrLf & "Create it?",
                        "Save project", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                    If answer <> DialogResult.Yes Then Return False
                End If
                target = ProjectStore.Create(rootFull, projectName)
            End If

            PrepareSceneImageFor(target, previous)
            _scene.ToConfig(target.Config)
            _simulation.ToConfig(target.Config)
            ProbeSettings.ListToConfig(_probes, target.Config)
            SourceSettings.ListToConfig(_sources, target.Config)
            target.Save()

            SetCurrentProject(target)
            Return True
        Catch ex As Exception
            MessageBox.Show(Me, "The project could not be saved:" & vbCrLf & vbCrLf & ex.Message,
                            "Save project", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Makes the scene image path in _scene right for the project it is saved in: an image that does not already lie
    ''' inside the target project's own folder (the previous project's folder, or anywhere else) is copied into the
    ''' target's scene_images folder. This is the same copy that btnLOadSceneImage_Click already does when a project
    ''' exists; this one catches an image that was picked before the (new) project was ever saved.
    ''' </summary>
    Private Sub PrepareSceneImageFor(target As Project, previous As Project)
        If _scene.ImageFile.Length = 0 OrElse Object.ReferenceEquals(target, previous) Then Return

        Dim oldFolder As String = If(previous Is Nothing, "", previous.FolderPath)
        Dim fullPath As String = SceneSettings.ToFullPath(oldFolder, _scene.ImageFile)

        If File.Exists(fullPath) AndAlso Not IsInsideFolder(fullPath, target.FolderPath) Then
            fullPath = CopyIntoSceneImageFolder(target, fullPath)
        End If

        _scene.ImageFile = SceneSettings.ToStoredPath(target.FolderPath, fullPath)
    End Sub

    ''' <summary>True when fullPath lies inside folder (or one of its subfolders).</summary>
    Private Shared Function IsInsideFolder(fullPath As String, folder As String) As Boolean
        If String.IsNullOrEmpty(folder) Then Return False
        Dim prefix As String = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Return Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(prefix), StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' Copies an image file into the scene_images folder of a project and returns the full path of the copy. A different
    ''' file with the same name is not overwritten: the copy then gets a number. A copy that is already there (the same
    ''' file, same size) is reused instead of copied again.
    ''' </summary>
    Private Shared Function CopyIntoSceneImageFolder(project As Project, fullPath As String) As String
        Directory.CreateDirectory(project.SceneImageFolder)
        Dim fileName As String = Path.GetFileName(fullPath)
        Dim copy As String = Path.Combine(project.SceneImageFolder, fileName)

        Dim counter As Integer = 2
        Do While File.Exists(copy) AndAlso New FileInfo(copy).Length <> New FileInfo(fullPath).Length
            copy = Path.Combine(project.SceneImageFolder, Path.GetFileNameWithoutExtension(fileName) & " (" &
                                counter.ToString(CultureInfo.InvariantCulture) & ")" & Path.GetExtension(fileName))
            counter += 1
        Loop
        If Not File.Exists(copy) Then File.Copy(fullPath, copy)
        Return copy
    End Function

    ''' <summary>
    ''' Asks what to do with unsaved changes before they would be lost. Returns False when the user cancels
    ''' (or saving failed), True when it is fine to continue.
    ''' </summary>
    Private Function ConfirmSaveProject() As Boolean
        ' Nothing to save when no named project exists yet (a project only exists once it has been loaded from
        ' the list or created and named through btnNewProject's dialog) - there is no such thing as an
        ' "unsaved new project" to ask about any more.
        If _project Is Nothing OrElse Not _projectDirty Then Return True

        Dim answer As DialogResult = MessageBox.Show(Me, "There are unsaved changes in the project '" & _project.Name & "'. Save them first?",
                                                     "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
        Select Case answer
            Case DialogResult.Yes
                Return SaveProject()
            Case DialogResult.No
                Return True
            Case Else
                Return False
        End Select
    End Function

    ''' <summary>Resets the in-memory project state (scene, simulation, probes, sources) to blank defaults.
    ''' Does not touch grpSceneProperties/grpSimulationSettings/grpProjects.Enabled - a caller that goes on to
    ''' make a real, named project current (SetCurrentProject) re-enables them there; a caller that ends up with
    ''' no project at all (e.g. deleting the one that was open) should instead disable them, since - per the
    ''' startup design - there is no such thing as an editable "new project" that does not exist on disk yet.</summary>
    Private Sub StartNewProject()
        _project = Nothing
        _scene = New SceneSettings()
        ' chkDampingEdge and chkUseObjectSources have no software-chosen default of their own (see their
        ' comments in SceneSettings.vb) - a new project starts at whatever the controls' own Designer values are
        _scene.DampingEdgeEnabled = chkDampingEdge.Checked
        _scene.UseObjectSources = chkUseObjectSources.Checked
        ReplaceSceneImage(Nothing)
        txtProjectName.Text = ""
        ShowSceneInControls()
        _simulation = New SimulationSettings()
        ' numSimulationSteps and numTimeDecayFactor have no software-chosen default of their own (see their
        ' comments in SimulationSettings.vb) - a new project starts at whatever the controls' own Designer
        ' values are
        _simulation.Steps = CInt(numSimulationSteps.Value)
        _simulation.TimeDecayFactor = CDbl(numTimeDecayFactor.Value)
        ShowSimulationInControls()
        ResetProbes()
        ResetSources()
        _fieldVideoFile = ""
        _intensityVideoFile = ""
        UpdateVideoButtons()
        _projectDirty = False
        UpdateTitle()
        txtProjectName.Focus()
    End Sub

    ''' <summary>Makes a saved project the current one: shows it in the boxes and remembers it for the next start.
    ''' Also called by SaveProjectTo for a plain "Save project" of the project that is already open (target and
    ''' the previous _project are then the same folder) - the preview-clearing below must only run when the
    ''' project is actually changing, or every "Save configuration" would blank the Sources/Probes tab's picture
    ''' (see ClearPreviewImage's comment) even though nothing about that picture is out of date.</summary>
    Private Sub SetCurrentProject(opened As Project)
        Dim isDifferentProject As Boolean = _project Is Nothing OrElse
            Not String.Equals(_project.FolderPath, opened.FolderPath, StringComparison.OrdinalIgnoreCase)

        _project = opened
        txtRootFolder.Text = opened.RootFolder
        txtProjectName.Text = opened.Name
        UpdateProjectAccess()

        _settings.LastRootFolder = opened.RootFolder
        _settings.LastProjectName = opened.Name
        _settings.Save()

        If isDifferentProject Then
            ' a preview picture (Sources/Probes tab) loaded for the previous project has no relevance to this one -
            ' clear it so the tab shows this project's own scene image (or a blank placeholder) the next time it is
            ' visited, rather than a leftover picture from whatever was open before (AutoLoadSceneImageIfEmpty only
            ' fills a picture that is empty, so a stale one left behind would otherwise never be replaced)
            ClearPreviewImage(_picSource)
            ClearPreviewImage(_picProbe)
        End If

        LoadSceneFromProject()
        _simulation = SimulationSettings.FromConfig(_project.Config)
        ShowSimulationInControls()
        LoadProbesFromProject()
        LoadSourcesFromProject()
        _projectDirty = False
        UpdateTitle()
        RefreshProjectList()
    End Sub

    ''' <summary>Disposes and clears a preview picture's image (Sources/Probes tab), if it has one.</summary>
    Private Sub ClearPreviewImage(pic As PictureBox)
        If pic Is Nothing OrElse pic.Image Is Nothing Then Return
        Dim old As Image = pic.Image
        pic.Image = Nothing
        old.Dispose()
        pic.Invalidate()
    End Sub

    ''' <summary>Something in the project was changed and is not saved yet.</summary>
    Private Sub MarkProjectChanged()
        If _loadingScene OrElse _loadingSim OrElse _loadingProbe OrElse _loadingSource Then Return
        _projectDirty = True
        UpdateTitle()
    End Sub

    Private Sub UpdateTitle()
        Dim projectLabel As String = If(_project Is Nothing, "(new project)", _project.Name)
        Text = "2D wave simulation - " & projectLabel & If(_projectDirty, " *", "") & If(_simStatus.Length > 0, "   |   " & _simStatus, "")
    End Sub

    ''' <summary>
    ''' Lets the user choose a folder: type or paste a full path directly into the box, or click "Browse..." for
    ''' the usual Windows tree picker - rooted at "This PC" rather than the Desktop, so every drive (C:\, D:\, a
    ''' network drive, anywhere) is one click away instead of buried under Desktop. This replaces a plain
    ''' FolderBrowserDialog call, which on this machine shows no address bar to type a path into and makes other
    ''' drives awkward to reach from its Desktop-rooted tree. Returns the chosen folder, or an empty string when
    ''' cancelled; when allowNewFolder is False, OK is refused until the typed/picked path actually exists (a new,
    ''' not-yet-created one is fine when allowNewFolder is True - the caller decides whether/how to create it).
    ''' </summary>
    Private Function BrowseForFolder(description As String, startFolder As String, allowNewFolder As Boolean) As String
        Using dlg As New Form()
            dlg.Text = "Choose a folder"
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.MinimizeBox = False
            dlg.MaximizeBox = False
            dlg.ShowInTaskbar = False
            dlg.ClientSize = New Size(480, 124)

            Dim lbl As New Label()
            lbl.Text = description
            lbl.SetBounds(12, 10, 456, 34)

            Dim txt As New TextBox()
            txt.SetBounds(12, 50, 362, 22)
            txt.Text = startFolder

            Dim btnBrowse As New Button()
            btnBrowse.Text = "Browse..."
            btnBrowse.SetBounds(380, 49, 88, 24)
            AddHandler btnBrowse.Click, Sub(sender As Object, e As EventArgs)
                                             Using pick As New FolderBrowserDialog()
                                                 pick.Description = description
                                                 pick.ShowNewFolderButton = allowNewFolder
                                                 ' MyComputer (not the default Desktop) so every drive is a
                                                 ' top-level node, reachable in one click instead of nested
                                                 ' under Desktop/This PC
                                                 pick.RootFolder = Environment.SpecialFolder.MyComputer
                                                 If Directory.Exists(txt.Text.Trim()) Then pick.SelectedPath = txt.Text.Trim()
                                                 If pick.ShowDialog(dlg) = DialogResult.OK Then
                                                     txt.Text = pick.SelectedPath
                                                     txt.SelectAll()
                                                 End If
                                             End Using
                                         End Sub

            Dim btnOk As New Button()
            btnOk.Text = "OK"
            btnOk.SetBounds(228, 86, 84, 26)
            AddHandler btnOk.Click, Sub(sender As Object, e As EventArgs)
                                         Dim path As String = txt.Text.Trim()
                                         If path.Length = 0 Then
                                             MessageBox.Show(dlg, "Enter or browse to a folder first.", "Choose a folder",
                                                             MessageBoxButtons.OK, MessageBoxIcon.Information)
                                             Return
                                         End If
                                         If Not allowNewFolder AndAlso Not Directory.Exists(path) Then
                                             MessageBox.Show(dlg, "This folder does not exist:" & vbCrLf & path,
                                                             "Choose a folder", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                             Return
                                         End If
                                         dlg.DialogResult = DialogResult.OK
                                         dlg.Close()
                                     End Sub

            Dim btnCancel As New Button()
            btnCancel.Text = "Cancel"
            btnCancel.DialogResult = DialogResult.Cancel
            btnCancel.SetBounds(320, 86, 84, 26)

            dlg.Controls.AddRange(New Control() {lbl, txt, btnBrowse, btnOk, btnCancel})
            dlg.AcceptButton = btnOk
            dlg.CancelButton = btnCancel
            dlg.ActiveControl = txt

            If dlg.ShowDialog(Me) = DialogResult.OK Then Return txt.Text.Trim()
        End Using
        Return ""
    End Function

    ' ------------------------------------------------------------------ the project list (Configuration tab)
    '
    ' lstAllProjects lists every project found directly inside the chosen project root folder (File > Choose project
    ' root, AppSettings.ProjectRoot) - a quick way to switch projects without browsing to their folder every
    ' time. New creates another project there; Load/Delete/Rename act on the selected entry.
    '
    ' Two columns, colProjectName and colLastEdit ("last edited" = the last write time of that project's
    ' configuration.json). Clicking either header sorts by it (lstAllProjects_ColumnClick), toggling direction on a
    ' repeat click of the same header - the usual Explorer/Excel convention. Each row's Tag holds a
    ' ProjectListEntry so the raw name and date stay available without re-parsing the displayed text.

    ''' <summary>One row in lstAllProjects, kept in ListViewItem.Tag.</summary>
    Private NotInheritable Class ProjectListEntry
        Public ReadOnly Name As String
        Public ReadOnly LastModified As DateTime

        Public Sub New(name As String, lastModified As DateTime)
            Me.Name = name
            Me.LastModified = lastModified
        End Sub
    End Class

    ''' <summary>Reads every project under AppSettings.ProjectRoot into a ProjectListEntry (name plus the last
    ''' write time of its configuration.json). Shared by lstAllProjects (Configuration tab) and lstAllProjects3
    ''' (Outputs tab) - see the "the Outputs tab" section further down.</summary>
    Private Function BuildProjectEntries() As List(Of ProjectListEntry)
        Dim entries As New List(Of ProjectListEntry)()
        For Each name As String In ProjectStore.ListProjects(_settings.ProjectRoot)
            Dim folder As String = ProjectStore.FolderPathFor(_settings.ProjectRoot, name)
            Dim configPath As String = Path.Combine(folder, ProjectConfig.FileName)
            Dim lastModified As DateTime = If(File.Exists(configPath), File.GetLastWriteTime(configPath), Directory.GetLastWriteTime(folder))
            entries.Add(New ProjectListEntry(name, lastModified))
        Next
        Return entries
    End Function

    ''' <summary>Sorts the project entries in place, by name (sortColumn 0) or last-edited (sortColumn 1).</summary>
    Private Sub SortProjectEntries(entries As List(Of ProjectListEntry), sortColumn As Integer, ascending As Boolean)
        Dim result As Comparison(Of ProjectListEntry)
        If sortColumn = 0 Then
            result = Function(a As ProjectListEntry, b As ProjectListEntry) String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
        Else
            result = Function(a As ProjectListEntry, b As ProjectListEntry) DateTime.Compare(a.LastModified, b.LastModified)
        End If
        entries.Sort(result)
        If Not ascending Then entries.Reverse()
    End Sub

    ''' <summary>Fills a two-column (name, last edited) project ListView from the given, already-sorted entries.</summary>
    Private Sub PopulateProjectListView(list As ListView, entries As List(Of ProjectListEntry))
        list.BeginUpdate()
        Try
            list.Items.Clear()
            For Each entry As ProjectListEntry In entries
                Dim item As New ListViewItem(entry.Name)
                item.SubItems.Add(entry.LastModified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                item.Tag = entry
                list.Items.Add(item)
            Next
        Finally
            list.EndUpdate()
        End Try
    End Sub

    ''' <summary>The plain project name selected in a project ListView (lstAllProjects or lstAllProjects3), or ""
    ''' when nothing is selected there.</summary>
    Private Function SelectedProjectName(list As ListView) As String
        If list.SelectedItems.Count = 0 Then Return ""
        Dim entry As ProjectListEntry = TryCast(list.SelectedItems(0).Tag, ProjectListEntry)
        Return If(entry IsNot Nothing, entry.Name, "")
    End Function

    ''' <summary>Selects, in a project ListView, the row for the project with the given name (case-insensitive), if any.</summary>
    Private Sub SelectProjectInList(list As ListView, name As String)
        If name.Length = 0 Then Return
        For Each item As ListViewItem In list.Items
            Dim entry As ProjectListEntry = DirectCast(item.Tag, ProjectListEntry)
            If String.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase) Then
                item.Selected = True
                item.EnsureVisible()
                Return
            End If
        Next
    End Sub

    ''' <summary>Repopulates lstAllProjects from AppSettings.ProjectRoot, sorted per _projectSortColumn/Ascending.
    ''' Keeps the current selection, by name, when that project is still in the refreshed list.</summary>
    Private Sub RefreshProjectList()
        Dim selectedName As String = SelectedProjectName(lstAllProjects)

        Dim entries As List(Of ProjectListEntry) = BuildProjectEntries()
        SortProjectEntries(entries, _projectSortColumn, _projectSortAscending)
        PopulateProjectListView(lstAllProjects, entries)

        UpdateProjectListSortHeaders()
        SelectProjectInList(lstAllProjects, selectedName)
        UpdateProjectListButtons()
    End Sub

    ''' <summary>Clicking a column header sorts by it; clicking the same header again reverses the direction.</summary>
    Private Sub lstAllProjects_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles lstAllProjects.ColumnClick
        If e.Column = _projectSortColumn Then
            _projectSortAscending = Not _projectSortAscending
        Else
            _projectSortColumn = e.Column
            _projectSortAscending = True
        End If
        RefreshProjectList()
    End Sub

    ''' <summary>Marks the currently sorted column's header with a small arrow, the way a sortable list usually does.</summary>
    Private Sub UpdateProjectListSortHeaders()
        Dim arrow As String = If(_projectSortAscending, " ^", " v")
        colProjectName.Text = "Project name" & If(_projectSortColumn = 0, arrow, "")
        colLastEdit.Text = "Last edited" & If(_projectSortColumn = 1, arrow, "")
    End Sub

    ''' <summary>New needs a chosen project root; Load/Delete/Rename only make sense with something selected.</summary>
    Private Sub UpdateProjectListButtons()
        Dim hasSelection As Boolean = (lstAllProjects.SelectedItems.Count > 0)
        btnNewProject.Enabled = (_settings IsNot Nothing) AndAlso (_settings.ProjectRoot.Length > 0)
        btnLoadProject.Enabled = hasSelection
        btnDeleteProject.Enabled = hasSelection
        btnRenameProject.Enabled = hasSelection
    End Sub

    ''' <summary>
    ''' Creates a new, empty project (folders and configuration.json, default settings) in the chosen project
    ''' root, under a name the user types in a dialog, and makes it the current project. Never overwrites an
    ''' existing project: a name already used there is refused and the dialog reopens so a different name can be
    ''' chosen. Asks to save unsaved changes in the current project first, like Load. Shared by the button and
    ''' File > New - there is no such thing as a "new project" until it has actually been named and created here,
    ''' so File > New no longer leaves an unnamed, un-saved project open for editing.
    ''' </summary>
    Private Sub btnNewProject2_Click(sender As Object, e As EventArgs) Handles btnNewProject.Click, NewToolStripMenuItem.Click
        If _settings.ProjectRoot.Length = 0 Then
            MessageBox.Show(Me, "Choose a project root folder first (File > Choose project root).", "New project",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Not ConfirmSaveProject() Then Return

        Dim name As String = ""
        Do
            If Not PromptForText("New project", "Name for the new project:", name, name) Then Return
            name = name.Trim()

            Dim problem As String = ProjectStore.ValidateName(name)
            If problem.Length > 0 Then
                MessageBox.Show(Me, problem, "New project", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Continue Do
            End If
            If ProjectStore.IsProjectFolder(ProjectStore.FolderPathFor(_settings.ProjectRoot, name)) Then
                MessageBox.Show(Me, "A project named '" & name & "' already exists in this root folder. Choose a different name.",
                                "New project", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Continue Do
            End If

            Try
                Dim created As Project = ProjectStore.Create(_settings.ProjectRoot, name)
                StartNewProject()

                ' Write the just-computed defaults (StartNewProject read numSimulationSteps/numTimeDecayFactor
                ' etc. from their Designer values) into the new project's configuration.json before
                ' SetCurrentProject reads it back - otherwise, since a freshly created project has no "steps" or
                ' "time_decay_factor" key yet, FromConfig would fall back to its own hardcoded legacy-compatibility
                ' defaults (for an older project saved before these settings existed) instead, silently discarding
                ' the Designer values StartNewProject just set.
                _scene.ToConfig(created.Config)
                _simulation.ToConfig(created.Config)
                ProbeSettings.ListToConfig(_probes, created.Config)
                SourceSettings.ListToConfig(_sources, created.Config)
                created.Save()

                SetCurrentProject(created)
                ToolStripStatusLabel1.Text = "Project '" & name & "' created."
                Return
            Catch ex As Exception
                MessageBox.Show(Me, "The project could not be created:" & vbCrLf & vbCrLf & ex.Message,
                                "New project", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Continue Do
            End Try
        Loop
    End Sub

    Private Sub lstAllProjects_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstAllProjects.SelectedIndexChanged
        UpdateProjectListButtons()
    End Sub

    ''' <summary>Double-clicking a project in the list loads it, the same as pressing Load.</summary>
    Private Sub lstAllProjects_DoubleClick(sender As Object, e As EventArgs) Handles lstAllProjects.DoubleClick
        btnLoadProject_Click(sender, e)
    End Sub

    ''' <summary>The project folder for the entry currently selected in lstAllProjects, or "" when nothing is selected.</summary>
    Private Function SelectedProjectFolder() As String
        Dim name As String = SelectedProjectName(lstAllProjects)
        If name.Length = 0 Then Return ""
        Return ProjectStore.FolderPathFor(_settings.ProjectRoot, name)
    End Function

    ''' <summary>True when the given project folder is the one currently open, and a simulation is running for
    ''' it - Delete and Rename refuse in that case, since the run may still be writing files into that folder.</summary>
    Private Function IsOpenProjectRunning(folder As String) As Boolean
        Return (_project IsNot Nothing) AndAlso (_runner IsNot Nothing) AndAlso
               String.Equals(_project.FolderPath, folder, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>Loads the project selected in lstAllProjects - the same as "Open existing", just picked from the
    ''' list instead of browsed to. Asks to save unsaved changes in the current project first, like every other
    ''' action that replaces it (ConfirmSaveProject).</summary>
    Private Sub btnLoadProject_Click(sender As Object, e As EventArgs) Handles btnLoadProject.Click
        Dim folder As String = SelectedProjectFolder()
        If folder.Length = 0 Then Return

        Try
            If Not ConfirmSaveProject() Then Return
            SetCurrentProject(ProjectStore.Open(folder))
        Catch ex As Exception
            MessageBox.Show(Me, "The project could not be opened:" & vbCrLf & vbCrLf & ex.Message,
                            "Load project", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ''' <summary>Deletes the project selected in lstAllProjects, after confirmation. Refuses while a simulation
    ''' for that same project is still running. Clears the editor to a new, unsaved project when the deleted
    ''' project was the one currently open, since its folder is gone.</summary>
    Private Sub btnDeleteProject_Click(sender As Object, e As EventArgs) Handles btnDeleteProject.Click
        Dim name As String = SelectedProjectName(lstAllProjects)
        Dim folder As String = SelectedProjectFolder()
        If folder.Length = 0 Then Return

        If IsOpenProjectRunning(folder) Then
            MessageBox.Show(Me, "A simulation is running for this project. Stop it first.", "Delete project",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim message As String = "Permanently delete the project '" & name & "' and everything in its folder?" & vbCrLf & vbCrLf & folder

        ' the assets folder is where the user keeps their own third-party files (e.g. a Photoshop source file) -
        ' point this out explicitly, since deleting the project deletes those too, and they have no other copy here
        Dim assetCount As Integer = CountFilesIn(Path.Combine(folder, ProjectStore.AssetsFolderName))
        If assetCount > 0 Then
            message &= vbCrLf & vbCrLf & "This includes " & assetCount.ToString(CultureInfo.InvariantCulture) &
                       " file(s) in its assets folder - copy them elsewhere first if you want to keep them."
        End If

        Dim answer As DialogResult = MessageBox.Show(Me, message, "Delete project", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If answer <> DialogResult.Yes Then Return

        Dim wasOpenProject As Boolean = (_project IsNot Nothing) AndAlso String.Equals(_project.FolderPath, folder, StringComparison.OrdinalIgnoreCase)
        Try
            ProjectStore.Delete(folder)
        Catch ex As Exception
            MessageBox.Show(Me, "The project could not be deleted:" & vbCrLf & vbCrLf & ex.Message,
                            "Delete project", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            RefreshProjectList()
            Return
        End Try

        If wasOpenProject Then
            StartNewProject()
            UpdateProjectAccess()
        End If
        RefreshProjectList()
        ToolStripStatusLabel1.Text = "Project '" & name & "' deleted."
    End Sub

    ''' <summary>The number of files in folderPath (recursively), or 0 when it does not exist or cannot be read.</summary>
    Private Shared Function CountFilesIn(folderPath As String) As Integer
        If Not Directory.Exists(folderPath) Then Return 0
        Try
            Return Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).Length
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Return 0
        End Try
    End Function

    ''' <summary>
    ''' Renames the project selected in lstAllProjects. ProjectStore.Rename checks the new name and refuses a
    ''' collision with an existing project (or any other file/folder with that name); if the renamed project was
    ''' the one currently open, it is reopened under its new name and folder so the editor and the title bar stay
    ''' in sync (a project's name is always just its folder name - see Project.Name).
    ''' </summary>
    Private Sub btnRenameProject_Click(sender As Object, e As EventArgs) Handles btnRenameProject.Click
        Dim name As String = SelectedProjectName(lstAllProjects)
        Dim folder As String = SelectedProjectFolder()
        If folder.Length = 0 Then Return

        If IsOpenProjectRunning(folder) Then
            MessageBox.Show(Me, "A simulation is running for this project. Stop it first.", "Rename project",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim newName As String = name
        If Not PromptForText("Rename project", "New name for '" & name & "':", name, newName) Then Return
        newName = newName.Trim()
        If newName = name Then Return

        Dim wasOpenProject As Boolean = (_project IsNot Nothing) AndAlso String.Equals(_project.FolderPath, folder, StringComparison.OrdinalIgnoreCase)
        Try
            Dim newFolder As String = ProjectStore.Rename(folder, newName)
            If wasOpenProject Then SetCurrentProject(ProjectStore.Open(newFolder))

            RefreshProjectList()
            SelectProjectInList(lstAllProjects, newName)
            ToolStripStatusLabel1.Text = "Project '" & name & "' renamed to '" & newName & "'."
        Catch ex As Exception
            MessageBox.Show(Me, "The project could not be renamed:" & vbCrLf & vbCrLf & ex.Message,
                            "Rename project", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ' ------------------------------------------------------------------ the Outputs tab (project browser)
    '
    ' lstAllProjects3 lists every project the same way lstAllProjects does (see BuildProjectEntries and friends,
    ' above); clicking a project fills lstScenes/lstImages/lstVideo from that project's scene_images/output_frames
    ' /output_video folders. Clicking a scene or image shows it in pnlPreview (a PictureBox built in code, see
    ' Form1_Load, since pnlPreview is a plain empty Panel). Each of the three file lists has its own name/"Created
    ' on" column-click sort, the same idea as lstAllProjects/lstAllProjects3 (see FillFileList and friends below).
    '
    ' lstVideo shows just the file name and its creation date as text - no per-row thumbnail. Selecting exactly
    ' one video instead shows a thumbnail-sized preview of it in pnlPreview, using the same operating-system shell
    ' thumbnail cache Explorer itself uses (GetShellThumbnail, further down) rather than an actual video component
    ' (there is no dependency-free way to decode/play a frame with just System.Drawing/System.Windows.Forms).
    ' Selecting more than one video (lstVideo allows multi-select, for the delete button) clears the preview
    ' instead, since a thumbnail can only stand for one file at a time.

    Private Shared ReadOnly SceneImageExtensions As String() = {".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif"}
    Private Shared ReadOnly OutputImageExtensions As String() = {".png"}
    Private Shared ReadOnly OutputVideoExtensions As String() = {".mp4", ".avi"}

    ''' <summary>Sets up the PictureBox for pnlPreview and does the tab's first population. Called once, from
    ''' Form1_Load.</summary>
    Private Sub InitializeOutputsTab()
        _outputPreviewPic = New PictureBox()
        _outputPreviewPic.Dock = DockStyle.Fill
        _outputPreviewPic.SizeMode = PictureBoxSizeMode.Zoom
        pnlPreview.Controls.Add(_outputPreviewPic)

        RefreshOutputProjectList()
        UpdateShareWithEditorsButton()
    End Sub

    ''' <summary>Repopulates lstAllProjects3, sorted per _outputProjectSortColumn/Ascending, and selects a project
    ''' afterwards: preferredName when one is given (used to default to the currently open project when the tab is
    ''' focused - see tabMainFunctions_SelectedIndexChanged), otherwise whatever was already selected there, when
    ''' that project is still in the refreshed list.</summary>
    Private Sub RefreshOutputProjectList(Optional preferredName As String = "")
        Dim selectedName As String = If(preferredName.Length > 0, preferredName, SelectedProjectName(lstAllProjects3))

        Dim entries As List(Of ProjectListEntry) = BuildProjectEntries()
        SortProjectEntries(entries, _outputProjectSortColumn, _outputProjectSortAscending)
        PopulateProjectListView(lstAllProjects3, entries)

        UpdateOutputProjectSortHeaders()
        SelectProjectInList(lstAllProjects3, selectedName)
    End Sub

    ''' <summary>Clicking a column header sorts by it; clicking the same header again reverses the direction.</summary>
    Private Sub lstAllProjects3_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles lstAllProjects3.ColumnClick
        If e.Column = _outputProjectSortColumn Then
            _outputProjectSortAscending = Not _outputProjectSortAscending
        Else
            _outputProjectSortColumn = e.Column
            _outputProjectSortAscending = True
        End If
        RefreshOutputProjectList()
    End Sub

    Private Sub UpdateOutputProjectSortHeaders()
        Dim arrow As String = If(_outputProjectSortAscending, " ^", " v")
        colProjectName2.Text = "Project name" & If(_outputProjectSortColumn = 0, arrow, "")
        colLastEdited2.Text = "Last edited" & If(_outputProjectSortColumn = 1, arrow, "")
    End Sub

    ''' <summary>The folder of the project currently selected in lstAllProjects3, or "" when nothing is selected.</summary>
    Private Function SelectedOutputProjectFolder() As String
        Dim name As String = SelectedProjectName(lstAllProjects3)
        If name.Length = 0 Then Return ""
        Return ProjectStore.FolderPathFor(_settings.ProjectRoot, name)
    End Function

    ''' <summary>Clicking a project fills the three file lists from its output folders and clears the preview
    ''' (the previously selected scene/image no longer applies to the newly selected project).</summary>
    Private Sub lstAllProjects3_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstAllProjects3.SelectedIndexChanged
        RefreshOutputFileLists()
    End Sub

    ''' <summary>Refills lstScenes/lstImages/lstVideo from whichever project is currently selected in
    ''' lstAllProjects3, and clears the preview and the Copy-to-clipboard button. Used both after picking a
    ''' different project and after anything that changes the files themselves (delete, load external) - in
    ''' both cases the previous selection no longer applies, so there is nothing to preserve here (contrast
    ''' RefillSceneList/RefillImageList/RefillVideoList, used by the column-click sort handlers, which do keep
    ''' the selection).</summary>
    Private Sub RefreshOutputFileLists()
        Dim folder As String = SelectedOutputProjectFolder()

        FillFileList(lstScenes, folder, ProjectStore.SceneImageFolderName, SceneImageExtensions, _sceneSortColumn, _sceneSortAscending)
        FillFileList(lstImages, folder, ProjectStore.OutputFramesFolderName, OutputImageExtensions, _imageSortColumn, _imageSortAscending)
        FillFileList(lstVideo, folder, ProjectStore.OutputVideoFolderName, OutputVideoExtensions, _videoSortColumn, _videoSortAscending)
        UpdateFileListSortHeaders(colSceneName, "Scene file name", colSceneCreated, "Created on", _sceneSortColumn, _sceneSortAscending)
        UpdateFileListSortHeaders(colImageName, "Image file name", colCreatedOn, "Created on", _imageSortColumn, _imageSortAscending)
        UpdateFileListSortHeaders(colVideoName, "Video file name", colCreatedOn2, "Created on", _videoSortColumn, _videoSortAscending)
        ClearOutputPreview()
        UpdateCopyToClipboardButton()
    End Sub

    ''' <summary>Fills a ListView with the matching files of one project subfolder: just the file name when the
    ''' list has one column, plus a "Created on" sub-item when it has two (lstScenes, lstImages, lstVideo all do).
    ''' sortColumn/sortAscending pick the order: 0 sorts by file name, 1 (only meaningful when there is a
    ''' "Created on" column) sorts by creation date. Each item's Tag is the file's full path, for ShowOutputPreview
    ''' /lstVideo_SelectedIndexChanged (and the delete/open/view buttons) to read back.</summary>
    Private Sub FillFileList(list As ListView, projectFolder As String, subfolderName As String, extensions As String(),
                             sortColumn As Integer, sortAscending As Boolean)
        list.BeginUpdate()
        Try
            list.Items.Clear()
            If projectFolder.Length = 0 Then Return

            Dim folder As String = Path.Combine(projectFolder, subfolderName)
            If Not Directory.Exists(folder) Then Return

            Dim files As New List(Of String)(Directory.GetFiles(folder))
            files.RemoveAll(Function(f As String) Array.IndexOf(extensions, Path.GetExtension(f).ToLowerInvariant()) < 0)

            Dim byCreatedDate As Boolean = (list.Columns.Count > 1) AndAlso (sortColumn = 1)
            files.Sort(Function(a As String, b As String) CompareFileEntries(a, b, byCreatedDate, sortAscending))

            For Each f As String In files
                Dim item As New ListViewItem(Path.GetFileName(f))
                If list.Columns.Count > 1 Then
                    item.SubItems.Add(File.GetCreationTime(f).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                End If
                item.Tag = f
                list.Items.Add(item)
            Next
        Finally
            list.EndUpdate()
        End Try
    End Sub

    ''' <summary>Compares two files by name (the usual case) or by creation date, ascending or descending - the
    ''' sort FillFileList applies.</summary>
    Private Shared Function CompareFileEntries(a As String, b As String, byCreatedDate As Boolean, ascending As Boolean) As Integer
        Dim result As Integer
        If byCreatedDate Then
            result = File.GetCreationTime(a).CompareTo(File.GetCreationTime(b))
        Else
            result = String.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase)
        End If
        If Not ascending Then result = -result
        Return result
    End Function

    ''' <summary>Sets a name column's and a "Created on" column's header text, with a sort-direction arrow appended
    ''' to whichever one is currently sorted (sortColumn 0 = name, 1 = created on). Always sets both from their
    ''' base label, so the arrow never compounds across repeated calls.</summary>
    Private Sub UpdateFileListSortHeaders(nameColumn As ColumnHeader, nameLabel As String, dateColumn As ColumnHeader, dateLabel As String,
                                          sortColumn As Integer, ascending As Boolean)
        Dim arrow As String = If(ascending, " ^", " v")
        nameColumn.Text = nameLabel & If(sortColumn = 0, arrow, "")
        dateColumn.Text = dateLabel & If(sortColumn = 1, arrow, "")
    End Sub

    ''' <summary>The full paths (ListViewItem.Tag) of a file list's currently selected item(s), so a sort refill
    ''' can restore the selection afterwards (see ReselectFiles).</summary>
    Private Function SelectedFilePaths(list As ListView) As List(Of String)
        Dim result As New List(Of String)()
        For Each item As ListViewItem In list.SelectedItems
            Dim p As String = TryCast(item.Tag, String)
            If Not String.IsNullOrEmpty(p) Then result.Add(p)
        Next
        Return result
    End Function

    ''' <summary>Re-selects whichever of a file list's items still have one of the given paths as their Tag - used
    ''' after a sort refill to keep the same file(s) selected even though the rows have moved.</summary>
    Private Sub ReselectFiles(list As ListView, paths As List(Of String))
        If paths.Count = 0 Then Return
        For Each item As ListViewItem In list.Items
            Dim p As String = TryCast(item.Tag, String)
            If p IsNot Nothing AndAlso paths.Contains(p, StringComparer.OrdinalIgnoreCase) Then item.Selected = True
        Next
    End Sub

    ''' <summary>Re-sorts lstScenes per _sceneSortColumn/Ascending, keeping the current selection.</summary>
    Private Sub RefillSceneList()
        Dim selected As List(Of String) = SelectedFilePaths(lstScenes)
        FillFileList(lstScenes, SelectedOutputProjectFolder(), ProjectStore.SceneImageFolderName, SceneImageExtensions,
                    _sceneSortColumn, _sceneSortAscending)
        UpdateFileListSortHeaders(colSceneName, "Scene file name", colSceneCreated, "Created on", _sceneSortColumn, _sceneSortAscending)
        ReselectFiles(lstScenes, selected)
    End Sub

    ''' <summary>Re-sorts lstImages per _imageSortColumn/Ascending, keeping the current selection.</summary>
    Private Sub RefillImageList()
        Dim selected As List(Of String) = SelectedFilePaths(lstImages)
        FillFileList(lstImages, SelectedOutputProjectFolder(), ProjectStore.OutputFramesFolderName, OutputImageExtensions,
                    _imageSortColumn, _imageSortAscending)
        UpdateFileListSortHeaders(colImageName, "Image file name", colCreatedOn, "Created on", _imageSortColumn, _imageSortAscending)
        ReselectFiles(lstImages, selected)
    End Sub

    ''' <summary>Re-sorts lstVideo per _videoSortColumn/Ascending, keeping the current selection.</summary>
    Private Sub RefillVideoList()
        Dim selected As List(Of String) = SelectedFilePaths(lstVideo)
        FillFileList(lstVideo, SelectedOutputProjectFolder(), ProjectStore.OutputVideoFolderName, OutputVideoExtensions,
                    _videoSortColumn, _videoSortAscending)
        UpdateFileListSortHeaders(colVideoName, "Video file name", colCreatedOn2, "Created on", _videoSortColumn, _videoSortAscending)
        ReselectFiles(lstVideo, selected)
    End Sub

    ''' <summary>Clicking a column header sorts that list by it; clicking the same header again reverses the
    ''' direction - the same idea as lstAllProjects/lstAllProjects3's ColumnClick handlers.</summary>
    Private Sub lstScenes_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles lstScenes.ColumnClick
        ToggleFileSort(_sceneSortColumn, _sceneSortAscending, e.Column)
        RefillSceneList()
    End Sub

    Private Sub lstImages_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles lstImages.ColumnClick
        ToggleFileSort(_imageSortColumn, _imageSortAscending, e.Column)
        RefillImageList()
    End Sub

    Private Sub lstVideo_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles lstVideo.ColumnClick
        ToggleFileSort(_videoSortColumn, _videoSortAscending, e.Column)
        RefillVideoList()
    End Sub

    ''' <summary>Shared toggle logic for the three ColumnClick handlers above: same column clicked again reverses
    ''' direction, a different column switches to it (ascending).</summary>
    Private Shared Sub ToggleFileSort(ByRef sortColumn As Integer, ByRef sortAscending As Boolean, clickedColumn As Integer)
        If clickedColumn = sortColumn Then
            sortAscending = Not sortAscending
        Else
            sortColumn = clickedColumn
            sortAscending = True
        End If
    End Sub

    ''' <summary>True while ClearOtherFileListSelections is clearing the other two file lists' selections, so
    ''' those lists' own SelectedIndexChanged handlers (fired by that clearing) do nothing instead of clearing
    ''' each other right back or re-touching the preview - see ClearOtherFileListSelections.</summary>
    Private _clearingFileListSelection As Boolean

    ''' <summary>Deselects lstScenes/lstImages/lstVideo except for "active" (the one the user just clicked in), so
    ''' only one of the three ever shows a selection at a time - otherwise it is unclear which list's item
    ''' pnlPreview is actually showing. The Projects list (lstAllProjects3) is not one of these three and keeps
    ''' its own selection regardless.</summary>
    Private Sub ClearOtherFileListSelections(active As ListView)
        _clearingFileListSelection = True
        Try
            If Not active Is lstScenes Then lstScenes.SelectedItems.Clear()
            If Not active Is lstImages Then lstImages.SelectedItems.Clear()
            If Not active Is lstVideo Then lstVideo.SelectedItems.Clear()
        Finally
            _clearingFileListSelection = False
        End Try
    End Sub

    Private Sub lstScenes_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstScenes.SelectedIndexChanged
        If _clearingFileListSelection Then Return
        ClearOtherFileListSelections(lstScenes)
        ShowOutputPreview(lstScenes)
    End Sub

    Private Sub lstImages_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstImages.SelectedIndexChanged
        If _clearingFileListSelection Then Return
        ClearOtherFileListSelections(lstImages)
        ShowOutputPreview(lstImages)
        UpdateCopyToClipboardButton()
    End Sub

    ''' <summary>Loads the selected item's file (its Tag, from FillFileList) into pnlPreview.</summary>
    Private Sub ShowOutputPreview(list As ListView)
        If list.SelectedItems.Count = 0 Then Return
        Dim imagePath As String = TryCast(list.SelectedItems(0).Tag, String)
        If String.IsNullOrEmpty(imagePath) OrElse Not File.Exists(imagePath) Then
            ClearOutputPreview()
            Return
        End If

        Try
            SetOutputPreviewImage(LoadImageCopy(imagePath))
        Catch ex As Exception
            ClearOutputPreview()
        End Try
    End Sub

    ''' <summary>Shows a thumbnail-sized preview of the single selected video in pnlPreview, via the operating
    ''' system's own shell thumbnail cache (GetShellThumbnail, further down) - there is no dependency-free way to
    ''' decode/draw an actual video frame. Selecting zero or more than one video (lstVideo allows multi-select)
    ''' clears the preview instead, since a single thumbnail cannot stand in for several files.</summary>
    Private Sub lstVideo_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstVideo.SelectedIndexChanged
        If _clearingFileListSelection Then Return
        ClearOtherFileListSelections(lstVideo)

        If lstVideo.SelectedItems.Count <> 1 Then
            ClearOutputPreview()
            Return
        End If

        Dim videoPath As String = TryCast(lstVideo.SelectedItems(0).Tag, String)
        If String.IsNullOrEmpty(videoPath) OrElse Not File.Exists(videoPath) Then
            ClearOutputPreview()
            Return
        End If

        Try
            Dim width As Integer = Math.Max(64, _outputPreviewPic.ClientSize.Width)
            Dim height As Integer = Math.Max(64, _outputPreviewPic.ClientSize.Height)
            SetOutputPreviewImage(GetShellThumbnail(videoPath, width, height), isVideo:=True)
        Catch ex As Exception
            ClearOutputPreview()
        End Try
    End Sub

    ''' <summary>Shows an image in pnlPreview, disposing whatever was shown there before. isVideo marks whether
    ''' this is a video's thumbnail rather than an actual scene/output image - see _outputPreviewIsVideo.</summary>
    Private Sub SetOutputPreviewImage(image As Image, Optional isVideo As Boolean = False)
        Dim old As Image = _outputPreviewPic.Image
        _outputPreviewPic.Image = image
        If old IsNot Nothing Then old.Dispose()

        _outputPreviewIsVideo = isVideo
        UpdateShareWithEditorsButton()
    End Sub

    Private Sub ClearOutputPreview()
        If _outputPreviewPic IsNot Nothing Then SetOutputPreviewImage(Nothing)
    End Sub

    ' ---- the tab's buttons

    ''' <summary>
    ''' Deletes the scene image(s) selected in lstScenes, after confirmation. When the project browsed here is the
    ''' one currently open in the editor, and one of the selected files is its scene image in use
    ''' (radUseSceneImage/_scene.ImageFile), an extra line warns about that before asking to confirm; if the user
    ''' still goes ahead, radUserDefinedScene is checked afterwards for that project - exactly what clicking it
    ''' by hand does (see SceneMode_CheckedChanged) - so the editor is not left pointing at a deleted file.
    ''' </summary>
    Private Sub btnDeleteScene_Click(sender As Object, e As EventArgs) Handles btnDeleteScene.Click
        If lstScenes.SelectedItems.Count = 0 Then
            MessageBox.Show(Me, "Select a scene image in the list first.", "Delete scene",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim paths As New List(Of String)()
        For Each item As ListViewItem In lstScenes.SelectedItems
            paths.Add(DirectCast(item.Tag, String))
        Next

        Dim isOpenProject As Boolean = (_project IsNot Nothing) AndAlso
            String.Equals(_project.FolderPath, SelectedOutputProjectFolder(), StringComparison.OrdinalIgnoreCase)
        Dim currentImagePath As String = If(isOpenProject AndAlso _scene.UseImage,
            SceneSettings.ToFullPath(_project.FolderPath, _scene.ImageFile), "")
        Dim usesCurrentImage As Boolean = currentImagePath.Length > 0 AndAlso
            paths.Exists(Function(p As String) String.Equals(p, currentImagePath, StringComparison.OrdinalIgnoreCase))

        Dim message As String = If(paths.Count = 1,
            "Permanently delete '" & Path.GetFileName(paths(0)) & "'?",
            "Permanently delete these " & paths.Count.ToString(CultureInfo.InvariantCulture) & " scene images?")
        If usesCurrentImage Then
            message &= vbCrLf & vbCrLf & "This is the scene image currently used by the open project. Deleting it " &
                        "will switch the scene to ""User defined"" instead."
        End If

        Dim answer As DialogResult = MessageBox.Show(Me, message, "Delete scene", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If answer <> DialogResult.Yes Then Return

        Dim errors As New List(Of String)()
        For Each p As String In paths
            Try
                File.Delete(p)
            Catch ex As Exception
                errors.Add(Path.GetFileName(p) & ": " & ex.Message)
            End Try
        Next

        If usesCurrentImage Then radUserDefinedScene.Checked = True

        RefreshOutputFileLists()
        ShowDeleteErrorsIfAny(errors, "Delete scene")
    End Sub

    ''' <summary>Same as "Load scene image" on the Configuration tab (btnLoadSceneImage): browses for an image and
    ''' sets it as the currently open project's scene image. Refreshes lstScenes afterwards when the project
    ''' browsed here happens to be that same open project, so a newly copied-in file shows up right away.</summary>
    Private Sub btnLoadExternal_Click(sender As Object, e As EventArgs) Handles btnLoadExternal.Click
        btnLOadSceneImage_Click(sender, e)

        If _project IsNot Nothing AndAlso
           String.Equals(_project.FolderPath, SelectedOutputProjectFolder(), StringComparison.OrdinalIgnoreCase) Then
            RefreshOutputFileLists()
        End If
    End Sub

    ''' <summary>Deletes the image(s) selected in lstImages, after confirmation.</summary>
    Private Sub btnDeleteImages_Click(sender As Object, e As EventArgs) Handles btnDeleteImages.Click
        DeleteSelectedImages()
    End Sub

    ''' <summary>Same as btnDeleteImages, so the Del key does the same thing as the button.</summary>
    Private Sub lstImages_KeyDown(sender As Object, e As KeyEventArgs) Handles lstImages.KeyDown
        If e.KeyCode = Keys.Delete Then
            e.Handled = True
            DeleteSelectedImages()
        End If
    End Sub

    Private Sub DeleteSelectedImages()
        If lstImages.SelectedItems.Count = 0 Then
            MessageBox.Show(Me, "Select one or more images in the list first.", "Delete images",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim count As Integer = lstImages.SelectedItems.Count
        Dim message As String = If(count = 1,
            "Permanently delete '" & lstImages.SelectedItems(0).Text & "'?",
            "Permanently delete these " & count.ToString(CultureInfo.InvariantCulture) & " images?")
        Dim answer As DialogResult = MessageBox.Show(Me, message, "Delete images", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If answer <> DialogResult.Yes Then Return

        Dim errors As New List(Of String)()
        For Each item As ListViewItem In lstImages.SelectedItems
            Dim path As String = DirectCast(item.Tag, String)
            Try
                File.Delete(path)
            Catch ex As Exception
                errors.Add(item.Text & ": " & ex.Message)
            End Try
        Next

        RefreshOutputFileLists()
        ShowDeleteErrorsIfAny(errors, "Delete images")
    End Sub

    ''' <summary>Copies the single selected image in lstImages to the clipboard (for pasting into Paint,
    ''' Photoshop, and so on). Only enabled while exactly one image is selected - see UpdateCopyToClipboardButton.</summary>
    Private Sub btnCopyToClipboard_Click(sender As Object, e As EventArgs) Handles btnCopyToClipboard.Click
        If lstImages.SelectedItems.Count <> 1 Then Return
        Dim imagePath As String = TryCast(lstImages.SelectedItems(0).Tag, String)
        If String.IsNullOrEmpty(imagePath) OrElse Not File.Exists(imagePath) Then Return

        Try
            Using bmp As Bitmap = LoadImageCopy(imagePath)
                Clipboard.SetImage(bmp)
            End Using
            ToolStripStatusLabel1.Text = "Copied '" & Path.GetFileName(imagePath) & "' to the clipboard."
        Catch ex As Exception
            MessageBox.Show(Me, "The image could not be copied to the clipboard:" & vbCrLf & vbCrLf & ex.Message,
                            "Copy to clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ''' <summary>btnCopyToClipboard only makes sense for exactly one selected image.</summary>
    Private Sub UpdateCopyToClipboardButton()
        btnCopyToClipboard.Enabled = (lstImages.SelectedItems.Count = 1)
    End Sub

    ''' <summary>Opens the selected project's output_frames folder in Explorer, in "Large icons" view (see
    ''' OpenFolderInExplorer).</summary>
    Private Sub btnOpenImageFolder2_Click(sender As Object, e As EventArgs) Handles btnOpenImageFolder2.Click
        Dim projectFolder As String = SelectedOutputProjectFolder()
        If projectFolder.Length = 0 Then
            MessageBox.Show(Me, "Select a project in the list first.", "Open image folder",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            OpenFolderInExplorer(Path.Combine(projectFolder, ProjectStore.OutputFramesFolderName), FVM_ICON)
        Catch ex As Exception
            MessageBox.Show(Me, "The image folder could not be opened:" & vbCrLf & vbCrLf & ex.Message, "Open image folder",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ''' <summary>Deletes the video(s) selected in lstVideo, after confirmation.</summary>
    Private Sub btnDeleteVideos_Click(sender As Object, e As EventArgs) Handles btnDeleteVideos.Click
        DeleteSelectedVideos()
    End Sub

    ''' <summary>Same as btnDeleteVideos, so the Del key does the same thing as the button.</summary>
    Private Sub lstVideo_KeyDown(sender As Object, e As KeyEventArgs) Handles lstVideo.KeyDown
        If e.KeyCode = Keys.Delete Then
            e.Handled = True
            DeleteSelectedVideos()
        End If
    End Sub

    Private Sub DeleteSelectedVideos()
        If lstVideo.SelectedItems.Count = 0 Then
            MessageBox.Show(Me, "Select one or more videos in the list first.", "Delete videos",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim count As Integer = lstVideo.SelectedItems.Count
        Dim message As String = If(count = 1,
            "Permanently delete '" & lstVideo.SelectedItems(0).Text & "'?",
            "Permanently delete these " & count.ToString(CultureInfo.InvariantCulture) & " videos?")
        Dim answer As DialogResult = MessageBox.Show(Me, message, "Delete videos", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If answer <> DialogResult.Yes Then Return

        Dim errors As New List(Of String)()
        For Each item As ListViewItem In lstVideo.SelectedItems
            Dim path As String = DirectCast(item.Tag, String)
            Try
                File.Delete(path)
            Catch ex As Exception
                errors.Add(item.Text & ": " & ex.Message)
            End Try
        Next

        RefreshOutputFileLists()
        ShowDeleteErrorsIfAny(errors, "Delete videos")
    End Sub

    ''' <summary>Opens the video selected in lstVideo with whatever program Windows uses for it - same as
    ''' btnViewFieldVideo/btnViewIntensityVideo on the Preview tab (OpenVideo).</summary>
    Private Sub btnViewVIdeoInPlayer2_Click(sender As Object, e As EventArgs) Handles btnViewVIdeoInPlayer2.Click
        If lstVideo.SelectedItems.Count = 0 Then
            MessageBox.Show(Me, "Select a video in the list first.", "View in player",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        OpenVideo(DirectCast(lstVideo.SelectedItems(0).Tag, String))
    End Sub

    ''' <summary>Double-clicking a video in lstVideo opens it in the player, same as btnViewVIdeoInPlayer2.</summary>
    Private Sub lstVideo_DoubleClick(sender As Object, e As EventArgs) Handles lstVideo.DoubleClick
        If lstVideo.SelectedItems.Count = 0 Then Return
        OpenVideo(DirectCast(lstVideo.SelectedItems(0).Tag, String))
    End Sub

    ''' <summary>Renames the video file selected in lstVideo (exactly one), using a popup entry box - same
    ''' PromptForText dialog as btnRenameProject.</summary>
    Private Sub btnRenameVideo_Click(sender As Object, e As EventArgs) Handles btnRenameVideo.Click
        If lstVideo.SelectedItems.Count <> 1 Then
            MessageBox.Show(Me, "Select a single video in the list first.", "Rename video",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim oldPath As String = DirectCast(lstVideo.SelectedItems(0).Tag, String)
        Dim oldName As String = Path.GetFileName(oldPath)

        Dim newName As String = oldName
        If Not PromptForText("Rename video", "New name for '" & oldName & "':", oldName, newName) Then Return
        newName = newName.Trim()
        If newName.Length = 0 OrElse newName = oldName Then Return
        If newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 Then
            MessageBox.Show(Me, "That name contains characters that are not allowed in a file name.", "Rename video",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim newPath As String = Path.Combine(Path.GetDirectoryName(oldPath), newName)
        If File.Exists(newPath) Then
            MessageBox.Show(Me, "A file named '" & newName & "' already exists.", "Rename video",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            File.Move(oldPath, newPath)
            RefreshOutputFileLists()
        Catch ex As Exception
            MessageBox.Show(Me, "The video could not be renamed:" & vbCrLf & vbCrLf & ex.Message, "Rename video",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ''' <summary>Opens the selected project's output_video folder in Explorer, in "Details" view - same as
    ''' btnOpenVideoFolder on the Preview tab (OpenFolderInExplorer).</summary>
    Private Sub btnOpenVIdeoFolder2_Click(sender As Object, e As EventArgs) Handles btnOpenVIdeoFolder2.Click
        Dim projectFolder As String = SelectedOutputProjectFolder()
        If projectFolder.Length = 0 Then
            MessageBox.Show(Me, "Select a project in the list first.", "Open video folder",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            OpenFolderInExplorer(Path.Combine(projectFolder, ProjectStore.OutputVideoFolderName), FVM_DETAILS)
        Catch ex As Exception
            MessageBox.Show(Me, "The video folder could not be opened:" & vbCrLf & vbCrLf & ex.Message, "Open video folder",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ''' <summary>Puts a copy of whatever pnlPreview currently shows (a scene or an output image) into pnlSourceImage
    ''' and pnlProbeImage - the same thing btnShareFieldImage/btnShareIntensityImage do for the live preview on the
    ''' Preview tab (CopyPreviewToTabs, further down). Only enabled for a scene/image preview - see
    ''' UpdateShareWithEditorsButton.</summary>
    Private Sub btnShareWithEditors_Click(sender As Object, e As EventArgs) Handles btnShareWithEditors.Click
        CopyPreviewToTabs(_outputPreviewPic)
    End Sub

    ''' <summary>btnShareWithEditors only makes sense for a scene/output image preview: a video's thumbnail has a
    ''' different size than the simulation, so it is disabled while pnlPreview shows one (or nothing at all).</summary>
    Private Sub UpdateShareWithEditorsButton()
        btnShareWithEditors.Enabled = (Not _outputPreviewIsVideo) AndAlso (_outputPreviewPic IsNot Nothing) AndAlso
            (_outputPreviewPic.Image IsNot Nothing)
    End Sub

    ''' <summary>Shared tail end of the three delete buttons: reports any files that could not be deleted (e.g.
    ''' because they were open elsewhere), after the list has already been refreshed with whatever did succeed.</summary>
    Private Sub ShowDeleteErrorsIfAny(errors As List(Of String), title As String)
        If errors.Count = 0 Then Return
        MessageBox.Show(Me, "Some files could not be deleted:" & vbCrLf & vbCrLf & String.Join(vbCrLf, errors),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    ' ---- the video preview thumbnail, via the Windows shell (no video codec of our own)

    ' Windows shell thumbnail interop (IShellItemImageFactory) - plain P/Invoke/COM interop against shell32.dll
    ' and gdi32.dll, no NuGet package or project reference needed, but genuine COM interop nonetheless, which
    ' could not be exercised on an actual Windows machine while writing it. GetShellThumbnail is written so a
    ' wrong detail here surfaces as "no thumbnail for this file" (via the Try/Catch in lstVideo_SelectedIndexChanged)
    ' rather than a crash - if the preview comes out consistently missing for videos, this is the place to revisit.

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeSize
        Public cx As Integer
        Public cy As Integer
    End Structure

    <Flags>
    Private Enum ThumbnailOptions As Integer
        None = &H0
        BiggerSizeOk = &H1
        ThumbnailOnly = &H8
    End Enum

    <ComImport(), Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface IShellItemImageFactory
        Sub GetImage(<[In]> size As NativeSize, <[In]> flags As ThumbnailOptions, <Out> ByRef phbm As IntPtr)
    End Interface

    <DllImport("shell32.dll", CharSet:=CharSet.Unicode, PreserveSig:=False)>
    Private Shared Sub SHCreateItemFromParsingName(
        <MarshalAs(UnmanagedType.LPWStr)> path As String,
        pbc As IntPtr,
        <[In]> ByRef riid As Guid,
        <MarshalAs(UnmanagedType.Interface)> <Out> ByRef ppv As Object)
    End Sub

    <DllImport("gdi32.dll")>
    Private Shared Function DeleteObject(hObject As IntPtr) As Boolean
    End Function

    ''' <summary>Asks Windows for the same thumbnail Explorer would show for this file, at roughly the given size.
    ''' Returns Nothing, never throws, when the shell has no thumbnail for it.</summary>
    Private Shared Function GetShellThumbnail(filePath As String, width As Integer, height As Integer) As Bitmap
        Dim riid As Guid = GetType(IShellItemImageFactory).GUID
        Dim obj As Object = Nothing
        SHCreateItemFromParsingName(filePath, IntPtr.Zero, riid, obj)
        Dim factory As IShellItemImageFactory = TryCast(obj, IShellItemImageFactory)
        If factory Is Nothing Then Return Nothing

        Dim hbmp As IntPtr = IntPtr.Zero
        Try
            Dim size As New NativeSize With {.cx = width, .cy = height}
            factory.GetImage(size, ThumbnailOptions.ThumbnailOnly Or ThumbnailOptions.BiggerSizeOk, hbmp)
            If hbmp = IntPtr.Zero Then Return Nothing
            Using raw As Bitmap = CType(Image.FromHbitmap(hbmp), Bitmap)
                Return New Bitmap(raw)
            End Using
        Finally
            If hbmp <> IntPtr.Zero Then DeleteObject(hbmp)
            Marshal.ReleaseComObject(factory)
        End Try
    End Function

    ' ------------------------------------------------------------------ scene properties (Configuration tab)
    '
    ' The settings are kept in _scene and stored in the "scene" section of the project's configuration file
    ' (see SceneSettings) when the project is saved.

    Private _scene As New SceneSettings()

    ' True while the code itself fills the controls (their change events are then ignored)
    Private _loadingScene As Boolean

    ' the scene image (Nothing when none is loaded) and the generated picture of an empty user defined canvas
    Private _sceneImage As Bitmap
    Private _emptyPreview As Bitmap

    Private ReadOnly _sceneTip As New ToolTip()

    Private Sub InitializeSceneControls()
        picImagePreview.BorderStyle = BorderStyle.FixedSingle
        picImagePreview.BackColor = SystemColors.ControlLight
        picImagePreview.SizeMode = PictureBoxSizeMode.Zoom

        lblImageNameAndSize.AutoSize = False
        lblImageNameAndSize.AutoEllipsis = True
        lblImageNameAndSize.Size = New Size(470, 20)

        txtXSize.MaxLength = 5
        txtYsize.MaxLength = 5
        txtDampingEdgeSize.MaxLength = 4

        ' amplitude factor of the sources in the scene image
        numScaleImageSource.Minimum = 0D
        numScaleImageSource.Maximum = CDec(SceneSettings.MaxImageSourceScale)

        ' No ShowSceneInControls() here: grpSceneProperties is disabled until a real project exists
        ' (UpdateProjectAccess). Populating it now from a blank _scene would overwrite whatever defaults the
        ' Designer gave these controls before StartNewProject/SetCurrentProject ever get a chance to read them
        ' back out (see the matching comment in InitializeSimulationControls, where this actually matters -
        ' numSimulationSteps' Designer default of 3000).
    End Sub

    ''' <summary>Reads the scene settings of the current project (and its image) and shows them.</summary>
    Private Sub LoadSceneFromProject()
        If _project Is Nothing Then Return

        _scene = SceneSettings.FromConfig(_project.Config)

        ReplaceSceneImage(Nothing)
        If _scene.ImageFile.Length > 0 Then
            Dim fullPath As String = SceneSettings.ToFullPath(_project.FolderPath, _scene.ImageFile)
            If File.Exists(fullPath) Then
                Try
                    ReplaceSceneImage(LoadImageCopy(fullPath))
                Catch ex As Exception
                    ' the image label tells that the image could not be loaded
                End Try
            End If
        End If

        ShowSceneInControls()
    End Sub

    ''' <summary>Copies the values of _scene into the controls.</summary>
    Private Sub ShowSceneInControls()
        _loadingScene = True
        Try
            radUseSceneImage.Checked = _scene.UseImage
            radUserDefinedScene.Checked = Not _scene.UseImage
            txtXSize.Text = _scene.UserWidth.ToString(CultureInfo.InvariantCulture)
            txtYsize.Text = _scene.UserHeight.ToString(CultureInfo.InvariantCulture)
            chkDampingEdge.Checked = _scene.DampingEdgeEnabled
            txtDampingEdgeSize.Text = _scene.DampingEdgeSize.ToString(CultureInfo.InvariantCulture)
            chkUseImageSources.Checked = _scene.UseSources
            numScaleImageSource.Value = ClampToRange(numScaleImageSource, _scene.ImageSourceScale)
            chkUseObjectSources.Checked = _scene.UseObjectSources
            numRefractiveIndex.Value = ClampToRange(numRefractiveIndex, _scene.BackgroundRefractiveIndex)
            txtXSize.ForeColor = SystemColors.WindowText
            txtYsize.ForeColor = SystemColors.WindowText
        Finally
            _loadingScene = False
        End Try

        UpdateSceneControlState()
        UpdateScenePreview()
        ShowDampingValidity()
    End Sub

    ''' <summary>Enables the controls that belong to the chosen canvas type and to the damping edge check box.</summary>
    Private Sub UpdateSceneControlState()
        Dim useImage As Boolean = _scene.UseImage
        btnLoadSceneImage.Enabled = useImage
        lblImageNameAndSize.Enabled = useImage

        txtXSize.Enabled = Not useImage
        txtYsize.Enabled = Not useImage
        lblX.Enabled = Not useImage
        lblPix.Enabled = Not useImage
        numRefractiveIndex.Enabled = Not useImage      ' a scene image has its own refractive index per pixel
        Label20.Enabled = Not useImage

        txtDampingEdgeSize.Enabled = _scene.DampingEdgeEnabled
        Label1.Enabled = _scene.DampingEdgeEnabled

        numScaleImageSource.Enabled = _scene.UseSources
        lblScaleImgSourceIntensity.Enabled = _scene.UseSources
    End Sub

    ' ---- preview

    Private Sub UpdateScenePreview()
        UpdateImageLabel()

        If _scene.UseImage Then
            picImagePreview.SizeMode = PictureBoxSizeMode.Zoom
            picImagePreview.Image = _sceneImage
            DisposeEmptyPreview()
        Else
            ' user defined canvas: an empty picture with the proportions of the canvas
            picImagePreview.SizeMode = PictureBoxSizeMode.CenterImage
            Dim fresh As Bitmap = MakeEmptyCanvasPreview(_scene.UserWidth, _scene.UserHeight)
            picImagePreview.Image = fresh
            DisposeEmptyPreview()
            _emptyPreview = fresh
        End If
    End Sub

    Private Sub UpdateImageLabel()
        If _scene.ImageFile.Length = 0 Then
            lblImageNameAndSize.Text = "(no image loaded)"
        ElseIf _sceneImage Is Nothing Then
            lblImageNameAndSize.Text = "(image not found or not readable: " & Path.GetFileName(_scene.ImageFile) & ")"
        Else
            lblImageNameAndSize.Text = Path.GetFileName(_scene.ImageFile) & "  (" &
                _sceneImage.Width.ToString(CultureInfo.InvariantCulture) & " x " &
                _sceneImage.Height.ToString(CultureInfo.InvariantCulture) & " px)"
        End If
    End Sub

    Private Function MakeEmptyCanvasPreview(canvasWidth As Integer, canvasHeight As Integer) As Bitmap
        Dim boxW As Integer = Math.Max(16, picImagePreview.ClientSize.Width - 8)
        Dim boxH As Integer = Math.Max(16, picImagePreview.ClientSize.Height - 8)
        Dim scale As Double = Math.Min(boxW / CDbl(canvasWidth), boxH / CDbl(canvasHeight))
        Dim w As Integer = Math.Max(2, CInt(Math.Round(canvasWidth * scale)))
        Dim h As Integer = Math.Max(2, CInt(Math.Round(canvasHeight * scale)))

        Dim bmp As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Using g As Graphics = Graphics.FromImage(bmp)
            g.Clear(Color.White)
            Using pen As New Pen(Color.Gray)
                g.DrawRectangle(pen, 0, 0, w - 1, h - 1)
            End Using
            Using f As New Font("Microsoft Sans Serif", 9.0F), sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                g.DrawString(canvasWidth.ToString(CultureInfo.InvariantCulture) & " x " &
                             canvasHeight.ToString(CultureInfo.InvariantCulture) & " px (empty scene)",
                             f, Brushes.Gray, New RectangleF(0, 0, w, h), sf)
            End Using
        End Using
        Return bmp
    End Function

    Private Sub DisposeEmptyPreview()
        If _emptyPreview IsNot Nothing Then
            _emptyPreview.Dispose()
            _emptyPreview = Nothing
        End If
    End Sub

    ''' <summary>Makes newImage the scene image and disposes the previous one.</summary>
    Private Sub ReplaceSceneImage(newImage As Bitmap)
        Dim old As Bitmap = _sceneImage
        _sceneImage = newImage
        If old IsNot Nothing Then
            If Object.ReferenceEquals(picImagePreview.Image, old) Then picImagePreview.Image = Nothing
            old.Dispose()
        End If
        UpdateGlowEdgesImage()
    End Sub

    ''' <summary>Disposes _glowEdgesImage and its cached resized copy (GetGlowOverlayResized) and sets both to
    ''' Nothing, without touching _sceneImage - used both by UpdateGlowEdgesImage (about to rebuild it from the
    ''' scene image) and by SceneMode_CheckedChanged (switching to radUserDefinedScene, where the overlay should
    ''' disappear even though _sceneImage itself is kept around in case the user switches back).</summary>
    Private Sub ClearGlowEdgesImage()
        If _glowEdgesImage IsNot Nothing Then
            _glowEdgesImage.Dispose()
            _glowEdgesImage = Nothing
        End If
        If _glowOverlayCache IsNot Nothing Then
            _glowOverlayCache.Dispose()
            _glowOverlayCache = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Rebuilds _glowEdgesImage (the "glowing edges" picture used as the Field/Intensity preview overlay - see
    ''' ComputeGlowingEdges) from the current scene image. Called from ReplaceSceneImage, so this runs every time
    ''' the scene image itself changes, and from SceneMode_CheckedChanged when switching back to
    ''' radUseSceneImage. Also refreshes whatever is currently shown on the Field/Intensity previews, so a
    ''' checked overlay updates immediately rather than only on the next simulation frame.
    ''' </summary>
    Private Sub UpdateGlowEdgesImage()
        ClearGlowEdgesImage()
        If _sceneImage IsNot Nothing Then _glowEdgesImage = ComputeGlowingEdges(_sceneImage)

        RefreshPreviewOverlay(_picField, FieldOverlayGain())
        RefreshPreviewOverlay(_picIntensity, IntensityOverlayGain())
    End Sub

    ''' <summary>How strongly to blend the glow-edges overlay into the Field preview: 0 when
    ''' chkDiplayOverlayInField is unchecked (no overlay at all), otherwise numBrightnessFieldOverlay's value -
    ''' see ComposeGlowOverlay's gain parameter.</summary>
    Private Function FieldOverlayGain() As Double
        Return If(chkDiplayOverlayInField.Checked, CDbl(numBrightnessFieldOverlay.Value), 0.0)
    End Function

    ''' <summary>The Intensity preview's counterpart to FieldOverlayGain.</summary>
    Private Function IntensityOverlayGain() As Double
        Return If(chkDiplayOverlayInIntensity.Checked, CDbl(numBrightnessIntensityOverlay.Value), 0.0)
    End Function

    ''' <summary>
    ''' Builds a "glowing edges" picture of source: a same-size picture that is black except along its edges
    ''' (fast color changes), where each of the three color channels is set to how sharp the change in THAT
    ''' channel is there (a Sobel gradient magnitude, scaled and clamped to a byte). This is deliberately done
    ''' per channel rather than as a single grayscale edge strength, so e.g. a hard red-to-black edge lights up
    ''' mostly in red - keeping a hint of the original colors in the edge lines, the way Photoshop's "Glowing
    ''' Edges" filter does. The GlowScale constant below is the one tunable "how bright" knob - a typical hard
    ''' (full black-to-white) edge produces a raw Sobel magnitude of up to about 1442, and GlowScale brings that
    ''' into the 0-255 byte range without every ordinary edge saturating to pure white; ask if this should be
    ''' stronger/weaker. The outermost ring of pixels is left black (there is no full 3x3 neighborhood there).
    ''' </summary>
    Private Shared Function ComputeGlowingEdges(source As Bitmap) As Bitmap
        Const GlowScale As Double = 0.25

        Dim w As Integer = source.Width
        Dim h As Integer = source.Height
        Dim result As New Bitmap(w, h, PixelFormat.Format24bppRgb)
        Using g As Graphics = Graphics.FromImage(result)
            g.Clear(Color.Black)
        End Using
        If w < 3 OrElse h < 3 Then Return result    ' too small for a 3x3 kernel - stays plain black

        ' normalize the source into a plain 24bppRgb picture first (it may be indexed, 32bppArgb, ...)
        Using normalized As New Bitmap(w, h, PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(normalized)
                g.DrawImageUnscaled(source, 0, 0)
            End Using

            Dim rect As New Rectangle(0, 0, w, h)
            Dim srcData As BitmapData = normalized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            Dim dstData As BitmapData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb)
            Try
                Dim srcStride As Integer = srcData.Stride
                Dim dstStride As Integer = dstData.Stride
                Dim srcBytes(srcStride * h - 1) As Byte
                Dim dstBytes(dstStride * h - 1) As Byte
                Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length)

                ' 24bppRgb stores each pixel as three bytes, in B,G,R order
                For y As Integer = 1 To h - 2
                    Dim rowUp As Integer = (y - 1) * srcStride
                    Dim rowMid As Integer = y * srcStride
                    Dim rowDown As Integer = (y + 1) * srcStride
                    Dim dstRow As Integer = y * dstStride
                    For x As Integer = 1 To w - 2
                        Dim cUp As Integer = rowUp + (x - 1) * 3
                        Dim cMid As Integer = rowMid + (x - 1) * 3
                        Dim cDown As Integer = rowDown + (x - 1) * 3
                        Dim dstOffset As Integer = dstRow + x * 3

                        For channel As Integer = 0 To 2
                            Dim p00 As Integer = srcBytes(cUp + channel)
                            Dim p01 As Integer = srcBytes(cUp + 3 + channel)
                            Dim p02 As Integer = srcBytes(cUp + 6 + channel)
                            Dim p10 As Integer = srcBytes(cMid + channel)
                            Dim p12 As Integer = srcBytes(cMid + 6 + channel)
                            Dim p20 As Integer = srcBytes(cDown + channel)
                            Dim p21 As Integer = srcBytes(cDown + 3 + channel)
                            Dim p22 As Integer = srcBytes(cDown + 6 + channel)

                            Dim gx As Integer = (p02 + 2 * p12 + p22) - (p00 + 2 * p10 + p20)
                            Dim gy As Integer = (p20 + 2 * p21 + p22) - (p00 + 2 * p01 + p02)
                            Dim magnitude As Double = Math.Sqrt(CDbl(gx) * gx + CDbl(gy) * gy) * GlowScale
                            dstBytes(dstOffset + channel) = CByte(Math.Min(255.0, magnitude))
                        Next
                    Next
                Next

                Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length)
            Finally
                normalized.UnlockBits(srcData)
                result.UnlockBits(dstData)
            End Try
        End Using

        Return result
    End Function

    ''' <summary>The glow-edges picture (_glowEdgesImage), resized to match a preview frame's size - cached and
    ''' only rebuilt when that size changes, since the preview panels can ask for it on every single visualized
    ''' step of a running simulation. Returns Nothing when there is no glow-edges picture (no scene image).</summary>
    Private Function GetGlowOverlayResized(targetSize As Size) As Bitmap
        If _glowEdgesImage Is Nothing Then Return Nothing
        If _glowOverlayCache IsNot Nothing AndAlso _glowOverlayCacheSize.Equals(targetSize) Then Return _glowOverlayCache

        If _glowOverlayCache IsNot Nothing Then _glowOverlayCache.Dispose()
        Dim resized As New Bitmap(Math.Max(1, targetSize.Width), Math.Max(1, targetSize.Height), PixelFormat.Format24bppRgb)
        Using g As Graphics = Graphics.FromImage(resized)
            g.Clear(Color.Black)
            g.InterpolationMode = InterpolationMode.HighQualityBilinear
            g.DrawImage(_glowEdgesImage, New Rectangle(Point.Empty, targetSize))
        End Using
        _glowOverlayCache = resized
        _glowOverlayCacheSize = targetSize
        Return _glowOverlayCache
    End Function

    ''' <summary>Returns a new bitmap: a copy of frame with the glow-edges overlay (ComputeGlowingEdges,
    ''' GetGlowOverlayResized) additively blended on top, each of the overlay's bytes multiplied by gain first
    ''' (numBrightnessFieldOverlay/numBrightnessIntensityOverlay - 1 leaves ComputeGlowingEdges' own brightness
    ''' as is, above 1 brightens the overlay, 0 removes it entirely). Multiplying (rather than e.g. adding a flat
    ''' offset) is what keeps a still-black background black at any gain - 0 times anything is still 0 - while
    ''' scaling up faint edges where the color change was small, which is the actual complaint this is for: a
    ''' plain "brightness" slider that also lifted the black background to gray would defeat its own purpose
    ''' here. The overlay's black pixels (no edge there) leave frame untouched, and its bright pixels brighten
    ''' frame, clamped at 255 per channel. frame itself is never modified. Falls back to a plain copy of frame
    ''' when there is no glow-edges picture to blend in.</summary>
    Private Function ComposeGlowOverlay(frame As Bitmap, gain As Double) As Bitmap
        Dim overlay As Bitmap = GetGlowOverlayResized(frame.Size)
        Dim composed As New Bitmap(frame)
        If overlay Is Nothing Then Return composed
        Dim clampedGain As Double = Math.Max(0.0, gain)

        Dim bytesPerDstPixel As Integer = Bitmap.GetPixelFormatSize(composed.PixelFormat) \ 8
        If bytesPerDstPixel < 3 Then Return composed   ' an indexed/unexpected format we can't safely blend into

        Dim rect As New Rectangle(0, 0, frame.Width, frame.Height)
        Dim dstData As BitmapData = composed.LockBits(rect, ImageLockMode.ReadWrite, composed.PixelFormat)
        Dim srcData As BitmapData = overlay.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
        Try
            Dim dstStride As Integer = dstData.Stride
            Dim srcStride As Integer = srcData.Stride
            Dim h As Integer = frame.Height
            Dim w As Integer = frame.Width
            Dim dstBytes(dstStride * h - 1) As Byte
            Dim srcBytes(srcStride * h - 1) As Byte
            Marshal.Copy(dstData.Scan0, dstBytes, 0, dstBytes.Length)
            Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length)

            For y As Integer = 0 To h - 1
                Dim dRow As Integer = y * dstStride
                Dim sRow As Integer = y * srcStride
                For x As Integer = 0 To w - 1
                    Dim d As Integer = dRow + x * bytesPerDstPixel
                    Dim s As Integer = sRow + x * 3
                    ' both layouts are B,G,R... (composed may have a 4th, alpha byte - left untouched)
                    dstBytes(d) = CByte(Math.Min(255.0, CDbl(dstBytes(d)) + CDbl(srcBytes(s)) * clampedGain))
                    dstBytes(d + 1) = CByte(Math.Min(255.0, CDbl(dstBytes(d + 1)) + CDbl(srcBytes(s + 1)) * clampedGain))
                    dstBytes(d + 2) = CByte(Math.Min(255.0, CDbl(dstBytes(d + 2)) + CDbl(srcBytes(s + 2)) * clampedGain))
                Next
            Next

            Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length)
        Finally
            composed.UnlockBits(dstData)
            overlay.UnlockBits(srcData)
        End Try

        Return composed
    End Function

    ''' <summary>
    ''' Shows frame in box, replacing/disposing whatever was shown before, and additively blends in the
    ''' glow-edges overlay (ComposeGlowOverlay) when gain is above 0. box.Tag always holds the frame exactly as
    ''' produced (never itself modified), so the overlay can be added, removed, or re-scaled again later purely
    ''' from what is already shown - see RefreshPreviewOverlay - without needing a fresh frame from a run that
    ''' may by then be paused or already finished.
    ''' </summary>
    Private Sub SetPreviewFrame(box As PictureBox, frame As Bitmap, gain As Double)
        Dim oldImage As Image = box.Image
        Dim oldRaw As Bitmap = TryCast(box.Tag, Bitmap)

        box.Tag = frame
        box.Image = If(gain > 0.0 AndAlso _glowEdgesImage IsNot Nothing, ComposeGlowOverlay(frame, gain), DirectCast(frame, Image))

        If oldImage IsNot Nothing AndAlso Not Object.ReferenceEquals(oldImage, oldRaw) Then oldImage.Dispose()
        If oldRaw IsNot Nothing Then oldRaw.Dispose()
    End Sub

    ''' <summary>Re-applies (or removes, or re-scales) the glow-edges overlay on whatever box is currently
    ''' showing, using the raw frame kept in box.Tag (see SetPreviewFrame) - so this works without needing a new
    ''' frame from a running simulation. Used when a "Display overlay" checkbox or its brightness box is
    ''' changed, and when the glow-edges picture itself changes (UpdateGlowEdgesImage). Does nothing when box
    ''' has nothing shown yet.</summary>
    Private Sub RefreshPreviewOverlay(box As PictureBox, gain As Double)
        If box Is Nothing Then Return
        Dim raw As Bitmap = TryCast(box.Tag, Bitmap)
        If raw Is Nothing Then Return

        Dim oldImage As Image = box.Image
        box.Image = If(gain > 0.0 AndAlso _glowEdgesImage IsNot Nothing, ComposeGlowOverlay(raw, gain), DirectCast(raw, Image))
        If oldImage IsNot Nothing AndAlso Not Object.ReferenceEquals(oldImage, raw) Then oldImage.Dispose()
    End Sub

    Private Sub chkDiplayOverlayInField_CheckedChanged(sender As Object, e As EventArgs) Handles chkDiplayOverlayInField.CheckedChanged
        RefreshPreviewOverlay(_picField, FieldOverlayGain())
    End Sub

    Private Sub chkDiplayOverlayInIntensity_CheckedChanged(sender As Object, e As EventArgs) Handles chkDiplayOverlayInIntensity.CheckedChanged
        RefreshPreviewOverlay(_picIntensity, IntensityOverlayGain())
    End Sub

    ''' <summary>numBrightnessFieldOverlay/numBrightnessIntensityOverlay (Viewer tab, next to the "Display
    ''' overlay" checkboxes) scale the glow-edges overlay's own brightness - 1 is the Designer default (no
    ''' change), higher brightens faint edges, 0 is the same as unchecking the overlay entirely. See
    ''' ComposeGlowOverlay's gain parameter for why a multiplier (not a flat offset) is used.</summary>
    Private Sub numBrightnessFieldOverlay_ValueChanged(sender As Object, e As EventArgs) Handles numBrightnessFieldOverlay.ValueChanged
        RefreshPreviewOverlay(_picField, FieldOverlayGain())
    End Sub

    Private Sub numBrightnessIntensityOverlay_ValueChanged(sender As Object, e As EventArgs) Handles numBrightnessIntensityOverlay.ValueChanged
        RefreshPreviewOverlay(_picIntensity, IntensityOverlayGain())
    End Sub

    ''' <summary>Reads an image file into memory (the file is not kept open or locked).</summary>
    Private Shared Function LoadImageCopy(fullPath As String) As Bitmap
        Using fs As New FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Using source As Image = Image.FromStream(fs)
                Return New Bitmap(source)
            End Using
        End Using
    End Function

    ''' <summary>The canvas size that the simulation would use now, or an empty size when it is not known (no image).</summary>
    Private Function EffectiveCanvas() As Size
        If _scene.UseImage Then
            If _sceneImage Is Nothing Then Return Size.Empty
            Return _sceneImage.Size
        End If
        Return New Size(_scene.UserWidth, _scene.UserHeight)
    End Function

    ''' <summary>
    ''' Loads the scene image (as chosen on the Configuration tab) into pic, or - when no scene image is loaded (a
    ''' user defined canvas, or the image could not be read) - a plain black image sized to the current canvas
    ''' (EffectiveCanvas). Used by the "Import scene image" buttons on the Sources and Probes tabs.
    ''' </summary>
    Private Sub ImportSceneImageInto(pic As PictureBox)
        Dim old As Image = pic.Image
        Dim fresh As Bitmap

        If _scene.UseImage AndAlso _sceneImage IsNot Nothing Then
            fresh = New Bitmap(_sceneImage)
        Else
            Dim canvas As Size = EffectiveCanvas()
            If canvas.Width <= 0 OrElse canvas.Height <= 0 Then
                MessageBox.Show(Me, "The canvas size is not known yet" & vbCrLf &
                                "(choose a scene image, or a user defined width and height, on the Configuration tab first).",
                                "Import scene image", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            fresh = New Bitmap(canvas.Width, canvas.Height)
            Using g As Graphics = Graphics.FromImage(fresh)
                g.Clear(Color.Black)
            End Using
        End If

        pic.Image = fresh
        pic.Invalidate()
        If old IsNot Nothing Then old.Dispose()
    End Sub

    ''' <summary>
    ''' Loads the scene image (as chosen on the Configuration tab), or a black placeholder sized to the current
    ''' canvas when there is none, into pic - but only when pic does not already have an image. Called when the
    ''' Sources or Probes tab is opened or switched to, so there is always something to see and pick a position in
    ''' without an explicit "Import scene image" first, while never overwriting a picture the user already loaded
    ''' or that a simulation run already produced.
    ''' </summary>
    Private Sub AutoLoadSceneImageIfEmpty(pic As PictureBox)
        If pic Is Nothing OrElse pic.Image IsNot Nothing Then Return

        If _scene.UseImage AndAlso _sceneImage IsNot Nothing Then
            pic.Image = New Bitmap(_sceneImage)
            pic.Invalidate()
            Return
        End If

        Dim canvas As Size = EffectiveCanvas()
        If canvas.Width <= 0 OrElse canvas.Height <= 0 Then Return ' canvas size not known yet - nothing to show

        Dim placeholder As New Bitmap(canvas.Width, canvas.Height)
        Using g As Graphics = Graphics.FromImage(placeholder)
            g.Clear(Color.Black)
        End Using
        pic.Image = placeholder
        pic.Invalidate()
    End Sub

    ' ---- validation

    Private Shared Function TryParseInt(text As String, minValue As Integer, maxValue As Integer, ByRef value As Integer) As Boolean
        If Integer.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, value) Then
            Return value >= minValue AndAlso value <= maxValue
        End If
        Return False
    End Function

    ''' <summary>Empty when the text is a usable damping edge size, otherwise a message.</summary>
    Private Function DampingProblem(text As String, ByRef value As Integer) As String
        If Not TryParseInt(text, 1, SceneSettings.MaxDampingEdgeSize, value) Then
            Return "Enter a whole number of pixels, 1 or more."
        End If
        Dim canvas As Size = EffectiveCanvas()
        If canvas.Width > 0 AndAlso 2 * value >= Math.Min(canvas.Width, canvas.Height) Then
            Return "The damping edge must be less than half of the smaller canvas side (" &
                   Math.Min(canvas.Width, canvas.Height).ToString(CultureInfo.InvariantCulture) & " px)."
        End If
        Return ""
    End Function

    ''' <summary>Shows the damping edge size in red when it does not fit the canvas.</summary>
    Private Sub ShowDampingValidity()
        Dim value As Integer
        If DampingProblem(txtDampingEdgeSize.Text, value).Length = 0 Then
            txtDampingEdgeSize.ForeColor = SystemColors.WindowText
        Else
            txtDampingEdgeSize.ForeColor = Color.Red
        End If
    End Sub

    ' ---- events

    ''' <summary>Switching to radUserDefinedScene drops the glow-edges overlay (ClearGlowEdgesImage) - without it,
    ''' a "Display overlay" checkbox left checked on the Preview tab would otherwise keep showing the old scene
    ''' image's overlay, which no longer corresponds to anything in a user-defined scene, and that stale overlay
    ''' would then also be baked into any picture shared afterwards into the Sources/Probes tabs (CopyPreviewToTabs).
    ''' _sceneImage itself is kept (not disposed) so switching back to radUseSceneImage restores the overlay too,
    ''' via UpdateGlowEdgesImage, without needing to reload the image file.</summary>
    Private Sub SceneMode_CheckedChanged(sender As Object, e As EventArgs) Handles radUseSceneImage.CheckedChanged, radUserDefinedScene.CheckedChanged
        If _loadingScene Then Return
        Dim button As RadioButton = DirectCast(sender, RadioButton)
        If Not button.Checked Then Return

        _scene.UseImage = Object.ReferenceEquals(button, radUseSceneImage)
        If _scene.UseImage Then
            UpdateGlowEdgesImage()
        Else
            ClearGlowEdgesImage()
            RefreshPreviewOverlay(_picField, FieldOverlayGain())
            RefreshPreviewOverlay(_picIntensity, IntensityOverlayGain())
        End If
        UpdateSceneControlState()
        UpdateScenePreview()
        ShowDampingValidity()
        MarkProjectChanged()
    End Sub

    Private Sub btnLOadSceneImage_Click(sender As Object, e As EventArgs) Handles btnLoadSceneImage.Click
        Dim projectFolder As String = If(_project Is Nothing, "", _project.FolderPath)

        Using dlg As New OpenFileDialog()
            dlg.Title = "Choose the scene image"
            dlg.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|All files (*.*)|*.*"
            If _project IsNot Nothing Then
                If Directory.Exists(_project.SceneImageFolder) Then
                    dlg.InitialDirectory = _project.SceneImageFolder
                Else
                    dlg.InitialDirectory = _project.FolderPath
                End If
            End If
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim loaded As Bitmap = Nothing
            Try
                loaded = LoadImageCopy(dlg.FileName)
            Catch ex As Exception
                MessageBox.Show(Me, "The file could not be read as an image:" & vbCrLf & vbCrLf & ex.Message,
                                "Scene image", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End Try

            ReplaceSceneImage(loaded)

            ' an image from outside the project's own folder is copied in, so the project does not depend on a file
            ' elsewhere on disk; the copy is what gets used and saved from here on
            Dim imagePath As String = dlg.FileName
            If _project IsNot Nothing AndAlso Not IsInsideFolder(imagePath, _project.FolderPath) Then
                imagePath = CopyIntoSceneImageFolder(_project, imagePath)
            End If

            _scene.ImageFile = SceneSettings.ToStoredPath(projectFolder, imagePath)
            _scene.UseImage = True
        End Using

        _loadingScene = True
        Try
            radUseSceneImage.Checked = True
        Finally
            _loadingScene = False
        End Try

        UpdateSceneControlState()
        UpdateScenePreview()
        ShowDampingValidity()
        MarkProjectChanged()
    End Sub

    Private Sub CanvasSize_TextChanged(sender As Object, e As EventArgs) Handles txtXSize.TextChanged, txtYsize.TextChanged
        If _loadingScene Then Return
        Dim box As TextBox = DirectCast(sender, TextBox)
        Dim value As Integer

        If TryParseInt(box.Text, SceneSettings.MinCanvasSize, SceneSettings.MaxCanvasSize, value) Then
            box.ForeColor = SystemColors.WindowText
            _sceneTip.Hide(box)
            If Object.ReferenceEquals(box, txtXSize) Then
                _scene.UserWidth = value
            Else
                _scene.UserHeight = value
            End If
            UpdateScenePreview()
            ShowDampingValidity()
            MarkProjectChanged()
        Else
            box.ForeColor = Color.Red
            _sceneTip.Show("Enter a whole number of pixels from " & SceneSettings.MinCanvasSize.ToString(CultureInfo.InvariantCulture) &
                           " to " & SceneSettings.MaxCanvasSize.ToString(CultureInfo.InvariantCulture) & ".",
                           box, 0, box.Height + 2, 3000)
        End If
    End Sub

    Private Sub chkDampingEdge_CheckedChanged(sender As Object, e As EventArgs) Handles chkDampingEdge.CheckedChanged
        If _loadingScene Then Return
        _scene.DampingEdgeEnabled = chkDampingEdge.Checked
        UpdateSceneControlState()
        MarkProjectChanged()
    End Sub

    Private Sub DampingSize_TextChanged(sender As Object, e As EventArgs) Handles txtDampingEdgeSize.TextChanged
        If _loadingScene Then Return
        Dim value As Integer
        Dim problem As String = DampingProblem(txtDampingEdgeSize.Text, value)

        If problem.Length = 0 Then
            _scene.DampingEdgeSize = value
            txtDampingEdgeSize.ForeColor = SystemColors.WindowText
            _sceneTip.Hide(txtDampingEdgeSize)
            MarkProjectChanged()
        Else
            txtDampingEdgeSize.ForeColor = Color.Red
            _sceneTip.Show(problem, txtDampingEdgeSize, 0, txtDampingEdgeSize.Height + 2, 3000)
        End If
    End Sub

    ''' <summary>When a size box is left its text is put back to the last valid value, and the scene is saved.</summary>
    Private Sub SceneText_Leave(sender As Object, e As EventArgs) Handles txtXSize.Leave, txtYsize.Leave, txtDampingEdgeSize.Leave
        Dim box As TextBox = DirectCast(sender, TextBox)
        _sceneTip.Hide(box)

        _loadingScene = True
        Try
            If Object.ReferenceEquals(box, txtXSize) Then
                box.Text = _scene.UserWidth.ToString(CultureInfo.InvariantCulture)
            ElseIf Object.ReferenceEquals(box, txtYsize) Then
                box.Text = _scene.UserHeight.ToString(CultureInfo.InvariantCulture)
            Else
                box.Text = _scene.DampingEdgeSize.ToString(CultureInfo.InvariantCulture)
            End If
            box.ForeColor = SystemColors.WindowText
        Finally
            _loadingScene = False
        End Try

        ShowDampingValidity()
    End Sub

    Private Sub chkUseSources_CheckedChanged(sender As Object, e As EventArgs) Handles chkUseImageSources.CheckedChanged
        If _loadingScene Then Return
        _scene.UseSources = chkUseImageSources.Checked
        UpdateSceneControlState()
        MarkProjectChanged()
    End Sub

    Private Sub chkUseObjectSources_CheckedChanged(sender As Object, e As EventArgs) Handles chkUseObjectSources.CheckedChanged
        If _loadingScene Then Return
        _scene.UseObjectSources = chkUseObjectSources.Checked
        MarkProjectChanged()
    End Sub

    Private Sub ImageSourceScale_ValueChanged(sender As Object, e As EventArgs) Handles numScaleImageSource.ValueChanged
        If _loadingScene Then Return
        _scene.ImageSourceScale = CDbl(numScaleImageSource.Value)
        MarkProjectChanged()
    End Sub

    Private Sub RefractiveIndex_ValueChanged(sender As Object, e As EventArgs) Handles numRefractiveIndex.ValueChanged
        If _loadingScene Then Return
        _scene.BackgroundRefractiveIndex = CDbl(numRefractiveIndex.Value)
        MarkProjectChanged()
    End Sub

    ''' <summary>The value limited to the range of the numeric box.</summary>
    Private Shared Function ClampToRange(box As NumericUpDown, value As Double) As Decimal
        Dim v As Decimal = If(value < CDbl(box.Minimum), box.Minimum, If(value > CDbl(box.Maximum), box.Maximum, CDec(value)))
        Return v
    End Function

    Private Sub btnEditSourceList_Click(sender As Object, e As EventArgs) Handles btnEditObjectSourceList.Click
        tabMainFunctions.SelectedTab = tabSourceList
    End Sub

    ' ------------------------------------------------------------------ scheme folder and list

    Private Sub btbChooseFFolder_Click(sender As Object, e As EventArgs) Handles btbChooseFFolder.Click
        Using dlg As New FolderBrowserDialog()
            dlg.Description = "Choose the folder that contains the color scheme (.json) files"
            If Directory.Exists(txtSchemeFolder.Text) Then dlg.SelectedPath = txtSchemeFolder.Text
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                txtSchemeFolder.Text = dlg.SelectedPath
                LoadSchemeList()
            End If
        End Using
    End Sub

    Private Sub txtSchemeFolder_KeyDown(sender As Object, e As KeyEventArgs) Handles txtSchemeFolder.KeyDown
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            LoadSchemeList()
        End If
    End Sub

    ''' <summary>Reads all schemes from the folder in txtSchemeFolder and fills the list.</summary>
    Private Sub LoadSchemeList()
        Dim folder As String = txtSchemeFolder.Text.Trim()
        If Not Directory.Exists(folder) Then
            MessageBox.Show(Me, "The folder '" & folder & "' does not exist.", "Color schemes",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim errors As New List(Of String)()
        _schemes = ColorSchemeStore.LoadFolder(folder, errors)
        SortSchemes(_schemes, _schemeSortColumn, _schemeSortAscending)

        ListView1.BeginUpdate()
        Try
            ListView1.Items.Clear()
            _thumbs.Images.Clear()
            For Each old As Bitmap In _thumbBitmaps
                old.Dispose()
            Next
            _thumbBitmaps.Clear()

            For Each scheme As ColorScheme In _schemes
                Dim thumb As Bitmap = MakeThumbnail(scheme)
                _thumbBitmaps.Add(thumb)
                _thumbs.Images.Add(thumb)

                Dim item As New ListViewItem(scheme.Name, _thumbs.Images.Count - 1)
                item.SubItems.Add(If(scheme.HasStops, ColorScheme.StopCount.ToString() & "-stop", "table"))
                item.SubItems.Add(If(String.IsNullOrEmpty(scheme.SchemeType), "-", scheme.SchemeType))
                item.Tag = scheme
                ListView1.Items.Add(item)
            Next
        Finally
            ListView1.EndUpdate()
        End Try

        UpdateSchemeListSortHeaders()

        If errors.Count > 0 Then
            Dim shown As Integer = Math.Min(errors.Count, 8)
            Dim text As String = String.Join(vbCrLf, errors.GetRange(0, shown))
            If errors.Count > shown Then text &= vbCrLf & "... and " & (errors.Count - shown).ToString() & " more"
            MessageBox.Show(Me, "Some files could not be read as color schemes:" & vbCrLf & vbCrLf & text,
                            "Color schemes", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If

        RefreshSchemeCombos()

        _previewScheme = Nothing
        RefreshEditorState()
    End Sub

    ''' <summary>Sorts schemes in place: by name (sortColumn 0), or by type then name (sortColumn 2, or anything
    ''' else) - "type then name" is also the tie-break used within a column-0 (name) sort's reversed order, same
    ''' as SortProjectEntries's style.</summary>
    Private Sub SortSchemes(schemes As List(Of ColorScheme), sortColumn As Integer, ascending As Boolean)
        Dim result As Comparison(Of ColorScheme)
        If sortColumn = 0 Then
            result = Function(a As ColorScheme, b As ColorScheme) String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
        Else
            result = Function(a As ColorScheme, b As ColorScheme)
                          Dim c As Integer = String.Compare(a.SchemeType, b.SchemeType, StringComparison.OrdinalIgnoreCase)
                          If c <> 0 Then Return c
                          Return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                      End Function
        End If
        schemes.Sort(result)
        If Not ascending Then schemes.Reverse()
    End Sub

    ''' <summary>Clicking the Scheme or Type header sorts by it; clicking the same header again reverses the
    ''' direction. The Kind column (1) is not sortable.</summary>
    Private Sub ListView1_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles ListView1.ColumnClick
        If e.Column <> 0 AndAlso e.Column <> 2 Then Return
        If e.Column = _schemeSortColumn Then
            _schemeSortAscending = Not _schemeSortAscending
        Else
            _schemeSortColumn = e.Column
            _schemeSortAscending = True
        End If
        LoadSchemeList()
    End Sub

    ''' <summary>Marks the currently sorted column's header with a small arrow, the way lstAllProjects does.</summary>
    Private Sub UpdateSchemeListSortHeaders()
        If ListView1.Columns.Count < 3 Then Return
        Dim arrow As String = If(_schemeSortAscending, " ^", " v")
        ListView1.Columns(0).Text = "Scheme" & If(_schemeSortColumn = 0, arrow, "")
        ListView1.Columns(2).Text = "Type" & If(_schemeSortColumn = 2, arrow, "")
    End Sub

    Private Sub ListView1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListView1.SelectedIndexChanged
        If ListView1.SelectedItems.Count = 0 Then
            _previewScheme = Nothing
        Else
            _previewScheme = DirectCast(ListView1.SelectedItems(0).Tag, ColorScheme)
        End If
        RefreshEditorState()
    End Sub

    Private Sub ListView1_DoubleClick(sender As Object, e As EventArgs) Handles ListView1.DoubleClick
        LoadPreviewedScheme()
    End Sub

    ' ------------------------------------------------------------------ load / keep

    Private Sub btnLoadScheme_Click(sender As Object, e As EventArgs) Handles btnLoadScheme.Click
        If _previewScheme Is Nothing Then
            MessageBox.Show(Me, "Select a scheme in the list first.", "Load scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        LoadPreviewedScheme()
    End Sub

    ' Escape in the list ends the preview and shows the current scheme again
    Private Sub ListView1_KeyDown(sender As Object, e As KeyEventArgs) Handles ListView1.KeyDown
        If e.KeyCode = Keys.Escape Then
            ListView1.SelectedItems.Clear()
            e.Handled = True
        End If
    End Sub

    ''' <summary>Makes a working copy of the previewed scheme the current scheme. Returns False if cancelled.</summary>
    Private Function LoadPreviewedScheme() As Boolean
        If _previewScheme Is Nothing Then Return False
        If Not ConfirmDiscardChanges() Then Return False

        Dim source As ColorScheme = _previewScheme
        _current = source.Clone(source.Name)
        _current.FilePath = source.FilePath
        _dirty = False
        _selectedStop = -1

        ListView1.SelectedItems.Clear()   ' ends the preview; also refreshes the display
        RefreshEditorState()
        Return True
    End Function

    Private Function ConfirmDiscardChanges() As Boolean
        If Not _dirty Then Return True
        Dim answer As DialogResult = MessageBox.Show(Me,
            "The current color scheme '" & _current.Name & "' has unsaved changes. Discard them?",
            "Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        Return answer = DialogResult.Yes
    End Function

    ' ------------------------------------------------------------------ editing the stops

    Private Sub StopButton_Click(sender As Object, e As EventArgs) Handles btnColor1.Click, btnCOlor2.Click, btnColor3.Click, btnColor4.Click, btnColor5.Click, btnColor6.Click, btnColor7.Click, btnColor9.Click, btnColor8.Click
        Dim index As Integer = Array.IndexOf(_stopButtons, DirectCast(sender, Button))
        If index < 0 Then Return

        ' clicking a Color button while a list item is only previewed loads that scheme first
        If _previewScheme IsNot Nothing Then
            If Not LoadPreviewedScheme() Then Return
        End If
        If _current Is Nothing Then Return

        ' an imported scheme is a full table without stops: editing it means making a stop-based approximation
        If Not _current.HasStops Then
            Dim answer As DialogResult = MessageBox.Show(Me,
                "The scheme '" & _current.Name & "' is a full table of 255 colors without " &
                ColorScheme.StopCount.ToString() & " editable stops." & vbCrLf & vbCrLf &
                "Editing it creates a smooth " & ColorScheme.StopCount.ToString() & "-stop approximation as a new, " &
                "unsaved scheme (the original file stays unchanged). Continue?",
                "Edit scheme", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If answer <> DialogResult.Yes Then Return
            _current = _current.ToStopScheme(_current.Name & " (" & ColorScheme.StopCount.ToString() & "-stop)")
            _dirty = True
        End If

        _selectedStop = index
        Dim c As Color = _current.Stops(index)
        _picker.OriginalColor = c
        _picker.SelectedColor = c
        RefreshEditorState()
    End Sub

    Private Sub Picker_ColorChanged(sender As Object, e As EventArgs)
        If _current Is Nothing OrElse Not _current.HasStops OrElse _selectedStop < 0 Then Return
        _current.SetStop(_selectedStop, _picker.SelectedColor)
        _dirty = True
        RefreshEditorState()
    End Sub

    ' ------------------------------------------------------------------ save

    Private Sub btnSaveCurrentScheme_Click(sender As Object, e As EventArgs) Handles btnSaveCurrentScheme.Click
        If _current Is Nothing Then Return
        If String.IsNullOrEmpty(_current.FilePath) Then
            SaveAsNew()
            Return
        End If
        Try
            ColorSchemeStore.Save(_current, _current.FilePath)
            _dirty = False
        Catch ex As Exception
            MessageBox.Show(Me, "The scheme could not be saved:" & vbCrLf & ex.Message, "Save scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try
        ReloadListAfterSave()
    End Sub

    Private Sub btnSaveAsColorScheme_Click(sender As Object, e As EventArgs) Handles btnSaveAsColorScheme.Click
        SaveAsNew()
    End Sub

    Private Sub SaveAsNew()
        If _current Is Nothing Then Return

        Dim folder As String = txtSchemeFolder.Text.Trim()
        If Not Directory.Exists(folder) Then
            MessageBox.Show(Me, "Choose an existing scheme folder first.", "Save scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim newName As String = Microsoft.VisualBasic.Interaction.InputBox(
            "Name for the new color scheme:", "Save as new scheme", _current.Name).Trim()
        If newName.Length = 0 Then Return

        Dim target As String = ColorSchemeStore.PathFor(folder, newName)
        If File.Exists(target) Then
            Dim answer As DialogResult = MessageBox.Show(Me,
                "A scheme file named '" & Path.GetFileName(target) & "' already exists. Overwrite it?",
                "Save scheme", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            If answer <> DialogResult.Yes Then Return
        End If

        Try
            Dim copy As ColorScheme = _current.Clone(newName)
            If copy.HasStops AndAlso String.IsNullOrEmpty(copy.Category) Then copy.Category = "custom"
            ColorSchemeStore.Save(copy, target)
            _current = copy
            _dirty = False
        Catch ex As Exception
            MessageBox.Show(Me, "The scheme could not be saved:" & vbCrLf & ex.Message, "Save scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try
        ReloadListAfterSave()
    End Sub

    Private Sub ReloadListAfterSave()
        LoadSchemeList()
    End Sub

    ' ------------------------------------------------------------------ rename / delete (list toolbar)

    ''' <summary>
    ''' Renames the color scheme selected in the list (btnRenameColorScheme). Works directly on the scheme's JSON
    ''' file: writes a new file under the new name and removes the old one. If the renamed scheme is also the one
    ''' currently loaded in the editor (_current), that in-memory copy is renamed too, so a later Save does not
    ''' resurrect the old file under the old name.
    ''' </summary>
    Private Sub btnRenameColorScheme_Click(sender As Object, e As EventArgs) Handles btnRenameColorScheme.Click
        If _previewScheme Is Nothing Then
            MessageBox.Show(Me, "Select a scheme in the list first.", "Rename color scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim scheme As ColorScheme = _previewScheme
        Dim oldName As String = scheme.Name
        Dim oldPath As String = scheme.FilePath

        Dim newName As String = oldName
        If Not PromptForText("Rename color scheme", "New name for '" & oldName & "':", oldName, newName) Then Return
        newName = newName.Trim()
        If newName.Length = 0 OrElse newName = oldName Then Return

        Dim folder As String = If(String.IsNullOrEmpty(oldPath), txtSchemeFolder.Text.Trim(), Path.GetDirectoryName(oldPath))
        Dim newPath As String = ColorSchemeStore.PathFor(folder, newName)

        If Not String.Equals(newPath, oldPath, StringComparison.OrdinalIgnoreCase) AndAlso File.Exists(newPath) Then
            MessageBox.Show(Me, "A scheme file named '" & Path.GetFileName(newPath) & "' already exists.",
                            "Rename color scheme", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            scheme.Name = newName
            ColorSchemeStore.Save(scheme, newPath)
            If Not String.IsNullOrEmpty(oldPath) AndAlso Not String.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) AndAlso File.Exists(oldPath) Then
                File.Delete(oldPath)
            End If

            If _current IsNot Nothing AndAlso Not String.IsNullOrEmpty(oldPath) AndAlso
               String.Equals(_current.FilePath, oldPath, StringComparison.OrdinalIgnoreCase) Then
                _current.Name = newName
                _current.FilePath = newPath
            End If
        Catch ex As Exception
            MessageBox.Show(Me, "The scheme could not be renamed:" & vbCrLf & ex.Message, "Rename color scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        LoadSchemeList()
        SelectSchemeInList(newName)
        ToolStripStatusLabel1.Text = "Color scheme '" & oldName & "' renamed to '" & newName & "'."
    End Sub

    ''' <summary>
    ''' Permanently deletes the color scheme selected in the list (btnDeleteColorScheme), after asking for
    ''' confirmation. If the deleted scheme is also the one currently loaded in the editor, the editor keeps its
    ''' in-memory copy on screen (nothing changes there) but that copy no longer has a file to save back over -
    ''' the next save falls through to "save as new" instead.
    ''' </summary>
    Private Sub btnDeleteColorScheme_Click(sender As Object, e As EventArgs) Handles btnDeleteColorScheme.Click
        If _previewScheme Is Nothing Then
            MessageBox.Show(Me, "Select a scheme in the list first.", "Delete color scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim scheme As ColorScheme = _previewScheme
        Dim name As String = scheme.Name
        Dim path As String = scheme.FilePath

        If String.IsNullOrEmpty(path) OrElse Not File.Exists(path) Then
            MessageBox.Show(Me, "That scheme has no file on disk (nothing to delete).", "Delete color scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim answer As DialogResult = MessageBox.Show(Me,
            "Permanently delete the color scheme '" & name & "'?" & vbCrLf & vbCrLf & path,
            "Delete color scheme", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If answer <> DialogResult.Yes Then Return

        Try
            File.Delete(path)
        Catch ex As Exception
            MessageBox.Show(Me, "The scheme could not be deleted:" & vbCrLf & ex.Message, "Delete color scheme",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End Try

        If _current IsNot Nothing AndAlso String.Equals(_current.FilePath, path, StringComparison.OrdinalIgnoreCase) Then
            _current.FilePath = Nothing
        End If

        _previewScheme = Nothing
        LoadSchemeList()
        ToolStripStatusLabel1.Text = "Color scheme '" & name & "' deleted."
    End Sub

    ''' <summary>Selects, in ListView1, the item for the scheme with the given name (case-insensitive), if any.</summary>
    Private Sub SelectSchemeInList(name As String)
        For Each item As ListViewItem In ListView1.Items
            Dim scheme As ColorScheme = TryCast(item.Tag, ColorScheme)
            If scheme IsNot Nothing AndAlso String.Equals(scheme.Name, name, StringComparison.OrdinalIgnoreCase) Then
                item.Selected = True
                item.EnsureVisible()
                Exit For
            End If
        Next
    End Sub

    ' ------------------------------------------------------------------ scheme type (field / intensity)

    ' The radio buttons say what the current scheme is meant for. The choice is saved in the scheme file
    ' as "scheme_type": "field" or "scheme_type": "intensity" and is shown again when the file is loaded.
    Private Sub SchemeType_CheckedChanged(sender As Object, e As EventArgs) Handles radSchemeTypeField.CheckedChanged, radSchemeTypeIntensity.CheckedChanged
        If _updatingRadios Then Return
        If _current Is Nothing OrElse _previewScheme IsNot Nothing Then Return

        Dim radio As RadioButton = DirectCast(sender, RadioButton)
        If Not radio.Checked Then Return   ' only react to the button that became checked

        Dim newType As String = If(radio Is radSchemeTypeField, ColorScheme.SchemeTypeField, ColorScheme.SchemeTypeIntensity)
        If _current.SchemeType <> newType Then
            _current.SchemeType = newType
            _dirty = True
            RefreshEditorState()
        End If
    End Sub

    ' ------------------------------------------------------------------ display

    ''' <summary>The scheme shown in the scheme bar: the previewed one, otherwise the current one.</summary>
    Private Function DisplayedScheme() As ColorScheme
        If _previewScheme IsNot Nothing Then Return _previewScheme
        Return _current
    End Function

    ''' <summary>Brings the buttons, texts, picker and scheme bar in line with the current state.</summary>
    Private Sub RefreshEditorState()
        Dim previewing As Boolean = (_previewScheme IsNot Nothing)
        Dim shown As ColorScheme = DisplayedScheme()
        Dim hasCurrent As Boolean = (_current IsNot Nothing)
        Dim editing As Boolean = hasCurrent AndAlso Not previewing AndAlso _selectedStop >= 0

        If hasCurrent Then
            txtCurrentColorScheme.Text = _current.Name & If(_dirty, "   (unsaved changes)", "")
        Else
            txtCurrentColorScheme.Text = "(none loaded)"
        End If

        For k As Integer = 0 To ColorScheme.StopCount - 1
            Dim btn As Button = _stopButtons(k)
            btn.Enabled = (shown IsNot Nothing)
            If shown IsNot Nothing Then
                Dim c As Color = StopColorOf(shown, k)
                btn.BackColor = c
                btn.ForeColor = ContrastColor(c)
            Else
                btn.BackColor = SystemColors.Control
                btn.ForeColor = SystemColors.ControlText
            End If
            btn.FlatAppearance.BorderSize = If(editing AndAlso k = _selectedStop, 4, 1)
        Next

        _picker.Enabled = editing
        If editing Then
            lblColorPicker.Text = "Color Picker - editing Color " & (_selectedStop + 1).ToString() & StopRole(_selectedStop)
        Else
            lblColorPicker.Text = "Color Picker (click a Color button to edit it)"
        End If

        btnLoadScheme.Enabled = previewing
        btnSaveCurrentScheme.Enabled = hasCurrent AndAlso _dirty
        btnSaveAsColorScheme.Enabled = hasCurrent

        ' radio buttons show the type of the displayed scheme; they can only be changed for the current scheme
        _updatingRadios = True
        Try
            Dim shownType As String = If(shown Is Nothing, "", shown.SchemeType)
            radSchemeTypeField.Checked = (shownType = ColorScheme.SchemeTypeField)
            radSchemeTypeIntensity.Checked = (shownType = ColorScheme.SchemeTypeIntensity)
            radSchemeTypeField.Enabled = hasCurrent AndAlso Not previewing
            radSchemeTypeIntensity.Enabled = hasCurrent AndAlso Not previewing
        Finally
            _updatingRadios = False
        End Try

        pnlScheme.Invalidate()
    End Sub

    Private Sub pnlScheme_Paint(sender As Object, e As PaintEventArgs) Handles pnlScheme.Paint
        Dim scheme As ColorScheme = DisplayedScheme()
        Dim g As Graphics = e.Graphics
        Dim r As Rectangle = pnlScheme.ClientRectangle
        If scheme Is Nothing Then
            g.Clear(SystemColors.ControlLight)
            Return
        End If

        ' vertical gradient: top = entry 0 (Color 1), bottom = entry 254 (last Color), middle = entry 127 (centre Color)
        Using strip As New Bitmap(1, ColorScheme.EntryCount, PixelFormat.Format32bppArgb)
            For i As Integer = 0 To ColorScheme.EntryCount - 1
                strip.SetPixel(0, i, scheme.GetEntry(i))
            Next
            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            g.DrawImage(strip, r)
        End Using

        ' tick marks at the stop positions (dark and light so they show on any color)
        Dim editingHere As Boolean = (_previewScheme Is Nothing AndAlso _selectedStop >= 0)
        For k As Integer = 0 To ColorScheme.StopCount - 1
            Dim y As Integer = CInt(Math.Round(k * (r.Height - 1) / CDbl(ColorScheme.StopCount - 1)))
            Dim selected As Boolean = editingHere AndAlso k = _selectedStop
            Dim tick As Integer = If(selected, r.Width, 9)
            Dim thick As Integer = If(selected, 3, 1)
            Using darkPen As New Pen(Color.Black, CSng(thick + 2))
                g.DrawLine(darkPen, 0, y, tick, y)
                g.DrawLine(darkPen, r.Width - tick, y, r.Width, y)
            End Using
            Using lightPen As New Pen(Color.White, CSng(thick))
                g.DrawLine(lightPen, 0, y, tick, y)
                g.DrawLine(lightPen, r.Width - tick, y, r.Width, y)
            End Using
        Next

        ' scale labels that depend on the scheme type
        If scheme.SchemeType = ColorScheme.SchemeTypeField Then
            DrawScaleLabel(g, r, "-1", 0, scheme)
            DrawScaleLabel(g, r, "0", ColorScheme.EntryCount \ 2, scheme)
            DrawScaleLabel(g, r, "+1", ColorScheme.EntryCount - 1, scheme)
        ElseIf scheme.SchemeType = ColorScheme.SchemeTypeIntensity Then
            DrawScaleLabel(g, r, "0", 0, scheme)
            DrawScaleLabel(g, r, "max", ColorScheme.EntryCount - 1, scheme)
        End If
    End Sub

    ''' <summary>Draws a small label on the scheme bar at the height of a table entry, readable on any color.</summary>
    Private Sub DrawScaleLabel(g As Graphics, bar As Rectangle, text As String, entryIndex As Integer, scheme As ColorScheme)
        Dim textSize As Size = TextRenderer.MeasureText(g, text, pnlScheme.Font)
        Dim y As Integer = CInt(Math.Round(entryIndex * (bar.Height - 1) / CDbl(ColorScheme.EntryCount - 1)))
        Dim top As Integer = y - textSize.Height \ 2
        If top < 12 Then top = 12                                      ' keep clear of the end ticks
        If top + textSize.Height > bar.Height - 12 Then top = bar.Height - 12 - textSize.Height
        Dim box As New Rectangle(0, top, bar.Width, textSize.Height)
        TextRenderer.DrawText(g, text, pnlScheme.Font, box, ContrastColor(scheme.GetEntry(entryIndex)),
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
    End Sub

    ' ------------------------------------------------------------------ helpers

    Private Shared Function StopColorOf(scheme As ColorScheme, k As Integer) As Color
        If scheme.HasStops Then Return scheme.Stops(k)
        Return scheme.SampleAt(k * (ColorScheme.EntryCount - 1) / CDbl(ColorScheme.StopCount - 1))
    End Function

    Private Shared Function StopRole(k As Integer) As String
        If k = 0 Then Return " (start of the scale)"
        If k = ColorScheme.StopCount - 1 Then Return " (end of the scale)"
        If ColorScheme.StopCount Mod 2 = 1 AndAlso k = ColorScheme.StopCount \ 2 Then Return " (centre = zero field)"
        Return ""
    End Function

    Private Shared Function ContrastColor(c As Color) As Color
        Dim lum As Double = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B
        If lum > 140.0 Then Return Color.Black
        Return Color.White
    End Function

    ''' <summary>A horizontal preview of a scheme for the list: left = entry 0, right = entry 254.</summary>
    Private Shared Function MakeThumbnail(scheme As ColorScheme) As Bitmap
        Dim bmp As New Bitmap(ThumbWidth, ThumbHeight, PixelFormat.Format32bppArgb)
        Using g As Graphics = Graphics.FromImage(bmp)
            For x As Integer = 0 To ThumbWidth - 1
                Dim index As Integer = CInt(Math.Round(x * (ColorScheme.EntryCount - 1) / CDbl(ThumbWidth - 1)))
                Using br As New SolidBrush(scheme.GetEntry(index))
                    g.FillRectangle(br, x, 0, 1, ThumbHeight)
                End Using
            Next
            g.DrawRectangle(Pens.Gray, 0, 0, ThumbWidth - 1, ThumbHeight - 1)
        End Using
        Return bmp
    End Function

    ' ------------------------------------------------------------------ simulation
    '
    ' The settings on the Configuration tab (steps, video export, previews, color schemes) are kept in _simulation
    ' and saved in configuration.json with the project. "Start simulation" hands a copy of everything to a
    ' SimulationRunner, which does the work on a background thread. The runner reports back through events;
    ' PostToUi passes them on to this thread.

    Private _simulation As New SimulationSettings()

    ' True while the code fills the controls, so that their events do not change the settings
    Private _loadingSim As Boolean

    ' The running simulation (Nothing when none runs) and the progress text shown in the title bar
    Private _runner As SimulationRunner
    Private _simStatus As String = ""

    ' The videos of the last finished run (empty = none) and the settings of the run that is running or ran last
    Private _fieldVideoFile As String = ""
    Private _intensityVideoFile As String = ""
    Private _runInfo As String = ""

    ' state of the running simulation: paused, a stop was asked, and the last progress text
    Private _paused As Boolean
    Private _stopRequested As Boolean
    Private _progressText As String = ""
    Private _lastStepsDone As Integer

    ' the color tables of the run that is running or ran last (for a still export while paused, so it matches the
    ' colors of the live preview), and a full-resolution snapshot of its last frame (for a still export once the
    ' run has fully stopped - see SimulationResult.LastFieldFrame/LastIntensityFrame)
    Private _lastFieldLut As Integer()
    Private _lastIntensityLut As Integer()
    Private _lastFullFieldFrame As Bitmap
    Private _lastFullIntensityFrame As Bitmap

    ' The pictures on the Preview tab
    Private _picField As PictureBox
    Private _picIntensity As PictureBox

    ' the "glowing edges" picture of the scene image (ComputeGlowingEdges), same size as _sceneImage, rebuilt
    ' whenever the scene image changes (see ReplaceSceneImage/UpdateGlowEdgesImage); Nothing when there is no
    ' scene image. Optionally overlaid on the Field/Intensity previews - chkDiplayOverlayInField/
    ' chkDiplayOverlayInIntensity say whether to, per preview - see ComposeGlowOverlay/RefreshPreviewOverlay.
    Private _glowEdgesImage As Bitmap

    ' _glowEdgesImage resized to whatever size a preview frame last asked for (GetGlowOverlayResized) - rebuilding
    ' this by resampling the full scene-image-sized picture on every single visualized step of a run would be
    ' wasteful, so it is cached until either the target size or _glowEdgesImage itself changes.
    Private _glowOverlayCache As Bitmap
    Private _glowOverlayCacheSize As Size

    ' The pictures on the Sources and Probes tabs (copies of a preview picture, for later evaluation)
    Private _picSource As PictureBox
    Private _picProbe As PictureBox

    ' True once a simulation has run (and ended), so that the stop button becomes a restart button
    Private _hasRun As Boolean

    Private Sub InitializeSimulationControls()
        numVisualizeEvery.Minimum = 1

        ' brightness factors of the field and intensity pictures (render_field / render_intensity in the Python code)
        numFieldScaling.Minimum = 0.1D
        numFieldScaling.Maximum = 1000D
        numIntensityScaling.Minimum = 0.1D
        numIntensityScaling.Maximum = 1000D

        ' numFieldScaling2/numIntensityScaling2 (Viewer tab) are a second set of controls for the very same
        ' FieldBrightness/IntensityBrightness values as numFieldScaling/numIntensityScaling (Configuration tab) -
        ' see numScaleFieldValues_ValueChanged/numScaleIntensity_ValueChanged and their "2" counterparts, which
        ' keep both pairs in sync. Match the originals' range/precision so a value entered in either pair means
        ' the same thing and is not rejected/rounded differently by the other.
        numFieldScaling2.Minimum = numFieldScaling.Minimum
        numFieldScaling2.Maximum = numFieldScaling.Maximum
        numFieldScaling2.DecimalPlaces = numFieldScaling.DecimalPlaces
        numFieldScaling2.Increment = numFieldScaling.Increment
        numIntensityScaling2.Minimum = numIntensityScaling.Minimum
        numIntensityScaling2.Maximum = numIntensityScaling.Maximum
        numIntensityScaling2.DecimalPlaces = numIntensityScaling.DecimalPlaces
        numIntensityScaling2.Increment = numIntensityScaling.Increment

        ' offsets (black levels of the color tables): 2 decimals, otherwise the default of -0.05 would be rounded
        For Each box As NumericUpDown In New NumericUpDown() {numOffsetField, numOffsetIntensity}
            box.DecimalPlaces = 2
            box.Minimum = -1D
            box.Maximum = 1D
            box.Increment = 0.01D
        Next

        numQuality.Minimum = 1D
        numQuality.Maximum = 100D
        numQuality.DecimalPlaces = 0

        ' the summary of a run is shown in the status bar, its details in the tooltip
        StatusStrip1.ShowItemToolTips = True
        ToolStripStatusLabel1.Spring = True
        ToolStripStatusLabel1.TextAlign = ContentAlignment.MiddleLeft
        ToolStripStatusLabel1.Text = ""

        UpdateRunButtons()

        btnViewIntensityVideo.Text = "View intensity video in media player"
        UpdateVideoButtons()
        cmbFieldColorScheme.DropDownStyle = ComboBoxStyle.DropDownList
        cmbIntensityColorScheme.DropDownStyle = ComboBoxStyle.DropDownList

        _picField = MakePreviewBox(pnlPreviewField)
        _picIntensity = MakePreviewBox(pnlPreviewIntensity)
        _picSource = MakePreviewBox(pnlSourceImage)
        _picProbe = MakePreviewBox(pnlProbeImage)
        UpdateCopyButtons()

        ' warn, in lblWarningField/lblWarningIntensity, when the last (or next) run will not export that video -
        ' shown/hidden per video in UpdateSimulationControlState; these are Designer labels placed in the clear,
        ' not (like the dynamic label this used to be) a control positioned over other controls at runtime
        lblWarningField.Text = "Field video not exported"
        lblWarningField.ForeColor = Color.DarkBlue
        lblWarningIntensity.Text = "Intensity video not exported"
        lblWarningIntensity.ForeColor = Color.DarkBlue

        ' No ShowSimulationInControls() here: grpSimulationSettings is disabled until a real project exists
        ' (UpdateProjectAccess). Steps and TimeDecayFactor have no software default of their own any more (see
        ' their comments in SimulationSettings.vb) - populating the controls now, from the blank _simulation this
        ' form starts with, would clamp numSimulationSteps/numTimeDecayFactor down from their Designer defaults
        ' (3000 and 10) to their Minimum before StartNewProject ever gets a chance to read the Designer value back
        ' out of them. StartNewProject/SetCurrentProject call ShowSimulationInControls() themselves once a real
        ' project exists.
    End Sub

    ''' <summary>Puts a picture box that fills the panel (the picture is shown as large as fits) in a preview panel.</summary>
    Private Shared Function MakePreviewBox(host As Panel) As PictureBox
        host.BackColor = Color.Black
        Dim box As New PictureBox()
        box.Dock = DockStyle.Fill
        box.SizeMode = PictureBoxSizeMode.Zoom
        box.BackColor = Color.Black
        host.Controls.Add(box)
        Return box
    End Function

    ''' <summary>Copies the values of _simulation into the controls.</summary>
    Private Sub ShowSimulationInControls()
        _loadingSim = True
        Try
            ' the range of the numeric box is set in the designer; keep the settings inside it
            Dim every As Integer = Math.Max(CInt(numVisualizeEvery.Minimum), Math.Min(CInt(numVisualizeEvery.Maximum), _simulation.VisualizeEvery))
            _simulation.VisualizeEvery = every

            numSimulationSteps.Value = ClampToRange(numSimulationSteps, _simulation.Steps)
            numVisualizeEvery.Value = every

            numFieldScaling.Value = ClampToRange(numFieldScaling, _simulation.FieldBrightness)
            numIntensityScaling.Value = ClampToRange(numIntensityScaling, _simulation.IntensityBrightness)
            numFieldScaling2.Value = ClampToRange(numFieldScaling2, _simulation.FieldBrightness)
            numIntensityScaling2.Value = ClampToRange(numIntensityScaling2, _simulation.IntensityBrightness)
            numQuality.Value = ClampToRange(numQuality, _simulation.VideoQuality)
            numOffsetField.Value = ClampToRange(numOffsetField, _simulation.FieldBlackLevel)
            numOffsetIntensity.Value = ClampToRange(numOffsetIntensity, _simulation.IntensityBlackLevel)
            numTimeDecayFactor.Value = ClampToRange(numTimeDecayFactor, _simulation.TimeDecayFactor)

            chkExportVideoField.Checked = _simulation.ExportField
            chkExportVideoIntensity.Checked = _simulation.ExportIntensity
            chkExportVideoField2.Checked = _simulation.ExportField
            chkExportVideoIntensity2.Checked = _simulation.ExportIntensity
            chkPreviewVideoField.Checked = _simulation.PreviewField
            chkPreviewVideoIntensity.Checked = _simulation.PreviewIntensity
            chkUseGPU.Checked = _simulation.UseGpu
            chkUse4thOrderLaplacian.Checked = _simulation.Use4thOrderLaplacian

            FillSchemeCombo(cmbFieldColorScheme, ColorScheme.SchemeTypeField, _simulation.FieldColorScheme)
            FillSchemeCombo(cmbIntensityColorScheme, ColorScheme.SchemeTypeIntensity, _simulation.IntensityColorScheme)
        Finally
            _loadingSim = False
        End Try

        UpdateSimulationControlState()
    End Sub

    ''' <summary>A color scheme combo box is only useful when the field (or intensity) is exported or previewed.</summary>
    Private Sub UpdateSimulationControlState()
        cmbFieldColorScheme.Enabled = chkExportVideoField.Checked OrElse chkPreviewVideoField.Checked
        cmbIntensityColorScheme.Enabled = chkExportVideoIntensity.Checked OrElse chkPreviewVideoIntensity.Checked

        ' the quality only matters for exported videos
        Dim exporting As Boolean = chkExportVideoField.Checked OrElse chkExportVideoIntensity.Checked
        numQuality.Enabled = exporting
        lblVideoQuality.Enabled = exporting
        lblWarningField.Visible = Not chkExportVideoField.Checked
        lblWarningIntensity.Visible = Not chkExportVideoIntensity.Checked
    End Sub

    ''' <summary>Fills the two color scheme combo boxes again from the loaded schemes (after loading or saving schemes).</summary>
    Private Sub RefreshSchemeCombos()
        Dim wasLoading As Boolean = _loadingSim
        _loadingSim = True
        Try
            FillSchemeCombo(cmbFieldColorScheme, ColorScheme.SchemeTypeField, _simulation.FieldColorScheme)
            FillSchemeCombo(cmbIntensityColorScheme, ColorScheme.SchemeTypeIntensity, _simulation.IntensityColorScheme)
        Finally
            _loadingSim = wasLoading
        End Try
    End Sub

    ''' <summary>Lists the schemes of one type (field or intensity); schemes without a type are left out.</summary>
    Private Sub FillSchemeCombo(combo As ComboBox, schemeType As String, selectedName As String)
        combo.BeginUpdate()
        Try
            combo.Items.Clear()
            Dim selected As ColorScheme = Nothing
            For Each scheme As ColorScheme In _schemes
                If String.Equals(scheme.SchemeType, schemeType, StringComparison.OrdinalIgnoreCase) Then
                    combo.Items.Add(scheme)
                    If String.Equals(scheme.Name, selectedName, StringComparison.OrdinalIgnoreCase) Then selected = scheme
                End If
            Next
            If selected IsNot Nothing Then
                combo.SelectedItem = selected
            Else
                combo.SelectedIndex = -1
            End If
        Finally
            combo.EndUpdate()
        End Try
    End Sub

    Private Function FindScheme(name As String, schemeType As String) As ColorScheme
        For Each scheme As ColorScheme In _schemes
            If String.Equals(scheme.Name, name, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(scheme.SchemeType, schemeType, StringComparison.OrdinalIgnoreCase) Then Return scheme
        Next
        Return Nothing
    End Function

    ' ---- events of the settings controls

    Private Sub tabMainFunctions_SelectedIndexChanged(sender As Object, e As EventArgs) Handles tabMainFunctions.SelectedIndexChanged
        ' cmbFieldColorScheme/cmbIntensityColorScheme live on the Configuration tab. Schemes may have been added,
        ' renamed or deleted on the Color Scheme tab since these were last filled - or even just added to the
        ' folder from outside the app - so re-read the scheme folder from disk (RefreshSchemeCombos alone only
        ' refills the combos from the in-memory list, which can be stale) rather than assume it is still current.
        If tabMainFunctions.SelectedTab Is tabConfiguration Then
            Dim folder As String = txtSchemeFolder.Text.Trim()
            If Directory.Exists(folder) Then
                LoadSchemeList()
            Else
                RefreshSchemeCombos()
            End If
        End If

        ' lstAllProjects3 (Outputs tab) has the same staleness problem - a project may have appeared, been
        ' renamed or been deleted since it was last filled, so re-read it fresh on every arrival too. Default the
        ' selection to whichever project is currently open in the editor (falls back to the previous selection,
        ' or none, when no project is open - see RefreshOutputProjectList).
        If tabMainFunctions.SelectedTab Is tabOutput Then
            RefreshOutputProjectList(If(_project IsNot Nothing, _project.Name, ""))
        End If
    End Sub

    ''' <summary>numSimulationSteps replaces the old cmbSimulationSteps combo box, so the number of steps is no
    ''' longer limited to a fixed preset list - the range (currently 1 to 50,000) is set in the Designer, and the
    ''' control itself never allows an out-of-range or non-integer value, so unlike the old free-text combo this
    ''' needs no separate parsing/validation/error-coloring here.</summary>
    Private Sub numSimulationSteps_ValueChanged(sender As Object, e As EventArgs) Handles numSimulationSteps.ValueChanged
        If _loadingSim Then Return
        _simulation.Steps = CInt(numSimulationSteps.Value)
        MarkProjectChanged()
    End Sub

    Private Sub numVisualizeEvery_ValueChanged(sender As Object, e As EventArgs) Handles numVisualizeEvery.ValueChanged
        If _loadingSim Then Return
        _simulation.VisualizeEvery = CInt(numVisualizeEvery.Value)
        MarkProjectChanged()
    End Sub

    ''' <summary>Copies a value into a NumericUpDown that mirrors another one (numFieldScaling/numFieldScaling2,
    ''' numIntensityScaling/numIntensityScaling2 - Configuration tab and Viewer tab controls for the very same
    ''' setting), without re-triggering that control's own ValueChanged - every simulation control's ValueChanged
    ''' already checks _loadingSim first, so reusing it here as the reentrancy guard stops the two controls from
    ''' setting each other back and forth.</summary>
    Private Sub SetLinkedScalingValue(box As NumericUpDown, value As Decimal)
        Dim clamped As Decimal = If(value < box.Minimum, box.Minimum, If(value > box.Maximum, box.Maximum, value))
        If box.Value = clamped Then Return
        Dim wasLoading As Boolean = _loadingSim
        _loadingSim = True
        Try
            box.Value = clamped
        Finally
            _loadingSim = wasLoading
        End Try
    End Sub

    ''' <summary>Copies a checked state into a CheckBox that mirrors another one (chkExportVideoField/
    ''' chkExportVideoField2, chkExportVideoIntensity/chkExportVideoIntensity2 - Configuration tab and Viewer tab
    ''' controls for the very same setting) - the same _loadingSim reentrancy-guard idea as SetLinkedScalingValue,
    ''' for a CheckBox's Checked instead of a NumericUpDown's Value.</summary>
    Private Sub SetLinkedCheckedValue(box As CheckBox, value As Boolean)
        If box.Checked = value Then Return
        Dim wasLoading As Boolean = _loadingSim
        _loadingSim = True
        Try
            box.Checked = value
        Finally
            _loadingSim = wasLoading
        End Try
    End Sub

    Private Sub numScaleFieldValues_ValueChanged(sender As Object, e As EventArgs) Handles numFieldScaling.ValueChanged
        If _loadingSim Then Return
        _simulation.FieldBrightness = CDbl(numFieldScaling.Value)
        SetLinkedScalingValue(numFieldScaling2, numFieldScaling.Value)
        MarkProjectChanged()
    End Sub

    Private Sub numScaleFieldValues2_ValueChanged(sender As Object, e As EventArgs) Handles numFieldScaling2.ValueChanged
        If _loadingSim Then Return
        _simulation.FieldBrightness = CDbl(numFieldScaling2.Value)
        SetLinkedScalingValue(numFieldScaling, numFieldScaling2.Value)
        MarkProjectChanged()
    End Sub

    Private Sub numQuality_ValueChanged(sender As Object, e As EventArgs) Handles numQuality.ValueChanged
        If _loadingSim Then Return
        _simulation.VideoQuality = CInt(numQuality.Value)
        MarkProjectChanged()
    End Sub

    Private Sub numOffsetField_ValueChanged(sender As Object, e As EventArgs) Handles numOffsetField.ValueChanged
        If _loadingSim Then Return
        _simulation.FieldBlackLevel = CDbl(numOffsetField.Value)
        MarkProjectChanged()
    End Sub

    Private Sub numOffsetIntensity_ValueChanged(sender As Object, e As EventArgs) Handles numOffsetIntensity.ValueChanged
        If _loadingSim Then Return
        _simulation.IntensityBlackLevel = CDbl(numOffsetIntensity.Value)
        MarkProjectChanged()
    End Sub

    ''' <summary>"Intensity decay factor" - see SimulationSettings.TimeDecayFactor's comment.</summary>
    Private Sub numTimeDecayFactor_ValueChanged(sender As Object, e As EventArgs) Handles numTimeDecayFactor.ValueChanged
        If _loadingSim Then Return
        _simulation.TimeDecayFactor = CDbl(numTimeDecayFactor.Value)
        MarkProjectChanged()
    End Sub

    Private Sub numScaleIntensity_ValueChanged(sender As Object, e As EventArgs) Handles numIntensityScaling.ValueChanged
        If _loadingSim Then Return
        _simulation.IntensityBrightness = CDbl(numIntensityScaling.Value)
        SetLinkedScalingValue(numIntensityScaling2, numIntensityScaling.Value)
        MarkProjectChanged()
    End Sub

    Private Sub numScaleIntensity2_ValueChanged(sender As Object, e As EventArgs) Handles numIntensityScaling2.ValueChanged
        If _loadingSim Then Return
        _simulation.IntensityBrightness = CDbl(numIntensityScaling2.Value)
        SetLinkedScalingValue(numIntensityScaling, numIntensityScaling2.Value)
        MarkProjectChanged()
    End Sub

    Private Sub SimulationOption_CheckedChanged(sender As Object, e As EventArgs) Handles chkExportVideoField.CheckedChanged,
        chkExportVideoIntensity.CheckedChanged, chkPreviewVideoField.CheckedChanged, chkPreviewVideoIntensity.CheckedChanged
        If _loadingSim Then Return
        _simulation.ExportField = chkExportVideoField.Checked
        _simulation.ExportIntensity = chkExportVideoIntensity.Checked
        _simulation.PreviewField = chkPreviewVideoField.Checked
        _simulation.PreviewIntensity = chkPreviewVideoIntensity.Checked
        SetLinkedCheckedValue(chkExportVideoField2, chkExportVideoField.Checked)
        SetLinkedCheckedValue(chkExportVideoIntensity2, chkExportVideoIntensity.Checked)
        UpdateSimulationControlState()
        MarkProjectChanged()
    End Sub

    ''' <summary>chkExportVideoField2/chkExportVideoIntensity2 (Viewer tab) mirror chkExportVideoField/
    ''' chkExportVideoIntensity (Configuration tab) - the same pair-linking pattern as numFieldScaling/
    ''' numFieldScaling2 (SetLinkedScalingValue/SetLinkedCheckedValue).</summary>
    Private Sub chkExportVideoField2_CheckedChanged(sender As Object, e As EventArgs) Handles chkExportVideoField2.CheckedChanged
        If _loadingSim Then Return
        _simulation.ExportField = chkExportVideoField2.Checked
        SetLinkedCheckedValue(chkExportVideoField, chkExportVideoField2.Checked)
        UpdateSimulationControlState()
        MarkProjectChanged()
    End Sub

    Private Sub chkExportVideoIntensity2_CheckedChanged(sender As Object, e As EventArgs) Handles chkExportVideoIntensity2.CheckedChanged
        If _loadingSim Then Return
        _simulation.ExportIntensity = chkExportVideoIntensity2.Checked
        SetLinkedCheckedValue(chkExportVideoIntensity, chkExportVideoIntensity2.Checked)
        UpdateSimulationControlState()
        MarkProjectChanged()
    End Sub

    Private Sub chkUseGPU_CheckedChanged(sender As Object, e As EventArgs) Handles chkUseGPU.CheckedChanged
        If _loadingSim Then Return
        _simulation.UseGpu = chkUseGPU.Checked
        MarkProjectChanged()
    End Sub

    Private Sub chkUse4thOrderLaplacian_CheckedChanged(sender As Object, e As EventArgs) Handles chkUse4thOrderLaplacian.CheckedChanged
        If _loadingSim Then Return
        _simulation.Use4thOrderLaplacian = chkUse4thOrderLaplacian.Checked
        MarkProjectChanged()
    End Sub

    Private Sub SchemeCombo_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbFieldColorScheme.SelectedIndexChanged,
        cmbIntensityColorScheme.SelectedIndexChanged
        If _loadingSim Then Return
        Dim combo As ComboBox = DirectCast(sender, ComboBox)
        Dim scheme As ColorScheme = TryCast(combo.SelectedItem, ColorScheme)
        If scheme Is Nothing Then Return

        If combo Is cmbFieldColorScheme Then
            _simulation.FieldColorScheme = scheme.Name
        Else
            _simulation.IntensityColorScheme = scheme.Name
        End If
        MarkProjectChanged()
    End Sub

    ' ---- start and stop

    Private Sub btnStartSimulation_Click(sender As Object, e As EventArgs) Handles btnStartSimulation.Click
        If _runner IsNot Nothing Then
            ' the button is also a stop button while a simulation runs
            RequestStop()
            Return
        End If
        StartSimulation()
    End Sub

    ' ---- sharing a picture with the Sources and Probes tabs (grpShareImage, Preview tab)

    ''' <summary>Puts a copy of the current scene image (or the black placeholder when there is none) in
    ''' pnlSourceImage and pnlProbeImage - the same picture the "Import scene image" buttons on those tabs load,
    ''' just from here instead.</summary>
    Private Sub btnShareSceneImage_Click(sender As Object, e As EventArgs) Handles btnShareSceneImage.Click
        ImportSceneImageInto(_picSource)
        ImportSceneImageInto(_picProbe)
    End Sub

    Private Sub btnShareFieldImage_Click(sender As Object, e As EventArgs) Handles btnShareFieldImage.Click
        CopyPreviewToTabs(_picField)
    End Sub

    Private Sub btnShareIntensityImage_Click(sender As Object, e As EventArgs) Handles btnShareIntensityImage.Click
        CopyPreviewToTabs(_picIntensity)
    End Sub

    ''' <summary>Puts a copy of the picture that is shown in a preview on the Sources tab and on the Probes tab -
    ''' source.Image exactly as currently displayed, glow-edges overlay baked in when "Display overlay" happens
    ''' to be checked for that preview (SetPreviewFrame/RefreshPreviewOverlay), and not when it is not.</summary>
    Private Sub CopyPreviewToTabs(source As PictureBox)
        Dim shown As Image = source.Image
        If shown Is Nothing Then Return

        For Each target As PictureBox In New PictureBox() {_picSource, _picProbe}
            Dim copy As New Bitmap(shown)
            Dim old As Image = target.Image
            target.Image = copy
            If old IsNot Nothing Then old.Dispose()
        Next
    End Sub

    ''' <summary>The field/intensity share buttons are only on when their preview shows a picture; btnShareSceneImage
    ''' is not gated the same way, matching "Import scene image" on the Sources/Probes tabs, which always works
    ''' (it falls back to a black placeholder when there is no scene image).</summary>
    Private Sub UpdateCopyButtons()
        btnShareFieldImage.Enabled = (_picField IsNot Nothing) AndAlso (_picField.Image IsNot Nothing)
        btnShareIntensityImage.Enabled = (_picIntensity IsNot Nothing) AndAlso (_picIntensity.Image IsNot Nothing)
        UpdateExportButtons()
    End Sub

    ''' <summary>
    ''' The "Export current Field/Intensity frame" buttons are only on while the simulation is not actively
    ''' running (paused, or fully stopped) and there is something to export: either a live, paused engine
    ''' (RenderCurrentFrame), a full-resolution snapshot cached from the last run (_lastFullFieldFrame /
    ''' _lastFullIntensityFrame), or at least the picture currently shown in the preview.
    ''' </summary>
    Private Sub UpdateExportButtons()
        Dim canExport As Boolean = _paused OrElse _runner Is Nothing
        btnExportFieldFrame.Enabled = canExport AndAlso
            (_lastFullFieldFrame IsNot Nothing OrElse ((_picField IsNot Nothing) AndAlso (_picField.Image IsNot Nothing)))
        btnExportIntensityFrame.Enabled = canExport AndAlso
            (_lastFullIntensityFrame IsNot Nothing OrElse ((_picIntensity IsNot Nothing) AndAlso (_picIntensity.Image IsNot Nothing)))
    End Sub

    Private Sub btnPauzeResume_Click(sender As Object, e As EventArgs) Handles btnPauzeResume.Click
        If _runner Is Nothing OrElse _stopRequested Then Return

        If _paused Then
            _paused = False
            _simStatus = _progressText
            ToolStripStatusLabel1.Text = "Simulation running..."
            _runner.ResumeRun()
            ' back to the Preview tab, so the user follows the simulation again
            tabMainFunctions.SelectedTab = tabViewer
        Else
            _paused = True
            _simStatus = "paused - " & _progressText
            ToolStripStatusLabel1.Text = "Simulation paused (press Resume to continue, or Stop to finish and save the videos)"
            _runner.PauseRun()
        End If
        UpdateRunButtons()
        UpdateTitle()
    End Sub

    ' ---- grpNavigateTabs (Preview tab): jump straight to another tab

    Private Sub btnGotoConfiguration_Click(sender As Object, e As EventArgs) Handles btnGotoConfiguration.Click
        tabMainFunctions.SelectedTab = tabConfiguration
    End Sub

    Private Sub btnGotoSources_Click(sender As Object, e As EventArgs) Handles btnGotoSources.Click
        tabMainFunctions.SelectedTab = tabSourceList
    End Sub

    Private Sub btnGotoProbes_Click(sender As Object, e As EventArgs) Handles btnGotoProbes.Click
        tabMainFunctions.SelectedTab = tabProbeList
    End Sub

    Private Sub btnGotoPreview_Click(sender As Object, e As EventArgs) Handles btnGotoPreview.Click
        tabMainFunctions.SelectedTab = tabViewer
    End Sub

    ''' <summary>
    ''' Stops the run after the current step. The runner then closes the videos properly and reports that it is
    ''' finished, which switches on the buttons that open the videos.
    ''' </summary>
    Private Sub RequestStop()
        If _runner Is Nothing OrElse _stopRequested Then Return

        _stopRequested = True
        _paused = False
        _runner.Cancel()
        btnStartSimulation.Enabled = False
        btnStartSimulation.Text = "Stopping..."
        _simStatus = "stopping..."
        ToolStripStatusLabel1.Text = "Stopping the simulation and closing the video files..."
        UpdateRunButtons()
        UpdateTitle()
    End Sub

    ''' <summary>
    ''' The pause button on the Preview tab is only on while a simulation runs. btnStartSimulation is also the
    ''' stop/restart button (see its own Click handler) and keeps its own Text/Enabled state (RequestStop,
    ''' StartSimulation, OnSimulationFinished) - there used to be a separate btnStopSimulation for that, removed
    ''' as redundant once btnStartSimulation started doing double duty.
    ''' </summary>
    Private Sub UpdateRunButtons()
        Dim running As Boolean = (_runner IsNot Nothing) AndAlso Not _stopRequested
        ' Not actually disabling the button here: with its custom Azure BackColor, Windows' own disabled-button
        ' rendering draws the text with a strange two-tone (etched) effect instead of plain gray. So the button
        ' stays enabled and its color is set by hand instead - btnPauzeResume_Click already ignores clicks while
        ' no simulation is running, so behaviorally it is still "disabled".
        btnPauzeResume.ForeColor = If(running, SystemColors.ControlText, Color.Gray)
        btnPauzeResume.Text = If(_paused, "Resume simulation", "Pause simulation")
        UpdateExportButtons()
    End Sub

    Private Sub ShowSimulationProblem(message As String)
        MessageBox.Show(Me, message, "Start simulation", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ''' <summary>
    ''' Takes the values from the controls, so the run always uses what is on the screen (also a value that was
    ''' typed in but not confirmed yet is used).
    ''' </summary>
    Private Sub ReadSimulationControls()
        numVisualizeEvery.Validate()
        numFieldScaling.Validate()
        numIntensityScaling.Validate()
        numFieldScaling2.Validate()
        numIntensityScaling2.Validate()
        numOffsetField.Validate()
        numOffsetIntensity.Validate()
        numQuality.Validate()
        numScaleImageSource.Validate()
        numTimeDecayFactor.Validate()

        _simulation.VisualizeEvery = CInt(numVisualizeEvery.Value)
        _simulation.FieldBrightness = CDbl(numFieldScaling.Value)
        _simulation.IntensityBrightness = CDbl(numIntensityScaling.Value)
        _simulation.FieldBlackLevel = CDbl(numOffsetField.Value)
        _simulation.IntensityBlackLevel = CDbl(numOffsetIntensity.Value)
        _simulation.VideoQuality = CInt(numQuality.Value)
        _simulation.TimeDecayFactor = CDbl(numTimeDecayFactor.Value)
        _simulation.ExportField = chkExportVideoField.Checked
        _simulation.ExportIntensity = chkExportVideoIntensity.Checked
        _simulation.PreviewField = chkPreviewVideoField.Checked
        _simulation.PreviewIntensity = chkPreviewVideoIntensity.Checked
        _simulation.UseGpu = chkUseGPU.Checked
        _simulation.Use4thOrderLaplacian = chkUse4thOrderLaplacian.Checked
        _scene.ImageSourceScale = CDbl(numScaleImageSource.Value)
    End Sub

    ''' <summary>Checks the settings, builds the job and starts the runner.</summary>
    Private Sub StartSimulation()
        ReadSimulationControls()
        Dim settings As SimulationSettings = _simulation
        Dim needField As Boolean = settings.ExportField OrElse settings.PreviewField
        Dim needIntensity As Boolean = settings.ExportIntensity OrElse settings.PreviewIntensity
        Dim export As Boolean = settings.ExportField OrElse settings.ExportIntensity
        Dim preview As Boolean = settings.PreviewField OrElse settings.PreviewIntensity

        If Not (needField OrElse needIntensity) Then
            ShowSimulationProblem("Nothing to do: tick at least one of the export or preview options.")
            Return
        End If

        ' numSimulationSteps (a NumericUpDown) never holds an out-of-range or non-integer value, so unlike the old
        ' cmbSimulationSteps combo box this needs no parsing/validation here
        settings.Steps = CInt(numSimulationSteps.Value)

        ' the videos are written in the output folder of the project, so the project has to exist on disk
        If export AndAlso _project Is Nothing Then
            Dim answer As DialogResult = MessageBox.Show(Me,
                "The videos are saved in the output folder of the project, so the project must be saved first." & vbCrLf & vbCrLf &
                "Save the project now?", "Start simulation", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If answer <> DialogResult.Yes Then Return
            If Not SaveProject() Then Return
            settings = _simulation
        End If

        ' the canvas
        Dim canvas As Size = EffectiveCanvas()
        If canvas.Width <= 0 Then
            ShowSimulationProblem("There is no scene yet: load a scene image in the Scene properties, or define a canvas size.")
            Return
        End If

        Dim damping As Integer = 0
        If _scene.DampingEdgeEnabled Then
            Dim problem As String = DampingProblem(txtDampingEdgeSize.Text, damping)
            If problem.Length > 0 Then
                ShowSimulationProblem("The damping edge cannot be used: " & problem)
                txtDampingEdgeSize.Focus()
                Return
            End If
        End If

        ' without any source (image or object) nothing happens
        Dim useSources As Boolean = _scene.UseSources
        Dim hasImageSources As Boolean = _scene.UseImage AndAlso useSources AndAlso SceneData.CountSources(_sceneImage) > 0
        Dim hasObjectSources As Boolean = _scene.UseObjectSources AndAlso _sources.Count > 0
        Dim noSources As String = ""
        If Not hasImageSources AndAlso Not hasObjectSources Then
            Dim reasons As New List(Of String)()
            If _scene.UseImage Then
                If Not useSources Then
                    reasons.Add("image sources are switched off in the Scene properties")
                ElseIf SceneData.CountSources(_sceneImage) = 0 Then
                    reasons.Add("the scene image has no sources (no pixels with a green value above 0)")
                End If
            End If
            If Not _scene.UseObjectSources Then
                reasons.Add("object sources are switched off in the Scene properties")
            ElseIf _sources.Count = 0 Then
                reasons.Add("there are no object sources yet (add one on the Sources tab)")
            End If
            noSources = "This scene has no sources yet: " & String.Join("; ", reasons) & ". The field will stay empty."
        End If
        If noSources.Length > 0 Then
            Dim answer As DialogResult = MessageBox.Show(Me, noSources & vbCrLf & vbCrLf & "Run the simulation anyway?",
                "Start simulation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
            If answer <> DialogResult.Yes Then Return
        End If

        ' color tables
        Dim fieldLut As Integer() = Nothing
        Dim intensityLut As Integer() = Nothing
        If needField Then
            Dim scheme As ColorScheme = FindScheme(settings.FieldColorScheme, ColorScheme.SchemeTypeField)
            If scheme Is Nothing Then
                ShowSimulationProblem("Choose a color scheme for the field first.")
                cmbFieldColorScheme.Focus()
                Return
            End If
            fieldLut = ColorLut.Build(scheme, False, settings.FieldBlackLevel).Argb
        End If
        If needIntensity Then
            Dim scheme As ColorScheme = FindScheme(settings.IntensityColorScheme, ColorScheme.SchemeTypeIntensity)
            If scheme Is Nothing Then
                ShowSimulationProblem("Choose a color scheme for the intensity first.")
                cmbIntensityColorScheme.Focus()
                Return
            End If
            intensityLut = ColorLut.Build(scheme, False, settings.IntensityBlackLevel).Argb
        End If

        ' the job: the runner works on copies, so the settings can be edited while it runs
        Dim job As New SimulationJob()
        If _scene.UseImage Then
            job.Image = New Bitmap(_sceneImage)
        Else
            job.Width = canvas.Width
            job.Height = canvas.Height
            job.BackgroundRefractiveIndex = _scene.BackgroundRefractiveIndex
        End If
        job.UseSources = useSources
        job.SourceAmplitude = _scene.ImageSourceScale
        job.DampingEdge = damping
        job.Settings = settings.Clone()
        job.FieldLut = fieldLut
        job.IntensityLut = intensityLut
        _lastFieldLut = fieldLut
        _lastIntensityLut = intensityLut
        job.PreviewWidth = Math.Max(16, Math.Min(pnlPreviewField.ClientSize.Width, pnlPreviewIntensity.ClientSize.Width))
        job.PreviewHeight = Math.Max(16, Math.Min(pnlPreviewField.ClientSize.Height, pnlPreviewIntensity.ClientSize.Height))
        job.Probes = ProbesForRun()
        job.ObjectSources = SourcesForRun()

        ' the glow-edges overlay (Viewer tab) is only ever needed by the runner to bake into an exported video -
        ' the live preview blends its own copy in locally (ShowPreview/ComposeGlowOverlay), which can track the
        ' checkbox/brightness box live while this, like Settings, is frozen at the start of the run
        job.FieldOverlayGain = FieldOverlayGain()
        job.IntensityOverlayGain = IntensityOverlayGain()
        If export AndAlso _glowEdgesImage IsNot Nothing AndAlso (job.FieldOverlayGain > 0.0 OrElse job.IntensityOverlayGain > 0.0) Then
            job.GlowOverlay = New Bitmap(_glowEdgesImage)
        End If

        If export Then
            Try
                Directory.CreateDirectory(_project.OutputVideoFolder)
            Catch ex As Exception
                If job.Image IsNot Nothing Then job.Image.Dispose()
                If job.GlowOverlay IsNot Nothing Then job.GlowOverlay.Dispose()
                ShowSimulationProblem("The output folder could not be created:" & vbCrLf & _project.OutputVideoFolder & vbCrLf & vbCrLf & ex.Message)
                Return
            End Try
            Dim suffix As String = NextNumericSuffix(_project.OutputVideoFolder).ToString("D4", CultureInfo.InvariantCulture)
            If settings.ExportField Then
                job.FieldVideoPath = Path.Combine(_project.OutputVideoFolder, _project.Name & "_field_" & suffix & ".mp4")
            End If
            If settings.ExportIntensity Then
                job.IntensityVideoPath = Path.Combine(_project.OutputVideoFolder, _project.Name & "_intensity_" & suffix & ".mp4")
            End If
        End If

        ' go
        ClearPreviews()
        BeginProbeRun(job.Probes, canvas, settings.Steps)
        _fieldVideoFile = ""
        _intensityVideoFile = ""
        UpdateVideoButtons()
        _runInfo = "Field: brightness " & settings.FieldBrightness.ToString("0.0#", CultureInfo.CurrentCulture) &
                   ", offset " & settings.FieldBlackLevel.ToString("0.0#", CultureInfo.CurrentCulture) &
                   vbCrLf & "Intensity: brightness " & settings.IntensityBrightness.ToString("0.0#", CultureInfo.CurrentCulture) &
                   ", offset " & settings.IntensityBlackLevel.ToString("0.0#", CultureInfo.CurrentCulture) &
                   vbCrLf & "Image source scale: " & _scene.ImageSourceScale.ToString("0.0#", CultureInfo.CurrentCulture)
        If preview Then tabMainFunctions.SelectedTab = tabViewer

        Dim runner As New SimulationRunner(job)
        AddHandler runner.ProgressChanged, Sub(done As Integer, total As Integer) PostToUi(Sub() ShowProgress(runner, done, total))
        AddHandler runner.EngineReady, Sub(description As String, note As String) PostToUi(Sub() ShowEngine(runner, description, note))
        AddHandler runner.ProbesUpdated, Sub(update As ProbeUpdate) PostToUi(Sub() ShowProbeSamples(runner, update))
        AddHandler runner.PreviewReady, Sub(isField As Boolean, frame As Bitmap) PostToUi(Sub() ShowPreview(runner, isField, frame))
        AddHandler runner.Finished, Sub(result As SimulationResult) PostToUi(Sub() OnSimulationFinished(runner, result))

        _runner = runner
        btnStartSimulation.Text = "Stop Simulation"
        _simStatus = "starting..."
        _progressText = "starting..."
        _paused = False
        _stopRequested = False
        UpdateRunButtons()
        ToolStripStatusLabel1.Text = "Simulation running..."
        ToolStripStatusLabel1.ToolTipText = ""
        UpdateTitle()
        runner.Start()
    End Sub

    ' ---- messages from the runner (they arrive on the worker thread)

    Private Sub PostToUi(work As Action)
        If IsDisposed OrElse Not IsHandleCreated Then Return
        Try
            BeginInvoke(work)
        Catch ex As InvalidOperationException
            ' the window is gone (this includes ObjectDisposedException)
        End Try
    End Sub

    Private Sub ShowProgress(runner As SimulationRunner, done As Integer, total As Integer)
        If runner IsNot _runner OrElse _simStatus = "stopping..." Then Return
        _lastStepsDone = done
        Dim percent As Integer = CInt(100L * done \ Math.Max(1, total))
        _progressText = "step " & done.ToString("N0", CultureInfo.CurrentCulture) & " of " & total.ToString("N0", CultureInfo.CurrentCulture) &
                        " (" & percent.ToString(CultureInfo.InvariantCulture) & "%)"
        _simStatus = If(_paused, "paused - ", "") & _progressText
        UpdateTitle()
    End Sub

    ''' <summary>Tells in the status bar where the simulation runs (CPU or GPU), and why the GPU is not used if it was asked for.</summary>
    Private Sub ShowEngine(runner As SimulationRunner, description As String, note As String)
        If runner IsNot _runner OrElse IsDisposed Then Return
        Dim text As String = "Simulation running on " & description
        If note.Length > 0 Then text &= " - " & note
        ToolStripStatusLabel1.Text = text
        ToolStripStatusLabel1.ToolTipText = text
    End Sub

    Private Sub ShowPreview(runner As SimulationRunner, isField As Boolean, frame As Bitmap)
        If runner IsNot _runner OrElse IsDisposed Then
            frame.Dispose()
            runner.PreviewShown()
            Return
        End If

        Dim box As PictureBox = If(isField, _picField, _picIntensity)
        Dim gain As Double = If(isField, FieldOverlayGain(), IntensityOverlayGain())
        SetPreviewFrame(box, frame, gain)
        UpdateCopyButtons()
        runner.PreviewShown()
    End Sub

    Private Sub OnSimulationFinished(runner As SimulationRunner, result As SimulationResult)
        If runner IsNot _runner OrElse IsDisposed Then Return

        _runner = Nothing
        _hasRun = True
        _paused = False
        _stopRequested = False
        _lastStepsDone = result.StepsDone
        UpdateRunButtons()
        _simStatus = ""
        btnStartSimulation.Text = "Start Simulation"
        btnStartSimulation.Enabled = True
        UpdateTitle()

        _fieldVideoFile = result.FieldFile
        _intensityVideoFile = result.IntensityFile
        UpdateVideoButtons()

        ' a full-resolution snapshot of the last frame, for a still export once nothing is running any more
        Dim oldField As Bitmap = _lastFullFieldFrame
        _lastFullFieldFrame = result.LastFieldFrame
        If oldField IsNot Nothing Then oldField.Dispose()
        Dim oldIntensity As Bitmap = _lastFullIntensityFrame
        _lastFullIntensityFrame = result.LastIntensityFrame
        If oldIntensity IsNot Nothing Then oldIntensity.Dispose()
        UpdateExportButtons()

        Dim time As String = result.Elapsed.ToString("hh\:mm\:ss", CultureInfo.InvariantCulture)
        Dim files As String = ""
        If result.Files.Count > 0 Then
            files = vbCrLf & vbCrLf & "Videos (in " & Path.GetDirectoryName(result.Files(0)) & "):"
            For Each videoFile As String In result.Files
                files &= vbCrLf & Path.GetFileName(videoFile)
            Next
        End If
        If result.Status <> SimulationStatus.Failed AndAlso result.Message.Length > 0 Then
            files &= vbCrLf & vbCrLf & result.Message
        End If
        If result.Engine.Length > 0 Then
            files &= vbCrLf & vbCrLf & "Engine: " & result.Engine
            If result.EngineNote.Length > 0 Then files &= vbCrLf & result.EngineNote
        End If
        files &= vbCrLf & vbCrLf & _runInfo
        Dim stepsText As String = result.StepsDone.ToString("N0", CultureInfo.CurrentCulture)

        ' the summary goes in the status bar (the details in its tooltip); only a failure gets a message box
        Dim summary As String
        Select Case result.Status
            Case SimulationStatus.Completed
                summary = "Simulation completed: " & stepsText & " steps in " & time
            Case SimulationStatus.Cancelled
                summary = "Simulation stopped after " & stepsText & " steps (" & time & ")"
            Case Else
                summary = "Simulation failed after " & stepsText & " steps"
        End Select
        If result.Elapsed.TotalSeconds > 0.5 AndAlso result.StepsDone > 0 Then
            summary &= " - " & (result.StepsDone / result.Elapsed.TotalSeconds).ToString("0.0", CultureInfo.CurrentCulture) & " steps/s"
        End If
        If result.Engine.Length > 0 Then
            summary &= " - " & result.Engine
            If result.EngineNote.Length > 0 Then summary &= " (" & result.EngineNote & ")"
        End If
        If result.Files.Count > 0 Then
            Dim names As New List(Of String)()
            For Each videoFile As String In result.Files
                names.Add(Path.GetFileName(videoFile))
            Next
            summary &= " - video: " & String.Join(", ", names)
        End If
        If result.Status <> SimulationStatus.Failed AndAlso result.Message.Length > 0 Then
            summary &= " - MP4 (H.264) was not available, the videos are .avi files"
        End If

        ToolStripStatusLabel1.Text = summary
        ToolStripStatusLabel1.ToolTipText = summary & files

        If result.Status = SimulationStatus.Failed Then
            MessageBox.Show(Me, "The simulation failed after " & stepsText & " steps:" & vbCrLf & vbCrLf & result.Message & files,
                            "Simulation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    ' ---- the videos of the last run

    ''' <summary>The buttons under the previews are on when the video of the last run exists.</summary>
    Private Sub UpdateVideoButtons()
        btnViewFieldVideo.Enabled = _fieldVideoFile.Length > 0 AndAlso File.Exists(_fieldVideoFile)
        btnViewIntensityVideo.Enabled = _intensityVideoFile.Length > 0 AndAlso File.Exists(_intensityVideoFile)
    End Sub

    Private Sub btnViewFieldVideo_Click(sender As Object, e As EventArgs) Handles btnViewFieldVideo.Click
        OpenVideo(_fieldVideoFile)
    End Sub

    Private Sub btnViewIntensityVideo_Click(sender As Object, e As EventArgs) Handles btnViewIntensityVideo.Click
        OpenVideo(_intensityVideoFile)
    End Sub

    ''' <summary>Opens a video with the program that Windows uses for this kind of file.</summary>
    Private Sub OpenVideo(videoFile As String)
        If videoFile.Length = 0 OrElse Not File.Exists(videoFile) Then
            MessageBox.Show(Me, "The video file does not exist (any more):" & vbCrLf & videoFile, "Video",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            UpdateVideoButtons()
            Return
        End If
        Try
            Dim info As New ProcessStartInfo(videoFile)
            info.UseShellExecute = True
            Process.Start(info)
        Catch ex As Exception
            MessageBox.Show(Me, "The video could not be opened:" & vbCrLf & vbCrLf & ex.Message, "Video",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub btnOpenVideoFolder_Click(sender As Object, e As EventArgs) Handles btnOpenVideoFolder.Click
        OpenVideoFolder()
    End Sub

    Private Sub btnOpenImageFolder_Click(sender As Object, e As EventArgs) Handles btnOpenImageFolder.Click
        OpenFramesFolder()
    End Sub

    ' ---- exporting a single still frame (Preview tab)

    Private Sub btnExportFieldFrame_Click(sender As Object, e As EventArgs) Handles btnExportFieldFrame.Click
        ExportFrame(True)
    End Sub

    Private Sub btnExportIntensityFrame_Click(sender As Object, e As EventArgs) Handles btnExportIntensityFrame.Click
        ExportFrame(False)
    End Sub

    ''' <summary>
    ''' Saves the current Field or Intensity picture, at full simulation resolution, as a PNG in the project's
    ''' output frames folder. Only enabled (see UpdateExportButtons) while nothing is actively running: while
    ''' paused, a fresh frame is rendered straight from the live, paused engine (full resolution - not the
    ''' downscaled picture the preview shows); once the run has fully stopped, the last full-resolution frame it
    ''' produced just before closing is used instead (_lastFullFieldFrame/_lastFullIntensityFrame), since the
    ''' engine itself no longer exists by then.
    ''' </summary>
    Private Sub ExportFrame(isField As Boolean)
        Dim frame As Bitmap = Nothing
        Dim disposeFrame As Boolean = False

        If _runner IsNot Nothing AndAlso _paused Then
            Dim lut As Integer() = If(isField, _lastFieldLut, _lastIntensityLut)
            Dim brightness As Single = CSng(If(isField, _simulation.FieldBrightness, _simulation.IntensityBrightness))
            frame = _runner.RenderCurrentFrame(isField, lut, brightness)
            disposeFrame = True
        End If

        If frame Is Nothing Then frame = If(isField, _lastFullFieldFrame, _lastFullIntensityFrame)

        If frame Is Nothing Then
            MessageBox.Show(Me, "There is no picture to export yet.", "Export frame", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Try
            Directory.CreateDirectory(_project.OutputFramesFolder)
        Catch ex As Exception
            If disposeFrame Then frame.Dispose()
            MessageBox.Show(Me, "The output folder could not be created:" & vbCrLf & _project.OutputFramesFolder & vbCrLf & vbCrLf & ex.Message,
                            "Export frame", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End Try

        Dim kind As String = If(isField, "field", "intensity")
        Dim gain As Double = If(isField, FieldOverlayGain(), IntensityOverlayGain())
        Dim toSave As Bitmap = frame
        Dim disposeToSave As Boolean = False
        If gain > 0.0 AndAlso _glowEdgesImage IsNot Nothing Then
            toSave = ComposeGlowOverlay(frame, gain)
            disposeToSave = True
        End If

        Dim suffix As String = NextNumericSuffix(_project.OutputFramesFolder).ToString("D4", CultureInfo.InvariantCulture)
        Dim fileName As String = _project.Name & "_" & kind & "_frame" & _lastStepsDone.ToString(CultureInfo.InvariantCulture) & "_" & suffix & ".png"
        Dim fullPath As String = Path.Combine(_project.OutputFramesFolder, fileName)

        Try
            toSave.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png)
            ToolStripStatusLabel1.Text = "Frame exported: " & fileName
        Catch ex As Exception
            MessageBox.Show(Me, "The frame could not be saved:" & vbCrLf & vbCrLf & ex.Message, "Export frame", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            If disposeToSave Then toSave.Dispose()
            If disposeFrame Then frame.Dispose()
        End Try
    End Sub

    ''' <summary>Next available 4-digit numeric filename suffix for a file saved into folder: the highest existing
    ''' file in folder whose name (before the extension) ends in exactly 4 digits, plus 1 - starts at 1 when there
    ''' is no such file yet. Used for exported frames (ExportFrame) and videos (RunSimulation) instead of the
    ''' previous date/time stamp, which was found to make file names too long. Formats with "D4" (e.g. "0001"),
    ''' growing past 4 digits rather than wrapping once the count passes 9999.</summary>
    Private Function NextNumericSuffix(folder As String) As Integer
        Dim best As Integer = 0
        If Directory.Exists(folder) Then
            For Each f As String In Directory.GetFiles(folder)
                Dim nameOnly As String = Path.GetFileNameWithoutExtension(f)
                If nameOnly.Length >= 4 Then
                    Dim tail As String = nameOnly.Substring(nameOnly.Length - 4)
                    Dim n As Integer
                    If Integer.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, n) Then
                        If n > best Then best = n
                    End If
                End If
            Next
        End If
        Return best + 1
    End Function

    ''' <summary>
    ''' Opens the project's video output folder in Windows Explorer - which already shows video thumbnails and lets
    ''' the files be renamed or deleted there, so there is no need to build a picker of our own for that. Opened in
    ''' "Details" view (see OpenFolderInExplorer) - more useful than thumbnails for a folder of video files.
    ''' </summary>
    Private Sub OpenVideoFolder()
        Dim folder As String = If(_project Is Nothing, "", _project.OutputVideoFolder)
        If folder.Length = 0 Then
            MessageBox.Show(Me, "There is no project open yet.", "Open video folder", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            ' so the folder can be opened even before any video has been exported
            OpenFolderInExplorer(folder, FVM_DETAILS)
        Catch ex As Exception
            MessageBox.Show(Me, "The video folder could not be opened:" & vbCrLf & vbCrLf & ex.Message, "Open video folder",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ''' <summary>Opens the project's output frames folder (btnOpenImageFolder on the Preview tab) in Windows
    ''' Explorer, the same way OpenVideoFolder does for the video folder - but in "Large icons" view (see
    ''' OpenFolderInExplorer), which shows thumbnails of the exported images.</summary>
    Private Sub OpenFramesFolder()
        Dim folder As String = If(_project Is Nothing, "", _project.OutputFramesFolder)
        If folder.Length = 0 Then
            MessageBox.Show(Me, "There is no project open yet.", "Open image folder", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            ' so the folder can be opened even before any frame has been exported
            OpenFolderInExplorer(folder, FVM_ICON)
        Catch ex As Exception
            MessageBox.Show(Me, "The image folder could not be opened:" & vbCrLf & vbCrLf & ex.Message, "Open image folder",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ' Windows Explorer's own view-mode numbers (the classic Shell "FOLDERVIEWMODE" enum), used below.
    Private Const FVM_ICON As Integer = 1      ' "Large icons"
    Private Const FVM_DETAILS As Integer = 4   ' "Details"

    ''' <summary>
    ''' Opens a folder in Windows Explorer and tries to make that window use the given view mode (one of the
    ''' FVM_ constants above). Explorer has no command-line switch for this, so the folder is opened as before
    ''' (explorer.exe on the path) and then, best-effort, the "Shell.Application" COM automation object is used
    ''' to find the newly opened window for that exact folder and set its Document.CurrentViewMode. This uses late
    ''' binding via reflection rather than a direct COM reference, since Option Strict is on here and adding a COM
    ''' reference to the project for this alone is more trouble than it is worth.
    '''
    ''' This is not fully reliable: if a window for the folder is already open, Explorer reuses it instead of
    ''' opening a new one (so its view stays whatever it already was), and Explorer separately remembers, per
    ''' folder, whatever view the user leaves it in afterwards - so this only really matters the first time (or
    ''' after Explorer has forgotten it). Either way the folder itself always opens; only the requested view can
    ''' silently fail to apply, which is why every failure here is swallowed rather than shown to the user.
    ''' </summary>
    Private Sub OpenFolderInExplorer(folder As String, viewMode As Integer)
        Directory.CreateDirectory(folder)
        Dim info As New ProcessStartInfo("explorer.exe", """" & folder & """")
        info.UseShellExecute = True
        Process.Start(info)

        Try
            SetExplorerWindowViewMode(folder, viewMode)
        Catch
            ' best effort only - the folder is already open regardless of whether this succeeds
        End Try
    End Sub

    Private Sub SetExplorerWindowViewMode(folder As String, viewMode As Integer)
        Dim targetPath As String = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)
        Dim shellType As Type = Type.GetTypeFromProgID("Shell.Application")
        If shellType Is Nothing Then Return
        Dim shell As Object = Activator.CreateInstance(shellType)

        Try
            Const memberFlags As Reflection.BindingFlags = Reflection.BindingFlags.InvokeMethod Or Reflection.BindingFlags.GetProperty

            ' give the freshly launched Explorer window a moment to appear
            For attempt As Integer = 1 To 8
                Dim windows As Object = shellType.InvokeMember("Windows", memberFlags, Nothing, shell, Nothing)
                Dim windowsType As Type = windows.GetType()
                Dim count As Integer = CInt(windowsType.InvokeMember("Count", memberFlags, Nothing, windows, Nothing))

                For i As Integer = 0 To count - 1
                    Dim win As Object = windowsType.InvokeMember("Item", memberFlags, Nothing, windows, New Object() {i})
                    If win Is Nothing Then Continue For
                    Dim winType As Type = win.GetType()

                    Dim locationUrl As String = TryCast(winType.InvokeMember("LocationURL", memberFlags, Nothing, win, Nothing), String)
                    If String.IsNullOrEmpty(locationUrl) Then Continue For

                    Dim winPath As String
                    Try
                        winPath = New Uri(locationUrl).LocalPath.TrimEnd(Path.DirectorySeparatorChar)
                    Catch
                        Continue For
                    End Try

                    If String.Equals(winPath, targetPath, StringComparison.OrdinalIgnoreCase) Then
                        Dim doc As Object = winType.InvokeMember("Document", memberFlags, Nothing, win, Nothing)
                        If doc IsNot Nothing Then
                            doc.GetType().InvokeMember("CurrentViewMode", Reflection.BindingFlags.SetProperty, Nothing, doc, New Object() {viewMode})
                        End If
                        Return
                    End If
                Next

                Threading.Thread.Sleep(120)
            Next
        Finally
            Runtime.InteropServices.Marshal.ReleaseComObject(shell)
        End Try
    End Sub

    ''' <summary>Removes the pictures from the Preview tab and frees them (including the raw frame kept in
    ''' box.Tag by SetPreviewFrame, when it is a different bitmap from what box.Image itself was showing).</summary>
    Private Sub ClearPreviews()
        For Each box As PictureBox In New PictureBox() {_picField, _picIntensity}
            If box Is Nothing Then Continue For
            Dim oldImage As Image = box.Image
            Dim oldRaw As Bitmap = TryCast(box.Tag, Bitmap)
            box.Image = Nothing
            box.Tag = Nothing
            If oldImage IsNot Nothing AndAlso Not Object.ReferenceEquals(oldImage, oldRaw) Then oldImage.Dispose()
            If oldRaw IsNot Nothing Then oldRaw.Dispose()
        Next

        ' a new run is starting - the cached last-frame snapshots are from the previous run, so drop them
        If _lastFullFieldFrame IsNot Nothing Then _lastFullFieldFrame.Dispose()
        _lastFullFieldFrame = Nothing
        If _lastFullIntensityFrame IsNot Nothing Then _lastFullIntensityFrame.Dispose()
        _lastFullIntensityFrame = Nothing

        UpdateCopyButtons()
    End Sub

    ' grpProbe1_Enter/grpProbe2_Enter and Label21/Label22 Click are unused Designer-generated stubs (harmless, left
    ' empty). The Pick button, the Sources combo boxes and Button1 (the Probes tab's "Import scene image", still
    ' under its Designer default name) are wired in Form1.Source.vb/Form1.Probes.vb instead, where the rest of
    ' those tabs lives.
    Private Sub grpProbe1_Enter(sender As Object, e As EventArgs) Handles grpProbe1.Enter, grpProbe2.Enter
    End Sub

    Private Sub Label21_Click(sender As Object, e As EventArgs) Handles Label21.Click, Label22.Click
    End Sub

    Private Sub lblVideoQuality_Click(sender As Object, e As EventArgs) Handles lblVideoQuality.Click

    End Sub

    Private Sub btnSaveConfiguration2_Click(sender As Object, e As EventArgs) Handles btnSaveConfiguration2.Click
        SaveProject()
    End Sub
End Class
