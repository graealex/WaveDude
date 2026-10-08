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
Imports System.Globalization

' ------------------------------------------------------------------ object sources (Sources tab)
'
' An object source is a point (or, for a composite source, an X-Y array of points) that drives the field directly,
' as opposed to the image-derived sources (the green channel of the scene image). Unlike an image source it has its
' own frequency, amplitude and phase, and it can be continuous or pulsed (on for a number of cycles, then off for a
' number of cycles). A composite source is a set of point sources built from the same base ("single source")
' parameters, optionally randomized per element.
'
' Editing a source is a two step process:
'   1. txtName/cmbMainType/grpSingleSource/grpComposite describe the source; the position is typed or picked with
'      the Pick button in the picture (a copy of a Preview tab picture, "Copy Image to probe and source tab"). A
'      freshly created source has no position yet (SourceSettings.HasPosition is False) - the picture shows no
'      marker for it until one is picked or typed, so an unpositioned source is never mistaken for one at (0, 0)
'      or wherever the numeric fields default to.
'   2. "Save source" resolves those parameters - for a composite source, including a fresh random draw when
'      "Inherit + randomize" is chosen - into a fixed list of point-source elements (_editing.Elements), then adds
'      the result to lstSources (a new source) or updates the selected one. Only saved sources are used in a
'      simulation (when "Use object sources" is ticked in the scene properties) - the engine always runs the
'      already-generated element list, never re-randomizing, so a run is reproducible.
' Selecting a source in lstSources loads it straight into the editor (there is no separate Edit step/button).
' New and Copy load a (blank, respectively copied) source into the editor and clear the list selection; neither
' touches lstSources itself until Save is pressed, which adds or updates the entry.
Partial Public Class Form1

    ' the sources of the project
    Private _sources As New List(Of SourceSettings)()

    ' the source currently shown in the editor: -1 while creating a new one (New or Copy), otherwise its index in _sources
    Private _currentSourceIndex As Integer = -1

    ' the source being built in the editor; GenerateElements fills _editing.Elements, SaveSource commits it to _sources
    Private _editing As New SourceSettings()

    ' True while the code itself fills the editor controls (their change events are then ignored)
    Private _loadingSource As Boolean

    ' True while the list is being rebuilt, or its selection set, by code rather than by the user clicking in it -
    ' so lstSources_SelectedIndexChanged does not reload the editor for that (see RefreshSourceList)
    Private _syncingSourceList As Boolean

    ' True while waiting for a click in the picture for the source position
    Private _sourcePicking As Boolean
    Private _pickSourceButtonText As String = "Pick"

    Private ReadOnly _sourceRandom As New Random()

    ' btnSaveSource's own Designer BackColor, captured once at startup so ClearSourceDirty can restore exactly that
    ' (rather than a hardcoded color) once the pending changes it warns about are saved
    Private _sourceSaveButtonDefaultBackColor As Color

    ' ---------------------------------------------------------------- setting up

    Private Sub InitializeSourceControls()
        _pickSourceButtonText = btnPickSourcePosition.Text
        _sourceSaveButtonDefaultBackColor = btnSaveSource.BackColor
        WireSourceDirtyTracking(grpSourceDetails)

        numSourcePositionX.DecimalPlaces = 0
        numSourcePositionX.Minimum = 0D
        numSourcePositionX.Maximum = CDec(SceneSettings.MaxCanvasSize - 1)
        numSourcePositionY.DecimalPlaces = 0
        numSourcePositionY.Minimum = 0D
        numSourcePositionY.Maximum = CDec(SceneSettings.MaxCanvasSize - 1)

        ' Minimum (1) and Maximum (999) are both odd, and Increment is 2, so the up/down spinner buttons can only
        ' ever land on an odd array size - see ArraySize_Validating for what catches an even value typed in
        ' directly (the spinner can't produce one, but the keyboard can).
        numArraySizeX.DecimalPlaces = 0
        numArraySizeX.Minimum = 1D
        numArraySizeX.Maximum = 999D
        numArraySizeX.Increment = 2D
        numArraySizeY.DecimalPlaces = 0
        numArraySizeY.Minimum = 1D
        numArraySizeY.Maximum = 999D
        numArraySizeY.Increment = 2D

        numSpacingSizeX.DecimalPlaces = 0
        numSpacingSizeX.Minimum = 0D
        numSpacingSizeX.Maximum = CDec(SceneSettings.MaxCanvasSize)
        numSpacingSizeY.DecimalPlaces = 0
        numSpacingSizeY.Minimum = 0D
        numSpacingSizeY.Maximum = CDec(SceneSettings.MaxCanvasSize)

        numAmplitude.DecimalPlaces = 0
        numPulseWidthCycles.DecimalPlaces = 0
        If numPulseWidthCycles.Minimum < 1D Then numPulseWidthCycles.Minimum = 1D
        If numPulseWidthCycles.Maximum < 100D Then numPulseWidthCycles.Maximum = 1000000D
        numOffStateCycles.DecimalPlaces = 0
        numRandomizeFrequency.DecimalPlaces = 0
        numRandomizeOffWidth.DecimalPlaces = 0
        numRandomizePosition.DecimalPlaces = 0
        NumRandomizePhase.DecimalPlaces = 0

        AddHandler _picSource.Paint, AddressOf SourcePicture_Paint
        AddHandler _picSource.MouseClick, AddressOf SourcePicture_MouseClick

        ' owner-drawn so a switched-off source (chkSourceOn unchecked) can be shown greyed out in the list -
        ' it stays fully selectable, only its text color changes (see lstSources_DrawItem)
        lstSources.DrawMode = DrawMode.OwnerDrawFixed

        RefreshSourceList()
        ShowSourceInControls(_editing)
        ClearSourceDirty()
        UpdateListButtonState()
    End Sub

    ''' <summary>Loads the project's sources and shows the first one (if any) selected and loaded into the editor,
    ''' rather than always starting the editor on a blank "new source" template - so opening a project that already
    ''' has sources shows one of them, not a to-be-inserted one. "First" is whichever GenerateElements/save order the
    ''' list is in; there is no separate "last edited" tracking kept across a save/reload to prefer instead.</summary>
    Private Sub LoadSourcesFromProject()
        If _project Is Nothing Then Return
        _sources = SourceSettings.ListFromConfig(_project.Config)
        If _sources.Count > 0 Then
            _currentSourceIndex = 0
            _editing = _sources(0).Clone()
        Else
            _currentSourceIndex = -1
            _editing = New SourceSettings()
        End If
        RefreshSourceList()
        _syncingSourceList = True
        Try
            lstSources.SelectedIndex = _currentSourceIndex
        Finally
            _syncingSourceList = False
        End Try
        ShowSourceInControls(_editing)
        ClearSourceDirty()
        UpdateListButtonState()
    End Sub

    Private Sub ResetSources()
        _sources = New List(Of SourceSettings)()
        _currentSourceIndex = -1
        _editing = New SourceSettings()
        RefreshSourceList()
        If _picSource IsNot Nothing Then
            ShowSourceInControls(_editing)
            ClearSourceDirty()
        End If
        UpdateListButtonState()
    End Sub

    ''' <summary>The elements of every saved source, for a simulation run; Nothing when object sources are switched off or there are none.</summary>
    Private Function SourcesForRun() As List(Of SourceElement)
        If _scene Is Nothing OrElse Not _scene.UseObjectSources OrElse _sources.Count = 0 Then Return Nothing

        Dim result As New List(Of SourceElement)()
        For Each s As SourceSettings In _sources
            ' switched off (chkSourceOn unchecked): keep the source and its parameters, just leave it out of the
            ' run entirely, so it neither drives the field nor gets in the way of waves passing through it
            If Not s.SourceOn Then Continue For
            If s.Elements Is Nothing OrElse s.Elements.Count = 0 Then
                ' a source saved before it was ever generated (should not normally happen) - generate it now, once
                s.GenerateElements(_sourceRandom)
            End If
            result.AddRange(s.Elements)
        Next
        Return result
    End Function

    ' ---------------------------------------------------------------- the list

    Private Sub RefreshSourceList()
        Dim selected As Integer = lstSources.SelectedIndex
        _syncingSourceList = True
        Try
            lstSources.Items.Clear()
            For Each s As SourceSettings In _sources
                lstSources.Items.Add(SourceListText(s))
            Next
            If selected >= 0 AndAlso selected < lstSources.Items.Count Then lstSources.SelectedIndex = selected
        Finally
            _syncingSourceList = False
        End Try
    End Sub

    Private Shared Function SourceListText(s As SourceSettings) As String
        If s.MainType = SourceMainType.Composite Then
            Return s.Name & " (composite, " & s.ArraySizeX.ToString(CultureInfo.InvariantCulture) & " x " &
                  s.ArraySizeY.ToString(CultureInfo.InvariantCulture) & ")"
        End If
        Return s.Name & " (single)"
    End Function

    ''' <summary>Draws a switched-off source (chkSourceOn unchecked) greyed out; it stays fully selectable, only the
    ''' text color changes, so the selection highlight still shows normally.</summary>
    Private Sub lstSources_DrawItem(sender As Object, e As DrawItemEventArgs) Handles lstSources.DrawItem
        If e.Index < 0 OrElse e.Index >= lstSources.Items.Count Then Return
        e.DrawBackground()

        Dim isOff As Boolean = (e.Index < _sources.Count AndAlso Not _sources(e.Index).SourceOn)
        Dim textColor As Color = If(isOff, Color.Gray, e.ForeColor)

        Using brush As New SolidBrush(textColor)
            e.Graphics.DrawString(lstSources.Items(e.Index).ToString(), e.Font, brush, e.Bounds)
        End Using
        e.DrawFocusRectangle()
    End Sub

    ''' <summary>Selecting a source loads it straight into the editor; clearing the selection (New/Copy, or the
    ''' list becoming empty) shows a blank/default source instead. Programmatic selection changes (RefreshSourceList,
    ''' btnSaveSource_Click) set _syncingSourceList first so they do not re-trigger this.</summary>
    Private Sub lstSources_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lstSources.SelectedIndexChanged
        UpdateListButtonState()
        If _syncingSourceList Then Return

        EndSourcePick()
        If lstSources.SelectedIndex < 0 Then
            _currentSourceIndex = -1
            _editing = New SourceSettings()
        Else
            _currentSourceIndex = lstSources.SelectedIndex
            _editing = _sources(_currentSourceIndex).Clone()
        End If
        ShowSourceInControls(_editing)
        ClearSourceDirty()
        If _picSource IsNot Nothing Then _picSource.Invalidate()
        picPreviewSource.Invalidate()
        ToolStripStatusLabel1.Text = If(_currentSourceIndex >= 0,
                                        "Editing '" & _editing.Name & "': press Save source to keep the changes.",
                                        "")
    End Sub

    Private Sub UpdateListButtonState()
        Dim hasSelection As Boolean = (lstSources.SelectedIndex >= 0)
        btnCopy.Enabled = hasSelection
        btnDelete.Enabled = hasSelection
    End Sub

    Private Sub btnNew_Click(sender As Object, e As EventArgs) Handles btnNew.Click
        EndSourcePick()
        _syncingSourceList = True
        Try
            lstSources.SelectedIndex = -1
        Finally
            _syncingSourceList = False
        End Try
        _currentSourceIndex = -1
        _editing = New SourceSettings()
        _editing.Name = "Source " & (_sources.Count + 1).ToString(CultureInfo.InvariantCulture)
        ShowSourceInControls(_editing)
        MarkSourceDirty() ' not saved yet - pressing Save source is what actually adds it
        UpdateListButtonState()
        If _picSource IsNot Nothing Then _picSource.Invalidate()
        picPreviewSource.Invalidate()
        ToolStripStatusLabel1.Text = "New source: fill in its properties, then press Save source."
        txtName.Focus()
        txtName.SelectAll()
    End Sub

    Private Sub btnCopy_Click(sender As Object, e As EventArgs) Handles btnCopy.Click
        If lstSources.SelectedIndex < 0 Then Return
        EndSourcePick()
        Dim sourceName As String = _sources(lstSources.SelectedIndex).Name
        _editing = _sources(lstSources.SelectedIndex).Clone()
        _editing.Name = _editing.Name & " copy"
        _syncingSourceList = True
        Try
            lstSources.SelectedIndex = -1
        Finally
            _syncingSourceList = False
        End Try
        _currentSourceIndex = -1
        ShowSourceInControls(_editing)
        MarkSourceDirty() ' not saved yet - pressing Save source is what actually adds it
        UpdateListButtonState()
        If _picSource IsNot Nothing Then _picSource.Invalidate()
        picPreviewSource.Invalidate()
        ToolStripStatusLabel1.Text = "Copy of '" & sourceName & "': change it and press Save source to add it as a new source."
        txtName.Focus()
        txtName.SelectAll()
    End Sub

    Private Sub btnDelete_Click(sender As Object, e As EventArgs) Handles btnDelete.Click
        If lstSources.SelectedIndex < 0 Then Return
        Dim index As Integer = lstSources.SelectedIndex
        Dim name As String = _sources(index).Name
        Dim answer As DialogResult = MessageBox.Show(Me, "Delete the source '" & name & "'?", "Delete source",
                                                      MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If answer <> DialogResult.Yes Then Return

        _sources.RemoveAt(index)
        If _currentSourceIndex = index Then
            _currentSourceIndex = -1
            _editing = New SourceSettings()
            ShowSourceInControls(_editing)
            ClearSourceDirty()
        ElseIf _currentSourceIndex > index Then
            _currentSourceIndex -= 1
        End If
        RefreshSourceList()
        UpdateListButtonState()
        MarkProjectChanged()
        If _picSource IsNot Nothing Then _picSource.Invalidate()
        picPreviewSource.Invalidate()
        ToolStripStatusLabel1.Text = "Source '" & name & "' deleted."
    End Sub

    Private Sub btnSaveSource_Click(sender As Object, e As EventArgs) Handles btnSaveSource.Click
        CommitSourceEdits()
    End Sub

    ''' <summary>
    ''' Reads the editor into _editing, (re)generates its elements and writes the result back to _sources - updating
    ''' the entry at _currentSourceIndex when one is selected, or adding a new entry otherwise - then clears the
    ''' "unsaved changes" indicator on btnSaveSource. Shared by "Save source" itself and by completing a position
    ''' pick for an already-selected source (see SourcePicture_MouseClick/EndSourcePick), so picking a new position
    ''' for an existing source is never left stale until a separate press of Save source. Returns False (without
    ''' committing anything) when the source has no name yet, same as "Save source" always required.
    ''' </summary>
    Private Function CommitSourceEdits() As Boolean
        ReadSourceControls(_editing)
        If _editing.Name.Trim().Length = 0 Then
            MessageBox.Show(Me, "Give the source a name first.", "Save source", MessageBoxButtons.OK, MessageBoxIcon.Information)
            txtName.Focus()
            Return False
        End If

        ' always (re)generate the elements from the current editor values, so a changed position (or any other
        ' parameter) is never left stale in a source that already had elements from an earlier generate/save
        _editing.GenerateElements(_sourceRandom)

        If _currentSourceIndex >= 0 AndAlso _currentSourceIndex < _sources.Count Then
            _sources(_currentSourceIndex) = _editing
        Else
            _sources.Add(_editing)
            _currentSourceIndex = _sources.Count - 1
        End If

        RefreshSourceList()
        _syncingSourceList = True
        Try
            lstSources.SelectedIndex = _currentSourceIndex
        Finally
            _syncingSourceList = False
        End Try
        UpdateListButtonState()
        MarkProjectChanged()
        If _picSource IsNot Nothing Then _picSource.Invalidate()
        picPreviewSource.Invalidate()
        ToolStripStatusLabel1.Text = "Source '" & _editing.Name & "' saved (" & _editing.Elements.Count.ToString(CultureInfo.InvariantCulture) & " element(s))."

        ' the editor keeps editing the source that was just saved, so pressing Save again updates it in place
        _editing = _editing.Clone()
        ClearSourceDirty()
        Return True
    End Function

    ' ---------------------------------------------------------------- the editor

    ''' <summary>Copies a source's values into the editor controls.</summary>
    Private Sub ShowSourceInControls(s As SourceSettings)
        s.Normalize()
        _loadingSource = True
        Try
            txtName.Text = s.Name
            cmbMainType.SelectedIndex = CInt(s.MainType)
            chkSourceOn.Checked = s.SourceOn
            chkDetachSource.Checked = s.DetachSource
            txtDetachAfterSteps.Text = s.DetachAfterSteps.ToString(CultureInfo.InvariantCulture)
            numSourcePositionX.Value = ClampToRange(numSourcePositionX, s.PosX)
            numSourcePositionY.Value = ClampToRange(numSourcePositionY, s.PosY)

            numFrequency.Value = ClampToRange(numFrequency, s.Frequency)
            numAmplitude.Value = ClampToRange(numAmplitude, s.Amplitude)
            numPhase.Value = ClampToRange(numPhase, s.PhaseDeg)
            radModePulsed.Checked = (s.Mode = SourceMode.Pulsed)
            radModeContinuous.Checked = (s.Mode = SourceMode.Continuous)
            cmbPulseShape.SelectedIndex = CInt(s.PulseShape)
            numPulseWidthCycles.Value = ClampToRange(numPulseWidthCycles, s.PulseWidthCycles)
            numOffStateCycles.Value = ClampToRange(numOffStateCycles, s.OffStateCycles)

            numArraySizeX.Value = ClampToRange(numArraySizeX, s.ArraySizeX)
            numArraySizeY.Value = ClampToRange(numArraySizeY, s.ArraySizeY)
            numSpacingSizeX.Value = ClampToRange(numSpacingSizeX, s.SpacingX)
            numSpacingSizeY.Value = ClampToRange(numSpacingSizeY, s.SpacingY)

            ' the three "amount" fields below are clamped against the just-loaded values above (see
            ' UpdateRandomizeLimits) - called explicitly here rather than relying only on the ValueChanged handler
            ' further down, since a value that happens to equal what a control already shows does not raise that event
            UpdateRandomizeLimits()

            cmbCompositeProperties.SelectedIndex = CInt(s.CompositeMode)
            chkRandomizeFrequency.Checked = s.RandomizeFrequency
            numRandomizeFrequency.Value = ClampToRange(numRandomizeFrequency, s.RandomizeFrequencyAmount)
            chkRandomizePhase.Checked = s.RandomizePhase
            NumRandomizePhase.Value = ClampToRange(NumRandomizePhase, s.RandomizePhaseAmount)
            chkOffWidth.Checked = s.RandomizeOffWidth
            numRandomizeOffWidth.Value = ClampToRange(numRandomizeOffWidth, s.RandomizeOffWidthAmount)
            chkRandomizePosition.Checked = s.RandomizePosition
            numRandomizePosition.Value = ClampToRange(numRandomizePosition, s.RandomizePositionAmount)
        Finally
            _loadingSource = False
        End Try

        UpdateSourceControlState()
        UpdateLambdaLabel()
    End Sub

    ' ---------------------------------------------------------------- unsaved-changes indicator (btnSaveSource)

    ''' <summary>
    ''' Wires every input control inside grpSourceDetails - recursively, since it nests grpSingleSource and
    ''' grpComposite - to MarkSourceDirty, so any change the user makes to a source's properties there turns
    ''' btnSaveSource orange until the change is saved (see MarkSourceDirty/ClearSourceDirty). Called once, from
    ''' InitializeSourceControls. The source's position (numSourcePositionX/Y, in grpPlaceSource, not grpSourceDetails)
    ''' is deliberately not included here: picking or typing a position is committed through its own path instead
    ''' (SourcePicture_MouseClick/SourcePosition_ValueChanged).
    ''' </summary>
    Private Sub WireSourceDirtyTracking(container As Control)
        For Each child As Control In container.Controls
            If TypeOf child Is TextBox Then
                AddHandler DirectCast(child, TextBox).TextChanged, AddressOf SourceDetail_Changed
            ElseIf TypeOf child Is NumericUpDown Then
                AddHandler DirectCast(child, NumericUpDown).ValueChanged, AddressOf SourceDetail_Changed
            ElseIf TypeOf child Is CheckBox Then
                AddHandler DirectCast(child, CheckBox).CheckedChanged, AddressOf SourceDetail_Changed
            ElseIf TypeOf child Is RadioButton Then
                AddHandler DirectCast(child, RadioButton).CheckedChanged, AddressOf SourceDetail_Changed
            ElseIf TypeOf child Is ComboBox Then
                AddHandler DirectCast(child, ComboBox).SelectedIndexChanged, AddressOf SourceDetail_Changed
            End If

            If child.HasChildren Then WireSourceDirtyTracking(child)
        Next
    End Sub

    ''' <summary>Common handler for every control wired by WireSourceDirtyTracking. Ignored while the code itself is
    ''' filling the controls (ShowSourceInControls sets _loadingSource first), so only an actual user edit counts.</summary>
    Private Sub SourceDetail_Changed(sender As Object, e As EventArgs)
        If _loadingSource Then Return
        MarkSourceDirty()
    End Sub

    ''' <summary>Flags that grpSourceDetails has changes not yet saved to the selected (or not yet added) source.</summary>
    Private Sub MarkSourceDirty()
        btnSaveSource.BackColor = Color.Orange
    End Sub

    ''' <summary>Restores btnSaveSource to its own Designer color, once its pending changes are saved (or discarded
    ''' by loading a different/blank source into the editor).</summary>
    Private Sub ClearSourceDirty()
        btnSaveSource.BackColor = _sourceSaveButtonDefaultBackColor
    End Sub

    ''' <summary>Reads the editor controls into a source.</summary>
    Private Sub ReadSourceControls(target As SourceSettings)
        target.Name = txtName.Text.Trim()
        target.MainType = If(cmbMainType.SelectedIndex = 1, SourceMainType.Composite, SourceMainType.Simple)
        target.SourceOn = chkSourceOn.Checked
        target.DetachSource = chkDetachSource.Checked
        Dim detachAfter As Integer
        If Integer.TryParse(txtDetachAfterSteps.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, detachAfter) AndAlso detachAfter >= 0 Then
            target.DetachAfterSteps = detachAfter
        End If
        target.PosX = CInt(numSourcePositionX.Value)
        target.PosY = CInt(numSourcePositionY.Value)

        target.Frequency = CInt(numFrequency.Value)
        target.Amplitude = CInt(numAmplitude.Value)
        target.PhaseDeg = CInt(numPhase.Value)
        target.Mode = If(radModePulsed.Checked, SourceMode.Pulsed, SourceMode.Continuous)
        target.PulseShape = If(cmbPulseShape.SelectedIndex = 1, SourcePulseShape.SmoothedSquare, SourcePulseShape.Gaussian)
        target.PulseWidthCycles = CInt(numPulseWidthCycles.Value)
        target.OffStateCycles = CInt(numOffStateCycles.Value)

        target.ArraySizeX = CInt(numArraySizeX.Value)
        target.ArraySizeY = CInt(numArraySizeY.Value)
        target.SpacingX = CInt(numSpacingSizeX.Value)
        target.SpacingY = CInt(numSpacingSizeY.Value)
        target.CompositeMode = If(cmbCompositeProperties.SelectedIndex = 1, SourceCompositeMode.InheritAndRandomize, SourceCompositeMode.InheritFromSingle)
        target.RandomizeFrequency = chkRandomizeFrequency.Checked
        target.RandomizeFrequencyAmount = CInt(numRandomizeFrequency.Value)
        target.RandomizePhase = chkRandomizePhase.Checked
        target.RandomizePhaseAmount = CInt(NumRandomizePhase.Value)
        target.RandomizeOffWidth = chkOffWidth.Checked
        target.RandomizeOffWidthAmount = CInt(numRandomizeOffWidth.Value)
        target.RandomizePosition = chkRandomizePosition.Checked
        target.RandomizePositionAmount = CInt(numRandomizePosition.Value)
        target.Normalize()
    End Sub

    ''' <summary>Enables the controls that apply to the chosen main type / mode / composite mode.</summary>
    Private Sub UpdateSourceControlState()
        Dim isComposite As Boolean = (cmbMainType.SelectedIndex = 1)
        grpComposite.Enabled = isComposite

        Dim isPulsed As Boolean = radModePulsed.Checked
        cmbPulseShape.Enabled = isPulsed
        numPulseWidthCycles.Enabled = isPulsed
        numOffStateCycles.Enabled = isPulsed

        txtDetachAfterSteps.Enabled = chkDetachSource.Checked

        Dim randomizeOn As Boolean = isComposite AndAlso (cmbCompositeProperties.SelectedIndex = 1)
        cmbCompositeProperties.Enabled = isComposite
        numArraySizeX.Enabled = isComposite
        numArraySizeY.Enabled = isComposite
        numSpacingSizeX.Enabled = isComposite
        numSpacingSizeY.Enabled = isComposite

        chkRandomizeFrequency.Enabled = randomizeOn
        chkRandomizePhase.Enabled = randomizeOn
        chkOffWidth.Enabled = randomizeOn
        chkRandomizePosition.Enabled = randomizeOn
        numRandomizeFrequency.Enabled = randomizeOn AndAlso chkRandomizeFrequency.Checked
        NumRandomizePhase.Enabled = randomizeOn AndAlso chkRandomizePhase.Checked
        numRandomizeOffWidth.Enabled = randomizeOn AndAlso chkOffWidth.Checked
        numRandomizePosition.Enabled = randomizeOn AndAlso chkRandomizePosition.Checked
    End Sub

    Private Sub SourceMainType_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbMainType.SelectedIndexChanged, cmbCompositeProperties.SelectedIndexChanged
        If _loadingSource Then Return
        UpdateSourceControlState()
    End Sub

    Private Sub SourceMode_CheckedChanged(sender As Object, e As EventArgs) Handles radModePulsed.CheckedChanged, radModeContinuous.CheckedChanged
        If _loadingSource Then Return
        UpdateSourceControlState()
    End Sub

    ''' <summary>Keeps lblLambdaIs in sync with numFrequency - not guarded by _loadingSource, since it must also
    ''' refresh when a source is loaded into the editor (ShowSourceInControls), not only on a user edit.</summary>
    Private Sub SourceFrequency_ValueChanged(sender As Object, e As EventArgs) Handles numFrequency.ValueChanged
        UpdateLambdaLabel()
    End Sub

    ''' <summary>
    ''' Shows an estimated wavelength for the current numFrequency value in lblLambdaIs, as "λ=xx,x px" (using the
    ''' current culture's decimal separator). lambda = c / f: f is the cyclic frequency (cycles/step) that a
    ''' source's Level (1-255, the numFrequency control) maps to via SourceFrequencyScale - the same formula the
    ''' engine itself uses (WaveSimulator/GpuWaveSimulator: level/255*0.5*SourceFrequencyScale is the *angular*
    ''' frequency in rad/step, so dividing by 2*pi gives cycles/step) - and c is the propagation speed set via
    ''' File > Set c-value (SimulationSettings.WaveSpeedC), in simulation pixels per step. This is only ever an
    ''' estimate/display value: the real per-pixel wave speed the simulation actually uses comes from the scene's
    ''' refractive index instead (see SceneData.C2) and is not necessarily uniform.
    ''' </summary>
    Private Sub UpdateLambdaLabel()
        If lblLambdaIs Is Nothing Then Return

        Dim level As Integer = CInt(numFrequency.Value)
        Dim scale As Double = If(_simulation IsNot Nothing, _simulation.SourceFrequencyScale, 2.0)
        Dim omega As Double = level / 255.0 * 0.5 * scale
        Dim cyclesPerStep As Double = omega / (2.0 * Math.PI)

        If cyclesPerStep <= 0.0 Then
            lblLambdaIs.Text = "λ=  -  px"
            Return
        End If

        Dim c As Double = If(_simulation IsNot Nothing, _simulation.WaveSpeedC, 0.563)
        Dim lambda As Double = c / cyclesPerStep
        ' the current culture's own decimal separator (a comma in nl-NL, for instance), not a fixed "."
        lblLambdaIs.Text = "λ=" & lambda.ToString("0.0", CultureInfo.CurrentCulture) & " px"
    End Sub

    Private Sub SourceDetach_CheckedChanged(sender As Object, e As EventArgs) Handles chkDetachSource.CheckedChanged
        If _loadingSource Then Return
        UpdateSourceControlState()
    End Sub

    Private Sub SourceRandomizeCheck_CheckedChanged(sender As Object, e As EventArgs) Handles chkRandomizeFrequency.CheckedChanged,
        chkRandomizePhase.CheckedChanged, chkOffWidth.CheckedChanged, chkRandomizePosition.CheckedChanged
        If _loadingSource Then Return
        UpdateSourceControlState()
    End Sub

    ''' <summary>Keeps the three randomize "amount" fields' Maximum in step with the single-source values they are
    ''' added to (see UpdateRandomizeLimits) whenever one of those values changes - not guarded by _loadingSource,
    ''' since ShowSourceInControls also relies on this to be current before it sets the amount fields' Value
    ''' (though it additionally calls UpdateRandomizeLimits directly itself, since a value that happens to already
    ''' match a control's current Value does not raise ValueChanged).</summary>
    Private Sub SourceRandomizeLimitInput_ValueChanged(sender As Object, e As EventArgs) Handles numFrequency.ValueChanged,
        numOffStateCycles.ValueChanged, numArraySizeX.ValueChanged, numArraySizeY.ValueChanged,
        numSpacingSizeX.ValueChanged, numSpacingSizeY.ValueChanged
        UpdateRandomizeLimits()
    End Sub

    ''' <summary>
    ''' Caps how large a "randomize amount" can be set to, so GenerateElements' Vinherited +/- amount (SourceSettings)
    ''' can never be forced to clip against a hard limit and so end up biased instead of a true symmetric spread:
    '''   - frequency (numRandomizeFrequency): the source's Level must stay within 1-255, so the amount cannot
    '''     exceed the smaller of (Frequency - 1) and (255 - Frequency).
    '''   - off-width cycles (numRandomizeOffWidth): GenerateElements floors the result at 1 cycle, so the amount
    '''     cannot exceed (OffStateCycles - 1).
    '''   - position (numRandomizePosition): position has no natural upper/lower bound the way frequency and
    '''     off-width do, so instead the amount is capped at half the array's own spacing along whichever axis
    '''     (or axes) actually separate elements (ArraySizeX/Y > 1) - a randomized element's jitter can then never
    '''     overshoot as far as a neighboring (unrandomized) grid position. An array that is only one element wide
    '''     in both directions (ArraySizeX = ArraySizeY = 1, no neighbor to protect against) is left unbounded.
    ''' Phase (NumRandomizePhase) is not limited here: its result is already clamped to -180..180 in GenerateElements,
    ''' and since +/-180 degrees is the same angle either way, that clipping does not bias the result the way it
    ''' would for frequency or off-width.
    ''' Lowering a Maximum below a field's current Value clamps that Value down to it automatically (standard
    ''' NumericUpDown behavior), so the amount fields never need to be set here, only their Maximum.
    ''' </summary>
    Private Sub UpdateRandomizeLimits()
        Dim frequency As Integer = CInt(numFrequency.Value)
        numRandomizeFrequency.Maximum = CDec(Math.Max(0, Math.Min(frequency - 1, 255 - frequency)))

        Dim offWidth As Integer = CInt(numOffStateCycles.Value)
        numRandomizeOffWidth.Maximum = CDec(Math.Max(0, offWidth - 1))

        Dim cols As Integer = CInt(numArraySizeX.Value)
        Dim rows As Integer = CInt(numArraySizeY.Value)
        If cols <= 1 AndAlso rows <= 1 Then
            numRandomizePosition.Maximum = CDec(SceneSettings.MaxCanvasSize)
        Else
            ' the smallest spacing among the axes that actually separate elements (Count > 1), without relying on
            ' System.Linq (not imported by this file) for a two/one-element Min()
            Dim relevantSpacing As New List(Of Integer)()
            If cols > 1 Then relevantSpacing.Add(CInt(numSpacingSizeX.Value))
            If rows > 1 Then relevantSpacing.Add(CInt(numSpacingSizeY.Value))
            Dim minSpacing As Integer = relevantSpacing(0)
            For i As Integer = 1 To relevantSpacing.Count - 1
                If relevantSpacing(i) < minSpacing Then minSpacing = relevantSpacing(i)
            Next
            numRandomizePosition.Maximum = CDec(Math.Max(0, minSpacing \ 2))
        End If
    End Sub

    ''' <summary>
    ''' numArraySizeX/numArraySizeY must stay odd: an even-sized array has its centre between two pixels instead
    ''' of on one, which (especially at small spacings) places the whole array off-grid in an irregular way. The
    ''' spinner buttons can never produce an even value (Minimum/Maximum are both odd and Increment is 2 - see
    ''' InitializeSourceControls), but typing a value directly can, so this catches that on Validating (the point
    ''' where a typed value is finalized - on Tab/click-away, or on Enter via the global NumericUpDown_KeyDown
    ''' hook that moves focus off the control) and bumps it up by one, with a warning so the change is not a
    ''' silent surprise. Not guarded by _loadingSource: Validating only fires from an actual focus change, never
    ''' from a plain Value assignment in code (e.g. ShowSourceInControls loading a project), so it can only ever
    ''' be the user's own keyboard entry.
    ''' </summary>
    Private Sub ArraySize_Validating(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles numArraySizeX.Validating, numArraySizeY.Validating
        Dim box As NumericUpDown = DirectCast(sender, NumericUpDown)
        Dim value As Integer = CInt(box.Value)
        If value Mod 2 <> 0 Then Return

        Dim corrected As Integer = Math.Min(CInt(box.Maximum), value + 1)
        box.Value = CDec(corrected)
        MessageBox.Show(Me, "Use uneven numbers only - the array size has been increased to " &
                        corrected.ToString(CultureInfo.InvariantCulture) & ".",
                        "Array size", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub SourceTabChanged(sender As Object, e As EventArgs) Handles tabMainFunctions.SelectedIndexChanged
        EndSourcePick()
        If tabMainFunctions.SelectedTab Is tabSourceList Then
            AutoLoadSceneImageIfEmpty(_picSource)
            SyncSourceListSelectionToEditor()
        End If
    End Sub

    ''' <summary>Makes lstSources' highlighted item and grpSourceDetails agree with the source actually loaded in
    ''' the editor (_editing/_currentSourceIndex - the last source that was selected or edited) whenever the tab
    ''' is opened. Without this, coming back from another tab could show some list item highlighted (left over
    ''' from before, or the list's own default) while the detail controls still showed a different source, or one
    ''' that no longer matched the highlighted entry at all.</summary>
    Private Sub SyncSourceListSelectionToEditor()
        _syncingSourceList = True
        Try
            If _currentSourceIndex >= 0 AndAlso _currentSourceIndex < lstSources.Items.Count Then
                lstSources.SelectedIndex = _currentSourceIndex
            Else
                lstSources.SelectedIndex = -1
            End If
        Finally
            _syncingSourceList = False
        End Try
        ShowSourceInControls(_editing)
    End Sub

    ' ---------------------------------------------------------------- picking the position in the picture

    Private Sub PickSourcePosition_Click(sender As Object, e As EventArgs) Handles btnPickSourcePosition.Click
        If _sourcePicking Then
            EndSourcePick()
        Else
            StartSourcePick()
        End If
    End Sub

    Private Sub StartSourcePick()
        If _picSource.Image Is Nothing Then EnsureSourcePicture()
        If _picSource.Image Is Nothing Then
            MessageBox.Show(Me, "There is no picture to pick from yet, and the canvas size is not known either" & vbCrLf &
                            "(choose a scene image, or a user defined width and height, on the Configuration tab first).",
                            "Source position", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        _sourcePicking = True
        _picSource.Cursor = Cursors.Cross
        btnPickSourcePosition.Text = "Cancel"
        ToolStripStatusLabel1.Text = "Click in the picture for the position of the source (press Cancel to stop picking)."
    End Sub

    ''' <summary>
    ''' When there is no picture yet to pick from (no simulation has run), makes a plain black one at the canvas size
    ''' that the Configuration tab currently specifies (the scene image's size, or the user defined width/height), so
    ''' a source position can still be picked before ever running a simulation. Replaced by the real picture as soon
    ''' as one is copied over from the Preview tab.
    ''' </summary>
    Private Sub EnsureSourcePicture()
        Dim canvas As Size = EffectiveCanvas()
        If canvas.Width <= 0 OrElse canvas.Height <= 0 Then Return

        Dim placeholder As New Bitmap(canvas.Width, canvas.Height)
        Using g As Graphics = Graphics.FromImage(placeholder)
            g.Clear(Color.Black)
        End Using
        _picSource.Image = placeholder
    End Sub

    Private Sub EndSourcePick()
        If Not _sourcePicking Then Return
        _sourcePicking = False
        btnPickSourcePosition.Text = _pickSourceButtonText
        If _picSource IsNot Nothing Then _picSource.Cursor = Cursors.Default
        ToolStripStatusLabel1.Text = ""
    End Sub

    Private Sub SourcePicture_MouseClick(sender As Object, e As MouseEventArgs)
        If Not _sourcePicking OrElse e.Button <> MouseButtons.Left Then Return

        Dim x As Integer
        Dim y As Integer
        If Not TryPictureToCanvas(_picSource, e.Location, x, y) Then Return

        numSourcePositionX.Value = ClampToRange(numSourcePositionX, x)
        numSourcePositionY.Value = ClampToRange(numSourcePositionY, y)
        ' SourcePosition_ValueChanged (below) updates _editing and invalidates the picture even when the values above
        ' did not actually change (e.g. clicking back on the same pixel), so do it here too, explicitly
        If Not _loadingSource Then
            _editing.PosX = x
            _editing.PosY = y
            _editing.HasPosition = True
            _picSource.Invalidate()
        End If
        EndSourcePick()

        ' picking a position for a source that is already in the list commits the editor's current data (the
        ' picked position and anything else changed since the last save) straight to it, the same as pressing Save
        ' source - so the pick is never left stale waiting for a separate save. A brand new/copied source (not
        ' selected in the list yet) is left as before: still just a live, unsaved position until Save source is
        ' pressed, since adding it here could otherwise silently add an incomplete/unnamed source to the list.
        If _currentSourceIndex >= 0 Then CommitSourceEdits()
    End Sub

    ''' <summary>Keeps the live marker in the picture in sync while typing a new position, same as picking one does.</summary>
    Private Sub SourcePosition_ValueChanged(sender As Object, e As EventArgs) Handles numSourcePositionX.ValueChanged, numSourcePositionY.ValueChanged
        If _loadingSource Then Return
        _editing.PosX = CInt(numSourcePositionX.Value)
        _editing.PosY = CInt(numSourcePositionY.Value)
        _editing.HasPosition = True
        If _picSource IsNot Nothing Then _picSource.Invalidate()
    End Sub

    ' ---------------------------------------------------------------- drawing over the picture

    ''' <summary>A marker for every saved source, and for the source being edited when it is not saved yet.</summary>
    Private Sub SourcePicture_Paint(sender As Object, e As PaintEventArgs)
        If _picSource.Image Is Nothing Then Return

        Dim canvas As Size = EffectiveCanvas()
        If canvas.Width <= 0 Then canvas = _picSource.Image.Size
        Dim area As RectangleF = ZoomedImageRectangle(_picSource)
        If area.Width <= 0 Then Return
        Dim sx As Single = area.Width / canvas.Width
        Dim sy As Single = area.Height / canvas.Height

        For i As Integer = 0 To _sources.Count - 1
            Dim isCurrent As Boolean = (i = _currentSourceIndex)
            ' the source being edited is shown at its live (not yet saved) position, so moving it in the picture or
            ' the X/Y boxes is reflected immediately instead of only after Save source
            Dim shown As SourceSettings = If(isCurrent, _editing, _sources(i))
            If shown.HasPosition Then DrawSourceMarker(e.Graphics, shown, area, sx, sy, isCurrent)
        Next

        ' the source being created or copied (not in _sources yet) is shown too, so its position can be seen while picking -
        ' but only once it actually has one (Pick or a typed X/Y), never at the unset default
        If _currentSourceIndex < 0 AndAlso _editing.HasPosition Then DrawSourceMarker(e.Graphics, _editing, area, sx, sy, True)
    End Sub

    Private Shared Sub DrawSourceMarker(g As Graphics, s As SourceSettings, area As RectangleF, sx As Single, sy As Single, isCurrent As Boolean)
        Dim color As Color = If(isCurrent, Color.FromArgb(230, 255, 208, 64), Color.FromArgb(200, 64, 180, 255))
        Dim radius As Single = If(isCurrent, 5.0F, 4.0F)

        Dim px As Single = area.X + s.PosX * sx
        Dim py As Single = area.Y + s.PosY * sy
        Using pen As New Pen(color, If(isCurrent, 2.0F, 1.5F))
            g.DrawEllipse(pen, px - radius, py - radius, radius * 2.0F, radius * 2.0F)
            g.DrawLine(pen, px - radius - 3.0F, py, px + radius + 3.0F, py)
            g.DrawLine(pen, px, py - radius - 3.0F, px, py + radius + 3.0F)
        End Using
        Using font As New Font("Segoe UI", 11.0F, FontStyle.Bold, GraphicsUnit.Pixel)
            Using brush As New SolidBrush(color)
                g.DrawString(s.Name, font, brush, px + radius + 4.0F, py - radius - 2.0F)
            End Using
        End Using
    End Sub

    ' ---------------------------------------------------------------- source configuration preview (picPreviewSource)

    ''' <summary>
    ''' Shows the layout of the source currently loaded in the editor (_editing) as white dots on a black background.
    ''' The source picture above is at canvas scale, so a source - especially a composite source's individual
    ''' elements - can be too small or too close together to make out there. chkAutoRescaleSourceOn (checked by
    ''' default) picks which of two views this shows instead:
    '''   - checked: zooms in on just the elements themselves (centered, scaled to fill the box, each shown as a
    '''     several-pixel dot so it stays visible), so their relative arrangement is always visible regardless of
    '''     where the source sits on the canvas or how tightly its array is spaced.
    '''   - unchecked: the true, unscaled size - one simulation pixel is one screen pixel, and each element is drawn
    '''     as exactly that: a single pixel, the same size it actually is in the simulation - so a composite source's
    '''     real physical footprint (how many actual simulation pixels its array spans) can be judged directly.
    ''' Either way it shows _editing.Elements - the last generated list - so it is blank for a brand new/copied
    ''' source until it is generated by Save source, and otherwise reflects whatever was last saved (or, for the
    ''' source just saved, just regenerated). Kept in sync by a picPreviewSource.Invalidate() call everywhere
    ''' _editing/the currently edited source changes (loading a different one, New/Copy/Delete, and Save source) -
    ''' the same places _picSource.Invalidate() is called for the position marker above - and by
    ''' chkAutoRescaleSourceOn_CheckedChanged when the view itself is switched.
    ''' </summary>
    Private Sub picPreviewSource_Paint(sender As Object, e As PaintEventArgs) Handles picPreviewSource.Paint
        e.Graphics.Clear(Color.Black)

        Dim elements As List(Of SourceElement) = If(_editing IsNot Nothing, _editing.Elements, Nothing)
        If elements Is Nothing OrElse elements.Count = 0 Then Return

        Dim minX As Integer = elements(0).PosX
        Dim maxX As Integer = elements(0).PosX
        Dim minY As Integer = elements(0).PosY
        Dim maxY As Integer = elements(0).PosY
        For Each el As SourceElement In elements
            If el.PosX < minX Then minX = el.PosX
            If el.PosX > maxX Then maxX = el.PosX
            If el.PosY < minY Then minY = el.PosY
            If el.PosY > maxY Then maxY = el.PosY
        Next

        Dim boxW As Single = picPreviewSource.ClientSize.Width
        Dim boxH As Single = picPreviewSource.ClientSize.Height
        If boxW <= 0.0F OrElse boxH <= 0.0F Then Return

        Dim centerX As Single = (minX + maxX) / 2.0F
        Dim centerY As Single = (minY + maxY) / 2.0F

        Dim autoRescale As Boolean = (chkAutoRescaleSourceOn Is Nothing) OrElse chkAutoRescaleSourceOn.Checked
        Dim scale As Single
        If autoRescale Then
            ' a single element (or several coinciding at the same spot) has no span to scale to - fall back to a
            ' fixed half-extent instead of dividing by zero, so it still just shows one dot in the middle of the box
            Dim halfExtent As Single = Math.Max(maxX - minX, maxY - minY) / 2.0F
            If halfExtent <= 0.0F Then halfExtent = 1.0F

            Const marginFraction As Single = 0.15F ' keeps outer dots from touching/clipping at the edge of the box
            Dim usable As Single = Math.Min(boxW, boxH) * (1.0F - 2.0F * marginFraction)
            scale = usable / (halfExtent * 2.0F)
        Else
            scale = 1.0F ' true size: one simulation pixel is one screen pixel
        End If

        Const dotRadius As Single = 3.0F
        Using brush As New SolidBrush(Color.White)
            For Each el As SourceElement In elements
                Dim px As Single = boxW / 2.0F + (el.PosX - centerX) * scale
                Dim py As Single = boxH / 2.0F + (el.PosY - centerY) * scale
                If autoRescale Then
                    e.Graphics.FillEllipse(brush, px - dotRadius, py - dotRadius, dotRadius * 2.0F, dotRadius * 2.0F)
                Else
                    e.Graphics.FillRectangle(brush, px, py, 1.0F, 1.0F)
                End If
            Next
        End Using
    End Sub

    ''' <summary>Redraws at the (centered, rescaled) size whenever the box itself is resized - same idea as any other
    ''' owner-drawn control that scales to its client area.</summary>
    Private Sub picPreviewSource_SizeChanged(sender As Object, e As EventArgs) Handles picPreviewSource.SizeChanged
        picPreviewSource.Invalidate()
    End Sub

    ''' <summary>Switches picPreviewSource between the auto-rescaled and true-size views (see picPreviewSource_Paint).</summary>
    Private Sub chkAutoRescaleSourceOn_CheckedChanged(sender As Object, e As EventArgs) Handles chkAutoRescaleSourceOn.CheckedChanged
        picPreviewSource.Invalidate()
    End Sub

End Class
