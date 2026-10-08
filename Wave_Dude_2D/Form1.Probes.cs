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
	/// <para>
	/// A probe measures the field (amplitude) or the intensity while the simulation runs and shows
	/// the result in a little graph box in the field or intensity picture: a point probe (A) plots
	/// the value at A against the simulation step; a line probe (A-B) plots the values along the
	/// line A-B (position on the line on X) at the current step, so it changes every step. The
	/// settings are kept in <c>_probes</c> and stored in the "probes" section of the project file.
	/// Each probe has its own "On" check box (<see cref="ProbeSettings.Active"/>): when off, the
	/// probe is hidden (its marker, graph box and burned-in overlay are all skipped) but it keeps
	/// being sampled, so switching it back on shows its recorded history rather than starting from
	/// a blank graph.
	/// </para>
	/// <para>
	/// There are two independent probes, Probe 1 and Probe 2, each with its own group of controls
	/// (<c>grpProbe1</c>/<c>grpProbe2</c>). <c>_probeUi</c> holds one <see cref="ProbeControlSet"/>
	/// per group, so the code that reads/writes a probe's settings and the events of its controls
	/// can be shared between the two groups instead of being written out twice.
	/// </para>
	/// <para>
	/// The Probes tab shows a copy of a simulation picture (Preview tab, "Copy Image..." buttons),
	/// shared by both probes. Each group has its own Pick button: it lets the user click in that
	/// picture for that probe's position A, for a line position B, and for the top left corner of
	/// that probe's graph box.
	/// </para>
	/// <para>
	/// Two coordinate systems are used throughout. Canvas pixels are 0-based cells of the
	/// simulation grid (size from <c>EffectiveCanvas()</c> while editing, <c>_runCanvas</c> for the
	/// current/last run); picture-box pixels are client coordinates of a <see cref="PictureBox"/>
	/// in Zoom mode, where the image is scaled uniformly and centred. <see cref="ZoomedImageRectangle"/>
	/// gives the image's rectangle in picture-box pixels; <see cref="TryPictureToCanvas"/> and
	/// <see cref="ToScreen"/> convert between the two.
	/// </para>
	/// </remarks>
	public partial class Form1
	{
		/// <summary>
		/// The control set for probe <paramref name="index"/>, or null while the form is still being
		/// constructed: <c>InitializeComponent</c> (which runs before <see cref="InitializeProbeControls"/>)
		/// sets some control values away from their .NET default, such as <c>numRunningWindow</c>'s 500,
		/// and that alone fires the control's change event immediately, before <c>_probeUi</c> exists.
		/// The event handlers below go through this instead of indexing <c>_probeUi</c> directly, so
		/// that early firing is a harmless no-op instead of a <see cref="NullReferenceException"/>.
		/// </summary>
		private Form1.ProbeControlSet ProbeUi(int index)
		{
			return _probeUi == null ? null : _probeUi[index];
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

		/// <summary>Keeps <c>_probes</c> at exactly <see cref="ProbeCount"/> entries after loading a project: pads with new (default) probes when a saved project has fewer (from before Probe 2 existed), trims extras when it somehow has more.</summary>
		private void NormalizeProbeCount()
		{
			while (_probes.Count < Form1.ProbeCount)
			{
				_probes.Add(new ProbeSettings());
			}
			while (_probes.Count > Form1.ProbeCount)
			{
				_probes.RemoveAt(_probes.Count - 1);
			}
		}

		/// <summary>
		/// One-time setup from <c>Form1_Load</c>: binds the designer controls of both probe panels into
		/// <c>_probeUi</c>, fills the fixed combo boxes (the choices are made here, not in the designer,
		/// in the order of the <see cref="ProbeSettings"/> enumerations because the selected index is
		/// cast straight to the enum), sets spinner ranges, hooks the paint and mouse handlers of the
		/// probe and preview pictures (which must already exist) and shows the current probes.
		/// </summary>
		private void InitializeProbeControls()
		{
			_probeUi = new Form1.ProbeControlSet[]
			{
				new Form1.ProbeControlSet(0, "Probe 1", chkProbeOn1, cmbPlaceOn, cmbMeasurement, cmbProbeType, cmbBoxSize, numAPosX, numAPosY, numBPosX, numBPosY, butPicPositions, radRunningScale, radFullScale, chkAutoScaleX, chkAutoScaleY, txtMinScaleY, txtMaxScaleY, chkStartAfterStep, txtStartAfterStep, numRunningWindow, new Control[] { numBPosX, numBPosY, Label14, Label15, Label16 }),
				new Form1.ProbeControlSet(1, "Probe 2", chkProbeOn2, cmbPlaceOn2, cmbMeasurement2, cmbProbeType2, cmbBoxSize2, numAPosX2, numAPosY2, numBPosX2, numBPosY2, butPicPositions2, radRunningScale2, radFullScale2, chkAutoScaleX2, chkAutoScaleY2, txtMinScaleY2, txtMaxScaleY2, chkStartAfterStep2, txtStartAfterStep2, numRunningWindow2, new Control[] { numBPosX2, numBPosY2, Label28, Label27, Label25 })
			};
			foreach (Form1.ProbeControlSet ui in _probeUi)
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
					position.Maximum = (decimal)(SceneSettings.MaxCanvasSize - 1);
				}
				ui.NumRunningWindow.DecimalPlaces = 0;
				ui.NumRunningWindow.Minimum = 10m;
				ui.NumRunningWindow.Maximum = ProbeSettings.MaxRunningWindow;
			}
			// the graphs and markers are drawn over the pictures (the pictures themselves stay clean)
			_picField.Paint += PreviewPicture_Paint;
			_picIntensity.Paint += PreviewPicture_Paint;
			_picProbe.Paint += ProbePicture_Paint;
			_picProbe.MouseClick += ProbePicture_MouseClick;
			_picProbe.MouseMove += ProbePicture_MouseMove;
			_picProbe.MouseLeave += ProbePicture_MouseLeave;
			ShowProbesInControls();
		}

		/// <summary>Cancels any position pick in progress and pushes every probe in <c>_probes</c> into its controls.</summary>
		private void ShowProbesInControls()
		{
			EndPick();
			foreach (Form1.ProbeControlSet ui in _probeUi)
			{
				ShowProbeInControls(ui);
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
			ProbeSettings probe = _probes[ui.Index];
			probe.Normalize();
			_loadingProbe = true;
			try
			{
				SetProbeRanges(ui);
				ui.ChkOn.Checked = probe.Active;
				ui.CmbPlaceOn.SelectedIndex = (int)probe.PlaceOn;
				FillMeasurementCombo(ui, probe);
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
				_loadingProbe = false;
			}
			UpdateProbeControlState(ui);
			InvalidateProbePictures();
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
			bool isLine = _probes[ui.Index].Kind == ProbeKind.Line;
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
			if (_probeUi == null)
			{
				return;
			}
			foreach (Form1.ProbeControlSet ui in _probeUi)
			{
				SetProbeRanges(ui);
			}
		}

		/// <summary>
		/// Limits the position spinners to the current canvas size (width-1 / height-1 in canvas pixels,
		/// or <see cref="SceneSettings.MaxCanvasSize"/> - 1 when the size is unknown). Lowering a maximum
		/// can clamp the value and fire ValueChanged, so <c>_loadingProbe</c> is raised meanwhile (and
		/// restored to its previous state, since this is also called from inside <see cref="ShowProbeInControls"/>).
		/// </summary>
		private void SetProbeRanges(Form1.ProbeControlSet ui)
		{
			Size canvas = EffectiveCanvas();
			decimal maxX = new decimal((canvas.Width > 0) ? (canvas.Width - 1) : (SceneSettings.MaxCanvasSize - 1));
			decimal maxY = new decimal((canvas.Height > 0) ? (canvas.Height - 1) : (SceneSettings.MaxCanvasSize - 1));
			bool wasLoading = _loadingProbe;
			_loadingProbe = true;
			try
			{
				ui.NumAPosX.Maximum = maxX;
				ui.NumBPosX.Maximum = maxX;
				ui.NumAPosY.Maximum = maxY;
				ui.NumBPosY.Maximum = maxY;
			}
			finally
			{
				_loadingProbe = wasLoading;
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
			ProbeSettings probe = _probes[ui.Index];
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
			HandleProbeComboChanged(ProbeUi(0), sender);
		}

		/// <summary>Probe 1: an A/B position spinner changed; the new canvas position is stored.</summary>
		private void ProbePosition_ValueChanged(object sender, EventArgs e)
		{
			HandleProbePositionChanged(ProbeUi(0));
		}

		/// <summary>Probe 1: the "on" check box toggled the probe's active flag.</summary>
		private void ProbeOn_CheckedChanged(object sender, EventArgs e)
		{
			HandleProbeOnChanged(ProbeUi(0));
		}

		/// <summary>Probe 1: Enter in a Y-range or start-after text box applies all controls.</summary>
		private void ProbeTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			HandleProbeTextBoxKeyDown(ProbeUi(0), e);
		}

		/// <summary>Probe 1: running/full scale radio button changed.</summary>
		private void ProbeScaleMode_CheckedChanged(object sender, EventArgs e)
		{
			HandleProbeScaleModeChanged(ProbeUi(0));
		}

		/// <summary>Probe 1: auto X / auto Y check box changed.</summary>
		private void ProbeAutoScale_CheckedChanged(object sender, EventArgs e)
		{
			HandleProbeAutoScaleChanged(ProbeUi(0));
		}

		/// <summary>Probe 1: a Y-range text box lost focus; all controls are applied.</summary>
		private void ProbeYRange_Validated(object sender, EventArgs e)
		{
			HandleProbeControlApplied(ProbeUi(0));
		}

		/// <summary>Probe 1: the "start after step" check box toggled.</summary>
		private void ProbeStartAfterStep_CheckedChanged(object sender, EventArgs e)
		{
			HandleProbeStartAfterStepChanged(ProbeUi(0));
		}

		/// <summary>Probe 1: the start-after step text box lost focus; all controls are applied.</summary>
		private void ProbeStartAfterStep_Validated(object sender, EventArgs e)
		{
			HandleProbeControlApplied(ProbeUi(0));
		}

		/// <summary>Probe 1: the running window spinner changed; all controls are applied.</summary>
		private void ProbeRunningWindow_ValueChanged(object sender, EventArgs e)
		{
			HandleProbeControlApplied(ProbeUi(0));
		}

		/// <summary>Probe 2: a combo box changed; see <see cref="HandleProbeComboChanged"/>.</summary>
		private void ProbeCombo2_SelectedIndexChanged(object sender, EventArgs e)
		{
			HandleProbeComboChanged(ProbeUi(1), sender);
		}

		/// <summary>Probe 2: an A/B position spinner changed; the new canvas position is stored.</summary>
		private void ProbePosition2_ValueChanged(object sender, EventArgs e)
		{
			HandleProbePositionChanged(ProbeUi(1));
		}

		/// <summary>Probe 2: the "on" check box toggled the probe's active flag.</summary>
		private void ProbeOn2_CheckedChanged(object sender, EventArgs e)
		{
			HandleProbeOnChanged(ProbeUi(1));
		}

		/// <summary>Probe 2: Enter in a Y-range or start-after text box applies all controls.</summary>
		private void ProbeTextBox2_KeyDown(object sender, KeyEventArgs e)
		{
			HandleProbeTextBoxKeyDown(ProbeUi(1), e);
		}

		/// <summary>Probe 2: running/full scale radio button changed.</summary>
		private void ProbeScaleMode2_CheckedChanged(object sender, EventArgs e)
		{
			HandleProbeScaleModeChanged(ProbeUi(1));
		}

		/// <summary>Probe 2: auto X / auto Y check box changed.</summary>
		private void ProbeAutoScale2_CheckedChanged(object sender, EventArgs e)
		{
			HandleProbeAutoScaleChanged(ProbeUi(1));
		}

		/// <summary>Probe 2: a Y-range text box lost focus; all controls are applied.</summary>
		private void ProbeYRange2_Validated(object sender, EventArgs e)
		{
			HandleProbeControlApplied(ProbeUi(1));
		}

		/// <summary>Probe 2: the "start after step" check box toggled.</summary>
		private void ProbeStartAfterStep2_CheckedChanged(object sender, EventArgs e)
		{
			HandleProbeStartAfterStepChanged(ProbeUi(1));
		}

		/// <summary>Probe 2: the start-after step text box lost focus; all controls are applied.</summary>
		private void ProbeStartAfterStep2_Validated(object sender, EventArgs e)
		{
			HandleProbeControlApplied(ProbeUi(1));
		}

		/// <summary>Probe 2: the running window spinner changed; all controls are applied.</summary>
		private void ProbeRunningWindow2_ValueChanged(object sender, EventArgs e)
		{
			HandleProbeControlApplied(ProbeUi(1));
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
			if (ui == null || _loadingProbe)
			{
				return;
			}
			if (sender == ui.CmbProbeType && _pickStep != 0 && _pickIndex == ui.Index)
			{
				// the number of clicks depends on the type; only cancel a pick that belongs to this probe
				EndPick();
			}
			if (sender == ui.CmbPlaceOn && ui.CmbPlaceOn.SelectedIndex >= 0)
			{
				// the measurements that are possible depend on the picture
				ProbeSettings probe = _probes[ui.Index];
				probe.PlaceOn = (ProbePlaceOn)ui.CmbPlaceOn.SelectedIndex;
				probe.Normalize();
				_loadingProbe = true;
				try
				{
					FillMeasurementCombo(ui, probe);
				}
				finally
				{
					_loadingProbe = false;
				}
			}
			ReadProbeControls(ui);
			UpdateProbeControlState(ui);
			ProbeSettingsChanged();
		}

		/// <summary>Stores the four position spinners (canvas pixels) into the probe and marks the project dirty; ignored while loading.</summary>
		private void HandleProbePositionChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || _loadingProbe)
			{
				return;
			}
			ProbeSettings probe = _probes[ui.Index];
			probe.PosAX = Convert.ToInt32(ui.NumAPosX.Value);
			probe.PosAY = Convert.ToInt32(ui.NumAPosY.Value);
			probe.PosBX = Convert.ToInt32(ui.NumBPosX.Value);
			probe.PosBY = Convert.ToInt32(ui.NumBPosY.Value);
			ProbeSettingsChanged();
		}

		/// <summary>Stores the "on" check box into <see cref="ProbeSettings.Active"/> and marks the project dirty; ignored while loading.</summary>
		private void HandleProbeOnChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || _loadingProbe)
			{
				return;
			}
			_probes[ui.Index].Active = ui.ChkOn.Checked;
			ProbeSettingsChanged();
		}

		/// <summary>
		/// Enter in a probe text box applies the value right away (like losing focus does) instead of
		/// just beeping: the key is swallowed and all controls of the probe are applied. These are
		/// single line boxes, so Enter never inserts an actual line break. Ignored while loading.
		/// </summary>
		private void HandleProbeTextBoxKeyDown(Form1.ProbeControlSet ui, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Return)
			{
				return;
			}
			e.SuppressKeyPress = true;
			e.Handled = true;
			if (ui == null || _loadingProbe)
			{
				return;
			}
			ReadProbeControls(ui);
			ProbeSettingsChanged();
		}

		/// <summary>Stores the scale mode radio buttons into the probe and marks the project dirty; ignored while loading.</summary>
		private void HandleProbeScaleModeChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || _loadingProbe)
			{
				return;
			}
			_probes[ui.Index].ScaleMode = (ui.RadFull.Checked ? ProbeScaleMode.Full : ProbeScaleMode.Running);
			ProbeSettingsChanged();
		}

		/// <summary>
		/// Stores the auto X / auto Y check boxes. When auto Y has just been switched off, the Y range the
		/// running graph currently shows is frozen into the probe (<see cref="FreezeValueRange"/>) and
		/// written into the min/max text boxes, so the user starts from the displayed range rather than
		/// the stale stored one. Ignored while loading.
		/// </summary>
		private void HandleProbeAutoScaleChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || _loadingProbe)
			{
				return;
			}
			ProbeSettings probe = _probes[ui.Index];
			bool wasAutoY = probe.AutoScaleY;
			probe.AutoScaleX = ui.ChkAutoX.Checked;
			probe.AutoScaleY = ui.ChkAutoY.Checked;
			// switching the automatic value axis off keeps the range that is shown now, ready to fine-tune by hand
			if (wasAutoY && !probe.AutoScaleY)
			{
				FreezeValueRange(ui, probe);
				_loadingProbe = true;
				try
				{
					ui.TxtMinY.Text = probe.YMin.ToString("G6", CultureInfo.InvariantCulture);
					ui.TxtMaxY.Text = probe.YMax.ToString("G6", CultureInfo.InvariantCulture);
				}
				finally
				{
					_loadingProbe = false;
				}
			}
			UpdateProbeControlState(ui);
			ProbeSettingsChanged();
		}

		/// <summary>Generic "commit" used by Validated and ValueChanged events: reads all controls back and marks the project dirty; ignored while loading.</summary>
		private void HandleProbeControlApplied(Form1.ProbeControlSet ui)
		{
			if (ui == null || _loadingProbe)
			{
				return;
			}
			ReadProbeControls(ui);
			ProbeSettingsChanged();
		}

		/// <summary>Stores the "start after step" check box, enables/disables its text box and marks the project dirty; ignored while loading.</summary>
		private void HandleProbeStartAfterStepChanged(Form1.ProbeControlSet ui)
		{
			if (ui == null || _loadingProbe)
			{
				return;
			}
			_probes[ui.Index].UseStartAfterStep = ui.ChkStartAfter.Checked;
			UpdateProbeControlState(ui);
			ProbeSettingsChanged();
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
			if (_probeTraces.Count <= index || _runProbes == null || _runProbes.Count <= index)
			{
				return;
			}
			if (_runProbes[index].Kind != p.Kind)
			{
				return;
			}
			ProbeTrace trace = _probeTraces[index];
			if (p.Kind == ProbeKind.Line)
			{
				// a line probe shows nothing at all before the start step: nothing to freeze
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
			ProbeGraphPainter.ComputeRanges(autoScaled, trace, _runSteps, ref xMin, ref xMax, ref yMin, ref yMax);
			if (yMax > yMin)
			{
				p.YMin = yMin;
				p.YMax = yMax;
			}
		}

		/// <summary>Marks the project dirty (unless a loading flag is set) and repaints all three probe-related pictures.</summary>
		private void ProbeSettingsChanged()
		{
			MarkProjectChanged();
			InvalidateProbePictures();
		}

		/// <summary>Invalidates the probe picture and the field/intensity previews so markers and graph boxes are redrawn; safe before the pictures exist.</summary>
		private void InvalidateProbePictures()
		{
			if (_picProbe != null)
			{
				_picProbe.Invalidate();
			}
			if (_picField != null)
			{
				_picField.Invalidate();
			}
			if (_picIntensity != null)
			{
				_picIntensity.Invalidate();
			}
		}

		/// <summary>
		/// Any main tab switch cancels a pick in progress and refreshes the spinner ranges (the canvas
		/// size may have changed on the Configuration tab); switching to the Probes tab additionally
		/// loads the scene image (or a black canvas) into the probe picture if it is still empty.
		/// </summary>
		private void ProbeTabChanged(object sender, EventArgs e)
		{
			EndPick();
			SetProbeRanges();
			if (tabMainFunctions.SelectedTab == tabProbeList)
			{
				AutoLoadSceneImageIfEmpty(_picProbe);
			}
		}

		/// <summary>Probe 1 "pick positions" button: starts picking in the probe picture, or cancels it when already picking.</summary>
		private void butPicPositions_Click(object sender, EventArgs e)
		{
			StartOrCancelPick(ProbeUi(0));
		}

		/// <summary>Probe 2 "pick positions" button: starts picking in the probe picture, or cancels it when already picking.</summary>
		private void butPicPositions2_Click(object sender, EventArgs e)
		{
			StartOrCancelPick(ProbeUi(1));
		}

		/// <summary>Toggles pick mode for one probe: cancels a pick in progress (for any probe) and, unless that pick was this probe's, starts a new one.</summary>
		private void StartOrCancelPick(Form1.ProbeControlSet ui)
		{
			if (ui == null)
			{
				return;
			}
			bool wasPicking = _pickStep != 0 && _pickIndex == ui.Index;
			EndPick();
			if (!wasPicking)
			{
				StartPick(ui);
			}
		}

		/// <summary>
		/// When there is no picture yet to pick from (no simulation has run), makes a plain black one at
		/// the canvas size that the Configuration tab currently specifies (the scene image's size, or the
		/// user defined width/height), so a probe position can still be picked before ever running a
		/// simulation. Replaced by the real picture as soon as one is copied over from the Preview tab.
		/// Does nothing when the canvas size is unknown. The bitmap is owned by the picture box.
		/// </summary>
		private void EnsureProbePicture()
		{
			Size canvas = EffectiveCanvas();
			if (canvas.Width <= 0 || canvas.Height <= 0)
			{
				return;
			}
			Bitmap placeholder = new Bitmap(canvas.Width, canvas.Height);
			using (Graphics g = Graphics.FromImage(placeholder))
			{
				g.Clear(Color.Black);
			}
			_picProbe.Image = placeholder;
		}

		/// <summary>
		/// Enters pick mode for <paramref name="ui"/>'s probe: makes sure the probe picture has an image
		/// (otherwise shows a message and gives up), sets <c>_pickIndex</c>/<c>_pickStep</c> to step 1,
		/// switches to a cross cursor, relabels the pick button "Cancel" and shows the first instruction
		/// in the status bar. Clicks are then handled by <see cref="ProbePicture_MouseClick"/>.
		/// </summary>
		private void StartPick(Form1.ProbeControlSet ui)
		{
			if (_picProbe.Image == null)
			{
				EnsureProbePicture();
			}
			if (_picProbe.Image == null)
			{
				MessageBox.Show(this, "There is no picture to pick from yet, and the canvas size is not known either\r\n(choose a scene image, or a user defined width and height, on the Configuration tab first).", "Probe positions", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			_pickIndex = ui.Index;
			_pickStep = 1;
			_pickHover = null;
			_picProbe.Cursor = Cursors.Cross;
			ui.ButPick.Text = "Cancel";
			ShowPickInstruction();
		}

		/// <summary>Writes the status-bar instruction for the current pick step (1: position A / the point, 2: position B, 3: top-left corner of the graph box).</summary>
		private void ShowPickInstruction()
		{
			Form1.ProbeControlSet ui = _probeUi[_pickIndex];
			switch (_pickStep)
			{
			case 1:
				ToolStripStatusLabel1.Text = ((_probes[_pickIndex].Kind == ProbeKind.Line) ? (ui.Title + ": click in the picture for position A, the start of the line (press Cancel to stop picking).") : (ui.Title + ": click in the picture for the position of the probe (press Cancel to stop picking)."));
				break;
			case 2:
				ToolStripStatusLabel1.Text = ui.Title + ": click in the picture for position B, the end of the line.";
				break;
			case 3:
				ToolStripStatusLabel1.Text = ui.Title + ": click in the picture for the top left corner of the graph box.";
				break;
			default:
				ToolStripStatusLabel1.Text = "";
				break;
			}
		}

		/// <summary>Leaves pick mode: restores the pick button text, clears step and hover, resets the cursor, repaints, and clears the status bar if a pick was active. Safe to call when nothing is being picked or before the probe picture exists.</summary>
		private void EndPick()
		{
			bool wasPicking = _pickStep != 0;
			if (wasPicking)
			{
				_probeUi[_pickIndex].ButPick.Text = _probeUi[_pickIndex].PickButtonText;
			}
			_pickStep = 0;
			_pickHover = null;
			if (_picProbe != null)
			{
				_picProbe.Cursor = Cursors.Default;
				_picProbe.Invalidate();
			}
			if (wasPicking)
			{
				ToolStripStatusLabel1.Text = "";
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
			if (_pickStep == 0 || e.Button != MouseButtons.Left)
			{
				return;
			}
			Form1.ProbeControlSet ui = _probeUi[_pickIndex];
			int x = 0;
			int y = 0;
			if (!TryPictureToCanvas(_picProbe, e.Location, ref x, ref y))
			{
				return;
			}
			switch (_pickStep)
			{
			case 1:
				ui.NumAPosX.Value = Form1.ClampToRange(ui.NumAPosX, x);
				ui.NumAPosY.Value = Form1.ClampToRange(ui.NumAPosY, y);
				_pickStep = ((_probes[_pickIndex].Kind == ProbeKind.Line) ? 2 : 3);
				ShowPickInstruction();
				break;
			case 2:
				ui.NumBPosX.Value = Form1.ClampToRange(ui.NumBPosX, x);
				ui.NumBPosY.Value = Form1.ClampToRange(ui.NumBPosY, y);
				_pickStep = 3;
				ShowPickInstruction();
				break;
			default:
				_probes[_pickIndex].BoxX = x;
				_probes[_pickIndex].BoxY = y;
				ProbeSettingsChanged();
				EndPick();
				break;
			}
			_picProbe.Invalidate();
		}

		/// <summary>During step 3 (graph box placement) remembers the mouse position in picture-box pixels so <see cref="ProbePicture_Paint"/> can preview the box there.</summary>
		private void ProbePicture_MouseMove(object sender, MouseEventArgs e)
		{
			if (_pickStep == 3)
			{
				_pickHover = new Point?(e.Location);
				_picProbe.Invalidate();
			}
		}

		/// <summary>Drops the box preview position when the mouse leaves the probe picture.</summary>
		private void ProbePicture_MouseLeave(object sender, EventArgs e)
		{
			if (_pickHover != null)
			{
				_pickHover = null;
				_picProbe.Invalidate();
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
			Size canvas = EffectiveCanvas();
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
			if (picture.Image == null || _runProbes == null || _runCanvas.Width <= 0)
			{
				return;
			}
			ProbePlaceOn placeOn = ((picture == _picField) ? ProbePlaceOn.Field : ProbePlaceOn.Intensity);
			RectangleF area = Form1.ZoomedImageRectangle(picture);
			if (area.Width <= 0f)
			{
				return;
			}
			float sx = area.Width / _runCanvas.Width;
			float sy = area.Height / _runCanvas.Height;
			for (int i = 0; i < _runProbes.Count; i++)
			{
				// what is measured comes from the run; how the graph looks can be changed while it runs
				ProbeSettings probe = _runProbes[i].Clone();
				if (i < _probes.Count)
				{
					probe.CopyDisplayFrom(_probes[i]);
				}
				if (probe.PlaceOn != placeOn || !probe.Active)
				{
					continue;
				}
				ProbeGraphPainter.DrawMarkers(e.Graphics, probe, i, area.X, area.Y, sx, sy);
				ProbeTrace trace = null;
				if (i < _probeTraces.Count)
				{
					trace = _probeTraces[i];
				}
				Rectangle rect = Form1.ToScreen(ProbeGraphPainter.BoxInCanvas(probe, _runCanvas.Width, _runCanvas.Height), area, sx, sy);
				ProbeGraphPainter.DrawGraph(e.Graphics, rect, probe.Title(i), probe, trace, _runSteps);
			}
		}

		/// <summary>
		/// Paint overlay for the probe picture on the Probes tab (scaled by <c>EffectiveCanvas()</c>,
		/// falling back to the image size): the markers of both probes, and the graph box outline of
		/// whichever one is being edited or positioned, as a translucent dashed yellow placeholder
		/// labelled with the probe title. A switched off probe ("On" unchecked) is hidden here too; the
		/// only exception is while the user is actively picking its position or graph box, so they can
		/// still see what they are placing. During pick step 3 the placeholder follows the hovered mouse
		/// position instead of the stored box position.
		/// </summary>
		private void ProbePicture_Paint(object sender, PaintEventArgs e)
		{
			if (_picProbe.Image == null)
			{
				return;
			}
			Size canvas = EffectiveCanvas();
			if (canvas.Width <= 0)
			{
				canvas = _picProbe.Image.Size;
			}
			RectangleF area = Form1.ZoomedImageRectangle(_picProbe);
			if (area.Width <= 0f)
			{
				return;
			}
			float sx = area.Width / canvas.Width;
			float sy = area.Height / canvas.Height;
			for (int i = 0; i < _probes.Count; i++)
			{
				ProbeSettings probe = _probes[i];
				bool picking = _pickStep != 0 && _pickIndex == i;
				if (!probe.Active && !picking)
				{
					continue;
				}
				ProbeGraphPainter.DrawMarkers(e.Graphics, probe, i, area.X, area.Y, sx, sy);
				// while the corner of this probe's box is being picked, the box follows the mouse
				ProbeSettings shown = probe;
				if (_pickStep == 3 && _pickIndex == i && _pickHover != null)
				{
					int hoverX = 0;
					int hoverY = 0;
					if (TryPictureToCanvas(_picProbe, _pickHover.Value, ref hoverX, ref hoverY))
					{
						shown = probe.Clone();
						shown.BoxX = hoverX;
						shown.BoxY = hoverY;
					}
				}
				Rectangle rect = Form1.ToScreen(ProbeGraphPainter.BoxInCanvas(shown, canvas.Width, canvas.Height), area, sx, sy);
				using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, 255, 208, 64)))
				{
					e.Graphics.FillRectangle(fill, rect);
				}
				using (Pen outline = new Pen(Color.FromArgb(230, 255, 208, 64), 1.5f))
				{
					outline.DashStyle = DashStyle.Dash;
					e.Graphics.DrawRectangle(outline, rect);
				}
				using (Font font = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
				{
					using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(240, 255, 208, 64)))
					{
						e.Graphics.DrawString(probe.Title(i), font, textBrush, rect.X + 4, rect.Y + 3);
					}
				}
			}
		}

		/// <summary>Replaces <c>_probes</c> with the list from the current project's config (padded/truncated to <see cref="ProbeCount"/>) and shows it; no-op without a project.</summary>
		private void LoadProbesFromProject()
		{
			if (_project == null)
			{
				return;
			}
			_probes = ProbeSettings.ListFromConfig(_project.Config);
			NormalizeProbeCount();
			ShowProbesInControls();
		}

		/// <summary>Replaces <c>_probes</c> with defaults (new project) and shows them.</summary>
		private void ResetProbes()
		{
			_probes = Form1.NewProbeList();
			ShowProbesInControls();
		}

		/// <summary>Commits every control to <c>_probes</c> and returns independent copies for the <c>SimulationJob</c>, so the worker thread never reads the instances the user keeps editing.</summary>
		private List<ProbeSettings> ProbesForRun()
		{
			foreach (Form1.ProbeControlSet ui in _probeUi)
			{
				ReadProbeControls(ui);
			}
			List<ProbeSettings> copies = new List<ProbeSettings>();
			foreach (ProbeSettings probe in _probes)
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
			_runProbes = probes;
			_runCanvas = canvas;
			_runSteps = steps;
			_probeTraces.Clear();
			if (probes != null)
			{
				for (int i = 0; i < probes.Count; i++)
				{
					_probeTraces.Add(new ProbeTrace());
				}
			}
			InvalidateProbePictures();
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
			if (runner != _runner || base.IsDisposed)
			{
				return;
			}
			int count = Math.Min(update.Samples.Length, _probeTraces.Count);
			for (int i = 0; i < count; i++)
			{
				_probeTraces[i].Series.Add(update.Samples[i]);
				if (update.Profiles[i] != null)
				{
					_probeTraces[i].SetProfile(update.Profiles[i], update.Lengths[i]);
				}
				_probeTraces[i].StepsSampled = update.StepsDone;
			}
			if (_picField != null)
			{
				_picField.Invalidate();
			}
			if (_picIntensity != null)
			{
				_picIntensity.Invalidate();
			}
		}
	}
}
