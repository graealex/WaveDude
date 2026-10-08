using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Simulation: the settings on the Configuration tab (steps, video export, previews, color schemes) are kept
	/// in <c>_simulation</c> (a <see cref="SimulationSettings"/>) and saved in configuration.json with the
	/// project. "Start simulation" hands a copy of everything to a <see cref="SimulationRunner"/>, which does the
	/// work on a background thread. The runner reports back through events; <see cref="PostToUi"/> passes them
	/// on to this (the UI) thread. Also home to the Preview tab (run/pause/stop, the field / intensity preview
	/// boxes with the glow-edges overlay, sharing pictures with the Sources/Probes tabs, still-frame export,
	/// opening the output folders in Explorer). Everything here runs on the UI thread unless stated otherwise.
	/// </summary>
	public partial class Form1
	{
		/// <summary>
		/// Called once from <c>Form1_Load</c>: sets the ranges of the simulation NumericUpDowns (the viewer-tab scaling boxes copy
		/// the configuration-tab ones), creates the four black preview boxes and puts the buttons, labels and status bar in their idle state.
		/// </summary>
		private void InitializeSimulationControls()
		{
			numVisualizeEvery.Minimum = 1m;

			// brightness factors of the field and intensity pictures (render_field / render_intensity in the Python code)
			numFieldScaling.Minimum = 0.1m;
			numFieldScaling.Maximum = 1000m;
			numIntensityScaling.Minimum = 0.1m;
			numIntensityScaling.Maximum = 1000m;

			// numFieldScaling2/numIntensityScaling2 (Viewer tab) are a second set of controls for the very same
			// FieldBrightness/IntensityBrightness values as numFieldScaling/numIntensityScaling (Configuration tab) -
			// see numScaleFieldValues_ValueChanged/numScaleIntensity_ValueChanged and their "2" counterparts, which
			// keep both pairs in sync. Match the originals' range/precision so a value entered in either pair means
			// the same thing and is not rejected/rounded differently by the other.
			numFieldScaling2.Minimum = numFieldScaling.Minimum;
			numFieldScaling2.Maximum = numFieldScaling.Maximum;
			numFieldScaling2.DecimalPlaces = numFieldScaling.DecimalPlaces;
			numFieldScaling2.Increment = numFieldScaling.Increment;
			numIntensityScaling2.Minimum = numIntensityScaling.Minimum;
			numIntensityScaling2.Maximum = numIntensityScaling.Maximum;
			numIntensityScaling2.DecimalPlaces = numIntensityScaling.DecimalPlaces;
			numIntensityScaling2.Increment = numIntensityScaling.Increment;

			// offsets (black levels of the color tables): 2 decimals, otherwise the default of -0.05 would be rounded
			foreach (NumericUpDown offsetBox in new NumericUpDown[] { numOffsetField, numOffsetIntensity })
			{
				offsetBox.DecimalPlaces = 2;
				offsetBox.Minimum = -1m;
				offsetBox.Maximum = 1m;
				offsetBox.Increment = 0.01m;
			}

			numQuality.Minimum = 1m;
			numQuality.Maximum = 100m;
			numQuality.DecimalPlaces = 0;

			// the summary of a run is shown in the status bar, its details in the tooltip
			StatusStrip1.ShowItemToolTips = true;
			ToolStripStatusLabel1.Spring = true;
			ToolStripStatusLabel1.TextAlign = ContentAlignment.MiddleLeft;
			ToolStripStatusLabel1.Text = "";

			UpdateRunButtons();

			btnViewIntensityVideo.Text = "View intensity video in media player";
			UpdateVideoButtons();
			cmbFieldColorScheme.DropDownStyle = ComboBoxStyle.DropDownList;
			cmbIntensityColorScheme.DropDownStyle = ComboBoxStyle.DropDownList;

			_picField = Form1.MakePreviewBox(pnlPreviewField);
			_picIntensity = Form1.MakePreviewBox(pnlPreviewIntensity);
			_picSource = Form1.MakePreviewBox(pnlSourceImage);
			_picProbe = Form1.MakePreviewBox(pnlProbeImage);
			UpdateCopyButtons();

			// warn, in lblWarningField/lblWarningIntensity, when the last (or next) run will not export that video -
			// shown/hidden per video in UpdateSimulationControlState; these are Designer labels placed in the clear,
			// not (like the dynamic label this used to be) a control positioned over other controls at runtime
			lblWarningField.Text = "Field video not exported";
			lblWarningField.ForeColor = Color.DarkBlue;
			lblWarningIntensity.Text = "Intensity video not exported";
			lblWarningIntensity.ForeColor = Color.DarkBlue;

			// No ShowSimulationInControls() here: grpSimulationSettings is disabled until a real project exists
			// (UpdateProjectAccess). Steps and TimeDecayFactor have no software default of their own any more (see
			// their comments in SimulationSettings) - populating the controls now, from the blank _simulation this
			// form starts with, would clamp numSimulationSteps/numTimeDecayFactor down from their Designer defaults
			// (3000 and 10) to their Minimum before StartNewProject ever gets a chance to read the Designer value back
			// out of them. StartNewProject/SetCurrentProject call ShowSimulationInControls() themselves once a real
			// project exists.
		}

		/// <summary>Creates a black, zoom-to-fit <see cref="PictureBox"/> that fills <paramref name="host"/>.</summary>
		private static PictureBox MakePreviewBox(Panel host)
		{
			host.BackColor = Color.Black;
			PictureBox pictureBox = new PictureBox();
			pictureBox.Dock = DockStyle.Fill;
			pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
			pictureBox.BackColor = Color.Black;
			host.Controls.Add(pictureBox);
			return pictureBox;
		}

		/// <summary>
		/// Copies <c>_simulation</c> into the controls, clamping every value to its box's range (the clamped
		/// <c>VisualizeEvery</c> is written back into the settings). <c>_loadingSim</c> is set meanwhile so the
		/// ValueChanged handlers neither write back nor mark the project dirty.
		/// </summary>
		private void ShowSimulationInControls()
		{
			_loadingSim = true;
			try
			{
				// the range of the numeric box is set in the designer; keep the settings inside it
				int visualizeEvery = Math.Max(Convert.ToInt32(numVisualizeEvery.Minimum), Math.Min(Convert.ToInt32(numVisualizeEvery.Maximum), _simulation.VisualizeEvery));
				_simulation.VisualizeEvery = visualizeEvery;
				numSimulationSteps.Value = Form1.ClampToRange(numSimulationSteps, _simulation.Steps);
				numVisualizeEvery.Value = visualizeEvery;
				numFieldScaling.Value = Form1.ClampToRange(numFieldScaling, _simulation.FieldBrightness);
				numIntensityScaling.Value = Form1.ClampToRange(numIntensityScaling, _simulation.IntensityBrightness);
				numFieldScaling2.Value = Form1.ClampToRange(numFieldScaling2, _simulation.FieldBrightness);
				numIntensityScaling2.Value = Form1.ClampToRange(numIntensityScaling2, _simulation.IntensityBrightness);
				numQuality.Value = Form1.ClampToRange(numQuality, _simulation.VideoQuality);
				numOffsetField.Value = Form1.ClampToRange(numOffsetField, _simulation.FieldBlackLevel);
				numOffsetIntensity.Value = Form1.ClampToRange(numOffsetIntensity, _simulation.IntensityBlackLevel);
				numTimeDecayFactor.Value = Form1.ClampToRange(numTimeDecayFactor, _simulation.TimeDecayFactor);
				chkExportVideoField.Checked = _simulation.ExportField;
				chkExportVideoIntensity.Checked = _simulation.ExportIntensity;
				chkExportVideoField2.Checked = _simulation.ExportField;
				chkExportVideoIntensity2.Checked = _simulation.ExportIntensity;
				chkPreviewVideoField.Checked = _simulation.PreviewField;
				chkPreviewVideoIntensity.Checked = _simulation.PreviewIntensity;
				chkUseGPU.Checked = _simulation.UseGpu;
				chkUse4thOrderLaplacian.Checked = _simulation.Use4thOrderLaplacian;
				FillSchemeCombo(cmbFieldColorScheme, ColorScheme.SchemeTypeField, _simulation.FieldColorScheme);
				FillSchemeCombo(cmbIntensityColorScheme, ColorScheme.SchemeTypeIntensity, _simulation.IntensityColorScheme);
			}
			finally
			{
				_loadingSim = false;
			}
			UpdateSimulationControlState();
		}

		/// <summary>Enables the scheme combos, the quality box and the "not exported" warnings according to the export / preview check boxes.</summary>
		private void UpdateSimulationControlState()
		{
			cmbFieldColorScheme.Enabled = chkExportVideoField.Checked || chkPreviewVideoField.Checked;
			cmbIntensityColorScheme.Enabled = chkExportVideoIntensity.Checked || chkPreviewVideoIntensity.Checked;
			// the quality only matters for exported videos
			bool exporting = chkExportVideoField.Checked || chkExportVideoIntensity.Checked;
			numQuality.Enabled = exporting;
			lblVideoQuality.Enabled = exporting;
			lblWarningField.Visible = !chkExportVideoField.Checked;
			lblWarningIntensity.Visible = !chkExportVideoIntensity.Checked;
		}

		/// <summary>
		/// Refills both scheme combos from <c>_schemes</c> (after the scheme list was reloaded) keeping the stored selections,
		/// without dirtying the project. Saves and restores <c>_loadingSim</c> so it can be called while a load is in progress.
		/// </summary>
		private void RefreshSchemeCombos()
		{
			bool wasLoading = _loadingSim;
			_loadingSim = true;
			try
			{
				FillSchemeCombo(cmbFieldColorScheme, ColorScheme.SchemeTypeField, _simulation.FieldColorScheme);
				FillSchemeCombo(cmbIntensityColorScheme, ColorScheme.SchemeTypeIntensity, _simulation.IntensityColorScheme);
			}
			finally
			{
				_loadingSim = wasLoading;
			}
		}

		/// <summary>Fills <paramref name="combo"/> with the schemes of one type and selects the one named <paramref name="selectedName"/> (case-insensitive), or nothing. Schemes without a type are left out.</summary>
		/// <param name="combo">The field or intensity scheme combo; its items become <see cref="ColorScheme"/> objects.</param>
		/// <param name="schemeType"><see cref="ColorScheme.SchemeTypeField"/> or <see cref="ColorScheme.SchemeTypeIntensity"/>.</param>
		/// <param name="selectedName">Name of the scheme to select; no selection when it is not in the list.</param>
		private void FillSchemeCombo(ComboBox combo, string schemeType, string selectedName)
		{
			combo.BeginUpdate();
			try
			{
				combo.Items.Clear();
				ColorScheme selected = null;
				foreach (ColorScheme scheme in _schemes)
				{
					if (string.Equals(scheme.SchemeType, schemeType, StringComparison.OrdinalIgnoreCase))
					{
						combo.Items.Add(scheme);
						if (string.Equals(scheme.Name, selectedName, StringComparison.OrdinalIgnoreCase))
						{
							selected = scheme;
						}
					}
				}
				if (selected != null)
				{
					combo.SelectedItem = selected;
				}
				else
				{
					combo.SelectedIndex = -1;
				}
			}
			finally
			{
				combo.EndUpdate();
			}
		}

		/// <summary>Looks up a loaded scheme by name and type (both case-insensitive).</summary>
		/// <returns>The scheme, or null when no such scheme is loaded.</returns>
		private ColorScheme FindScheme(string name, string schemeType)
		{
			foreach (ColorScheme scheme in _schemes)
			{
				if (string.Equals(scheme.Name, name, StringComparison.OrdinalIgnoreCase) && string.Equals(scheme.SchemeType, schemeType, StringComparison.OrdinalIgnoreCase))
				{
					return scheme;
				}
			}
			return null;
		}

		/// <summary>
		/// Switching to the Configuration tab reloads the scheme list from the scheme folder (or just refills the combos when the
		/// folder does not exist); switching to the Output tab refreshes its project list, preferring the current project.
		/// </summary>
		private void tabMainFunctions_SelectedIndexChanged(object sender, EventArgs e)
		{
			// cmbFieldColorScheme/cmbIntensityColorScheme live on the Configuration tab. Schemes may have been added,
			// renamed or deleted on the Color Scheme tab since these were last filled - or even just added to the
			// folder from outside the app - so re-read the scheme folder from disk (RefreshSchemeCombos alone only
			// refills the combos from the in-memory list, which can be stale) rather than assume it is still current.
			if (tabMainFunctions.SelectedTab == tabConfiguration)
			{
				string schemeFolder = txtSchemeFolder.Text.Trim();
				if (Directory.Exists(schemeFolder))
				{
					LoadSchemeList();
				}
				else
				{
					RefreshSchemeCombos();
				}
			}
			// lstAllProjects3 (Outputs tab) has the same staleness problem - a project may have appeared, been
			// renamed or been deleted since it was last filled, so re-read it fresh on every arrival too. Default the
			// selection to whichever project is currently open in the editor (falls back to the previous selection,
			// or none, when no project is open - see RefreshOutputProjectList).
			if (tabMainFunctions.SelectedTab == tabOutput)
			{
				RefreshOutputProjectList((_project != null) ? _project.Name : "");
			}
		}

		/// <summary>
		/// Stores the number of steps and marks the project dirty. <c>numSimulationSteps</c> replaces the old
		/// <c>cmbSimulationSteps</c> combo box, so the number of steps is no longer limited to a fixed preset list -
		/// the range (currently 1 to 50,000) is set in the Designer, and the control itself never allows an
		/// out-of-range or non-integer value, so unlike the old free-text combo this needs no separate
		/// parsing/validation/error-coloring here.
		/// </summary>
		private void numSimulationSteps_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.Steps = Convert.ToInt32(numSimulationSteps.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>Stores how many steps go by between two rendered frames.</summary>
		private void numVisualizeEvery_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.VisualizeEvery = Convert.ToInt32(numVisualizeEvery.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>
		/// Copies a value into a NumericUpDown that mirrors another one (<c>numFieldScaling</c>/<c>numFieldScaling2</c>,
		/// <c>numIntensityScaling</c>/<c>numIntensityScaling2</c> - Configuration tab and Viewer tab controls for the
		/// very same setting), without re-triggering that control's own ValueChanged - every simulation control's
		/// ValueChanged already checks <c>_loadingSim</c> first, so reusing it here as the reentrancy guard stops
		/// the two controls from setting each other back and forth.
		/// </summary>
		private void SetLinkedScalingValue(NumericUpDown box, decimal value)
		{
			decimal clamped = (value < box.Minimum) ? box.Minimum : ((value > box.Maximum) ? box.Maximum : value);
			if (box.Value != clamped)
			{
				bool wasLoading = _loadingSim;
				_loadingSim = true;
				try
				{
					box.Value = clamped;
				}
				finally
				{
					_loadingSim = wasLoading;
				}
			}
		}

		/// <summary>
		/// Copies a checked state into a CheckBox that mirrors another one (<c>chkExportVideoField</c>/
		/// <c>chkExportVideoField2</c>, <c>chkExportVideoIntensity</c>/<c>chkExportVideoIntensity2</c> - Configuration
		/// tab and Viewer tab controls for the very same setting) - the same <c>_loadingSim</c> reentrancy-guard idea
		/// as <see cref="SetLinkedScalingValue"/>, for a CheckBox's Checked instead of a NumericUpDown's Value.
		/// </summary>
		private void SetLinkedCheckedValue(CheckBox box, bool value)
		{
			if (box.Checked != value)
			{
				bool wasLoading = _loadingSim;
				_loadingSim = true;
				try
				{
					box.Checked = value;
				}
				finally
				{
					_loadingSim = wasLoading;
				}
			}
		}

		/// <summary>Stores the field brightness and mirrors it into the viewer-tab box.</summary>
		private void numScaleFieldValues_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.FieldBrightness = Convert.ToDouble(numFieldScaling.Value);
				SetLinkedScalingValue(numFieldScaling2, numFieldScaling.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>Viewer-tab copy: stores the field brightness and mirrors it back into the configuration-tab box.</summary>
		private void numScaleFieldValues2_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.FieldBrightness = Convert.ToDouble(numFieldScaling2.Value);
				SetLinkedScalingValue(numFieldScaling, numFieldScaling2.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>Stores the video quality (1..100).</summary>
		private void numQuality_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.VideoQuality = Convert.ToInt32(numQuality.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>Stores the field black level (-1..1 palette offset).</summary>
		private void numOffsetField_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.FieldBlackLevel = Convert.ToDouble(numOffsetField.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>Stores the intensity black level (-1..1 palette offset).</summary>
		private void numOffsetIntensity_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.IntensityBlackLevel = Convert.ToDouble(numOffsetIntensity.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>Stores the time decay factor.</summary>
		private void numTimeDecayFactor_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.TimeDecayFactor = Convert.ToDouble(numTimeDecayFactor.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>Stores the intensity brightness and mirrors it into the viewer-tab box.</summary>
		private void numScaleIntensity_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.IntensityBrightness = Convert.ToDouble(numIntensityScaling.Value);
				SetLinkedScalingValue(numIntensityScaling2, numIntensityScaling.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>Viewer-tab copy: stores the intensity brightness and mirrors it back into the configuration-tab box.</summary>
		private void numScaleIntensity2_ValueChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.IntensityBrightness = Convert.ToDouble(numIntensityScaling2.Value);
				SetLinkedScalingValue(numIntensityScaling, numIntensityScaling2.Value);
				MarkProjectChanged();
			}
		}

		/// <summary>
		/// Shared handler of the four export / preview boxes on the configuration tab: stores all four flags, mirrors the two
		/// export flags into the viewer tab and updates the dependent controls.
		/// </summary>
		private void SimulationOption_CheckedChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.ExportField = chkExportVideoField.Checked;
				_simulation.ExportIntensity = chkExportVideoIntensity.Checked;
				_simulation.PreviewField = chkPreviewVideoField.Checked;
				_simulation.PreviewIntensity = chkPreviewVideoIntensity.Checked;
				SetLinkedCheckedValue(chkExportVideoField2, chkExportVideoField.Checked);
				SetLinkedCheckedValue(chkExportVideoIntensity2, chkExportVideoIntensity.Checked);
				UpdateSimulationControlState();
				MarkProjectChanged();
			}
		}

		/// <summary>Viewer-tab copy of the field export box: stores the flag and mirrors it back.</summary>
		private void chkExportVideoField2_CheckedChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.ExportField = chkExportVideoField2.Checked;
				SetLinkedCheckedValue(chkExportVideoField, chkExportVideoField2.Checked);
				UpdateSimulationControlState();
				MarkProjectChanged();
			}
		}

		/// <summary>Viewer-tab copy of the intensity export box: stores the flag and mirrors it back.</summary>
		private void chkExportVideoIntensity2_CheckedChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.ExportIntensity = chkExportVideoIntensity2.Checked;
				SetLinkedCheckedValue(chkExportVideoIntensity, chkExportVideoIntensity2.Checked);
				UpdateSimulationControlState();
				MarkProjectChanged();
			}
		}

		/// <summary>Stores whether the OpenCL engine is tried first.</summary>
		private void chkUseGPU_CheckedChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.UseGpu = chkUseGPU.Checked;
				MarkProjectChanged();
			}
		}

		/// <summary>Stores the Laplacian order choice.</summary>
		private void chkUse4thOrderLaplacian_CheckedChanged(object sender, EventArgs e)
		{
			if (!_loadingSim)
			{
				_simulation.Use4thOrderLaplacian = chkUse4thOrderLaplacian.Checked;
				MarkProjectChanged();
			}
		}

		/// <summary>Shared handler of both scheme combos: stores the chosen scheme name in the field or intensity setting, depending on which combo raised it.</summary>
		private void SchemeCombo_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_loadingSim)
			{
				return;
			}
			ComboBox combo = (ComboBox)sender;
			ColorScheme scheme = combo.SelectedItem as ColorScheme;
			if (scheme == null)
			{
				return;
			}
			if (combo == cmbFieldColorScheme)
			{
				_simulation.FieldColorScheme = scheme.Name;
			}
			else
			{
				_simulation.IntensityColorScheme = scheme.Name;
			}
			MarkProjectChanged();
		}

		/// <summary>Starts a run, or asks the running one to stop (the button doubles as Stop while a run is active).</summary>
		private void btnStartSimulation_Click(object sender, EventArgs e)
		{
			if (_runner != null)
			{
				// the button is also a stop button while a simulation runs
				RequestStop();
			}
			else
			{
				StartSimulation();
			}
		}

		/// <summary>
		/// Puts a copy of the current scene image (or the black placeholder when there is none) in
		/// <c>pnlSourceImage</c> and <c>pnlProbeImage</c> - the same picture the "Import scene image" buttons on
		/// those tabs load, just from here instead (<see cref="ImportSceneImageInto"/>).
		/// </summary>
		private void btnShareSceneImage_Click(object sender, EventArgs e)
		{
			ImportSceneImageInto(_picSource);
			ImportSceneImageInto(_picProbe);
		}

		/// <summary>Copies the field preview into the Sources and Probes picture boxes.</summary>
		private void btnShareFieldImage_Click(object sender, EventArgs e)
		{
			CopyPreviewToTabs(_picField);
		}

		/// <summary>Copies the intensity preview into the Sources and Probes picture boxes.</summary>
		private void btnShareIntensityImage_Click(object sender, EventArgs e)
		{
			CopyPreviewToTabs(_picIntensity);
		}

		/// <summary>
		/// Gives <c>_picSource</c> and <c>_picProbe</c> (Sources tab and Probes tab) each their own copy of the image
		/// in <paramref name="source"/>, disposing what they showed before - <c>source.Image</c> exactly as currently
		/// displayed, glow-edges overlay baked in when "Display overlay" happens to be checked for that preview
		/// (<see cref="SetPreviewFrame"/>/<see cref="RefreshPreviewOverlay"/>), and not when it is not. Does nothing
		/// when there is no image.
		/// </summary>
		private void CopyPreviewToTabs(PictureBox source)
		{
			Image image = source.Image;
			if (image == null)
			{
				return;
			}
			foreach (PictureBox target in new PictureBox[] { _picSource, _picProbe })
			{
				Bitmap copy = new Bitmap(image);
				Image previous = target.Image;
				target.Image = copy;
				if (previous != null)
				{
					previous.Dispose();
				}
			}
		}

		/// <summary>
		/// The field/intensity share buttons are only on when their preview shows a picture; <c>btnShareSceneImage</c>
		/// is not gated the same way, matching "Import scene image" on the Sources/Probes tabs, which always works
		/// (it falls back to a black placeholder when there is no scene image). Also refreshes the export buttons.
		/// </summary>
		private void UpdateCopyButtons()
		{
			btnShareFieldImage.Enabled = _picField != null && _picField.Image != null;
			btnShareIntensityImage.Enabled = _picIntensity != null && _picIntensity.Image != null;
			UpdateExportButtons();
		}

		/// <summary>
		/// The "Export current Field/Intensity frame" buttons are only on while the simulation is not actively
		/// running (paused, or fully stopped) and there is something to export: either a live, paused engine
		/// (<see cref="SimulationRunner.RenderCurrentFrame"/>), a full-resolution snapshot cached from the last run
		/// (<c>_lastFullFieldFrame</c> / <c>_lastFullIntensityFrame</c>), or at least the picture currently shown in
		/// the preview.
		/// </summary>
		private void UpdateExportButtons()
		{
			bool canExport = _paused || _runner == null;
			btnExportFieldFrame.Enabled = canExport && (_lastFullFieldFrame != null || (_picField != null && _picField.Image != null));
			btnExportIntensityFrame.Enabled = canExport && (_lastFullIntensityFrame != null || (_picIntensity != null && _picIntensity.Image != null));
		}

		/// <summary>
		/// Toggles pause: blocks or releases the runner's gate, switches the status texts and shows the viewer tab on resume.
		/// Ignored when nothing runs or a stop is already pending.
		/// </summary>
		private void btnPauzeResume_Click(object sender, EventArgs e)
		{
			if (_runner == null || _stopRequested)
			{
				return;
			}
			if (_paused)
			{
				_paused = false;
				_simStatus = _progressText;
				ToolStripStatusLabel1.Text = "Simulation running...";
				_runner.ResumeRun();
				// back to the Preview tab, so the user follows the simulation again
				tabMainFunctions.SelectedTab = tabViewer;
			}
			else
			{
				_paused = true;
				_simStatus = "paused - " + _progressText;
				ToolStripStatusLabel1.Text = "Simulation paused (press Resume to continue, or Stop to finish and save the videos)";
				_runner.PauseRun();
			}
			UpdateRunButtons();
			UpdateTitle();
		}

		/// <summary>Shows the Configuration tab.</summary>
		private void btnGotoConfiguration_Click(object sender, EventArgs e)
		{
			tabMainFunctions.SelectedTab = tabConfiguration;
		}

		/// <summary>Shows the Sources tab.</summary>
		private void btnGotoSources_Click(object sender, EventArgs e)
		{
			tabMainFunctions.SelectedTab = tabSourceList;
		}

		/// <summary>Shows the Probes tab.</summary>
		private void btnGotoProbes_Click(object sender, EventArgs e)
		{
			tabMainFunctions.SelectedTab = tabProbeList;
		}

		/// <summary>Shows the viewer tab.</summary>
		private void btnGotoPreview_Click(object sender, EventArgs e)
		{
			tabMainFunctions.SelectedTab = tabViewer;
		}

		/// <summary>
		/// Asks the runner to cancel (it finishes the current step, closes the video files and raises <c>Finished</c>) and puts the
		/// UI into the "stopping" state; the Start button is disabled until <see cref="OnSimulationFinished"/> re-enables it.
		/// </summary>
		private void RequestStop()
		{
			if (_runner == null || _stopRequested)
			{
				return;
			}
			_stopRequested = true;
			_paused = false;
			_runner.Cancel();
			btnStartSimulation.Enabled = false;
			btnStartSimulation.Text = "Stopping...";
			_simStatus = "stopping...";
			ToolStripStatusLabel1.Text = "Stopping the simulation and closing the video files...";
			UpdateRunButtons();
			UpdateTitle();
		}

		/// <summary>
		/// Greys the pause button when it cannot be used, sets its Pause / Resume caption and refreshes the export
		/// buttons. The pause button on the Preview tab is only usable while a simulation runs.
		/// <c>btnStartSimulation</c> is also the stop/restart button (see its own Click handler) and keeps its own
		/// Text/Enabled state (<see cref="RequestStop"/>, <see cref="StartSimulation"/>,
		/// <see cref="OnSimulationFinished"/>) - there used to be a separate <c>btnStopSimulation</c> for that,
		/// removed as redundant once <c>btnStartSimulation</c> started doing double duty.
		/// </summary>
		private void UpdateRunButtons()
		{
			bool canPause = _runner != null && !_stopRequested;
			// Not actually disabling the button here: with its custom Azure BackColor, Windows' own disabled-button
			// rendering draws the text with a strange two-tone (etched) effect instead of plain gray. So the button
			// stays enabled and its color is set by hand instead - btnPauzeResume_Click already ignores clicks while
			// no simulation is running, so behaviorally it is still "disabled".
			btnPauzeResume.ForeColor = (canPause ? SystemColors.ControlText : Color.Gray);
			btnPauzeResume.Text = (_paused ? "Resume simulation" : "Pause simulation");
			UpdateExportButtons();
		}

		/// <summary>Shows <paramref name="message"/> in a "Start simulation" message box.</summary>
		private void ShowSimulationProblem(string message)
		{
			MessageBox.Show(this, message, "Start simulation", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}

		/// <summary>
		/// Validates every simulation control and copies it into <c>_simulation</c> (and the image source scale into
		/// <c>_scene</c>) just before a run, regardless of <c>_loadingSim</c>, so the run always uses what is on the
		/// screen (also a value that was typed in but not confirmed yet is used - that is what the <c>Validate()</c>
		/// calls are for).
		/// </summary>
		private void ReadSimulationControls()
		{
			numVisualizeEvery.Validate();
			numFieldScaling.Validate();
			numIntensityScaling.Validate();
			numFieldScaling2.Validate();
			numIntensityScaling2.Validate();
			numOffsetField.Validate();
			numOffsetIntensity.Validate();
			numQuality.Validate();
			numScaleImageSource.Validate();
			numTimeDecayFactor.Validate();
			_simulation.VisualizeEvery = Convert.ToInt32(numVisualizeEvery.Value);
			_simulation.FieldBrightness = Convert.ToDouble(numFieldScaling.Value);
			_simulation.IntensityBrightness = Convert.ToDouble(numIntensityScaling.Value);
			_simulation.FieldBlackLevel = Convert.ToDouble(numOffsetField.Value);
			_simulation.IntensityBlackLevel = Convert.ToDouble(numOffsetIntensity.Value);
			_simulation.VideoQuality = Convert.ToInt32(numQuality.Value);
			_simulation.TimeDecayFactor = Convert.ToDouble(numTimeDecayFactor.Value);
			_simulation.ExportField = chkExportVideoField.Checked;
			_simulation.ExportIntensity = chkExportVideoIntensity.Checked;
			_simulation.PreviewField = chkPreviewVideoField.Checked;
			_simulation.PreviewIntensity = chkPreviewVideoIntensity.Checked;
			_simulation.UseGpu = chkUseGPU.Checked;
			_simulation.Use4thOrderLaplacian = chkUse4thOrderLaplacian.Checked;
			_scene.ImageSourceScale = Convert.ToDouble(numScaleImageSource.Value);
		}

		/// <summary>
		/// UI thread. Validates the scene and settings (asks to save the project first when exporting, warns when the scene has no
		/// sources), builds a <see cref="SimulationJob"/> with cloned settings, palettes, probes, sources and numbered output file
		/// names, wires every <see cref="SimulationRunner"/> event through <see cref="PostToUi"/> and starts the worker thread.
		/// </summary>
		private void StartSimulation()
		{
			ReadSimulationControls();
			SimulationSettings settings = _simulation;
			bool wantField = settings.ExportField || settings.PreviewField;
			bool wantIntensity = settings.ExportIntensity || settings.PreviewIntensity;
			bool exporting = settings.ExportField || settings.ExportIntensity;
			bool previewing = settings.PreviewField || settings.PreviewIntensity;
			if (!wantField && !wantIntensity)
			{
				ShowSimulationProblem("Nothing to do: tick at least one of the export or preview options.");
				return;
			}
			// numSimulationSteps (a NumericUpDown) never holds an out-of-range or non-integer value, so unlike the old
			// cmbSimulationSteps combo box this needs no parsing/validation here
			settings.Steps = Convert.ToInt32(numSimulationSteps.Value);

			// the videos are written in the output folder of the project, so the project has to exist on disk
			if (exporting && _project == null)
			{
				DialogResult saveAnswer = MessageBox.Show(this, "The videos are saved in the output folder of the project, so the project must be saved first.\r\n\r\nSave the project now?", "Start simulation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
				if (saveAnswer != DialogResult.Yes)
				{
					return;
				}
				if (!SaveProject())
				{
					return;
				}
				// Saving replaces _simulation (SetCurrentProject reloads it from the config), so re-fetch it.
				settings = _simulation;
			}
			Size canvas = EffectiveCanvas();
			if (canvas.Width <= 0)
			{
				ShowSimulationProblem("There is no scene yet: load a scene image in the Scene properties, or define a canvas size.");
				return;
			}
			int dampingEdge = 0;
			if (_scene.DampingEdgeEnabled)
			{
				string dampingProblem = DampingProblem(txtDampingEdgeSize.Text, ref dampingEdge);
				if (dampingProblem.Length > 0)
				{
					ShowSimulationProblem("The damping edge cannot be used: " + dampingProblem);
					txtDampingEdgeSize.Focus();
					return;
				}
			}
			// without any source (image or object) nothing happens
			bool useSources = _scene.UseSources;
			bool hasImageSources = _scene.UseImage && useSources && SceneData.CountSources(_sceneImage) > 0;
			bool hasObjectSources = _scene.UseObjectSources && _sources.Count > 0;
			string noSourcesWarning = "";
			if (!hasImageSources && !hasObjectSources)
			{
				List<string> reasons = new List<string>();
				if (_scene.UseImage)
				{
					if (!useSources)
					{
						reasons.Add("image sources are switched off in the Scene properties");
					}
					else if (SceneData.CountSources(_sceneImage) == 0)
					{
						reasons.Add("the scene image has no sources (no pixels with a green value above 0)");
					}
				}
				if (!_scene.UseObjectSources)
				{
					reasons.Add("object sources are switched off in the Scene properties");
				}
				else if (_sources.Count == 0)
				{
					reasons.Add("there are no object sources yet (add one on the Sources tab)");
				}
				noSourcesWarning = "This scene has no sources yet: " + string.Join("; ", reasons) + ". The field will stay empty.";
			}
			if (noSourcesWarning.Length > 0)
			{
				DialogResult runAnyway = MessageBox.Show(this, noSourcesWarning + "\r\n\r\nRun the simulation anyway?", "Start simulation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
				if (runAnyway != DialogResult.Yes)
				{
					return;
				}
			}
			// color tables
			int[] fieldLut = null;
			int[] intensityLut = null;
			if (wantField)
			{
				ColorScheme fieldScheme = FindScheme(settings.FieldColorScheme, ColorScheme.SchemeTypeField);
				if (fieldScheme == null)
				{
					ShowSimulationProblem("Choose a color scheme for the field first.");
					cmbFieldColorScheme.Focus();
					return;
				}
				fieldLut = ColorLut.Build(fieldScheme, false, settings.FieldBlackLevel, false, false).Argb;
			}
			if (wantIntensity)
			{
				ColorScheme intensityScheme = FindScheme(settings.IntensityColorScheme, ColorScheme.SchemeTypeIntensity);
				if (intensityScheme == null)
				{
					ShowSimulationProblem("Choose a color scheme for the intensity first.");
					cmbIntensityColorScheme.Focus();
					return;
				}
				intensityLut = ColorLut.Build(intensityScheme, false, settings.IntensityBlackLevel, false, false).Argb;
			}
			// the job: the runner works on copies, so the settings can be edited while it runs
			SimulationJob job = new SimulationJob();
			if (_scene.UseImage)
			{
				job.Image = new Bitmap(_sceneImage);
			}
			else
			{
				job.Width = canvas.Width;
				job.Height = canvas.Height;
				job.BackgroundRefractiveIndex = _scene.BackgroundRefractiveIndex;
			}
			job.UseSources = useSources;
			job.SourceAmplitude = _scene.ImageSourceScale;
			job.DampingEdge = dampingEdge;
			job.Settings = settings.Clone();
			job.FieldLut = fieldLut;
			job.IntensityLut = intensityLut;
			_lastFieldLut = fieldLut;
			_lastIntensityLut = intensityLut;
			job.PreviewWidth = Math.Max(16, Math.Min(pnlPreviewField.ClientSize.Width, pnlPreviewIntensity.ClientSize.Width));
			job.PreviewHeight = Math.Max(16, Math.Min(pnlPreviewField.ClientSize.Height, pnlPreviewIntensity.ClientSize.Height));
			job.Probes = ProbesForRun();
			job.ObjectSources = SourcesForRun();

			// the glow-edges overlay (Viewer tab) is only ever needed by the runner to bake into an exported video -
			// the live preview blends its own copy in locally (ShowPreview/ComposeGlowOverlay), which can track the
			// checkbox/brightness box live while this, like Settings, is frozen at the start of the run
			job.FieldOverlayGain = FieldOverlayGain();
			job.IntensityOverlayGain = IntensityOverlayGain();
			if (exporting && _glowEdgesImage != null && (job.FieldOverlayGain > 0.0 || job.IntensityOverlayGain > 0.0))
			{
				job.GlowOverlay = new Bitmap(_glowEdgesImage);
			}
			if (exporting)
			{
				try
				{
					Directory.CreateDirectory(_project.OutputVideoFolder);
				}
				catch (Exception ex)
				{
					if (job.Image != null)
					{
						job.Image.Dispose();
					}
					if (job.GlowOverlay != null)
					{
						job.GlowOverlay.Dispose();
					}
					ShowSimulationProblem("The output folder could not be created:\r\n" + _project.OutputVideoFolder + "\r\n\r\n" + ex.Message);
					return;
				}
				string suffix = NextNumericSuffix(_project.OutputVideoFolder).ToString("D4", CultureInfo.InvariantCulture);
				if (settings.ExportField)
				{
					job.FieldVideoPath = Path.Combine(_project.OutputVideoFolder, _project.Name + "_field_" + suffix + ".mp4");
				}
				if (settings.ExportIntensity)
				{
					job.IntensityVideoPath = Path.Combine(_project.OutputVideoFolder, _project.Name + "_intensity_" + suffix + ".mp4");
				}
			}
			ClearPreviews();
			BeginProbeRun(job.Probes, canvas, settings.Steps);
			_fieldVideoFile = "";
			_intensityVideoFile = "";
			UpdateVideoButtons();
			_runInfo = "Field: brightness " + settings.FieldBrightness.ToString("0.0#", CultureInfo.CurrentCulture)
				+ ", offset " + settings.FieldBlackLevel.ToString("0.0#", CultureInfo.CurrentCulture)
				+ "\r\nIntensity: brightness " + settings.IntensityBrightness.ToString("0.0#", CultureInfo.CurrentCulture)
				+ ", offset " + settings.IntensityBlackLevel.ToString("0.0#", CultureInfo.CurrentCulture)
				+ "\r\nImage source scale: " + _scene.ImageSourceScale.ToString("0.0#", CultureInfo.CurrentCulture);
			if (previewing)
			{
				tabMainFunctions.SelectedTab = tabViewer;
			}
			SimulationRunner runner = new SimulationRunner(job);
			runner.ProgressChanged += (done, total) => PostToUi(() => ShowProgress(runner, done, total));
			runner.EngineReady += (description, note) => PostToUi(() => ShowEngine(runner, description, note));
			runner.ProbesUpdated += update => PostToUi(() => ShowProbeSamples(runner, update));
			runner.PreviewReady += (isField, frame) => PostToUi(() => ShowPreview(runner, isField, frame));
			runner.Finished += result => PostToUi(() => OnSimulationFinished(runner, result));
			_runner = runner;
			btnStartSimulation.Text = "Stop Simulation";
			_simStatus = "starting...";
			_progressText = "starting...";
			_paused = false;
			_stopRequested = false;
			UpdateRunButtons();
			ToolStripStatusLabel1.Text = "Simulation running...";
			ToolStripStatusLabel1.ToolTipText = "";
			UpdateTitle();
			runner.Start();
		}

		/// <summary>
		/// Queues <paramref name="work"/> on the UI thread with <c>BeginInvoke</c>. Called from the runner's worker thread;
		/// the work is dropped silently when the form is disposed or its handle is gone (including the
		/// <see cref="InvalidOperationException"/> thrown while the handle is being destroyed).
		/// </summary>
		private void PostToUi(Action work)
		{
			if (base.IsDisposed || !base.IsHandleCreated)
			{
				return;
			}
			try
			{
				base.BeginInvoke(work);
			}
			catch (InvalidOperationException)
			{
			}
		}

		/// <summary>
		/// UI thread (via <see cref="PostToUi"/>): updates the progress text and window title. Ignored for a stale runner and while
		/// stopping, so "stopping..." is not overwritten by a late progress event.
		/// </summary>
		private void ShowProgress(SimulationRunner runner, int done, int total)
		{
			// A late progress event from the worker must not overwrite the "stopping..." status set by RequestStop.
			if (runner != _runner || string.CompareOrdinal(_simStatus, "stopping...") == 0)
			{
				return;
			}
			_lastStepsDone = done;
			int percent = (int)(100L * done / Math.Max(1, total)); // long: 100 * steps can exceed int.MaxValue
			_progressText = "step " + done.ToString("N0", CultureInfo.CurrentCulture) + " of " + total.ToString("N0", CultureInfo.CurrentCulture) + " (" + percent.ToString(CultureInfo.InvariantCulture) + "%)";
			_simStatus = (_paused ? "paused - " : "") + _progressText;
			UpdateTitle();
		}

		/// <summary>UI thread (via <see cref="PostToUi"/>): shows which engine the runner picked, plus the fallback note, in the status bar.</summary>
		private void ShowEngine(SimulationRunner runner, string description, string note)
		{
			if (runner != _runner || base.IsDisposed)
			{
				return;
			}
			string status = "Simulation running on " + description;
			if (note.Length > 0)
			{
				status = status + " - " + note;
			}
			ToolStripStatusLabel1.Text = status;
			ToolStripStatusLabel1.ToolTipText = status;
		}

		/// <summary>
		/// UI thread (via <see cref="PostToUi"/>): shows the downscaled preview frame in the field or intensity box (composed with the
		/// glow overlay when enabled) and takes ownership of <paramref name="frame"/>; a frame for a stale runner is disposed instead.
		/// Always acknowledges with <see cref="SimulationRunner.PreviewShown"/> so the worker may render the next preview.
		/// </summary>
		private void ShowPreview(SimulationRunner runner, bool isField, Bitmap frame)
		{
			if (runner != _runner || base.IsDisposed)
			{
				frame.Dispose();
				runner.PreviewShown();
				return;
			}
			PictureBox target = (isField ? _picField : _picIntensity);
			double overlayGain = (isField ? FieldOverlayGain() : IntensityOverlayGain());
			SetPreviewFrame(target, frame, overlayGain);
			UpdateCopyButtons();
			runner.PreviewShown();
		}

		/// <summary>
		/// UI thread (via <see cref="PostToUi"/>): clears the runner, takes ownership of the result's full-size frames, enables the
		/// video buttons and builds the status-bar summary (steps, elapsed, steps/s, engine, files) with the details in its tooltip;
		/// a failed run is also reported in a message box.
		/// </summary>
		private void OnSimulationFinished(SimulationRunner runner, SimulationResult result)
		{
			if (runner != _runner || base.IsDisposed)
			{
				return;
			}
			_runner = null;
			_hasRun = true;
			_paused = false;
			_stopRequested = false;
			_lastStepsDone = result.StepsDone;
			UpdateRunButtons();
			_simStatus = "";
			btnStartSimulation.Text = "Start Simulation";
			btnStartSimulation.Enabled = true;
			UpdateTitle();
			_fieldVideoFile = result.FieldFile;
			_intensityVideoFile = result.IntensityFile;
			UpdateVideoButtons();

			// a full-resolution snapshot of the last frame, for a still export once nothing is running any more
			Bitmap previousFieldFrame = _lastFullFieldFrame;
			_lastFullFieldFrame = result.LastFieldFrame;
			if (previousFieldFrame != null)
			{
				previousFieldFrame.Dispose();
			}
			Bitmap previousIntensityFrame = _lastFullIntensityFrame;
			_lastFullIntensityFrame = result.LastIntensityFrame;
			if (previousIntensityFrame != null)
			{
				previousIntensityFrame.Dispose();
			}
			UpdateExportButtons();
			string elapsed = result.Elapsed.ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture);
			string details = "";
			if (result.Files.Count > 0)
			{
				details = "\r\n\r\nVideos (in " + Path.GetDirectoryName(result.Files[0]) + "):";
				foreach (string file in result.Files)
				{
					details = details + "\r\n" + Path.GetFileName(file);
				}
			}
			if (result.Status != SimulationStatus.Failed && result.Message.Length > 0)
			{
				details = details + "\r\n\r\n" + result.Message;
			}
			if (result.Engine.Length > 0)
			{
				details = details + "\r\n\r\nEngine: " + result.Engine;
				if (result.EngineNote.Length > 0)
				{
					details = details + "\r\n" + result.EngineNote;
				}
			}
			details = details + "\r\n\r\n" + _runInfo;
			string stepsText = result.StepsDone.ToString("N0", CultureInfo.CurrentCulture);

			// the summary goes in the status bar (the details in its tooltip); only a failure gets a message box
			string summary;
			switch (result.Status)
			{
			case SimulationStatus.Completed:
				summary = "Simulation completed: " + stepsText + " steps in " + elapsed;
				break;
			case SimulationStatus.Cancelled:
				summary = "Simulation stopped after " + stepsText + " steps (" + elapsed + ")";
				break;
			default:
				summary = "Simulation failed after " + stepsText + " steps";
				break;
			}
			if (result.Elapsed.TotalSeconds > 0.5 && result.StepsDone > 0)
			{
				summary = summary + " - " + ((double)result.StepsDone / result.Elapsed.TotalSeconds).ToString("0.0", CultureInfo.CurrentCulture) + " steps/s";
			}
			if (result.Engine.Length > 0)
			{
				summary = summary + " - " + result.Engine;
				if (result.EngineNote.Length > 0)
				{
					summary = summary + " (" + result.EngineNote + ")";
				}
			}
			if (result.Files.Count > 0)
			{
				List<string> fileNames = new List<string>();
				foreach (string file in result.Files)
				{
					fileNames.Add(Path.GetFileName(file));
				}
				summary = summary + " - video: " + string.Join(", ", fileNames);
			}
			if (result.Status != SimulationStatus.Failed && result.Message.Length > 0)
			{
				summary += " - MP4 (H.264) was not available, the videos are .avi files";
			}
			ToolStripStatusLabel1.Text = summary;
			ToolStripStatusLabel1.ToolTipText = summary + details;
			if (result.Status == SimulationStatus.Failed)
			{
				MessageBox.Show(this, "The simulation failed after " + stepsText + " steps:\r\n\r\n" + result.Message + details, "Simulation", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Enables the "view video" buttons when the last run's video files still exist.</summary>
		private void UpdateVideoButtons()
		{
			btnViewFieldVideo.Enabled = _fieldVideoFile.Length > 0 && File.Exists(_fieldVideoFile);
			btnViewIntensityVideo.Enabled = _intensityVideoFile.Length > 0 && File.Exists(_intensityVideoFile);
		}

		/// <summary>Opens the last field video in the default media player.</summary>
		private void btnViewFieldVideo_Click(object sender, EventArgs e)
		{
			OpenVideo(_fieldVideoFile);
		}

		/// <summary>Opens the last intensity video in the default media player.</summary>
		private void btnViewIntensityVideo_Click(object sender, EventArgs e)
		{
			OpenVideo(_intensityVideoFile);
		}

		/// <summary>Launches <paramref name="videoFile"/> with the shell's default player; tells the user when the file has gone or cannot be opened.</summary>
		private void OpenVideo(string videoFile)
		{
			if (videoFile.Length == 0 || !File.Exists(videoFile))
			{
				MessageBox.Show(this, "The video file does not exist (any more):\r\n" + videoFile, "Video", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				UpdateVideoButtons();
				return;
			}
			try
			{
				Process.Start(new ProcessStartInfo(videoFile)
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The video could not be opened:\r\n\r\n" + ex.Message, "Video", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Opens the project's video folder in Explorer.</summary>
		private void btnOpenVideoFolder_Click(object sender, EventArgs e)
		{
			OpenVideoFolder();
		}

		/// <summary>Opens the project's exported-frames folder in Explorer.</summary>
		private void btnOpenImageFolder_Click(object sender, EventArgs e)
		{
			OpenFramesFolder();
		}

		/// <summary>Saves the current or last field frame as a PNG.</summary>
		private void btnExportFieldFrame_Click(object sender, EventArgs e)
		{
			ExportFrame(true);
		}

		/// <summary>Saves the current or last intensity frame as a PNG.</summary>
		private void btnExportIntensityFrame_Click(object sender, EventArgs e)
		{
			ExportFrame(false);
		}

		/// <summary>
		/// Saves a full-size frame as PNG in the project's frames folder. While the run is paused the runner renders the live
		/// field / intensity with the palette of the current run (that bitmap is owned and disposed here); otherwise the last frame
		/// of the previous run is used. The glow overlay is composed in when enabled. The file name carries the step count and a
		/// 4-digit sequence number.
		/// </summary>
		private void ExportFrame(bool isField)
		{
			Bitmap frame = null;
			bool ownsFrame = false;
			if (_runner != null && _paused)
			{
				int[] lut = (isField ? _lastFieldLut : _lastIntensityLut);
				float brightness = (float)(isField ? _simulation.FieldBrightness : _simulation.IntensityBrightness);
				frame = _runner.RenderCurrentFrame(isField, lut, brightness);
				ownsFrame = true;
			}
			if (frame == null)
			{
				frame = (isField ? _lastFullFieldFrame : _lastFullIntensityFrame);
			}
			if (frame == null)
			{
				MessageBox.Show(this, "There is no picture to export yet.", "Export frame", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			try
			{
				Directory.CreateDirectory(_project.OutputFramesFolder);
			}
			catch (Exception ex)
			{
				if (ownsFrame)
				{
					frame.Dispose();
				}
				MessageBox.Show(this, "The output folder could not be created:\r\n" + _project.OutputFramesFolder + "\r\n\r\n" + ex.Message, "Export frame", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			string kind = (isField ? "field" : "intensity");
			double overlayGain = (isField ? FieldOverlayGain() : IntensityOverlayGain());
			Bitmap output = frame;
			bool ownsOutput = false;
			if (overlayGain > 0.0 && _glowEdgesImage != null)
			{
				output = ComposeGlowOverlay(frame, overlayGain);
				ownsOutput = true;
			}
			string suffix = NextNumericSuffix(_project.OutputFramesFolder).ToString("D4", CultureInfo.InvariantCulture);
			string fileName = _project.Name + "_" + kind + "_frame" + _lastStepsDone.ToString(CultureInfo.InvariantCulture) + "_" + suffix + ".png";
			string path = Path.Combine(_project.OutputFramesFolder, fileName);
			try
			{
				output.Save(path, ImageFormat.Png);
				ToolStripStatusLabel1.Text = "Frame exported: " + fileName;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The frame could not be saved:\r\n\r\n" + ex.Message, "Export frame", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			finally
			{
				if (ownsOutput)
				{
					output.Dispose();
				}
				if (ownsFrame)
				{
					frame.Dispose();
				}
			}
		}

		/// <summary>
		/// Next available 4-digit numeric filename suffix for a file saved into <paramref name="folder"/>: the highest
		/// existing file in the folder whose name (before the extension) ends in exactly 4 digits, plus 1 - starts at
		/// 1 when there is no such file yet. Used for exported frames (<see cref="ExportFrame"/>) and videos
		/// (<see cref="StartSimulation"/>) instead of the previous date/time stamp, which was found to make file
		/// names too long. Formats with "D4" (e.g. "0001"), growing past 4 digits rather than wrapping once the
		/// count passes 9999.
		/// </summary>
		private int NextNumericSuffix(string folder)
		{
			int highest = 0;
			if (Directory.Exists(folder))
			{
				foreach (string file in Directory.GetFiles(folder))
				{
					string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
					if (fileNameWithoutExtension.Length >= 4)
					{
						string tail = fileNameWithoutExtension.Substring(fileNameWithoutExtension.Length - 4);
						int value;
						if (int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > highest)
						{
							highest = value;
						}
					}
				}
			}
			return highest + 1;
		}

		/// <summary>
		/// Opens the project's video output folder in Windows Explorer - which already shows video thumbnails and
		/// lets the files be renamed or deleted there, so there is no need to build a picker of our own for that.
		/// Opened in "Details" view (see <see cref="OpenFolderInExplorer"/>) - more useful than thumbnails for a
		/// folder of video files. Reports when there is no project.
		/// </summary>
		private void OpenVideoFolder()
		{
			string folder = ((_project == null) ? "" : _project.OutputVideoFolder);
			if (folder.Length == 0)
			{
				MessageBox.Show(this, "There is no project open yet.", "Open video folder", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			try
			{
				// so the folder can be opened even before any video has been exported
				OpenFolderInExplorer(folder, FVM_DETAILS);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The video folder could not be opened:\r\n\r\n" + ex.Message, "Open video folder", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>
		/// Opens the project's output frames folder (<c>btnOpenImageFolder</c> on the Preview tab) in Windows
		/// Explorer, the same way <see cref="OpenVideoFolder"/> does for the video folder - but in "Large icons"
		/// view (see <see cref="OpenFolderInExplorer"/>), which shows thumbnails of the exported images. Reports
		/// when there is no project.
		/// </summary>
		private void OpenFramesFolder()
		{
			string folder = ((_project == null) ? "" : _project.OutputFramesFolder);
			if (folder.Length == 0)
			{
				MessageBox.Show(this, "There is no project open yet.", "Open image folder", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			try
			{
				// so the folder can be opened even before any frame has been exported
				OpenFolderInExplorer(folder, FVM_ICON);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The image folder could not be opened:\r\n\r\n" + ex.Message, "Open image folder", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>
		/// Creates <paramref name="folder"/> if needed, opens it in Windows Explorer and tries to make that window use
		/// the given view mode (one of the <c>FVM_</c> constants in <c>Form1.cs</c>). Explorer has no command-line
		/// switch for this, so the folder is opened as before (explorer.exe on the path) and then, best-effort, the
		/// "Shell.Application" COM automation object is used to find the newly opened window for that exact folder
		/// and set its <c>Document.CurrentViewMode</c> (<see cref="SetExplorerWindowViewMode"/>). This uses late
		/// binding via reflection rather than a direct COM reference, since adding a COM reference to the project
		/// for this alone is more trouble than it is worth.
		/// <para>
		/// This is not fully reliable: if a window for the folder is already open, Explorer reuses it instead of
		/// opening a new one (so its view stays whatever it already was), and Explorer separately remembers, per
		/// folder, whatever view the user leaves it in afterwards - so this only really matters the first time (or
		/// after Explorer has forgotten it). Either way the folder itself always opens; only the requested view can
		/// silently fail to apply, which is why every failure in the view-mode step is swallowed rather than shown
		/// to the user.
		/// </para>
		/// </summary>
		/// <param name="folder">Folder to open; created when missing (that may throw, unlike the view-mode step).</param>
		/// <param name="viewMode">Explorer FOLDERVIEWMODE value: <see cref="FVM_ICON"/> or <see cref="FVM_DETAILS"/>.</param>
		private void OpenFolderInExplorer(string folder, int viewMode)
		{
			Directory.CreateDirectory(folder);
			Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"")
			{
				UseShellExecute = true
			});
			try
			{
				SetExplorerWindowViewMode(folder, viewMode);
			}
			catch (Exception)
			{
				// best effort only - the folder is already open regardless of whether this succeeds
			}
		}

		/// <summary>
		/// Finds the Explorer window that was just opened on <paramref name="folder"/> (polling up to 8 times)
		/// through the Shell.Application COM automation object (late bound) and sets its view mode
		/// (<see cref="FVM_ICON"/> or <see cref="FVM_DETAILS"/>); see <see cref="OpenFolderInExplorer"/> for why and
		/// for the caveats. Runs on the UI thread and sleeps 120 ms between polls, so it can block the UI for about
		/// a second when the window never shows up.
		/// </summary>
		private void SetExplorerWindowViewMode(string folder, int viewMode)
		{
			string targetPath = Path.GetFullPath(folder).TrimEnd(new char[] { Path.DirectorySeparatorChar });
			Type shellType = Type.GetTypeFromProgID("Shell.Application");
			if (shellType == null)
			{
				return;
			}
			object shell = Activator.CreateInstance(shellType);
			try
			{
				const BindingFlags memberFlags = BindingFlags.InvokeMethod | BindingFlags.GetProperty;

				// give the freshly launched Explorer window a moment to appear
				for (int attempt = 1; attempt <= 8; attempt++)
				{
					object windows = shellType.InvokeMember("Windows", memberFlags, null, shell, null);
					Type windowsType = windows.GetType();
					int count = Convert.ToInt32(windowsType.InvokeMember("Count", memberFlags, null, windows, null));
					for (int i = 0; i < count; i++)
					{
						object window = windowsType.InvokeMember("Item", memberFlags, null, windows, new object[] { i });
						if (window == null)
						{
							continue;
						}
						Type windowType = window.GetType();
						string locationUrl = windowType.InvokeMember("LocationURL", memberFlags, null, window, null) as string;
						if (string.IsNullOrEmpty(locationUrl))
						{
							continue;
						}
						string windowPath;
						try
						{
							windowPath = new Uri(locationUrl).LocalPath.TrimEnd(new char[] { Path.DirectorySeparatorChar });
						}
						catch (Exception)
						{
							continue;
						}
						if (string.Equals(windowPath, targetPath, StringComparison.OrdinalIgnoreCase))
						{
							object document = windowType.InvokeMember("Document", memberFlags, null, window, null);
							if (document != null)
							{
								document.GetType().InvokeMember("CurrentViewMode", BindingFlags.SetProperty, null, document, new object[] { viewMode });
							}
							return;
						}
					}
					Thread.Sleep(120); // give Explorer time to register the new window
				}
			}
			finally
			{
				Marshal.ReleaseComObject(shell);
			}
		}

		/// <summary>
		/// Empties both preview boxes, disposing the shown image and the raw frame kept in <c>Tag</c> (they differ when the glow
		/// overlay was composed in), and drops the last full-size frames.
		/// </summary>
		private void ClearPreviews()
		{
			foreach (PictureBox pictureBox in new PictureBox[] { _picField, _picIntensity })
			{
				if (pictureBox == null)
				{
					continue;
				}
				Image image = pictureBox.Image;
				Bitmap tagged = pictureBox.Tag as Bitmap;
				pictureBox.Image = null;
				pictureBox.Tag = null;
				if (image != null && !object.ReferenceEquals(image, tagged))
				{
					image.Dispose();
				}
				if (tagged != null)
				{
					tagged.Dispose();
				}
			}

			// a new run is starting - the cached last-frame snapshots are from the previous run, so drop them
			if (_lastFullFieldFrame != null)
			{
				_lastFullFieldFrame.Dispose();
			}
			_lastFullFieldFrame = null;
			if (_lastFullIntensityFrame != null)
			{
				_lastFullIntensityFrame.Dispose();
			}
			_lastFullIntensityFrame = null;
			UpdateCopyButtons();
		}

		// grpProbe1_Enter/grpProbe2_Enter and Label21/Label22 Click are unused Designer-generated stubs (harmless, left
		// empty). The Pick button, the Sources combo boxes and Button1 (the Probes tab's "Import scene image", still
		// under its Designer default name) are wired in Form1.Sources.cs/Form1.Probes.cs instead, where the rest of
		// those tabs lives.

		/// <summary>Wired by the designer to both probe group boxes; intentionally does nothing.</summary>
		private void grpProbe1_Enter(object sender, EventArgs e)
		{
		}

		/// <summary>Wired by the designer to two labels; intentionally does nothing.</summary>
		private void Label21_Click(object sender, EventArgs e)
		{
		}

		/// <summary>Wired by the designer; intentionally does nothing.</summary>
		private void lblVideoQuality_Click(object sender, EventArgs e)
		{
		}

		/// <summary>Saves the project (viewer-tab copy of the Save button).</summary>
		private void btnSaveConfiguration2_Click(object sender, EventArgs e)
		{
			SaveProject();
		}

		/// <summary>
		/// "Created on" column header of the video list on the Output tab. Created and assigned by the designer; declared as a
		/// property (VB.NET WithEvents style) and placed in this file by the decompiler.
		/// </summary>
		protected internal virtual ColumnHeader colCreatedOn2 { get; set; }
	}
}
