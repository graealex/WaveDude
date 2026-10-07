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
	public partial class Form1
	{
		/// <summary>
		/// Called once from <c>Form1_Load</c>: sets the ranges of the simulation NumericUpDowns (the viewer-tab scaling boxes copy
		/// the configuration-tab ones), creates the four black preview boxes and puts the buttons, labels and status bar in their idle state.
		/// </summary>
		private void InitializeSimulationControls()
		{
			this.numVisualizeEvery.Minimum = 1m;
			this.numFieldScaling.Minimum = 0.1m;
			this.numFieldScaling.Maximum = 1000m;
			this.numIntensityScaling.Minimum = 0.1m;
			this.numIntensityScaling.Maximum = 1000m;
			this.numFieldScaling2.Minimum = this.numFieldScaling.Minimum;
			this.numFieldScaling2.Maximum = this.numFieldScaling.Maximum;
			this.numFieldScaling2.DecimalPlaces = this.numFieldScaling.DecimalPlaces;
			this.numFieldScaling2.Increment = this.numFieldScaling.Increment;
			this.numIntensityScaling2.Minimum = this.numIntensityScaling.Minimum;
			this.numIntensityScaling2.Maximum = this.numIntensityScaling.Maximum;
			this.numIntensityScaling2.DecimalPlaces = this.numIntensityScaling.DecimalPlaces;
			this.numIntensityScaling2.Increment = this.numIntensityScaling.Increment;
			foreach (NumericUpDown offsetBox in new NumericUpDown[] { this.numOffsetField, this.numOffsetIntensity })
			{
				offsetBox.DecimalPlaces = 2;
				offsetBox.Minimum = -1m;
				offsetBox.Maximum = 1m;
				offsetBox.Increment = 0.01m;
			}
			this.numQuality.Minimum = 1m;
			this.numQuality.Maximum = 100m;
			this.numQuality.DecimalPlaces = 0;
			this.StatusStrip1.ShowItemToolTips = true;
			this.ToolStripStatusLabel1.Spring = true;
			this.ToolStripStatusLabel1.TextAlign = ContentAlignment.MiddleLeft;
			this.ToolStripStatusLabel1.Text = "";
			this.UpdateRunButtons();
			this.btnViewIntensityVideo.Text = "View intensity video in media player";
			this.UpdateVideoButtons();
			this.cmbFieldColorScheme.DropDownStyle = ComboBoxStyle.DropDownList;
			this.cmbIntensityColorScheme.DropDownStyle = ComboBoxStyle.DropDownList;
			this._picField = Form1.MakePreviewBox(this.pnlPreviewField);
			this._picIntensity = Form1.MakePreviewBox(this.pnlPreviewIntensity);
			this._picSource = Form1.MakePreviewBox(this.pnlSourceImage);
			this._picProbe = Form1.MakePreviewBox(this.pnlProbeImage);
			this.UpdateCopyButtons();
			this.lblWarningField.Text = "Field video not exported";
			this.lblWarningField.ForeColor = Color.DarkBlue;
			this.lblWarningIntensity.Text = "Intensity video not exported";
			this.lblWarningIntensity.ForeColor = Color.DarkBlue;
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
			this._loadingSim = true;
			try
			{
				int visualizeEvery = Math.Max(Convert.ToInt32(this.numVisualizeEvery.Minimum), Math.Min(Convert.ToInt32(this.numVisualizeEvery.Maximum), this._simulation.VisualizeEvery));
				this._simulation.VisualizeEvery = visualizeEvery;
				this.numSimulationSteps.Value = Form1.ClampToRange(this.numSimulationSteps, this._simulation.Steps);
				this.numVisualizeEvery.Value = visualizeEvery;
				this.numFieldScaling.Value = Form1.ClampToRange(this.numFieldScaling, this._simulation.FieldBrightness);
				this.numIntensityScaling.Value = Form1.ClampToRange(this.numIntensityScaling, this._simulation.IntensityBrightness);
				this.numFieldScaling2.Value = Form1.ClampToRange(this.numFieldScaling2, this._simulation.FieldBrightness);
				this.numIntensityScaling2.Value = Form1.ClampToRange(this.numIntensityScaling2, this._simulation.IntensityBrightness);
				this.numQuality.Value = Form1.ClampToRange(this.numQuality, this._simulation.VideoQuality);
				this.numOffsetField.Value = Form1.ClampToRange(this.numOffsetField, this._simulation.FieldBlackLevel);
				this.numOffsetIntensity.Value = Form1.ClampToRange(this.numOffsetIntensity, this._simulation.IntensityBlackLevel);
				this.numTimeDecayFactor.Value = Form1.ClampToRange(this.numTimeDecayFactor, this._simulation.TimeDecayFactor);
				this.chkExportVideoField.Checked = this._simulation.ExportField;
				this.chkExportVideoIntensity.Checked = this._simulation.ExportIntensity;
				this.chkExportVideoField2.Checked = this._simulation.ExportField;
				this.chkExportVideoIntensity2.Checked = this._simulation.ExportIntensity;
				this.chkPreviewVideoField.Checked = this._simulation.PreviewField;
				this.chkPreviewVideoIntensity.Checked = this._simulation.PreviewIntensity;
				this.chkUseGPU.Checked = this._simulation.UseGpu;
				this.chkUse4thOrderLaplacian.Checked = this._simulation.Use4thOrderLaplacian;
				this.FillSchemeCombo(this.cmbFieldColorScheme, "field", this._simulation.FieldColorScheme);
				this.FillSchemeCombo(this.cmbIntensityColorScheme, "intensity", this._simulation.IntensityColorScheme);
			}
			finally
			{
				this._loadingSim = false;
			}
			this.UpdateSimulationControlState();
		}

		/// <summary>Enables the scheme combos, the quality box and the "not exported" warnings according to the export / preview check boxes.</summary>
		private void UpdateSimulationControlState()
		{
			this.cmbFieldColorScheme.Enabled = this.chkExportVideoField.Checked || this.chkPreviewVideoField.Checked;
			this.cmbIntensityColorScheme.Enabled = this.chkExportVideoIntensity.Checked || this.chkPreviewVideoIntensity.Checked;
			bool exporting = this.chkExportVideoField.Checked || this.chkExportVideoIntensity.Checked;
			this.numQuality.Enabled = exporting;
			this.lblVideoQuality.Enabled = exporting;
			this.lblWarningField.Visible = !this.chkExportVideoField.Checked;
			this.lblWarningIntensity.Visible = !this.chkExportVideoIntensity.Checked;
		}

		/// <summary>
		/// Refills both scheme combos from <c>_schemes</c> (after the scheme list was reloaded) keeping the stored selections,
		/// without dirtying the project. Saves and restores <c>_loadingSim</c> so it can be called while a load is in progress.
		/// </summary>
		private void RefreshSchemeCombos()
		{
			bool loadingSim = this._loadingSim;
			this._loadingSim = true;
			try
			{
				this.FillSchemeCombo(this.cmbFieldColorScheme, "field", this._simulation.FieldColorScheme);
				this.FillSchemeCombo(this.cmbIntensityColorScheme, "intensity", this._simulation.IntensityColorScheme);
			}
			finally
			{
				this._loadingSim = loadingSim;
			}
		}

		/// <summary>Fills <paramref name="combo"/> with the schemes of one type and selects the one named <paramref name="selectedName"/> (case-insensitive), or nothing.</summary>
		/// <param name="combo">The field or intensity scheme combo; its items become <see cref="ColorScheme"/> objects.</param>
		/// <param name="schemeType">"field" or "intensity".</param>
		/// <param name="selectedName">Name of the scheme to select; no selection when it is not in the list.</param>
		private void FillSchemeCombo(ComboBox combo, string schemeType, string selectedName)
		{
			combo.BeginUpdate();
			try
			{
				combo.Items.Clear();
				ColorScheme selected = null;
				foreach (ColorScheme scheme in this._schemes)
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
			foreach (ColorScheme scheme in this._schemes)
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
			if (this.tabMainFunctions.SelectedTab == this.tabConfiguration)
			{
				string schemeFolder = this.txtSchemeFolder.Text.Trim();
				if (Directory.Exists(schemeFolder))
				{
					this.LoadSchemeList();
				}
				else
				{
					this.RefreshSchemeCombos();
				}
			}
			if (this.tabMainFunctions.SelectedTab == this.tabOutput)
			{
				this.RefreshOutputProjectList((this._project != null) ? this._project.Name : "");
			}
		}

		/// <summary>Stores the number of steps and marks the project dirty.</summary>
		private void numSimulationSteps_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.Steps = Convert.ToInt32(this.numSimulationSteps.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Stores how many steps go by between two rendered frames.</summary>
		private void numVisualizeEvery_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.VisualizeEvery = Convert.ToInt32(this.numVisualizeEvery.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Mirrors a scaling value into the linked box on the other tab without raising the change handlers.</summary>
		private void SetLinkedScalingValue(NumericUpDown box, decimal value)
		{
			decimal clamped = (value < box.Minimum) ? box.Minimum : ((value > box.Maximum) ? box.Maximum : value);
			if (box.Value != clamped)
			{
				bool loadingSim = this._loadingSim;
				this._loadingSim = true;
				try
				{
					box.Value = clamped;
				}
				finally
				{
					this._loadingSim = loadingSim;
				}
			}
		}

		/// <summary>Mirrors a check state into the linked box on the other tab without raising its CheckedChanged handler.</summary>
		private void SetLinkedCheckedValue(CheckBox box, bool value)
		{
			if (box.Checked != value)
			{
				bool loadingSim = this._loadingSim;
				this._loadingSim = true;
				try
				{
					box.Checked = value;
				}
				finally
				{
					this._loadingSim = loadingSim;
				}
			}
		}

		/// <summary>Stores the field brightness and mirrors it into the viewer-tab box.</summary>
		private void numScaleFieldValues_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.FieldBrightness = Convert.ToDouble(this.numFieldScaling.Value);
				this.SetLinkedScalingValue(this.numFieldScaling2, this.numFieldScaling.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Viewer-tab copy: stores the field brightness and mirrors it back into the configuration-tab box.</summary>
		private void numScaleFieldValues2_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.FieldBrightness = Convert.ToDouble(this.numFieldScaling2.Value);
				this.SetLinkedScalingValue(this.numFieldScaling, this.numFieldScaling2.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Stores the video quality (1..100).</summary>
		private void numQuality_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.VideoQuality = Convert.ToInt32(this.numQuality.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Stores the field black level (-1..1 palette offset).</summary>
		private void numOffsetField_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.FieldBlackLevel = Convert.ToDouble(this.numOffsetField.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Stores the intensity black level (-1..1 palette offset).</summary>
		private void numOffsetIntensity_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.IntensityBlackLevel = Convert.ToDouble(this.numOffsetIntensity.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Stores the time decay factor.</summary>
		private void numTimeDecayFactor_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.TimeDecayFactor = Convert.ToDouble(this.numTimeDecayFactor.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Stores the intensity brightness and mirrors it into the viewer-tab box.</summary>
		private void numScaleIntensity_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.IntensityBrightness = Convert.ToDouble(this.numIntensityScaling.Value);
				this.SetLinkedScalingValue(this.numIntensityScaling2, this.numIntensityScaling.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>Viewer-tab copy: stores the intensity brightness and mirrors it back into the configuration-tab box.</summary>
		private void numScaleIntensity2_ValueChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.IntensityBrightness = Convert.ToDouble(this.numIntensityScaling2.Value);
				this.SetLinkedScalingValue(this.numIntensityScaling, this.numIntensityScaling2.Value);
				this.MarkProjectChanged();
			}
		}

		/// <summary>
		/// Shared handler of the four export / preview boxes on the configuration tab: stores all four flags, mirrors the two
		/// export flags into the viewer tab and updates the dependent controls.
		/// </summary>
		private void SimulationOption_CheckedChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.ExportField = this.chkExportVideoField.Checked;
				this._simulation.ExportIntensity = this.chkExportVideoIntensity.Checked;
				this._simulation.PreviewField = this.chkPreviewVideoField.Checked;
				this._simulation.PreviewIntensity = this.chkPreviewVideoIntensity.Checked;
				this.SetLinkedCheckedValue(this.chkExportVideoField2, this.chkExportVideoField.Checked);
				this.SetLinkedCheckedValue(this.chkExportVideoIntensity2, this.chkExportVideoIntensity.Checked);
				this.UpdateSimulationControlState();
				this.MarkProjectChanged();
			}
		}

		/// <summary>Viewer-tab copy of the field export box: stores the flag and mirrors it back.</summary>
		private void chkExportVideoField2_CheckedChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.ExportField = this.chkExportVideoField2.Checked;
				this.SetLinkedCheckedValue(this.chkExportVideoField, this.chkExportVideoField2.Checked);
				this.UpdateSimulationControlState();
				this.MarkProjectChanged();
			}
		}

		/// <summary>Viewer-tab copy of the intensity export box: stores the flag and mirrors it back.</summary>
		private void chkExportVideoIntensity2_CheckedChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.ExportIntensity = this.chkExportVideoIntensity2.Checked;
				this.SetLinkedCheckedValue(this.chkExportVideoIntensity, this.chkExportVideoIntensity2.Checked);
				this.UpdateSimulationControlState();
				this.MarkProjectChanged();
			}
		}

		/// <summary>Stores whether the OpenCL engine is tried first.</summary>
		private void chkUseGPU_CheckedChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.UseGpu = this.chkUseGPU.Checked;
				this.MarkProjectChanged();
			}
		}

		/// <summary>Stores the Laplacian order choice.</summary>
		private void chkUse4thOrderLaplacian_CheckedChanged(object sender, EventArgs e)
		{
			if (!this._loadingSim)
			{
				this._simulation.Use4thOrderLaplacian = this.chkUse4thOrderLaplacian.Checked;
				this.MarkProjectChanged();
			}
		}

		/// <summary>Shared handler of both scheme combos: stores the chosen scheme name in the field or intensity setting, depending on which combo raised it.</summary>
		private void SchemeCombo_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (this._loadingSim)
			{
				return;
			}
			ComboBox combo = (ComboBox)sender;
			ColorScheme scheme = combo.SelectedItem as ColorScheme;
			if (scheme == null)
			{
				return;
			}
			if (combo == this.cmbFieldColorScheme)
			{
				this._simulation.FieldColorScheme = scheme.Name;
			}
			else
			{
				this._simulation.IntensityColorScheme = scheme.Name;
			}
			this.MarkProjectChanged();
		}

		/// <summary>Starts a run, or asks the running one to stop (the button doubles as Stop while a run is active).</summary>
		private void btnStartSimulation_Click(object sender, EventArgs e)
		{
			if (this._runner != null)
			{
				this.RequestStop();
			}
			else
			{
				this.StartSimulation();
			}
		}

		/// <summary>Loads the scene image into the Sources and Probes picture boxes.</summary>
		private void btnShareSceneImage_Click(object sender, EventArgs e)
		{
			this.ImportSceneImageInto(this._picSource);
			this.ImportSceneImageInto(this._picProbe);
		}

		/// <summary>Copies the field preview into the Sources and Probes picture boxes.</summary>
		private void btnShareFieldImage_Click(object sender, EventArgs e)
		{
			this.CopyPreviewToTabs(this._picField);
		}

		/// <summary>Copies the intensity preview into the Sources and Probes picture boxes.</summary>
		private void btnShareIntensityImage_Click(object sender, EventArgs e)
		{
			this.CopyPreviewToTabs(this._picIntensity);
		}

		/// <summary>Gives <c>_picSource</c> and <c>_picProbe</c> each their own copy of the image in <paramref name="source"/>, disposing what they showed before. Does nothing when there is no image.</summary>
		private void CopyPreviewToTabs(PictureBox source)
		{
			Image image = source.Image;
			if (image == null)
			{
				return;
			}
			foreach (PictureBox target in new PictureBox[] { this._picSource, this._picProbe })
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

		/// <summary>Enables the share buttons when the matching preview box has an image; also refreshes the export buttons.</summary>
		private void UpdateCopyButtons()
		{
			this.btnShareFieldImage.Enabled = this._picField != null && this._picField.Image != null;
			this.btnShareIntensityImage.Enabled = this._picIntensity != null && this._picIntensity.Image != null;
			this.UpdateExportButtons();
		}

		/// <summary>Enables the frame export buttons when no run is active or it is paused, and a full-size or preview frame exists to export.</summary>
		private void UpdateExportButtons()
		{
			bool canExport = this._paused || this._runner == null;
			this.btnExportFieldFrame.Enabled = canExport && (this._lastFullFieldFrame != null || (this._picField != null && this._picField.Image != null));
			this.btnExportIntensityFrame.Enabled = canExport && (this._lastFullIntensityFrame != null || (this._picIntensity != null && this._picIntensity.Image != null));
		}

		/// <summary>
		/// Toggles pause: blocks or releases the runner's gate, switches the status texts and shows the viewer tab on resume.
		/// Ignored when nothing runs or a stop is already pending.
		/// </summary>
		private void btnPauzeResume_Click(object sender, EventArgs e)
		{
			if (this._runner == null || this._stopRequested)
			{
				return;
			}
			if (this._paused)
			{
				this._paused = false;
				this._simStatus = this._progressText;
				this.ToolStripStatusLabel1.Text = "Simulation running...";
				this._runner.ResumeRun();
				this.tabMainFunctions.SelectedTab = this.tabViewer;
			}
			else
			{
				this._paused = true;
				this._simStatus = "paused - " + this._progressText;
				this.ToolStripStatusLabel1.Text = "Simulation paused (press Resume to continue, or Stop to finish and save the videos)";
				this._runner.PauseRun();
			}
			this.UpdateRunButtons();
			this.UpdateTitle();
		}

		/// <summary>Shows the Configuration tab.</summary>
		private void btnGotoConfiguration_Click(object sender, EventArgs e)
		{
			this.tabMainFunctions.SelectedTab = this.tabConfiguration;
		}

		/// <summary>Shows the Sources tab.</summary>
		private void btnGotoSources_Click(object sender, EventArgs e)
		{
			this.tabMainFunctions.SelectedTab = this.tabSourceList;
		}

		/// <summary>Shows the Probes tab.</summary>
		private void btnGotoProbes_Click(object sender, EventArgs e)
		{
			this.tabMainFunctions.SelectedTab = this.tabProbeList;
		}

		/// <summary>Shows the viewer tab.</summary>
		private void btnGotoPreview_Click(object sender, EventArgs e)
		{
			this.tabMainFunctions.SelectedTab = this.tabViewer;
		}

		/// <summary>
		/// Asks the runner to cancel (it finishes the current step, closes the video files and raises <c>Finished</c>) and puts the
		/// UI into the "stopping" state; the Start button is disabled until <see cref="OnSimulationFinished"/> re-enables it.
		/// </summary>
		private void RequestStop()
		{
			if (this._runner == null || this._stopRequested)
			{
				return;
			}
			this._stopRequested = true;
			this._paused = false;
			this._runner.Cancel();
			this.btnStartSimulation.Enabled = false;
			this.btnStartSimulation.Text = "Stopping...";
			this._simStatus = "stopping...";
			this.ToolStripStatusLabel1.Text = "Stopping the simulation and closing the video files...";
			this.UpdateRunButtons();
			this.UpdateTitle();
		}

		/// <summary>Greys the pause button when it cannot be used, sets its Pause / Resume caption and refreshes the export buttons.</summary>
		private void UpdateRunButtons()
		{
			bool canPause = this._runner != null && !this._stopRequested;
			this.btnPauzeResume.ForeColor = (canPause ? SystemColors.ControlText : Color.Gray);
			this.btnPauzeResume.Text = (this._paused ? "Resume simulation" : "Pause simulation");
			this.UpdateExportButtons();
		}

		/// <summary>Shows <paramref name="message"/> in a "Start simulation" message box.</summary>
		private void ShowSimulationProblem(string message)
		{
			MessageBox.Show(this, message, "Start simulation", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}

		/// <summary>
		/// Validates every simulation control and copies it into <c>_simulation</c> (and the image source scale into
		/// <c>_scene</c>) just before a run, regardless of <c>_loadingSim</c>.
		/// </summary>
		private void ReadSimulationControls()
		{
			this.numVisualizeEvery.Validate();
			this.numFieldScaling.Validate();
			this.numIntensityScaling.Validate();
			this.numFieldScaling2.Validate();
			this.numIntensityScaling2.Validate();
			this.numOffsetField.Validate();
			this.numOffsetIntensity.Validate();
			this.numQuality.Validate();
			this.numScaleImageSource.Validate();
			this.numTimeDecayFactor.Validate();
			this._simulation.VisualizeEvery = Convert.ToInt32(this.numVisualizeEvery.Value);
			this._simulation.FieldBrightness = Convert.ToDouble(this.numFieldScaling.Value);
			this._simulation.IntensityBrightness = Convert.ToDouble(this.numIntensityScaling.Value);
			this._simulation.FieldBlackLevel = Convert.ToDouble(this.numOffsetField.Value);
			this._simulation.IntensityBlackLevel = Convert.ToDouble(this.numOffsetIntensity.Value);
			this._simulation.VideoQuality = Convert.ToInt32(this.numQuality.Value);
			this._simulation.TimeDecayFactor = Convert.ToDouble(this.numTimeDecayFactor.Value);
			this._simulation.ExportField = this.chkExportVideoField.Checked;
			this._simulation.ExportIntensity = this.chkExportVideoIntensity.Checked;
			this._simulation.PreviewField = this.chkPreviewVideoField.Checked;
			this._simulation.PreviewIntensity = this.chkPreviewVideoIntensity.Checked;
			this._simulation.UseGpu = this.chkUseGPU.Checked;
			this._simulation.Use4thOrderLaplacian = this.chkUse4thOrderLaplacian.Checked;
			this._scene.ImageSourceScale = Convert.ToDouble(this.numScaleImageSource.Value);
		}

		/// <summary>
		/// UI thread. Validates the scene and settings (asks to save the project first when exporting, warns when the scene has no
		/// sources), builds a <see cref="SimulationJob"/> with cloned settings, palettes, probes, sources and numbered output file
		/// names, wires every <see cref="SimulationRunner"/> event through <see cref="PostToUi"/> and starts the worker thread.
		/// </summary>
		private void StartSimulation()
		{
			this.ReadSimulationControls();
			SimulationSettings settings = this._simulation;
			bool wantField = settings.ExportField || settings.PreviewField;
			bool wantIntensity = settings.ExportIntensity || settings.PreviewIntensity;
			bool exporting = settings.ExportField || settings.ExportIntensity;
			bool previewing = settings.PreviewField || settings.PreviewIntensity;
			if (!wantField && !wantIntensity)
			{
				this.ShowSimulationProblem("Nothing to do: tick at least one of the export or preview options.");
				return;
			}
			settings.Steps = Convert.ToInt32(this.numSimulationSteps.Value);
			if (exporting && this._project == null)
			{
				DialogResult saveAnswer = MessageBox.Show(this, "The videos are saved in the output folder of the project, so the project must be saved first.\r\n\r\nSave the project now?", "Start simulation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
				if (saveAnswer != DialogResult.Yes)
				{
					return;
				}
				if (!this.SaveProject())
				{
					return;
				}
				// Saving replaces _simulation (SetCurrentProject reloads it from the config), so re-fetch it.
				settings = this._simulation;
			}
			Size canvas = this.EffectiveCanvas();
			if (canvas.Width <= 0)
			{
				this.ShowSimulationProblem("There is no scene yet: load a scene image in the Scene properties, or define a canvas size.");
				return;
			}
			int dampingEdge = 0;
			if (this._scene.DampingEdgeEnabled)
			{
				string dampingProblem = this.DampingProblem(this.txtDampingEdgeSize.Text, ref dampingEdge);
				if (dampingProblem.Length > 0)
				{
					this.ShowSimulationProblem("The damping edge cannot be used: " + dampingProblem);
					this.txtDampingEdgeSize.Focus();
					return;
				}
			}
			bool useSources = this._scene.UseSources;
			bool hasImageSources = this._scene.UseImage && useSources && SceneData.CountSources(this._sceneImage) > 0;
			bool hasObjectSources = this._scene.UseObjectSources && this._sources.Count > 0;
			string noSourcesWarning = "";
			if (!hasImageSources && !hasObjectSources)
			{
				List<string> reasons = new List<string>();
				if (this._scene.UseImage)
				{
					if (!useSources)
					{
						reasons.Add("image sources are switched off in the Scene properties");
					}
					else if (SceneData.CountSources(this._sceneImage) == 0)
					{
						reasons.Add("the scene image has no sources (no pixels with a green value above 0)");
					}
				}
				if (!this._scene.UseObjectSources)
				{
					reasons.Add("object sources are switched off in the Scene properties");
				}
				else if (this._sources.Count == 0)
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
			int[] fieldLut = null;
			int[] intensityLut = null;
			if (wantField)
			{
				ColorScheme fieldScheme = this.FindScheme(settings.FieldColorScheme, "field");
				if (fieldScheme == null)
				{
					this.ShowSimulationProblem("Choose a color scheme for the field first.");
					this.cmbFieldColorScheme.Focus();
					return;
				}
				fieldLut = ColorLut.Build(fieldScheme, false, settings.FieldBlackLevel, false, false).Argb;
			}
			if (wantIntensity)
			{
				ColorScheme intensityScheme = this.FindScheme(settings.IntensityColorScheme, "intensity");
				if (intensityScheme == null)
				{
					this.ShowSimulationProblem("Choose a color scheme for the intensity first.");
					this.cmbIntensityColorScheme.Focus();
					return;
				}
				intensityLut = ColorLut.Build(intensityScheme, false, settings.IntensityBlackLevel, false, false).Argb;
			}
			SimulationJob job = new SimulationJob();
			if (this._scene.UseImage)
			{
				job.Image = new Bitmap(this._sceneImage);
			}
			else
			{
				job.Width = canvas.Width;
				job.Height = canvas.Height;
				job.BackgroundRefractiveIndex = this._scene.BackgroundRefractiveIndex;
			}
			job.UseSources = useSources;
			job.SourceAmplitude = this._scene.ImageSourceScale;
			job.DampingEdge = dampingEdge;
			job.Settings = settings.Clone();
			job.FieldLut = fieldLut;
			job.IntensityLut = intensityLut;
			this._lastFieldLut = fieldLut;
			this._lastIntensityLut = intensityLut;
			job.PreviewWidth = Math.Max(16, Math.Min(this.pnlPreviewField.ClientSize.Width, this.pnlPreviewIntensity.ClientSize.Width));
			job.PreviewHeight = Math.Max(16, Math.Min(this.pnlPreviewField.ClientSize.Height, this.pnlPreviewIntensity.ClientSize.Height));
			job.Probes = this.ProbesForRun();
			job.ObjectSources = this.SourcesForRun();
			job.FieldOverlayGain = this.FieldOverlayGain();
			job.IntensityOverlayGain = this.IntensityOverlayGain();
			if (exporting && this._glowEdgesImage != null && (job.FieldOverlayGain > 0.0 || job.IntensityOverlayGain > 0.0))
			{
				job.GlowOverlay = new Bitmap(this._glowEdgesImage);
			}
			if (exporting)
			{
				try
				{
					Directory.CreateDirectory(this._project.OutputVideoFolder);
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
					this.ShowSimulationProblem("The output folder could not be created:\r\n" + this._project.OutputVideoFolder + "\r\n\r\n" + ex.Message);
					return;
				}
				string suffix = this.NextNumericSuffix(this._project.OutputVideoFolder).ToString("D4", CultureInfo.InvariantCulture);
				if (settings.ExportField)
				{
					job.FieldVideoPath = Path.Combine(this._project.OutputVideoFolder, this._project.Name + "_field_" + suffix + ".mp4");
				}
				if (settings.ExportIntensity)
				{
					job.IntensityVideoPath = Path.Combine(this._project.OutputVideoFolder, this._project.Name + "_intensity_" + suffix + ".mp4");
				}
			}
			this.ClearPreviews();
			this.BeginProbeRun(job.Probes, canvas, settings.Steps);
			this._fieldVideoFile = "";
			this._intensityVideoFile = "";
			this.UpdateVideoButtons();
			this._runInfo = "Field: brightness " + settings.FieldBrightness.ToString("0.0#", CultureInfo.CurrentCulture)
				+ ", offset " + settings.FieldBlackLevel.ToString("0.0#", CultureInfo.CurrentCulture)
				+ "\r\nIntensity: brightness " + settings.IntensityBrightness.ToString("0.0#", CultureInfo.CurrentCulture)
				+ ", offset " + settings.IntensityBlackLevel.ToString("0.0#", CultureInfo.CurrentCulture)
				+ "\r\nImage source scale: " + this._scene.ImageSourceScale.ToString("0.0#", CultureInfo.CurrentCulture);
			if (previewing)
			{
				this.tabMainFunctions.SelectedTab = this.tabViewer;
			}
			SimulationRunner runner = new SimulationRunner(job);
			runner.ProgressChanged += (done, total) => this.PostToUi(() => this.ShowProgress(runner, done, total));
			runner.EngineReady += (description, note) => this.PostToUi(() => this.ShowEngine(runner, description, note));
			runner.ProbesUpdated += update => this.PostToUi(() => this.ShowProbeSamples(runner, update));
			runner.PreviewReady += (isField, frame) => this.PostToUi(() => this.ShowPreview(runner, isField, frame));
			runner.Finished += result => this.PostToUi(() => this.OnSimulationFinished(runner, result));
			this._runner = runner;
			this.btnStartSimulation.Text = "Stop Simulation";
			this._simStatus = "starting...";
			this._progressText = "starting...";
			this._paused = false;
			this._stopRequested = false;
			this.UpdateRunButtons();
			this.ToolStripStatusLabel1.Text = "Simulation running...";
			this.ToolStripStatusLabel1.ToolTipText = "";
			this.UpdateTitle();
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
			if (runner != this._runner || string.CompareOrdinal(this._simStatus, "stopping...") == 0)
			{
				return;
			}
			this._lastStepsDone = done;
			int percent = (int)(100L * done / Math.Max(1, total)); // long: 100 * steps can exceed int.MaxValue
			this._progressText = "step " + done.ToString("N0", CultureInfo.CurrentCulture) + " of " + total.ToString("N0", CultureInfo.CurrentCulture) + " (" + percent.ToString(CultureInfo.InvariantCulture) + "%)";
			this._simStatus = (this._paused ? "paused - " : "") + this._progressText;
			this.UpdateTitle();
		}

		/// <summary>UI thread (via <see cref="PostToUi"/>): shows which engine the runner picked, plus the fallback note, in the status bar.</summary>
		private void ShowEngine(SimulationRunner runner, string description, string note)
		{
			if (runner != this._runner || base.IsDisposed)
			{
				return;
			}
			string status = "Simulation running on " + description;
			if (note.Length > 0)
			{
				status = status + " - " + note;
			}
			this.ToolStripStatusLabel1.Text = status;
			this.ToolStripStatusLabel1.ToolTipText = status;
		}

		/// <summary>
		/// UI thread (via <see cref="PostToUi"/>): shows the downscaled preview frame in the field or intensity box (composed with the
		/// glow overlay when enabled) and takes ownership of <paramref name="frame"/>; a frame for a stale runner is disposed instead.
		/// Always acknowledges with <see cref="SimulationRunner.PreviewShown"/> so the worker may render the next preview.
		/// </summary>
		private void ShowPreview(SimulationRunner runner, bool isField, Bitmap frame)
		{
			if (runner != this._runner || base.IsDisposed)
			{
				frame.Dispose();
				runner.PreviewShown();
				return;
			}
			PictureBox target = (isField ? this._picField : this._picIntensity);
			double overlayGain = (isField ? this.FieldOverlayGain() : this.IntensityOverlayGain());
			this.SetPreviewFrame(target, frame, overlayGain);
			this.UpdateCopyButtons();
			runner.PreviewShown();
		}

		/// <summary>
		/// UI thread (via <see cref="PostToUi"/>): clears the runner, takes ownership of the result's full-size frames, enables the
		/// video buttons and builds the status-bar summary (steps, elapsed, steps/s, engine, files) with the details in its tooltip;
		/// a failed run is also reported in a message box.
		/// </summary>
		private void OnSimulationFinished(SimulationRunner runner, SimulationResult result)
		{
			if (runner != this._runner || base.IsDisposed)
			{
				return;
			}
			this._runner = null;
			this._hasRun = true;
			this._paused = false;
			this._stopRequested = false;
			this._lastStepsDone = result.StepsDone;
			this.UpdateRunButtons();
			this._simStatus = "";
			this.btnStartSimulation.Text = "Start Simulation";
			this.btnStartSimulation.Enabled = true;
			this.UpdateTitle();
			this._fieldVideoFile = result.FieldFile;
			this._intensityVideoFile = result.IntensityFile;
			this.UpdateVideoButtons();
			Bitmap previousFieldFrame = this._lastFullFieldFrame;
			this._lastFullFieldFrame = result.LastFieldFrame;
			if (previousFieldFrame != null)
			{
				previousFieldFrame.Dispose();
			}
			Bitmap previousIntensityFrame = this._lastFullIntensityFrame;
			this._lastFullIntensityFrame = result.LastIntensityFrame;
			if (previousIntensityFrame != null)
			{
				previousIntensityFrame.Dispose();
			}
			this.UpdateExportButtons();
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
			details = details + "\r\n\r\n" + this._runInfo;
			string stepsText = result.StepsDone.ToString("N0", CultureInfo.CurrentCulture);
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
			this.ToolStripStatusLabel1.Text = summary;
			this.ToolStripStatusLabel1.ToolTipText = summary + details;
			if (result.Status == SimulationStatus.Failed)
			{
				MessageBox.Show(this, "The simulation failed after " + stepsText + " steps:\r\n\r\n" + result.Message + details, "Simulation", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Enables the "view video" buttons when the last run's video files still exist.</summary>
		private void UpdateVideoButtons()
		{
			this.btnViewFieldVideo.Enabled = this._fieldVideoFile.Length > 0 && File.Exists(this._fieldVideoFile);
			this.btnViewIntensityVideo.Enabled = this._intensityVideoFile.Length > 0 && File.Exists(this._intensityVideoFile);
		}

		/// <summary>Opens the last field video in the default media player.</summary>
		private void btnViewFieldVideo_Click(object sender, EventArgs e)
		{
			this.OpenVideo(this._fieldVideoFile);
		}

		/// <summary>Opens the last intensity video in the default media player.</summary>
		private void btnViewIntensityVideo_Click(object sender, EventArgs e)
		{
			this.OpenVideo(this._intensityVideoFile);
		}

		/// <summary>Launches <paramref name="videoFile"/> with the shell's default player; tells the user when the file has gone or cannot be opened.</summary>
		private void OpenVideo(string videoFile)
		{
			if (videoFile.Length == 0 || !File.Exists(videoFile))
			{
				MessageBox.Show(this, "The video file does not exist (any more):\r\n" + videoFile, "Video", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				this.UpdateVideoButtons();
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
			this.OpenVideoFolder();
		}

		/// <summary>Opens the project's exported-frames folder in Explorer.</summary>
		private void btnOpenImageFolder_Click(object sender, EventArgs e)
		{
			this.OpenFramesFolder();
		}

		/// <summary>Saves the current or last field frame as a PNG.</summary>
		private void btnExportFieldFrame_Click(object sender, EventArgs e)
		{
			this.ExportFrame(true);
		}

		/// <summary>Saves the current or last intensity frame as a PNG.</summary>
		private void btnExportIntensityFrame_Click(object sender, EventArgs e)
		{
			this.ExportFrame(false);
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
			if (this._runner != null && this._paused)
			{
				int[] lut = (isField ? this._lastFieldLut : this._lastIntensityLut);
				float brightness = (float)(isField ? this._simulation.FieldBrightness : this._simulation.IntensityBrightness);
				frame = this._runner.RenderCurrentFrame(isField, lut, brightness);
				ownsFrame = true;
			}
			if (frame == null)
			{
				frame = (isField ? this._lastFullFieldFrame : this._lastFullIntensityFrame);
			}
			if (frame == null)
			{
				MessageBox.Show(this, "There is no picture to export yet.", "Export frame", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			try
			{
				Directory.CreateDirectory(this._project.OutputFramesFolder);
			}
			catch (Exception ex)
			{
				if (ownsFrame)
				{
					frame.Dispose();
				}
				MessageBox.Show(this, "The output folder could not be created:\r\n" + this._project.OutputFramesFolder + "\r\n\r\n" + ex.Message, "Export frame", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			string kind = (isField ? "field" : "intensity");
			double overlayGain = (isField ? this.FieldOverlayGain() : this.IntensityOverlayGain());
			Bitmap output = frame;
			bool ownsOutput = false;
			if (overlayGain > 0.0 && this._glowEdgesImage != null)
			{
				output = this.ComposeGlowOverlay(frame, overlayGain);
				ownsOutput = true;
			}
			string suffix = this.NextNumericSuffix(this._project.OutputFramesFolder).ToString("D4", CultureInfo.InvariantCulture);
			string fileName = this._project.Name + "_" + kind + "_frame" + this._lastStepsDone.ToString(CultureInfo.InvariantCulture) + "_" + suffix + ".png";
			string path = Path.Combine(this._project.OutputFramesFolder, fileName);
			try
			{
				output.Save(path, ImageFormat.Png);
				this.ToolStripStatusLabel1.Text = "Frame exported: " + fileName;
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

		/// <summary>Returns one more than the highest 4-digit numeric suffix among the file names in <paramref name="folder"/>.</summary>
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

		/// <summary>Opens the project's video folder in Explorer (details view); reports when there is no project.</summary>
		private void OpenVideoFolder()
		{
			string folder = ((this._project == null) ? "" : this._project.OutputVideoFolder);
			if (folder.Length == 0)
			{
				MessageBox.Show(this, "There is no project open yet.", "Open video folder", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			try
			{
				this.OpenFolderInExplorer(folder, 4);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The video folder could not be opened:\r\n\r\n" + ex.Message, "Open video folder", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Opens the project's exported-frames folder in Explorer (icon view); reports when there is no project.</summary>
		private void OpenFramesFolder()
		{
			string folder = ((this._project == null) ? "" : this._project.OutputFramesFolder);
			if (folder.Length == 0)
			{
				MessageBox.Show(this, "There is no project open yet.", "Open image folder", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			try
			{
				this.OpenFolderInExplorer(folder, 1);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The image folder could not be opened:\r\n\r\n" + ex.Message, "Open image folder", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Creates <paramref name="folder"/> if needed, opens it in Explorer and tries to apply <paramref name="viewMode"/>; view-mode failures are ignored.</summary>
		/// <param name="folder">Folder to open; created when missing (that may throw, unlike the view-mode step).</param>
		/// <param name="viewMode">Explorer FOLDERVIEWMODE value: 1 = icons, 4 = details.</param>
		private void OpenFolderInExplorer(string folder, int viewMode)
		{
			Directory.CreateDirectory(folder);
			Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"")
			{
				UseShellExecute = true
			});
			try
			{
				this.SetExplorerWindowViewMode(folder, viewMode);
			}
			catch (Exception)
			{
			}
		}

		/// <summary>
		/// Finds the Explorer window that was just opened on <paramref name="folder"/> (polling up to 8 times)
		/// through the Shell.Application COM automation object (late bound) and sets its view mode (e.g. 1 = icons, 4 = details).
		/// Runs on the UI thread and sleeps 120 ms between polls, so it can block the UI for about a second when the window never shows up.
		/// </summary>
		private void SetExplorerWindowViewMode(string folder, int viewMode)
		{
			string target = Path.GetFullPath(folder).TrimEnd(new char[] { Path.DirectorySeparatorChar });
			Type shellType = Type.GetTypeFromProgID("Shell.Application");
			if (shellType == null)
			{
				return;
			}
			object shell = Activator.CreateInstance(shellType);
			try
			{
				for (int attempt = 1; attempt <= 8; attempt++)
				{
					object windows = shellType.InvokeMember("Windows", BindingFlags.InvokeMethod | BindingFlags.GetProperty, null, shell, null);
					Type windowsType = windows.GetType();
					int count = Convert.ToInt32(windowsType.InvokeMember("Count", BindingFlags.InvokeMethod | BindingFlags.GetProperty, null, windows, null));
					for (int i = 0; i < count; i++)
					{
						object window = windowsType.InvokeMember("Item", BindingFlags.InvokeMethod | BindingFlags.GetProperty, null, windows, new object[] { i });
						if (window == null)
						{
							continue;
						}
						Type windowType = window.GetType();
						string locationUrl = windowType.InvokeMember("LocationURL", BindingFlags.InvokeMethod | BindingFlags.GetProperty, null, window, null) as string;
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
						if (string.Equals(windowPath, target, StringComparison.OrdinalIgnoreCase))
						{
							object document = windowType.InvokeMember("Document", BindingFlags.InvokeMethod | BindingFlags.GetProperty, null, window, null);
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
			foreach (PictureBox pictureBox in new PictureBox[] { this._picField, this._picIntensity })
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
			if (this._lastFullFieldFrame != null)
			{
				this._lastFullFieldFrame.Dispose();
			}
			this._lastFullFieldFrame = null;
			if (this._lastFullIntensityFrame != null)
			{
				this._lastFullIntensityFrame.Dispose();
			}
			this._lastFullIntensityFrame = null;
			this.UpdateCopyButtons();
		}

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
			this.SaveProject();
		}

		/// <summary>
		/// "Created on" column header of the video list on the Output tab. Created and assigned by the designer; declared as a
		/// property (VB.NET WithEvents style) and placed in this file by the decompiler.
		/// </summary>
		protected internal virtual ColumnHeader colCreatedOn2 { get; set; }
	}
}
