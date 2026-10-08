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
Imports System.Globalization

' ------------------------------------------------------------------ probes (Probes tab)
'
' A probe measures the field (amplitude) or the intensity while the simulation runs and shows the result in a little
' graph box in the field or intensity picture:
'   Point (A)      the value at A against the simulation step
'   Line (A-B)     the values along the line A-B (position on the line on X) at the current step; it changes every step
' The settings of the probe are kept in _probes (see ProbeSettings) and stored in the "probes" section of the project
' file. Each probe has its own "On" checkbox (ProbeSettings.Active): when off, the probe is hidden - its marker,
' graph box and burned-in overlay are all skipped - but it keeps being sampled, so switching it back on shows its
' recorded history rather than starting from a blank graph.
'
' There are two independent probes, Probe 1 and Probe 2, each with its own group of controls (grpProbe1/grpProbe2).
' _probeUi holds one ProbeControlSet per group, so the code that reads/writes a probe's settings and the events of
' its controls can be shared between the two groups instead of being written out twice.
'
' The Probes tab shows a copy of a simulation picture (Preview tab, "Copy Image..." buttons), shared by both probes.
' Each group has its own Pick button: it lets you click in that picture for that probe's position A, for a line
' position B, and the top left corner of that probe's graph box.
Partial Public Class Form1

    ' the number of probe groups the controls support (Probe 1, Probe 2)
    Private Const ProbeCount As Integer = 2

    ' the probes of the project, always exactly ProbeCount entries, one per group of controls
    Private _probes As List(Of ProbeSettings) = NewProbeList()

    ' the controls of each probe group; built once in InitializeProbeControls
    Private _probeUi As ProbeControlSet()

    ' True while the code itself fills the probe controls (their change events are then ignored)
    Private _loadingProbe As Boolean

    ' picking positions in the picture: 0 = not picking, 1 = position A is next, 2 = position B is next (line only),
    ' 3 = the top left corner of the graph box is next. _pickIndex is the probe (into _probes/_probeUi) being picked.
    Private _pickStep As Integer
    Private _pickIndex As Integer
    Private _pickHover As Point?

    ' the probes, the canvas and the number of steps of the run whose pictures are shown in the previews
    Private _runProbes As List(Of ProbeSettings)
    Private _runCanvas As Size
    Private _runSteps As Integer
    Private ReadOnly _probeTraces As New List(Of ProbeTrace)()

    ''' <summary>
    ''' The control set for a probe index, or Nothing while the form is still being constructed: InitializeComponent
    ''' (which runs before InitializeProbeControls) sets some control values away from their .NET default, such as
    ''' numRunningWindow's 500, and that alone fires the control's change event immediately, before _probeUi exists.
    ''' The event handlers below go through this instead of indexing _probeUi directly, so that early firing is a
    ''' harmless no-op instead of a NullReferenceException.
    ''' </summary>
    Private Function ProbeUi(index As Integer) As ProbeControlSet
        If _probeUi Is Nothing Then Return Nothing
        Return _probeUi(index)
    End Function

    Private Shared Function NewProbeList() As List(Of ProbeSettings)
        Dim list As New List(Of ProbeSettings)()
        For i As Integer = 0 To ProbeCount - 1
            list.Add(New ProbeSettings())
        Next
        Return list
    End Function

    ''' <summary>Keeps _probes at exactly ProbeCount entries: pads with new (default) probes when a saved project has
    ''' fewer (from before Probe 2 existed), trims extras when it somehow has more.</summary>
    Private Sub NormalizeProbeCount()
        While _probes.Count < ProbeCount
            _probes.Add(New ProbeSettings())
        End While
        While _probes.Count > ProbeCount
            _probes.RemoveAt(_probes.Count - 1)
        End While
    End Sub

    ''' <summary>The controls of one probe's group box (Probe 1 or Probe 2), so the code that reads and writes a
    ''' probe's settings, and the events of its controls, can be shared between the two groups.</summary>
    Private NotInheritable Class ProbeControlSet
        Public ReadOnly Index As Integer
        Public ReadOnly Title As String
        Public ReadOnly ChkOn As CheckBox
        Public ReadOnly CmbPlaceOn As ComboBox
        Public ReadOnly CmbMeasurement As ComboBox
        Public ReadOnly CmbProbeType As ComboBox
        Public ReadOnly CmbBoxSize As ComboBox
        Public ReadOnly NumAPosX As NumericUpDown
        Public ReadOnly NumAPosY As NumericUpDown
        Public ReadOnly NumBPosX As NumericUpDown
        Public ReadOnly NumBPosY As NumericUpDown
        Public ReadOnly ButPick As Button
        Public ReadOnly RadRunning As RadioButton
        Public ReadOnly RadFull As RadioButton
        Public ReadOnly ChkAutoX As CheckBox
        Public ReadOnly ChkAutoY As CheckBox
        Public ReadOnly TxtMinY As TextBox
        Public ReadOnly TxtMaxY As TextBox
        Public ReadOnly ChkStartAfter As CheckBox
        Public ReadOnly TxtStartAfter As TextBox
        Public ReadOnly NumRunningWindow As NumericUpDown

        ''' <summary>The controls that only make sense for a line probe (position B and its labels); disabled for a point probe.</summary>
        Public ReadOnly BOnlyControls As Control()

        ''' <summary>The Pick button's own text ("Pick"), so it can be restored after showing "Cancel" while picking.</summary>
        Public ReadOnly PickButtonText As String

        Public Sub New(index As Integer, title As String, chkOn As CheckBox, cmbPlaceOn As ComboBox, cmbMeasurement As ComboBox,
                       cmbProbeType As ComboBox, cmbBoxSize As ComboBox, numAPosX As NumericUpDown, numAPosY As NumericUpDown,
                       numBPosX As NumericUpDown, numBPosY As NumericUpDown, butPick As Button, radRunning As RadioButton,
                       radFull As RadioButton, chkAutoX As CheckBox, chkAutoY As CheckBox, txtMinY As TextBox, txtMaxY As TextBox,
                       chkStartAfter As CheckBox, txtStartAfter As TextBox, numRunningWindow As NumericUpDown, bOnlyControls As Control())
            Me.Index = index
            Me.Title = title
            Me.ChkOn = chkOn
            Me.CmbPlaceOn = cmbPlaceOn
            Me.CmbMeasurement = cmbMeasurement
            Me.CmbProbeType = cmbProbeType
            Me.CmbBoxSize = cmbBoxSize
            Me.NumAPosX = numAPosX
            Me.NumAPosY = numAPosY
            Me.NumBPosX = numBPosX
            Me.NumBPosY = numBPosY
            Me.ButPick = butPick
            Me.RadRunning = radRunning
            Me.RadFull = radFull
            Me.ChkAutoX = chkAutoX
            Me.ChkAutoY = chkAutoY
            Me.TxtMinY = txtMinY
            Me.TxtMaxY = txtMaxY
            Me.ChkStartAfter = chkStartAfter
            Me.TxtStartAfter = txtStartAfter
            Me.NumRunningWindow = numRunningWindow
            Me.BOnlyControls = bOnlyControls
            Me.PickButtonText = butPick.Text
        End Sub
    End Class

    ' ---------------------------------------------------------------- setting up

    Private Sub InitializeProbeControls()
        _probeUi = New ProbeControlSet() {
            New ProbeControlSet(index:=0, title:="Probe 1", chkOn:=chkProbeOn1, cmbPlaceOn:=cmbPlaceOn, cmbMeasurement:=cmbMeasurement,
                                cmbProbeType:=cmbProbeType, cmbBoxSize:=cmbBoxSize, numAPosX:=numAPosX, numAPosY:=numAPosY,
                                numBPosX:=numBPosX, numBPosY:=numBPosY, butPick:=butPicPositions, radRunning:=radRunningScale,
                                radFull:=radFullScale, chkAutoX:=chkAutoScaleX, chkAutoY:=chkAutoScaleY, txtMinY:=txtMinScaleY,
                                txtMaxY:=txtMaxScaleY, chkStartAfter:=chkStartAfterStep, txtStartAfter:=txtStartAfterStep,
                                numRunningWindow:=numRunningWindow, bOnlyControls:=New Control() {numBPosX, numBPosY, Label14, Label15, Label16}),
            New ProbeControlSet(index:=1, title:="Probe 2", chkOn:=chkProbeOn2, cmbPlaceOn:=cmbPlaceOn2, cmbMeasurement:=cmbMeasurement2,
                                cmbProbeType:=cmbProbeType2, cmbBoxSize:=cmbBoxSize2, numAPosX:=numAPosX2, numAPosY:=numAPosY2,
                                numBPosX:=numBPosX2, numBPosY:=numBPosY2, butPick:=butPicPositions2, radRunning:=radRunningScale2,
                                radFull:=radFullScale2, chkAutoX:=chkAutoScaleX2, chkAutoY:=chkAutoScaleY2, txtMinY:=txtMinScaleY2,
                                txtMaxY:=txtMaxScaleY2, chkStartAfter:=chkStartAfterStep2, txtStartAfter:=txtStartAfterStep2,
                                numRunningWindow:=numRunningWindow2, bOnlyControls:=New Control() {numBPosX2, numBPosY2, Label28, Label27, Label25})
        }

        For Each ui As ProbeControlSet In _probeUi
            For Each combo As ComboBox In New ComboBox() {ui.CmbPlaceOn, ui.CmbMeasurement, ui.CmbProbeType, ui.CmbBoxSize}
                combo.DropDownStyle = ComboBoxStyle.DropDownList
            Next

            ' the choices are made here (not in the designer), in the order of the enumerations of ProbeSettings
            ui.CmbPlaceOn.Items.Clear()
            ui.CmbPlaceOn.Items.AddRange(New Object() {"Field", "Intensity"})
            ui.CmbProbeType.Items.Clear()
            ui.CmbProbeType.Items.AddRange(New Object() {"Point (A)", "Line (A-B)"})
            ui.CmbBoxSize.Items.Clear()
            ui.CmbBoxSize.Items.AddRange(New Object() {"Tiny (200 x 100)", "Small (300 x 150)", "Medium (400 x 200)", "Large (500 x 250)", "Extra Large (700 x 350)"})

            For Each box As NumericUpDown In New NumericUpDown() {ui.NumAPosX, ui.NumAPosY, ui.NumBPosX, ui.NumBPosY}
                box.DecimalPlaces = 0
                box.Minimum = 0D
                box.Maximum = CDec(SceneSettings.MaxCanvasSize - 1)
            Next

            ui.NumRunningWindow.DecimalPlaces = 0
            ui.NumRunningWindow.Minimum = 10D
            ui.NumRunningWindow.Maximum = CDec(ProbeSettings.MaxRunningWindow)
        Next

        ' the graphs and markers are drawn over the pictures (the pictures themselves stay clean)
        AddHandler _picField.Paint, AddressOf PreviewPicture_Paint
        AddHandler _picIntensity.Paint, AddressOf PreviewPicture_Paint
        AddHandler _picProbe.Paint, AddressOf ProbePicture_Paint
        AddHandler _picProbe.MouseClick, AddressOf ProbePicture_MouseClick
        AddHandler _picProbe.MouseMove, AddressOf ProbePicture_MouseMove
        AddHandler _picProbe.MouseLeave, AddressOf ProbePicture_MouseLeave

        ShowProbesInControls()
    End Sub

    ''' <summary>Copies the values of both probes into their controls.</summary>
    Private Sub ShowProbesInControls()
        EndPick()
        For Each ui As ProbeControlSet In _probeUi
            ShowProbeInControls(ui)
        Next
    End Sub

    ''' <summary>Copies the values of one probe into its group of controls.</summary>
    Private Sub ShowProbeInControls(ui As ProbeControlSet)
        Dim p As ProbeSettings = _probes(ui.Index)
        p.Normalize()

        _loadingProbe = True
        Try
            SetProbeRanges(ui)

            ui.ChkOn.Checked = p.Active
            ui.CmbPlaceOn.SelectedIndex = CInt(p.PlaceOn)
            FillMeasurementCombo(ui, p)
            ui.CmbProbeType.SelectedIndex = CInt(p.Kind)
            ui.CmbBoxSize.SelectedIndex = CInt(p.BoxSize)

            ui.NumAPosX.Value = ClampToRange(ui.NumAPosX, p.PosAX)
            ui.NumAPosY.Value = ClampToRange(ui.NumAPosY, p.PosAY)
            ui.NumBPosX.Value = ClampToRange(ui.NumBPosX, p.PosBX)
            ui.NumBPosY.Value = ClampToRange(ui.NumBPosY, p.PosBY)

            ui.RadRunning.Checked = (p.ScaleMode = ProbeScaleMode.Running)
            ui.RadFull.Checked = (p.ScaleMode = ProbeScaleMode.Full)
            ui.ChkAutoX.Checked = p.AutoScaleX
            ui.ChkAutoY.Checked = p.AutoScaleY
            ui.TxtMinY.Text = p.YMin.ToString("G6", CultureInfo.InvariantCulture)
            ui.TxtMaxY.Text = p.YMax.ToString("G6", CultureInfo.InvariantCulture)
            ui.ChkStartAfter.Checked = p.UseStartAfterStep
            ui.TxtStartAfter.Text = p.StartAfterStep.ToString(CultureInfo.InvariantCulture)
            ui.NumRunningWindow.Value = ClampToRange(ui.NumRunningWindow, p.RunningWindow)
        Finally
            _loadingProbe = False
        End Try

        UpdateProbeControlState(ui)
        InvalidateProbePictures()
    End Sub

    ''' <summary>The measurements that fit the picture: only the amplitude on the field, intensity and integrated intensity on the intensity.</summary>
    Private Sub FillMeasurementCombo(ui As ProbeControlSet, p As ProbeSettings)
        ui.CmbMeasurement.Items.Clear()
        If p.PlaceOn = ProbePlaceOn.Field Then
            ui.CmbMeasurement.Items.Add("Amplitude")
            ui.CmbMeasurement.SelectedIndex = 0
        Else
            ui.CmbMeasurement.Items.Add("Intensity")
            ui.CmbMeasurement.Items.Add("Integrated intensity")
            ui.CmbMeasurement.SelectedIndex = If(p.Measurement = ProbeMeasurement.IntegratedIntensity, 1, 0)
        End If
    End Sub

    Private Shared Function MeasurementFromIndex(placeOn As ProbePlaceOn, index As Integer) As ProbeMeasurement
        If placeOn = ProbePlaceOn.Field Then Return ProbeMeasurement.Amplitude
        Return If(index = 1, ProbeMeasurement.IntegratedIntensity, ProbeMeasurement.Intensity)
    End Function

    ''' <summary>
    ''' Position B is only used by a line; with a point only position A is available. The step axis (running or full
    ''' scale, automatic X, running window) is only used by a point: the X axis of a line is the line itself. The
    ''' manual Y range boxes are only usable when the Y axis is not scaled automatically, and the "start after step"
    ''' box only when that is ticked.
    ''' </summary>
    Private Sub UpdateProbeControlState(ui As ProbeControlSet)
        Dim isLine As Boolean = (_probes(ui.Index).Kind = ProbeKind.Line)
        For Each c As Control In ui.BOnlyControls
            c.Enabled = isLine
        Next
        For Each c As Control In New Control() {ui.RadRunning, ui.RadFull, ui.ChkAutoX, ui.NumRunningWindow}
            c.Enabled = Not isLine
        Next
        ui.TxtMinY.Enabled = Not ui.ChkAutoY.Checked
        ui.TxtMaxY.Enabled = Not ui.ChkAutoY.Checked
        ui.TxtStartAfter.Enabled = ui.ChkStartAfter.Checked
    End Sub

    ''' <summary>The positions of both probes can be as large as the canvas of the scene (when that is known).</summary>
    Private Sub SetProbeRanges()
        If _probeUi Is Nothing Then Return
        For Each ui As ProbeControlSet In _probeUi
            SetProbeRanges(ui)
        Next
    End Sub

    ''' <summary>The positions of one probe can be as large as the canvas of the scene (when that is known).</summary>
    Private Sub SetProbeRanges(ui As ProbeControlSet)
        Dim canvas As Size = EffectiveCanvas()
        Dim maxX As Decimal = CDec(If(canvas.Width > 0, canvas.Width - 1, SceneSettings.MaxCanvasSize - 1))
        Dim maxY As Decimal = CDec(If(canvas.Height > 0, canvas.Height - 1, SceneSettings.MaxCanvasSize - 1))

        Dim wasLoading As Boolean = _loadingProbe
        _loadingProbe = True
        Try
            ui.NumAPosX.Maximum = maxX
            ui.NumBPosX.Maximum = maxX
            ui.NumAPosY.Maximum = maxY
            ui.NumBPosY.Maximum = maxY
        Finally
            _loadingProbe = wasLoading
        End Try
    End Sub

    ''' <summary>Takes the values from one probe's controls into that probe.</summary>
    Private Sub ReadProbeControls(ui As ProbeControlSet)
        Dim p As ProbeSettings = _probes(ui.Index)

        p.Active = ui.ChkOn.Checked
        If ui.CmbPlaceOn.SelectedIndex >= 0 Then p.PlaceOn = CType(ui.CmbPlaceOn.SelectedIndex, ProbePlaceOn)
        If ui.CmbMeasurement.SelectedIndex >= 0 Then p.Measurement = MeasurementFromIndex(p.PlaceOn, ui.CmbMeasurement.SelectedIndex)
        If ui.CmbProbeType.SelectedIndex >= 0 Then p.Kind = CType(ui.CmbProbeType.SelectedIndex, ProbeKind)
        If ui.CmbBoxSize.SelectedIndex >= 0 Then p.BoxSize = CType(ui.CmbBoxSize.SelectedIndex, ProbeBoxSize)

        ui.NumAPosX.Validate()
        ui.NumAPosY.Validate()
        ui.NumBPosX.Validate()
        ui.NumBPosY.Validate()
        p.PosAX = CInt(ui.NumAPosX.Value)
        p.PosAY = CInt(ui.NumAPosY.Value)
        p.PosBX = CInt(ui.NumBPosX.Value)
        p.PosBY = CInt(ui.NumBPosY.Value)

        p.ScaleMode = If(ui.RadFull.Checked, ProbeScaleMode.Full, ProbeScaleMode.Running)
        p.AutoScaleX = ui.ChkAutoX.Checked
        p.AutoScaleY = ui.ChkAutoY.Checked

        If Not ui.ChkAutoY.Checked Then
            Dim yMin As Double
            Dim yMax As Double
            If Double.TryParse(ui.TxtMinY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, yMin) Then p.YMin = yMin
            If Double.TryParse(ui.TxtMaxY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, yMax) Then p.YMax = yMax
        End If

        p.UseStartAfterStep = ui.ChkStartAfter.Checked
        Dim startStep As Integer
        If Integer.TryParse(ui.TxtStartAfter.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, startStep) Then p.StartAfterStep = startStep

        ui.NumRunningWindow.Validate()
        p.RunningWindow = CInt(ui.NumRunningWindow.Value)

        p.Normalize()
    End Sub

    ' ---------------------------------------------------------------- events of the controls (Probe 1)

    Private Sub ProbeCombo_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbPlaceOn.SelectedIndexChanged,
        cmbMeasurement.SelectedIndexChanged, cmbProbeType.SelectedIndexChanged, cmbBoxSize.SelectedIndexChanged
        HandleProbeComboChanged(ProbeUi(0), sender)
    End Sub

    Private Sub ProbePosition_ValueChanged(sender As Object, e As EventArgs) Handles numAPosX.ValueChanged, numAPosY.ValueChanged,
        numBPosX.ValueChanged, numBPosY.ValueChanged
        HandleProbePositionChanged(ProbeUi(0))
    End Sub

    Private Sub ProbeOn_CheckedChanged(sender As Object, e As EventArgs) Handles chkProbeOn1.CheckedChanged
        HandleProbeOnChanged(ProbeUi(0))
    End Sub

    Private Sub ProbeTextBox_KeyDown(sender As Object, e As KeyEventArgs) Handles txtMinScaleY.KeyDown, txtMaxScaleY.KeyDown,
        txtStartAfterStep.KeyDown
        HandleProbeTextBoxKeyDown(ProbeUi(0), e)
    End Sub

    Private Sub ProbeScaleMode_CheckedChanged(sender As Object, e As EventArgs) Handles radRunningScale.CheckedChanged, radFullScale.CheckedChanged
        HandleProbeScaleModeChanged(ProbeUi(0))
    End Sub

    Private Sub ProbeAutoScale_CheckedChanged(sender As Object, e As EventArgs) Handles chkAutoScaleX.CheckedChanged, chkAutoScaleY.CheckedChanged
        HandleProbeAutoScaleChanged(ProbeUi(0))
    End Sub

    Private Sub ProbeYRange_Validated(sender As Object, e As EventArgs) Handles txtMinScaleY.Validated, txtMaxScaleY.Validated
        HandleProbeControlApplied(ProbeUi(0))
    End Sub

    Private Sub ProbeStartAfterStep_CheckedChanged(sender As Object, e As EventArgs) Handles chkStartAfterStep.CheckedChanged
        HandleProbeStartAfterStepChanged(ProbeUi(0))
    End Sub

    Private Sub ProbeStartAfterStep_Validated(sender As Object, e As EventArgs) Handles txtStartAfterStep.Validated
        HandleProbeControlApplied(ProbeUi(0))
    End Sub

    Private Sub ProbeRunningWindow_ValueChanged(sender As Object, e As EventArgs) Handles numRunningWindow.ValueChanged
        HandleProbeControlApplied(ProbeUi(0))
    End Sub

    ' ---------------------------------------------------------------- events of the controls (Probe 2), same handling as Probe 1

    Private Sub ProbeCombo2_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbPlaceOn2.SelectedIndexChanged,
        cmbMeasurement2.SelectedIndexChanged, cmbProbeType2.SelectedIndexChanged, cmbBoxSize2.SelectedIndexChanged
        HandleProbeComboChanged(ProbeUi(1), sender)
    End Sub

    Private Sub ProbePosition2_ValueChanged(sender As Object, e As EventArgs) Handles numAPosX2.ValueChanged, numAPosY2.ValueChanged,
        numBPosX2.ValueChanged, numBPosY2.ValueChanged
        HandleProbePositionChanged(ProbeUi(1))
    End Sub

    Private Sub ProbeOn2_CheckedChanged(sender As Object, e As EventArgs) Handles chkProbeOn2.CheckedChanged
        HandleProbeOnChanged(ProbeUi(1))
    End Sub

    Private Sub ProbeTextBox2_KeyDown(sender As Object, e As KeyEventArgs) Handles txtMinScaleY2.KeyDown, txtMaxScaleY2.KeyDown,
        txtStartAfterStep2.KeyDown
        HandleProbeTextBoxKeyDown(ProbeUi(1), e)
    End Sub

    Private Sub ProbeScaleMode2_CheckedChanged(sender As Object, e As EventArgs) Handles radRunningScale2.CheckedChanged, radFullScale2.CheckedChanged
        HandleProbeScaleModeChanged(ProbeUi(1))
    End Sub

    Private Sub ProbeAutoScale2_CheckedChanged(sender As Object, e As EventArgs) Handles chkAutoScaleX2.CheckedChanged, chkAutoScaleY2.CheckedChanged
        HandleProbeAutoScaleChanged(ProbeUi(1))
    End Sub

    Private Sub ProbeYRange2_Validated(sender As Object, e As EventArgs) Handles txtMinScaleY2.Validated, txtMaxScaleY2.Validated
        HandleProbeControlApplied(ProbeUi(1))
    End Sub

    Private Sub ProbeStartAfterStep2_CheckedChanged(sender As Object, e As EventArgs) Handles chkStartAfterStep2.CheckedChanged
        HandleProbeStartAfterStepChanged(ProbeUi(1))
    End Sub

    Private Sub ProbeStartAfterStep2_Validated(sender As Object, e As EventArgs) Handles txtStartAfterStep2.Validated
        HandleProbeControlApplied(ProbeUi(1))
    End Sub

    Private Sub ProbeRunningWindow2_ValueChanged(sender As Object, e As EventArgs) Handles numRunningWindow2.ValueChanged
        HandleProbeControlApplied(ProbeUi(1))
    End Sub

    ' ---------------------------------------------------------------- shared handling of one probe (given its control set)

    Private Sub HandleProbeComboChanged(ui As ProbeControlSet, sender As Object)
        If ui Is Nothing OrElse _loadingProbe Then Return
        If sender Is ui.CmbProbeType AndAlso _pickStep <> 0 AndAlso _pickIndex = ui.Index Then EndPick()      ' the number of clicks depends on the type; only cancel a pick that belongs to this probe

        If sender Is ui.CmbPlaceOn AndAlso ui.CmbPlaceOn.SelectedIndex >= 0 Then
            ' the measurements that are possible depend on the picture
            Dim p As ProbeSettings = _probes(ui.Index)
            p.PlaceOn = CType(ui.CmbPlaceOn.SelectedIndex, ProbePlaceOn)
            p.Normalize()
            _loadingProbe = True
            Try
                FillMeasurementCombo(ui, p)
            Finally
                _loadingProbe = False
            End Try
        End If

        ReadProbeControls(ui)
        UpdateProbeControlState(ui)
        ProbeSettingsChanged()
    End Sub

    Private Sub HandleProbePositionChanged(ui As ProbeControlSet)
        If ui Is Nothing OrElse _loadingProbe Then Return
        Dim p As ProbeSettings = _probes(ui.Index)
        p.PosAX = CInt(ui.NumAPosX.Value)
        p.PosAY = CInt(ui.NumAPosY.Value)
        p.PosBX = CInt(ui.NumBPosX.Value)
        p.PosBY = CInt(ui.NumBPosY.Value)
        ProbeSettingsChanged()
    End Sub

    Private Sub HandleProbeOnChanged(ui As ProbeControlSet)
        If ui Is Nothing OrElse _loadingProbe Then Return
        _probes(ui.Index).Active = ui.ChkOn.Checked
        ProbeSettingsChanged()
    End Sub

    ''' <summary>
    ''' Enter in a probe text box applies the value right away (like losing focus does), instead of just beeping;
    ''' these are single line boxes, so Enter never inserts an actual line break.
    ''' </summary>
    Private Sub HandleProbeTextBoxKeyDown(ui As ProbeControlSet, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter Then Return
        e.SuppressKeyPress = True
        e.Handled = True
        If ui Is Nothing OrElse _loadingProbe Then Return
        ReadProbeControls(ui)
        ProbeSettingsChanged()
    End Sub

    Private Sub HandleProbeScaleModeChanged(ui As ProbeControlSet)
        If ui Is Nothing OrElse _loadingProbe Then Return
        _probes(ui.Index).ScaleMode = If(ui.RadFull.Checked, ProbeScaleMode.Full, ProbeScaleMode.Running)
        ProbeSettingsChanged()
    End Sub

    Private Sub HandleProbeAutoScaleChanged(ui As ProbeControlSet)
        If ui Is Nothing OrElse _loadingProbe Then Return
        Dim p As ProbeSettings = _probes(ui.Index)
        Dim wasAutoY As Boolean = p.AutoScaleY
        p.AutoScaleX = ui.ChkAutoX.Checked
        p.AutoScaleY = ui.ChkAutoY.Checked

        ' switching the automatic value axis off keeps the range that is shown now, ready to fine-tune by hand
        If wasAutoY AndAlso Not p.AutoScaleY Then
            FreezeValueRange(ui, p)
            _loadingProbe = True
            Try
                ui.TxtMinY.Text = p.YMin.ToString("G6", CultureInfo.InvariantCulture)
                ui.TxtMaxY.Text = p.YMax.ToString("G6", CultureInfo.InvariantCulture)
            Finally
                _loadingProbe = False
            End Try
        End If
        UpdateProbeControlState(ui)
        ProbeSettingsChanged()
    End Sub

    ''' <summary>The shared tail of the Validated/ValueChanged handlers: re-read this probe's controls and mark the change.</summary>
    Private Sub HandleProbeControlApplied(ui As ProbeControlSet)
        If ui Is Nothing OrElse _loadingProbe Then Return
        ReadProbeControls(ui)
        ProbeSettingsChanged()
    End Sub

    Private Sub HandleProbeStartAfterStepChanged(ui As ProbeControlSet)
        If ui Is Nothing OrElse _loadingProbe Then Return
        _probes(ui.Index).UseStartAfterStep = ui.ChkStartAfter.Checked
        UpdateProbeControlState(ui)
        ProbeSettingsChanged()
    End Sub

    ''' <summary>Makes the current value range of the graph the fixed range.</summary>
    Private Sub FreezeValueRange(ui As ProbeControlSet, p As ProbeSettings)
        Dim i As Integer = ui.Index
        If _probeTraces.Count <= i OrElse _runProbes Is Nothing OrElse _runProbes.Count <= i Then Return
        If _runProbes(i).Kind <> p.Kind Then Return

        Dim trace As ProbeTrace = _probeTraces(i)
        If p.Kind = ProbeKind.Line Then
            ' a line probe shows nothing at all before the start step: nothing to freeze
            If p.UseStartAfterStep AndAlso trace.StepsSampled < p.StartAfterStep Then Return
            If trace.Profile Is Nothing Then Return
        ElseIf trace.Series.Count = 0 Then
            Return
        End If

        Dim automatic As ProbeSettings = p.Clone()
        automatic.AutoScaleY = True
        Dim x0, x1, y0, y1 As Double
        ProbeGraphPainter.ComputeRanges(automatic, trace, _runSteps, x0, x1, y0, y1)
        If y1 > y0 Then
            p.YMin = y0
            p.YMax = y1
        End If
    End Sub

    Private Sub ProbeSettingsChanged()
        MarkProjectChanged()
        InvalidateProbePictures()
    End Sub

    Private Sub InvalidateProbePictures()
        If _picProbe IsNot Nothing Then _picProbe.Invalidate()
        If _picField IsNot Nothing Then _picField.Invalidate()
        If _picIntensity IsNot Nothing Then _picIntensity.Invalidate()
    End Sub

    Private Sub ProbeTabChanged(sender As Object, e As EventArgs) Handles tabMainFunctions.SelectedIndexChanged
        EndPick()
        SetProbeRanges()
        If tabMainFunctions.SelectedTab Is tabProbeList Then
            AutoLoadSceneImageIfEmpty(_picProbe)
        End If
    End Sub

    ' ---------------------------------------------------------------- picking positions in the picture

    Private Sub butPicPositions_Click(sender As Object, e As EventArgs) Handles butPicPositions.Click
        StartOrCancelPick(ProbeUi(0))
    End Sub

    Private Sub butPicPositions2_Click(sender As Object, e As EventArgs) Handles butPicPositions2.Click
        StartOrCancelPick(ProbeUi(1))
    End Sub

    ''' <summary>Pressing a group's own Pick button while it is already picking cancels it; pressing it while the
    ''' other probe is being picked cancels that one and starts picking this probe instead.</summary>
    Private Sub StartOrCancelPick(ui As ProbeControlSet)
        If ui Is Nothing Then Return
        Dim wasPickingThis As Boolean = (_pickStep <> 0 AndAlso _pickIndex = ui.Index)
        EndPick()
        If Not wasPickingThis Then StartPick(ui)
    End Sub

    ''' <summary>
    ''' When there is no picture yet to pick from (no simulation has run), makes a plain black one at the canvas size
    ''' that the Configuration tab currently specifies (the scene image's size, or the user defined width/height), so
    ''' a probe position can still be picked before ever running a simulation. Replaced by the real picture as soon as
    ''' one is copied over from the Preview tab.
    ''' </summary>
    Private Sub EnsureProbePicture()
        Dim canvas As Size = EffectiveCanvas()
        If canvas.Width <= 0 OrElse canvas.Height <= 0 Then Return

        Dim placeholder As New Bitmap(canvas.Width, canvas.Height)
        Using g As Graphics = Graphics.FromImage(placeholder)
            g.Clear(Color.Black)
        End Using
        _picProbe.Image = placeholder
    End Sub

    Private Sub StartPick(ui As ProbeControlSet)
        If _picProbe.Image Is Nothing Then EnsureProbePicture()
        If _picProbe.Image Is Nothing Then
            MessageBox.Show(Me, "There is no picture to pick from yet, and the canvas size is not known either" & vbCrLf &
                            "(choose a scene image, or a user defined width and height, on the Configuration tab first).",
                            "Probe positions", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        _pickIndex = ui.Index
        _pickStep = 1
        _pickHover = Nothing
        _picProbe.Cursor = Cursors.Cross
        ui.ButPick.Text = "Cancel"
        ShowPickInstruction()
    End Sub

    Private Sub ShowPickInstruction()
        Dim ui As ProbeControlSet = _probeUi(_pickIndex)
        Select Case _pickStep
            Case 1
                ToolStripStatusLabel1.Text = If(_probes(_pickIndex).Kind = ProbeKind.Line,
                    ui.Title & ": click in the picture for position A, the start of the line (press Cancel to stop picking).",
                    ui.Title & ": click in the picture for the position of the probe (press Cancel to stop picking).")
            Case 2
                ToolStripStatusLabel1.Text = ui.Title & ": click in the picture for position B, the end of the line."
            Case 3
                ToolStripStatusLabel1.Text = ui.Title & ": click in the picture for the top left corner of the graph box."
            Case Else
                ToolStripStatusLabel1.Text = ""
        End Select
    End Sub

    Private Sub EndPick()
        Dim wasPicking As Boolean = (_pickStep <> 0)
        If wasPicking Then _probeUi(_pickIndex).ButPick.Text = _probeUi(_pickIndex).PickButtonText
        _pickStep = 0
        _pickHover = Nothing
        If _picProbe IsNot Nothing Then
            _picProbe.Cursor = Cursors.Default
            _picProbe.Invalidate()
        End If
        If wasPicking Then ToolStripStatusLabel1.Text = ""
    End Sub

    Private Sub ProbePicture_MouseClick(sender As Object, e As MouseEventArgs)
        If _pickStep = 0 OrElse e.Button <> MouseButtons.Left Then Return
        Dim ui As ProbeControlSet = _probeUi(_pickIndex)

        Dim x As Integer
        Dim y As Integer
        If Not TryPictureToCanvas(_picProbe, e.Location, x, y) Then Return

        Select Case _pickStep
            Case 1
                ui.NumAPosX.Value = ClampToRange(ui.NumAPosX, x)
                ui.NumAPosY.Value = ClampToRange(ui.NumAPosY, y)
                _pickStep = If(_probes(_pickIndex).Kind = ProbeKind.Line, 2, 3)
                ShowPickInstruction()
            Case 2
                ui.NumBPosX.Value = ClampToRange(ui.NumBPosX, x)
                ui.NumBPosY.Value = ClampToRange(ui.NumBPosY, y)
                _pickStep = 3
                ShowPickInstruction()
            Case Else
                _probes(_pickIndex).BoxX = x
                _probes(_pickIndex).BoxY = y
                ProbeSettingsChanged()
                EndPick()
                ToolStripStatusLabel1.Text = ""
        End Select
        _picProbe.Invalidate()
    End Sub

    ''' <summary>While the corner of the graph box is picked, the box follows the mouse.</summary>
    Private Sub ProbePicture_MouseMove(sender As Object, e As MouseEventArgs)
        If _pickStep <> 3 Then Return
        _pickHover = e.Location
        _picProbe.Invalidate()
    End Sub

    Private Sub ProbePicture_MouseLeave(sender As Object, e As EventArgs)
        If _pickHover.HasValue Then
            _pickHover = Nothing
            _picProbe.Invalidate()
        End If
    End Sub

    ''' <summary>The simulation pixel that is under a point of a picture box; False when the point is outside the picture.</summary>
    Private Function TryPictureToCanvas(box As PictureBox, point As Point, ByRef x As Integer, ByRef y As Integer) As Boolean
        If box.Image Is Nothing Then Return False

        Dim area As RectangleF = ZoomedImageRectangle(box)
        If area.Width <= 0 OrElse area.Height <= 0 OrElse Not area.Contains(point.X, point.Y) Then Return False

        Dim canvas As Size = EffectiveCanvas()
        If canvas.Width <= 0 Then canvas = box.Image.Size

        x = CInt(Math.Floor((point.X - area.X) / area.Width * canvas.Width))
        y = CInt(Math.Floor((point.Y - area.Y) / area.Height * canvas.Height))
        x = Math.Max(0, Math.Min(canvas.Width - 1, x))
        y = Math.Max(0, Math.Min(canvas.Height - 1, y))
        Return True
    End Function

    ''' <summary>The rectangle (in the client area of the picture box) in which the picture is shown (SizeMode Zoom).</summary>
    Private Shared Function ZoomedImageRectangle(box As PictureBox) As RectangleF
        Dim img As Image = box.Image
        If img Is Nothing OrElse img.Width <= 0 OrElse img.Height <= 0 Then Return RectangleF.Empty

        Dim cw As Single = box.ClientSize.Width
        Dim ch As Single = box.ClientSize.Height
        Dim scale As Single = Math.Min(cw / img.Width, ch / img.Height)
        Dim w As Single = img.Width * scale
        Dim h As Single = img.Height * scale
        Return New RectangleF((cw - w) / 2.0F, (ch - h) / 2.0F, w, h)
    End Function

    ''' <summary>A rectangle in scene pixels on the screen: the picture is shown in area, the scene has canvas pixels.</summary>
    Private Shared Function ToScreen(r As Rectangle, area As RectangleF, sx As Single, sy As Single) As Rectangle
        Return New Rectangle(CInt(Math.Round(area.X + r.X * sx)), CInt(Math.Round(area.Y + r.Y * sy)),
                             CInt(Math.Round(r.Width * sx)), CInt(Math.Round(r.Height * sy)))
    End Function

    ' ---------------------------------------------------------------- drawing over the pictures

    ''' <summary>The markers and graphs of the run over the field or intensity picture of the Preview tab.</summary>
    Private Sub PreviewPicture_Paint(sender As Object, e As PaintEventArgs)
        Dim box As PictureBox = DirectCast(sender, PictureBox)
        If box.Image Is Nothing OrElse _runProbes Is Nothing OrElse _runCanvas.Width <= 0 Then Return

        Dim place As ProbePlaceOn = If(box Is _picField, ProbePlaceOn.Field, ProbePlaceOn.Intensity)
        Dim area As RectangleF = ZoomedImageRectangle(box)
        If area.Width <= 0 Then Return
        Dim sx As Single = area.Width / _runCanvas.Width
        Dim sy As Single = area.Height / _runCanvas.Height

        For i As Integer = 0 To _runProbes.Count - 1
            ' what is measured comes from the run; how the graph looks can be changed while it runs
            Dim probe As ProbeSettings = _runProbes(i).Clone()
            If i < _probes.Count Then probe.CopyDisplayFrom(_probes(i))
            If probe.PlaceOn <> place OrElse Not probe.Active Then Continue For

            ProbeGraphPainter.DrawMarkers(e.Graphics, probe, i, area.X, area.Y, sx, sy)

            Dim trace As ProbeTrace = Nothing
            If i < _probeTraces.Count Then trace = _probeTraces(i)
            Dim rect As Rectangle = ToScreen(ProbeGraphPainter.BoxInCanvas(probe, _runCanvas.Width, _runCanvas.Height), area, sx, sy)
            ProbeGraphPainter.DrawGraph(e.Graphics, rect, probe.Title(i), probe, trace, _runSteps)
        Next
    End Sub

    ''' <summary>The markers of both probes, and the graph box outline of whichever one is being edited or positioned, over
    ''' the picture on the Probes tab. A switched off probe ("On" unchecked) is hidden here too - the only exception is
    ''' while you are actively picking its position or graph box, so you can still see what you are placing.</summary>
    Private Sub ProbePicture_Paint(sender As Object, e As PaintEventArgs)
        If _picProbe.Image Is Nothing Then Return

        Dim canvas As Size = EffectiveCanvas()
        If canvas.Width <= 0 Then canvas = _picProbe.Image.Size
        Dim area As RectangleF = ZoomedImageRectangle(_picProbe)
        If area.Width <= 0 Then Return
        Dim sx As Single = area.Width / canvas.Width
        Dim sy As Single = area.Height / canvas.Height

        For i As Integer = 0 To _probes.Count - 1
            Dim probe As ProbeSettings = _probes(i)
            Dim isBeingPicked As Boolean = (_pickStep <> 0 AndAlso _pickIndex = i)
            If Not probe.Active AndAlso Not isBeingPicked Then Continue For

            ProbeGraphPainter.DrawMarkers(e.Graphics, probe, i, area.X, area.Y, sx, sy)

            ' while the corner of this probe's box is being picked, the box follows the mouse
            Dim shown As ProbeSettings = probe
            If _pickStep = 3 AndAlso _pickIndex = i AndAlso _pickHover.HasValue Then
                Dim cx As Integer
                Dim cy As Integer
                If TryPictureToCanvas(_picProbe, _pickHover.Value, cx, cy) Then
                    shown = probe.Clone()
                    shown.BoxX = cx
                    shown.BoxY = cy
                End If
            End If

            Dim rect As Rectangle = ToScreen(ProbeGraphPainter.BoxInCanvas(shown, canvas.Width, canvas.Height), area, sx, sy)
            Using fill As New SolidBrush(Color.FromArgb(40, 255, 208, 64))
                e.Graphics.FillRectangle(fill, rect)
            End Using
            Using pen As New Pen(Color.FromArgb(230, 255, 208, 64), 1.5F)
                pen.DashStyle = DashStyle.Dash
                e.Graphics.DrawRectangle(pen, rect)
            End Using
            Using font As New Font("Segoe UI", 12.0F, FontStyle.Bold, GraphicsUnit.Pixel)
                Using brush As New SolidBrush(Color.FromArgb(240, 255, 208, 64))
                    e.Graphics.DrawString(probe.Title(i), font, brush, rect.X + 4, rect.Y + 3)
                End Using
            End Using
        Next
    End Sub

    ' ---------------------------------------------------------------- probes in a simulation run

    Private Sub LoadProbesFromProject()
        If _project Is Nothing Then Return
        _probes = ProbeSettings.ListFromConfig(_project.Config)
        NormalizeProbeCount()
        ShowProbesInControls()
    End Sub

    Private Sub ResetProbes()
        _probes = NewProbeList()
        ShowProbesInControls()
    End Sub

    ''' <summary>Copies of the probes for a run.</summary>
    Private Function ProbesForRun() As List(Of ProbeSettings)
        For Each ui As ProbeControlSet In _probeUi
            ReadProbeControls(ui)
        Next
        Dim list As New List(Of ProbeSettings)()
        For Each p As ProbeSettings In _probes
            list.Add(p.Clone())
        Next
        Return list
    End Function

    ''' <summary>A run starts: the graphs start empty.</summary>
    Private Sub BeginProbeRun(probes As List(Of ProbeSettings), canvas As Size, steps As Integer)
        _runProbes = probes
        _runCanvas = canvas
        _runSteps = steps

        _probeTraces.Clear()
        If probes IsNot Nothing Then
            For i As Integer = 0 To probes.Count - 1
                _probeTraces.Add(New ProbeTrace())
            Next
        End If
        InvalidateProbePictures()
    End Sub

    ''' <summary>New values of the probes arrive from the running simulation.</summary>
    Private Sub ShowProbeSamples(runner As SimulationRunner, update As ProbeUpdate)
        If runner IsNot _runner OrElse IsDisposed Then Return

        For i As Integer = 0 To Math.Min(update.Samples.Length, _probeTraces.Count) - 1
            _probeTraces(i).Series.Add(update.Samples(i))
            If update.Profiles(i) IsNot Nothing Then _probeTraces(i).SetProfile(update.Profiles(i), update.Lengths(i))
            _probeTraces(i).StepsSampled = update.StepsDone
        Next
        If _picField IsNot Nothing Then _picField.Invalidate()
        If _picIntensity IsNot Nothing Then _picIntensity.Invalidate()
    End Sub

End Class
