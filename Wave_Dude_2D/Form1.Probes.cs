using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Probes tab: editing the two <see cref="ProbeSettings"/> in <c>_probes</c> through the
	/// per-probe control sets in <c>_probeUi</c>, picking positions with the mouse on the probe
	/// picture, and overlaying probe markers and live graphs on the preview pictures.
	/// Everything here runs on the UI thread; <see cref="ShowProbeSamples"/> is the only entry point
	/// fed by the simulation worker, and it arrives already marshalled through <c>PostToUi</c>.
	/// </summary>
	/// <remarks>
	/// Two coordinate systems are used throughout. Canvas pixels are 0-based cells of the
	/// simulation grid (size from <c>EffectiveCanvas()</c> while editing, <c>_runCanvas</c> for the
	/// current/last run); picture-box pixels are client coordinates of a <see cref="PictureBox"/>
	/// in Zoom mode, where the image is scaled uniformly and centred. <see cref="ZoomedImageRectangle"/>
	/// gives the image's rectangle in picture-box pixels; <see cref="TryPictureToCanvas"/> and
	/// <see cref="ToScreen"/> convert between the two.
	/// </remarks>
	public partial class Form1
	{
		/// <summary>The control set for probe <paramref name="index"/>, or null before <see cref="InitializeProbeControls"/> has run (designer-wired handlers can fire that early).</summary>
		private Form1.ProbeControlSet ProbeUi(int index)
		{
			return this._probeUi == null ? null : this._probeUi[index];
		}

		/// <summary>Creates the default probe list: <see cref="ProbeCount"/> fresh, inactive probes.</summary>
		private static List<ProbeSettings> NewProbeList()
		{
			List<ProbeSettings> probes = new List<ProbeSettings>();
			for (int i = 0; i < Form1.ProbeCount; i++)
			{
				probes.Add(new ProbeSettings());
			}
			return probes;
		}

		/// <summary>Pads or truncates <c>_probes</c> to exactly <see cref="ProbeCount"/> entries after loading a project (the config may hold any count).</summary>
		private void NormalizeProbeCount()
		{
			while (this._probes.Count < Form1.ProbeCount)
			{
				this._probes.Add(new ProbeSettings());
			}
			while (this._probes.Count > Form1.ProbeCount)
			{
				this._probes.RemoveAt(this._probes.Count - 1);
			}
		}

		/// <summary>
		/// One-time setup from the constructor: binds the designer controls of both probe panels into
		/// <c>_probeUi</c>, fills the fixed combo boxes (item order mirrors the enum order because the
		/// selected index is cast straight to the enum), sets spinner ranges, hooks the paint and mouse
		/// handlers of the probe and preview pictures (which must already exist) and shows the current probes.
		/// </summary>
		private void InitializeProbeControls()
		{
			this._probeUi = new Form1.ProbeControlSet[]
			{
				new Form1.ProbeControlSet(0, "Probe 1", this.chkProbeOn1, this.cmbPlaceOn, this.cmbMeasurement, this.cmbProbeType, this.cmbBoxSize, this.numAPosX, this.numAPosY, this.numBPosX, this.numBPosY, this.butPicPositions, this.radRunningScale, this.radFullScale, this.chkAutoScaleX, this.chkAutoScaleY, this.txtMinScaleY, this.txtMaxScaleY, this.chkStartAfterStep, this.txtStartAfterStep, this.numRunningWindow, new Control[] { this.numBPosX, this.numBPosY, this.Label14, this.Label15, this.Label16 }),
				new Form1.ProbeControlSet(1, "Probe 2", this.chkProbeOn2, this.cmbPlaceOn2, this.cmbMeasurement2, this.cmbProbeType2, this.cmbBoxSize2, this.numAPosX2, this.numAPosY2, this.numBPosX2, this.numBPosY2, this.butPicPositions2, this.radRunningScale2, this.radFullScale2, this.chkAutoScaleX2, this.chkAutoScaleY2, this.txtMinScaleY2, this.txtMaxScaleY2, this.chkStartAfterStep2, this.txtStartAfterStep2, this.numRunningWindow2, new Control[] { this.numBPosX2, this.numBPosY2, this.Label28, this.Label27, this.Label25 })
			};
			foreach (Form1.ProbeControlSet ui in this._probeUi)
			{
				foreach (ComboBox combo in new ComboBox[] { ui.CmbPlaceOn, ui.CmbMeasurement, ui.CmbProbeType, ui.CmbBoxSize })
				{
					combo.DropDownStyle = ComboBoxStyle.DropDownList;
				}
				ui.CmbPlaceOn.Items.Clear();
				ui.CmbPlaceOn.Items.AddRange(new object[] { "Field", "Intensity" });
				ui.CmbProbeType.Items.Clear();
				ui.CmbProbeType.Items.AddRange(new object[] { "Point (A)", "Line (A-B)" });
				ui.CmbBoxSize.Items.Clear();
				ui.CmbBoxSize.Items.AddRange(new object[] { "Tiny (200 x 100)", "Small (300 x 150)", "Medium (400 x 200)", "Large (500 x 250)", "Extra Large (700 x 350)" });
				foreach (NumericUpDown position in new NumericUpDown[] { ui.NumAPosX, ui.NumAPosY, ui.NumBPosX, ui.NumBPosY })
				{
					position.DecimalPlaces = 0;
					position.Minimum = 0m;
					// Placeholder until SetProbeRanges knows the canvas size.
					position.Maximum = 16383m;
				}
				ui.NumRunningWindow.DecimalPlaces = 0;
				ui.NumRunningWindow.Minimum = 10m;
				ui.NumRunningWindow.Maximum = ProbeSettings.MaxRunningWindow;
			}
			this._picField.Paint += this.PreviewPicture_Paint;
			this._picIntensity.Paint += this.PreviewPicture_Paint;
			this._picProbe.Paint += this.ProbePicture_Paint;
			this._picProbe.MouseClick += this.ProbePicture_MouseClick;
			this._picProbe.MouseMove += this.ProbePicture_MouseMove;
			this._picProbe.MouseLeave += this.ProbePicture_MouseLeave;
			this.ShowProbesInControls();
		}

		/// <summary>Cancels any position pick in progress and pushes every probe in <c>_probes</c> into its controls.</summary>
		private void ShowProbesInControls()
		{
			this.EndPick();
			foreach (Form1.ProbeControlSet ui in this._probeUi)
			{
				this.ShowProbeInControls(ui);
			}
		}

		/// <summary>
		/// Normalizes the probe and writes all of its values into the controls of <paramref name="ui"/>.
		/// <c>_loadingProbe</c> is set meanwhile so the change handlers do not write back or mark the
		/// project dirty. Positions are clamped to the spinner ranges; the Y range is shown with "G6"
		/// in the invariant culture. Ends by refreshing the enabled states and repainting the pictures.
		/// </summary>
		private void ShowProbeInControls(Form1.ProbeControlSet ui)
		{
			ProbeSettings probe = this._probes[ui.Index];
			probe.Normalize();
			this._loadingProbe = true;
			try
			{
				this.SetProbeRanges(ui);
				ui.ChkOn.Checked = probe.Active;
				ui.CmbPlaceOn.SelectedIndex = (int)probe.PlaceOn;
				this.FillMeasurementCombo(ui, probe);
				ui.CmbProbeType.SelectedIndex = (int)probe.Kind;
				ui.CmbBoxSize.SelectedIndex = (int)probe.BoxSize;
				ui.NumAPosX.Value = Form1.ClampToRange(ui.NumAPosX, probe.PosAX);
				ui.NumAPosY.Value = Form1.ClampToRange(ui.NumAPosY, probe.PosAY);
				ui.NumBPosX.Value = Form1.ClampToRange(ui.NumBPosX, probe.PosBX);
				ui.NumBPosY.Value = Form1.ClampToRange(ui.NumBPosY, probe.PosBY);
				ui.RadRunning.Checked = probe.ScaleMode == ProbeScaleMode.Running;
				ui.RadFull.Checked = probe.ScaleMode == ProbeScaleMode.Full;
				ui.ChkAutoX.Checked = probe.AutoScaleX;
				ui.ChkAutoY.Checked = probe.AutoScaleY;
				ui.TxtMinY.Text = probe.YMin.ToString("G6", CultureInfo.InvariantCulture);
				ui.TxtMaxY.Text = probe.YMax.ToString("G6", CultureInfo.InvariantCulture);
				ui.ChkStartAfter.Checked = probe.UseStartAfterStep;
				ui.TxtStartAfter.Text = probe.StartAfterStep.ToString(CultureInfo.InvariantCulture);
				ui.NumRunningWindow.Value = Form1.ClampToRange(ui.NumRunningWindow, probe.RunningWindow);
			}
			finally
			{
				this._loadingProbe = false;
			}
			this.UpdateProbeControlState(ui);
			this.InvalidateProbePictures();
		}

		/// <summary>
		/// Rebuilds the measurement combo for the probe's <see cref="ProbeSettings.PlaceOn"/>: the field
		/// offers only "Amplitude", the intensity output "Intensity" and "Integrated intensity".
		/// Selects the entry matching <paramref name="p"/>'s measurement (index 1 = integrated).
		/// </summary>
		private void FillMeasurementCombo(Form1.ProbeControlSet ui, ProbeSettings p)
		{
			ui.CmbMeasurement.Items.Clear();
			if (p.PlaceOn == ProbePlaceOn.Field)
			{
				ui.CmbMeasurement.Items.Add("Amplitude");
				ui.CmbMeasurement.SelectedIndex = 0;
			}
			else
			{
				ui.CmbMeasurement.Items.Add("Intensity");
				ui.CmbMeasurement.Items.Add("Integrated intensity");
				ui.CmbMeasurement.SelectedIndex = ((p.Measurement == ProbeMeasurement.IntegratedIntensity) ? 1 : 0);
			}
		}

		/// <summary>Maps a measurement combo index back to the enum for the given surface (inverse of <see cref="FillMeasurementCombo"/>); the field always yields amplitude.</summary>
		private static ProbeMeasurement MeasurementFromIndex(ProbePlaceOn placeOn, int index)
		{
			if (placeOn == ProbePlaceOn.Field)
			{
				return ProbeMeasurement.Amplitude;
			}
			return index == 1 ? ProbeMeasurement.IntegratedIntensity : ProbeMeasurement.Intensity;
		}

		/// <summary>
		/// Enables/disables controls to match the probe: B position only for line probes; scale mode,
		/// auto X and running window only for point probes (a profile has a fixed X axis); the Y range
		/// boxes only when auto Y is off; the start-after step box only when its check box is on.
		/// </summary>
		private void UpdateProbeControlState(Form1.ProbeControlSet ui)
		{
			bool isLine = this._probes[ui.Index].Kind == ProbeKind.Line;
			foreach (Control control in ui.BOnlyControls)
			{
				control.Enabled = isLine;
			}
			foreach (Control control in new Control[] { ui.RadRunning, ui.RadFull, ui.ChkAutoX, ui.NumRunningWindow })
			{
				control.Enabled = !isLine;
			}
			ui.TxtMinY.Enabled = !ui.ChkAutoY.Checked;
			ui.TxtMaxY.Enabled = !ui.ChkAutoY.Checked;
			ui.TxtStartAfter.Enabled = ui.ChkStartAfter.Checked;
		}

		/// <summary>Applies <see cref="SetProbeRanges(Form1.ProbeControlSet)"/> to both probes; no-op before the controls are initialized.</summary>
		private void SetProbeRanges()
		{
			if (this._probeUi == null)
			{
				return;
			}
			foreach (Form1.ProbeControlSet ui in this._probeUi)
			{
				this.SetProbeRanges(ui);
			}
		}

		/// <summary>
		/// Limits the position spinners to the current canvas size (width-1 / height-1 in canvas pixels,
		/// or 16383 when the size is unknown). Lowering a maximum can clamp the value and fire
		/// ValueChanged, so <c>_loadingProbe</c> is raised meanwhile (and restored to its previous state,
		/// since this is also called from inside <see cref="ShowProbeInControls"/>).
		/// </summary>
		private void SetProbeRanges(Form1.ProbeControlSet ui)
		{
			Size canvas = this.EffectiveCanvas();
			decimal maxX = new decimal((canvas.Width > 0) ? (canvas.Width - 1) : 16383);
			decimal maxY = new decimal((canvas.Height > 0) ? (canvas.Height - 1) : 16383);
			bool wasLoading = this._loadingProbe;
			this._loadingProbe = true;
			try
			{
				ui.NumAPosX.Maximum = maxX;
				ui.NumBPosX.Maximum = maxX;
				ui.NumAPosY.Maximum = maxY;
				ui.NumBPosY.Maximum = maxY;
			}
			finally
			{
				this._loadingProbe = wasLoading;
			}
		}

		/// <summary>
		/// Copies every control of <paramref name="ui"/> into its <see cref="ProbeSettings"/>. Spinners are
		/// validated first so typed-but-uncommitted text counts. The Y range is parsed (invariant culture)
		/// only when auto Y is off, and unparsable text leaves the old value; same for the start-after step.
		/// Ends with <see cref="ProbeSettings.Normalize"/>. Does not mark the project dirty or repaint.
		/// </summary>
		private void ReadProbeControls(Form1.ProbeControlSet ui)
		{
			ProbeSettings probe = this._probes[ui.Index];
			probe.Active = ui.ChkOn.Checked;
			if (ui.CmbPlaceOn.SelectedIndex >= 0)
			{
				probe.PlaceOn = (ProbePlaceOn)ui.CmbPlaceOn.SelectedIndex;
			}
			if (ui.CmbMeasurement.SelectedIndex >= 0)
			{
				probe.Measurement = Form1.MeasurementFromIndex(probe.PlaceOn, ui.CmbMeasurement.SelectedIndex);
			}
			if (ui.CmbProbeType.SelectedIndex >= 0)
			{
				probe.Kind = (ProbeKind)ui.CmbProbeType.SelectedIndex;
			}
			if (ui.CmbBoxSize.SelectedIndex >= 0)
			{
				probe.BoxSize = (ProbeBoxSize)ui.CmbBoxSize.SelectedIndex;
			}
			ui.NumAPosX.Validate();
			ui.NumAPosY.Validate();
			ui.NumBPosX.Validate();
			ui.NumBPosY.Validate();
			probe.PosAX = Convert.ToInt32(ui.NumAPosX.Value);
			probe.PosAY = Convert.ToInt32(ui.NumAPosY.Value);
			probe.PosBX = Convert.ToInt32(ui.NumBPosX.Value);
			probe.PosBY = Convert.ToInt32(ui.NumBPosY.Value);
			probe.ScaleMode = (ui.RadFull.Checked ? ProbeScaleMode.Full : ProbeScaleMode.Running);
			probe.AutoScaleX = ui.ChkAutoX.Checked;
			probe.AutoScaleY = ui.ChkAutoY.Checked;
			if (!ui.ChkAutoY.Checked)
			{
				double yMin;
				if (double.TryParse(ui.TxtMinY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out yMin))
				{
					probe.YMin = yMin;
				}
				double yMax;
				if (double.TryParse(ui.TxtMaxY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out yMax))
				{
					probe.YMax = yMax;
				}
			}
			probe.UseStartAfterStep = ui.ChkStartAfter.Checked;
			int startAfter;
			if (int.TryParse(ui.TxtStartAfter.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out startAfter))
			{
				probe.StartAfterStep = startAfter;
			}
			ui.NumRunningWindow.Validate();
			probe.RunningWindow = Convert.ToInt32(ui.NumRunningWindow.Value);
			probe.Normalize();
		}

		/// <summary>Probe 1: a combo box (place on / measurement / type / box size) changed; see <see cref="HandleProbeComboChanged"/>.</summary>
		private void ProbeCombo_SelectedIndexChanged(object sender, EventArgs e)
		{
			this.HandleProbeComboChanged(this.ProbeUi(0), sender);
		}

		/// <summary>Probe 1: an A/B position spinner changed; the new canvas position is stored.</summary>
		private void ProbePosition_ValueChanged(object sender, EventArgs e)
		{
			this.HandleProbePositionChanged(this.ProbeUi(0));
		}

		/// <summary>Probe 1: the "on" check box toggled the probe's active flag.</summary>
		private void ProbeOn_CheckedChanged(object sender, EventArgs e)
		{
			this.HandleProbeOnChanged(this.ProbeUi(0));
		}

		/// <summary>Probe 1: Enter in a Y-range or start-after text box applies all controls.</summary>
		private void ProbeTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			this.HandleProbeTextBoxKeyDown(this.ProbeUi(0), e);
		}

		/// <summary>Probe 1: running/full scale radio button changed.</summary>
		private void ProbeScaleMode_CheckedChanged(object sender, EventArgs e)
		{
			this.HandleProbeScaleModeChanged(this.ProbeUi(0));
		}

		/// <summary>Probe 1: auto X / auto Y check box changed.</summary>
		private void ProbeAutoScale_CheckedChanged(object sender, EventArgs e)
		{
			this.HandleProbeAutoScaleChanged(this.ProbeUi(0));
		}

		/// <summary>Probe 1: a Y-range text box lost focus; all controls are applied.</summary>
		private void ProbeYRange_Validated(object sender, EventArgs e)
		{
			this.HandleProbeControlApplied(this.ProbeUi(0));
		}

		/// <summary>Probe 1: the "start after step" check box toggled.</summary>
		private void ProbeStartAfterStep_CheckedChanged(object sender, EventArgs e)
		{
			this.HandleProbeStartAfterStepChanged(this.ProbeUi(0));
		}

		/// <summary>Probe 1: the start-after step text box lost focus; all controls are applied.</summary>
		private void ProbeStartAfterStep_Validated(object sender, EventArgs e)
		{
			this.HandleProbeControlApplied(this.ProbeUi(0));
		}

		/// <summary>Probe 1: the running window spinner changed; all controls are applied.</summary>
		private void ProbeRunningWindow_ValueChanged(object sender, EventArgs e)
		{
			this.HandleProbeControlApplied(this.ProbeUi(0));
		}

		/// <summary>Probe 2: a combo box changed; see <see cref="HandleProbeComboChanged"/>.</summary>
		private void ProbeCombo2_SelectedIndexChanged(object sender, EventArgs e)
		{
			this.HandleProbeComboChanged(this.ProbeUi(1), sender);
		}

		/// <summary>Probe 2: an A/B position spinner changed; the new canvas position is stored.</summary>
		private void ProbePosition2_ValueChanged(object sender, EventArgs e)
		{
			this.HandleProbePositionChanged(this.ProbeUi(1));
		}

		/// <summary>Probe 2: the "on" check box toggled the probe's active flag.</summary>
		private void ProbeOn2_CheckedChanged(object sender, EventArgs e)
		{
			this.HandleProbeOnChanged(this.ProbeUi(1));
		}

		/// <summary>Probe 2: Enter in a Y-range or start-after text box applies all controls.</summary>
		private void ProbeTextBox2_KeyDown(object sender, KeyEventArgs e)
		{
			this.HandleProbeTextBoxKeyDown(this.ProbeUi(1), e);
		}

		/// <summary>Probe 2: running/full scale radio button changed.</summary>
		private void ProbeScaleMode2_CheckedChanged(object sender, EventArgs e)
		{
			this.HandleProbeScaleModeChanged(this.ProbeUi(1));
		}

		/// <summary>Probe 2: auto X / auto Y check box changed.</summary>
		private void ProbeAutoScale2_CheckedChanged(object sender, EventArgs e)
		{
			this.HandleProbeAutoScaleChanged(this.ProbeUi(1));
		}

		/// <summary>Probe 2: a Y-range text box lost focus; all controls are applied.</summary>
		private void ProbeYRange2_Validated(object sender, EventArgs e)
		{
			this.HandleProbeControlApplied(this.ProbeUi(1));
		}

		/// <summary>Probe 2: the "start after step" check box toggled.</summary>
		private void ProbeStartAfterStep2_CheckedChanged(object sender, EventArgs e)
		{
			this.HandleProbeStartAfterStepChanged(this.ProbeUi(1));
		}

		/// <summary>Probe 2: the start-after step text box lost focus; all controls are applied.</summary>
		private void ProbeStartAfterStep2_Validated(object sender, EventArgs e)
		{
			this.HandleProbeControlApplied(this.ProbeUi(1));
		}

		/// <summary>Probe 2: the running window spinner changed; all controls are applied.</summary>
		private void ProbeRunningWindow2_ValueChanged(object sender, EventArgs e)
		{
			this.HandleProbeControlApplied(this.ProbeUi(1));
		}

		/// <summary>
		/// Shared handler for the four combo boxes of a probe. Ignored while loading. Changing the probe
		/// type cancels a pick in progress for that probe; changing "place on" first updates the probe and
		/// refills the measurement combo (under the loading guard, so the refill does not recurse). Then
		/// all controls are read back, enabled states refreshed, and the project marked dirty.
		/// </summary>
		/// <param name="ui">Control set of the probe whose combo box changed; null before <see cref="InitializeProbeControls"/> has run.</param>
		/// <param name="sender">The combo box that raised the event.</param>
		private void HandleProbeComboChanged(Form1.ProbeControlSet ui, object sender)
		{
			if (ui == null || this._loadingProbe)
			{
				return;
			}
			if (sender == ui.CmbProbeType && this._pickStep != 0 && this._pickIndex == ui.Index)
			{
				this.EndPick();
			}
			if (sender == ui.CmbPlaceOn && ui.CmbPlaceOn.SelectedIndex >= 0)
			{
				ProbeSettings probe = this._probes[ui.Index];
				probe.PlaceOn = (ProbePlaceOn)ui.CmbPlaceOn.SelectedIndex;
				probe.Normalize();
				this._loadingProbe = true;
				try
				{
					this.FillMeasurementCombo(ui, probe);
				}
				finally
				{
					this._loadingProbe = false;
				}
			}
			this.ReadProbeControls(ui);
			this.UpdateProbeControlState(ui);
			this.ProbeSettingsChanged();
		}

		/// <summary>Stores the four position spinners (canvas pixels) into the probe and marks the project dirty; ignored while loading.</summary>
		private void HandleProbePositionChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || this._loadingProbe)
			{
				return;
			}
			ProbeSettings probe = this._probes[ui.Index];
			probe.PosAX = Convert.ToInt32(ui.NumAPosX.Value);
			probe.PosAY = Convert.ToInt32(ui.NumAPosY.Value);
			probe.PosBX = Convert.ToInt32(ui.NumBPosX.Value);
			probe.PosBY = Convert.ToInt32(ui.NumBPosY.Value);
			this.ProbeSettingsChanged();
		}

		/// <summary>Stores the "on" check box into <see cref="ProbeSettings.Active"/> and marks the project dirty; ignored while loading.</summary>
		private void HandleProbeOnChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || this._loadingProbe)
			{
				return;
			}
			this._probes[ui.Index].Active = ui.ChkOn.Checked;
			this.ProbeSettingsChanged();
		}

		/// <summary>On Enter (and only then): swallows the key so the text box does not beep, then applies all controls of the probe; ignored while loading.</summary>
		private void HandleProbeTextBoxKeyDown(Form1.ProbeControlSet ui, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Return)
			{
				return;
			}
			e.SuppressKeyPress = true;
			e.Handled = true;
			if (ui == null || this._loadingProbe)
			{
				return;
			}
			this.ReadProbeControls(ui);
			this.ProbeSettingsChanged();
		}

		/// <summary>Stores the scale mode radio buttons into the probe and marks the project dirty; ignored while loading.</summary>
		private void HandleProbeScaleModeChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || this._loadingProbe)
			{
				return;
			}
			this._probes[ui.Index].ScaleMode = (ui.RadFull.Checked ? ProbeScaleMode.Full : ProbeScaleMode.Running);
			this.ProbeSettingsChanged();
		}

		/// <summary>
		/// Stores the auto X / auto Y check boxes. When auto Y has just been switched off, the Y range the
		/// running graph currently shows is frozen into the probe (<see cref="FreezeValueRange"/>) and
		/// written into the min/max text boxes, so the user starts from the displayed range rather than
		/// the stale stored one. Ignored while loading.
		/// </summary>
		private void HandleProbeAutoScaleChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || this._loadingProbe)
			{
				return;
			}
			ProbeSettings probe = this._probes[ui.Index];
			bool wasAutoY = probe.AutoScaleY;
			probe.AutoScaleX = ui.ChkAutoX.Checked;
			probe.AutoScaleY = ui.ChkAutoY.Checked;
			if (wasAutoY && !probe.AutoScaleY)
			{
				this.FreezeValueRange(ui, probe);
				this._loadingProbe = true;
				try
				{
					ui.TxtMinY.Text = probe.YMin.ToString("G6", CultureInfo.InvariantCulture);
					ui.TxtMaxY.Text = probe.YMax.ToString("G6", CultureInfo.InvariantCulture);
				}
				finally
				{
					this._loadingProbe = false;
				}
			}
			this.UpdateProbeControlState(ui);
			this.ProbeSettingsChanged();
		}

		/// <summary>Generic "commit" used by Validated and ValueChanged events: reads all controls back and marks the project dirty; ignored while loading.</summary>
		private void HandleProbeControlApplied(Form1.ProbeControlSet ui)
		{
			if (ui == null || this._loadingProbe)
			{
				return;
			}
			this.ReadProbeControls(ui);
			this.ProbeSettingsChanged();
		}

		/// <summary>Stores the "start after step" check box, enables/disables its text box and marks the project dirty; ignored while loading.</summary>
		private void HandleProbeStartAfterStepChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || this._loadingProbe)
			{
				return;
			}
			this._probes[ui.Index].UseStartAfterStep = ui.ChkStartAfter.Checked;
			this.UpdateProbeControlState(ui);
			this.ProbeSettingsChanged();
		}

		/// <summary>
		/// When auto Y scaling is switched off, keeps the Y range the running graph currently shows:
		/// recomputes the auto-scaled range from the trace of the current/last run (via
		/// <see cref="ProbeGraphPainter.ComputeRanges"/> on an auto-Y clone) and stores it in
		/// <paramref name="p"/>. Does nothing when there is no run trace for this probe, when the run
		/// used a different probe kind, when a line probe has no profile yet (or is still gated by
		/// its start-after step), when a point probe has no samples, or when the computed range is empty.
		/// </summary>
		private void FreezeValueRange(Form1.ProbeControlSet ui, ProbeSettings p)
		{
			int index = ui.Index;
			if (this._probeTraces.Count <= index || this._runProbes == null || this._runProbes.Count <= index)
			{
				return;
			}
			if (this._runProbes[index].Kind != p.Kind)
			{
				return;
			}
			ProbeTrace trace = this._probeTraces[index];
			if (p.Kind == ProbeKind.Line)
			{
				if (p.UseStartAfterStep && trace.StepsSampled < p.StartAfterStep)
				{
					return;
				}
				if (trace.Profile == null)
				{
					return;
				}
			}
			else if (trace.Series.Count == 0)
			{
				return;
			}
			ProbeSettings autoScaled = p.Clone();
			autoScaled.AutoScaleY = true;
			double xMin = 0.0;
			double xMax = 0.0;
			double yMin = 0.0;
			double yMax = 0.0;
			ProbeGraphPainter.ComputeRanges(autoScaled, trace, this._runSteps, ref xMin, ref xMax, ref yMin, ref yMax);
			if (yMax > yMin)
			{
				p.YMin = yMin;
				p.YMax = yMax;
			}
		}

		/// <summary>Marks the project dirty (unless a loading flag is set) and repaints all three probe-related pictures.</summary>
		private void ProbeSettingsChanged()
		{
			this.MarkProjectChanged();
			this.InvalidateProbePictures();
		}

		/// <summary>Invalidates the probe picture and the field/intensity previews so markers and graph boxes are redrawn; safe before the pictures exist.</summary>
		private void InvalidateProbePictures()
		{
			if (this._picProbe != null)
			{
				this._picProbe.Invalidate();
			}
			if (this._picField != null)
			{
				this._picField.Invalidate();
			}
			if (this._picIntensity != null)
			{
				this._picIntensity.Invalidate();
			}
		}

		/// <summary>
		/// Any main tab switch cancels a pick in progress and refreshes the spinner ranges (the canvas
		/// size may have changed on the Configuration tab); switching to the Probes tab additionally
		/// loads the scene image (or a black canvas) into the probe picture if it is still empty.
		/// </summary>
		private void ProbeTabChanged(object sender, EventArgs e)
		{
			this.EndPick();
			this.SetProbeRanges();
			if (this.tabMainFunctions.SelectedTab == this.tabProbeList)
			{
				this.AutoLoadSceneImageIfEmpty(this._picProbe);
			}
		}

		/// <summary>Probe 1 "pick positions" button: starts picking in the probe picture, or cancels it when already picking.</summary>
		private void butPicPositions_Click(object sender, EventArgs e)
		{
			this.StartOrCancelPick(this.ProbeUi(0));
		}

		/// <summary>Probe 2 "pick positions" button: starts picking in the probe picture, or cancels it when already picking.</summary>
		private void butPicPositions2_Click(object sender, EventArgs e)
		{
			this.StartOrCancelPick(this.ProbeUi(1));
		}

		/// <summary>Toggles pick mode for one probe: cancels a pick in progress (for any probe) and, unless that pick was this probe's, starts a new one.</summary>
		private void StartOrCancelPick(Form1.ProbeControlSet ui)
		{
			if (ui == null)
			{
				return;
			}
			bool wasPicking = this._pickStep != 0 && this._pickIndex == ui.Index;
			this.EndPick();
			if (!wasPicking)
			{
				this.StartPick(ui);
			}
		}

		/// <summary>Gives the probe picture a black bitmap of the canvas size so positions can be picked without a scene image; does nothing when the canvas size is unknown. The bitmap is owned by the picture box.</summary>
		private void EnsureProbePicture()
		{
			Size canvas = this.EffectiveCanvas();
			if (canvas.Width <= 0 || canvas.Height <= 0)
			{
				return;
			}
			Bitmap bitmap = new Bitmap(canvas.Width, canvas.Height);
			using (Graphics g = Graphics.FromImage(bitmap))
			{
				g.Clear(Color.Black);
			}
			this._picProbe.Image = bitmap;
		}

		/// <summary>
		/// Enters pick mode for <paramref name="ui"/>'s probe: makes sure the probe picture has an image
		/// (otherwise shows a message and gives up), sets <c>_pickIndex</c>/<c>_pickStep</c> to step 1,
		/// switches to a cross cursor, relabels the pick button "Cancel" and shows the first instruction
		/// in the status bar. Clicks are then handled by <see cref="ProbePicture_MouseClick"/>.
		/// </summary>
		private void StartPick(Form1.ProbeControlSet ui)
		{
			if (this._picProbe.Image == null)
			{
				this.EnsureProbePicture();
			}
			if (this._picProbe.Image == null)
			{
				MessageBox.Show(this, "There is no picture to pick from yet, and the canvas size is not known either\r\n(choose a scene image, or a user defined width and height, on the Configuration tab first).", "Probe positions", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			this._pickIndex = ui.Index;
			this._pickStep = 1;
			this._pickHover = null;
			this._picProbe.Cursor = Cursors.Cross;
			ui.ButPick.Text = "Cancel";
			this.ShowPickInstruction();
		}

		/// <summary>Writes the status-bar instruction for the current pick step (1: position A / the point, 2: position B, 3: top-left corner of the graph box).</summary>
		private void ShowPickInstruction()
		{
			Form1.ProbeControlSet ui = this._probeUi[this._pickIndex];
			switch (this._pickStep)
			{
			case 1:
				this.ToolStripStatusLabel1.Text = ((this._probes[this._pickIndex].Kind == ProbeKind.Line) ? (ui.Title + ": click in the picture for position A, the start of the line (press Cancel to stop picking).") : (ui.Title + ": click in the picture for the position of the probe (press Cancel to stop picking)."));
				break;
			case 2:
				this.ToolStripStatusLabel1.Text = ui.Title + ": click in the picture for position B, the end of the line.";
				break;
			case 3:
				this.ToolStripStatusLabel1.Text = ui.Title + ": click in the picture for the top left corner of the graph box.";
				break;
			default:
				this.ToolStripStatusLabel1.Text = "";
				break;
			}
		}

		/// <summary>Leaves pick mode: restores the pick button text, clears step and hover, resets the cursor, repaints, and clears the status bar if a pick was active. Safe to call when nothing is being picked or before the probe picture exists.</summary>
		private void EndPick()
		{
			bool wasPicking = this._pickStep != 0;
			if (wasPicking)
			{
				this._probeUi[this._pickIndex].ButPick.Text = this._probeUi[this._pickIndex].PickButtonText;
			}
			this._pickStep = 0;
			this._pickHover = null;
			if (this._picProbe != null)
			{
				this._picProbe.Cursor = Cursors.Default;
				this._picProbe.Invalidate();
			}
			if (wasPicking)
			{
				this.ToolStripStatusLabel1.Text = "";
			}
		}

		/// <summary>
		/// Left click on the probe picture while picking: the click (picture-box pixels) is converted to a
		/// canvas pixel and, depending on the step, written into the A spinners (their ValueChanged stores
		/// it in the probe), the B spinners, or directly into <see cref="ProbeSettings.BoxX"/>/<c>BoxY</c>.
		/// Point probes skip step 2. Clicks outside the image area are ignored.
		/// </summary>
		private void ProbePicture_MouseClick(object sender, MouseEventArgs e)
		{
			if (this._pickStep == 0 || e.Button != MouseButtons.Left)
			{
				return;
			}
			Form1.ProbeControlSet ui = this._probeUi[this._pickIndex];
			int x = 0;
			int y = 0;
			if (!this.TryPictureToCanvas(this._picProbe, e.Location, ref x, ref y))
			{
				return;
			}
			switch (this._pickStep)
			{
			case 1:
				ui.NumAPosX.Value = Form1.ClampToRange(ui.NumAPosX, x);
				ui.NumAPosY.Value = Form1.ClampToRange(ui.NumAPosY, y);
				this._pickStep = ((this._probes[this._pickIndex].Kind == ProbeKind.Line) ? 2 : 3);
				this.ShowPickInstruction();
				break;
			case 2:
				ui.NumBPosX.Value = Form1.ClampToRange(ui.NumBPosX, x);
				ui.NumBPosY.Value = Form1.ClampToRange(ui.NumBPosY, y);
				this._pickStep = 3;
				this.ShowPickInstruction();
				break;
			default:
				this._probes[this._pickIndex].BoxX = x;
				this._probes[this._pickIndex].BoxY = y;
				this.ProbeSettingsChanged();
				this.EndPick();
				break;
			}
			this._picProbe.Invalidate();
		}

		/// <summary>During step 3 (graph box placement) remembers the mouse position in picture-box pixels so <see cref="ProbePicture_Paint"/> can preview the box there.</summary>
		private void ProbePicture_MouseMove(object sender, MouseEventArgs e)
		{
			if (this._pickStep == 3)
			{
				this._pickHover = new Point?(e.Location);
				this._picProbe.Invalidate();
			}
		}

		/// <summary>Drops the box preview position when the mouse leaves the probe picture.</summary>
		private void ProbePicture_MouseLeave(object sender, EventArgs e)
		{
			if (this._pickHover != null)
			{
				this._pickHover = null;
				this._picProbe.Invalidate();
			}
		}

		/// <summary>
		/// Maps a point in picture-box pixels to canvas pixel coordinates; false when the box has no
		/// image or the point lies outside the zoomed image area. The canvas size is
		/// <c>EffectiveCanvas()</c>, falling back to the image size when unknown. The result is floored
		/// and clamped to 0..width-1 / 0..height-1.
		/// </summary>
		private bool TryPictureToCanvas(PictureBox box, Point point, ref int x, ref int y)
		{
			if (box.Image == null)
			{
				return false;
			}
			RectangleF area = Form1.ZoomedImageRectangle(box);
			if (area.Width <= 0f || area.Height <= 0f || !area.Contains(point.X, point.Y))
			{
				return false;
			}
			Size canvas = this.EffectiveCanvas();
			if (canvas.Width <= 0)
			{
				canvas = box.Image.Size;
			}
			x = (int)Math.Floor((point.X - area.X) / area.Width * canvas.Width);
			y = (int)Math.Floor((point.Y - area.Y) / area.Height * canvas.Height);
			x = Math.Max(0, Math.Min(canvas.Width - 1, x));
			y = Math.Max(0, Math.Min(canvas.Height - 1, y));
			return true;
		}

		/// <summary>
		/// The rectangle (picture-box pixels) the image occupies inside a picture box in Zoom size mode:
		/// uniformly scaled to fit and centred. Empty when there is no image. All preview boxes are
		/// created with <c>SizeMode = Zoom</c>, so this matches what WinForms draws.
		/// </summary>
		private static RectangleF ZoomedImageRectangle(PictureBox box)
		{
			Image image = box.Image;
			if (image == null || image.Width <= 0 || image.Height <= 0)
			{
				return RectangleF.Empty;
			}
			float boxWidth = box.ClientSize.Width;
			float boxHeight = box.ClientSize.Height;
			float scale = Math.Min(boxWidth / image.Width, boxHeight / image.Height);
			float width = image.Width * scale;
			float height = image.Height * scale;
			return new RectangleF((boxWidth - width) / 2f, (boxHeight - height) / 2f, width, height);
		}

		/// <summary>
		/// Converts a rectangle in canvas pixels to picture-box pixels (despite the name, not screen
		/// coordinates): offsets by the image area's origin and scales by <paramref name="sx"/>/<paramref name="sy"/>
		/// (picture-box pixels per canvas pixel), rounding each edge.
		/// </summary>
		private static Rectangle ToScreen(Rectangle r, RectangleF area, float sx, float sy)
		{
			return new Rectangle((int)Math.Round(area.X + r.X * sx), (int)Math.Round(area.Y + r.Y * sy), (int)Math.Round(r.Width * sx), (int)Math.Round(r.Height * sy));
		}

		/// <summary>
		/// Paint overlay for the field and intensity preview pictures: draws markers and live graphs for
		/// the probes of the current/last run (<c>_runProbes</c>, scaled by <c>_runCanvas</c> because the
		/// picture shows run output) whose <see cref="ProbeSettings.PlaceOn"/> matches the picture.
		/// Position, kind and measurement come from the run copy, but display settings are taken from
		/// the probe being edited (<see cref="ProbeSettings.CopyDisplayFrom"/>) so box moves, scaling and
		/// the active flag apply immediately. Nothing is drawn before the first run.
		/// </summary>
		private void PreviewPicture_Paint(object sender, PaintEventArgs e)
		{
			PictureBox picture = (PictureBox)sender;
			if (picture.Image == null || this._runProbes == null || this._runCanvas.Width <= 0)
			{
				return;
			}
			ProbePlaceOn placeOn = ((picture == this._picField) ? ProbePlaceOn.Field : ProbePlaceOn.Intensity);
			RectangleF area = Form1.ZoomedImageRectangle(picture);
			if (area.Width <= 0f)
			{
				return;
			}
			float sx = area.Width / this._runCanvas.Width;
			float sy = area.Height / this._runCanvas.Height;
			for (int i = 0; i < this._runProbes.Count; i++)
			{
				ProbeSettings probe = this._runProbes[i].Clone();
				if (i < this._probes.Count)
				{
					probe.CopyDisplayFrom(this._probes[i]);
				}
				if (probe.PlaceOn != placeOn || !probe.Active)
				{
					continue;
				}
				ProbeGraphPainter.DrawMarkers(e.Graphics, probe, i, area.X, area.Y, sx, sy);
				ProbeTrace trace = null;
				if (i < this._probeTraces.Count)
				{
					trace = this._probeTraces[i];
				}
				Rectangle box = Form1.ToScreen(ProbeGraphPainter.BoxInCanvas(probe, this._runCanvas.Width, this._runCanvas.Height), area, sx, sy);
				ProbeGraphPainter.DrawGraph(e.Graphics, box, probe.Title(i), probe, trace, this._runSteps);
			}
		}

		/// <summary>
		/// Paint overlay for the probe picture on the Probes tab (scaled by <c>EffectiveCanvas()</c>,
		/// falling back to the image size): for every active probe, and for the probe currently being
		/// picked even if inactive, draws the position markers and a translucent dashed yellow placeholder
		/// where the graph box will be, labelled with the probe title. During pick step 3 the placeholder
		/// follows the hovered mouse position instead of the stored box position.
		/// </summary>
		private void ProbePicture_Paint(object sender, PaintEventArgs e)
		{
			if (this._picProbe.Image == null)
			{
				return;
			}
			Size canvas = this.EffectiveCanvas();
			if (canvas.Width <= 0)
			{
				canvas = this._picProbe.Image.Size;
			}
			RectangleF area = Form1.ZoomedImageRectangle(this._picProbe);
			if (area.Width <= 0f)
			{
				return;
			}
			float sx = area.Width / canvas.Width;
			float sy = area.Height / canvas.Height;
			for (int i = 0; i < this._probes.Count; i++)
			{
				ProbeSettings probe = this._probes[i];
				bool picking = this._pickStep != 0 && this._pickIndex == i;
				if (!probe.Active && !picking)
				{
					continue;
				}
				ProbeGraphPainter.DrawMarkers(e.Graphics, probe, i, area.X, area.Y, sx, sy);
				ProbeSettings shown = probe;
				if (this._pickStep == 3 && this._pickIndex == i && this._pickHover != null)
				{
					int hoverX = 0;
					int hoverY = 0;
					if (this.TryPictureToCanvas(this._picProbe, this._pickHover.Value, ref hoverX, ref hoverY))
					{
						shown = probe.Clone();
						shown.BoxX = hoverX;
						shown.BoxY = hoverY;
					}
				}
				Rectangle box = Form1.ToScreen(ProbeGraphPainter.BoxInCanvas(shown, canvas.Width, canvas.Height), area, sx, sy);
				using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, 255, 208, 64)))
				{
					e.Graphics.FillRectangle(fill, box);
				}
				using (Pen outline = new Pen(Color.FromArgb(230, 255, 208, 64), 1.5f))
				{
					outline.DashStyle = DashStyle.Dash;
					e.Graphics.DrawRectangle(outline, box);
				}
				using (Font font = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
				{
					using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(240, 255, 208, 64)))
					{
						e.Graphics.DrawString(probe.Title(i), font, textBrush, box.X + 4, box.Y + 3);
					}
				}
			}
		}

		/// <summary>Replaces <c>_probes</c> with the list from the current project's config (padded/truncated to <see cref="ProbeCount"/>) and shows it; no-op without a project.</summary>
		private void LoadProbesFromProject()
		{
			if (this._project == null)
			{
				return;
			}
			this._probes = ProbeSettings.ListFromConfig(this._project.Config);
			this.NormalizeProbeCount();
			this.ShowProbesInControls();
		}

		/// <summary>Replaces <c>_probes</c> with defaults (new project) and shows them.</summary>
		private void ResetProbes()
		{
			this._probes = Form1.NewProbeList();
			this.ShowProbesInControls();
		}

		/// <summary>Commits every control to <c>_probes</c> and returns independent copies for the <c>SimulationJob</c>, so the worker thread never reads the instances the user keeps editing.</summary>
		private List<ProbeSettings> ProbesForRun()
		{
			foreach (Form1.ProbeControlSet ui in this._probeUi)
			{
				this.ReadProbeControls(ui);
			}
			List<ProbeSettings> copies = new List<ProbeSettings>();
			foreach (ProbeSettings probe in this._probes)
			{
				copies.Add(probe.Clone());
			}
			return copies;
		}

		/// <summary>
		/// Called when a simulation starts: records the run's probe copies, canvas size (canvas pixels)
		/// and total step count for the paint handlers, replaces the traces with one empty
		/// <see cref="ProbeTrace"/> per probe, and repaints.
		/// </summary>
		/// <param name="probes">The copies from <see cref="ProbesForRun"/>; may be null.</param>
		/// <param name="canvas">Canvas size of the run in canvas pixels; the previews scale probe positions by it.</param>
		/// <param name="steps">Total steps of the run, used for the full-scale X axis.</param>
		private void BeginProbeRun(List<ProbeSettings> probes, Size canvas, int steps)
		{
			this._runProbes = probes;
			this._runCanvas = canvas;
			this._runSteps = steps;
			this._probeTraces.Clear();
			if (probes != null)
			{
				for (int i = 0; i < probes.Count; i++)
				{
					this._probeTraces.Add(new ProbeTrace());
				}
			}
			this.InvalidateProbePictures();
		}

		/// <summary>
		/// Receives a batch of probe results from <see cref="SimulationRunner.ProbesUpdated"/>; runs on the
		/// UI thread because the subscription wraps it in <c>PostToUi</c>. Batches from a runner that is no
		/// longer <c>_runner</c> (or arriving after dispose) are dropped. Appends the new samples to each
		/// trace's series, replaces the profile when the batch carries one, updates the sampled step count
		/// and repaints the field/intensity previews (the probe picture shows no live data).
		/// </summary>
		private void ShowProbeSamples(SimulationRunner runner, ProbeUpdate update)
		{
			if (runner != this._runner || base.IsDisposed)
			{
				return;
			}
			int count = Math.Min(update.Samples.Length, this._probeTraces.Count);
			for (int i = 0; i < count; i++)
			{
				this._probeTraces[i].Series.Add(update.Samples[i]);
				if (update.Profiles[i] != null)
				{
					this._probeTraces[i].SetProfile(update.Profiles[i], update.Lengths[i]);
				}
				this._probeTraces[i].StepsSampled = update.StepsDone;
			}
			if (this._picField != null)
			{
				this._picField.Invalidate();
			}
			if (this._picIntensity != null)
			{
				this._picIntensity.Invalidate();
			}
		}
	}
}
