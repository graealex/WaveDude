using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Source list tab: editing, saving and previewing the wave sources of the project.
	/// The list of saved sources lives in <c>_sources</c>; the editor controls always show
	/// <c>_editing</c>, which is a clone of the selected entry (or a fresh source when nothing
	/// is selected) until the user presses "Save source". <c>_loadingSource</c> guards the
	/// control change handlers while the editor is being filled, <c>_syncingSourceList</c>
	/// guards the list selection handler while the list is rebuilt. Everything here runs on
	/// the UI thread.
	/// </summary>
	public partial class Form1
	{
		/// <summary>
		/// One-time set-up at form load: remembers the "Pick" button text and the save button
		/// colour, sets the numeric input limits (positions 0..16383 canvas px, array sizes
		/// 1..999 stepping by 2, spacings 0..16384 px), wires dirty tracking and the scene
		/// picture events, switches the list to owner drawing and shows the initial source.
		/// </summary>
		private void InitializeSourceControls()
		{
			this._pickSourceButtonText = this.btnPickSourcePosition.Text;
			this._sourceSaveButtonDefaultBackColor = this.btnSaveSource.BackColor;
			this.WireSourceDirtyTracking(this.grpSourceDetails);
			this.numSourcePositionX.DecimalPlaces = 0;
			this.numSourcePositionX.Minimum = 0m;
			this.numSourcePositionX.Maximum = 16383m;
			this.numSourcePositionY.DecimalPlaces = 0;
			this.numSourcePositionY.Minimum = 0m;
			this.numSourcePositionY.Maximum = 16383m;
			this.numArraySizeX.DecimalPlaces = 0;
			this.numArraySizeX.Minimum = 1m;
			this.numArraySizeX.Maximum = 999m;
			this.numArraySizeX.Increment = 2m;
			this.numArraySizeY.DecimalPlaces = 0;
			this.numArraySizeY.Minimum = 1m;
			this.numArraySizeY.Maximum = 999m;
			this.numArraySizeY.Increment = 2m;
			this.numSpacingSizeX.DecimalPlaces = 0;
			this.numSpacingSizeX.Minimum = 0m;
			this.numSpacingSizeX.Maximum = 16384m;
			this.numSpacingSizeY.DecimalPlaces = 0;
			this.numSpacingSizeY.Minimum = 0m;
			this.numSpacingSizeY.Maximum = 16384m;
			this.numAmplitude.DecimalPlaces = 0;
			this.numPulseWidthCycles.DecimalPlaces = 0;
			if (this.numPulseWidthCycles.Minimum < 1m)
			{
				this.numPulseWidthCycles.Minimum = 1m;
			}
			// Designer limit is too small for long pulses; widen it without touching the Designer.
			if (this.numPulseWidthCycles.Maximum < 100m)
			{
				this.numPulseWidthCycles.Maximum = 1000000m;
			}
			this.numOffStateCycles.DecimalPlaces = 0;
			this.numRandomizeFrequency.DecimalPlaces = 0;
			this.numRandomizeOffWidth.DecimalPlaces = 0;
			this.numRandomizePosition.DecimalPlaces = 0;
			this.NumRandomizePhase.DecimalPlaces = 0;
			this._picSource.Paint += this.SourcePicture_Paint;
			this._picSource.MouseClick += this.SourcePicture_MouseClick;
			this.lstSources.DrawMode = DrawMode.OwnerDrawFixed;
			this.RefreshSourceList();
			this.ShowSourceInControls(this._editing);
			this.ClearSourceDirty();
			this.UpdateListButtonState();
		}

		/// <summary>
		/// Replaces the source list with the sources stored in the open project and selects
		/// the first one (or an empty editor when the project has none). No-op without a project.
		/// </summary>
		private void LoadSourcesFromProject()
		{
			if (this._project == null)
			{
				return;
			}
			this._sources = SourceSettings.ListFromConfig(this._project.Config);
			if (this._sources.Count > 0)
			{
				this._currentSourceIndex = 0;
				this._editing = this._sources[0].Clone();
			}
			else
			{
				this._currentSourceIndex = -1;
				this._editing = new SourceSettings();
			}
			this.RefreshSourceList();
			this._syncingSourceList = true;
			try
			{
				this.lstSources.SelectedIndex = this._currentSourceIndex;
			}
			finally
			{
				this._syncingSourceList = false;
			}
			this.ShowSourceInControls(this._editing);
			this.ClearSourceDirty();
			this.UpdateListButtonState();
		}

		/// <summary>
		/// Empties the source list and the editor for a new project. The editor is only
		/// refreshed once the source picture box exists, i.e. after the form has been built.
		/// </summary>
		private void ResetSources()
		{
			this._sources = new List<SourceSettings>();
			this._currentSourceIndex = -1;
			this._editing = new SourceSettings();
			this.RefreshSourceList();
			if (this._picSource != null)
			{
				this.ShowSourceInControls(this._editing);
				this.ClearSourceDirty();
			}
			this.UpdateListButtonState();
		}

		/// <summary>
		/// Collects the elements of all enabled sources for a simulation run, or null when
		/// object sources are not in use (scene flag off, or no sources at all). Sources whose
		/// element list is still empty are generated on the spot. The returned list holds the
		/// sources' own <see cref="SourceElement"/> instances, not copies; the engines only
		/// read them from the worker thread.
		/// </summary>
		private List<SourceElement> SourcesForRun()
		{
			if (this._scene == null || !this._scene.UseObjectSources || this._sources.Count == 0)
			{
				return null;
			}
			List<SourceElement> elements = new List<SourceElement>();
			foreach (SourceSettings source in this._sources)
			{
				if (!source.SourceOn)
				{
					continue;
				}
				if (source.Elements == null || source.Elements.Count == 0)
				{
					source.GenerateElements(this._sourceRandom);
				}
				elements.AddRange(source.Elements);
			}
			return elements;
		}

		/// <summary>Rebuilds the list box items from <c>_sources</c>, keeping the current selection index when it is still valid; the selection handler is suppressed meanwhile.</summary>
		private void RefreshSourceList()
		{
			int selectedIndex = this.lstSources.SelectedIndex;
			this._syncingSourceList = true;
			try
			{
				this.lstSources.Items.Clear();
				foreach (SourceSettings source in this._sources)
				{
					this.lstSources.Items.Add(Form1.SourceListText(source));
				}
				if (selectedIndex >= 0 && selectedIndex < this.lstSources.Items.Count)
				{
					this.lstSources.SelectedIndex = selectedIndex;
				}
			}
			finally
			{
				this._syncingSourceList = false;
			}
		}

		/// <summary>List box caption for a source: the name followed by "(single)" or "(composite, X x Y)".</summary>
		private static string SourceListText(SourceSettings s)
		{
			if (s.MainType == SourceMainType.Composite)
			{
				return s.Name + " (composite, " + s.ArraySizeX.ToString(CultureInfo.InvariantCulture) + " x " + s.ArraySizeY.ToString(CultureInfo.InvariantCulture) + ")";
			}
			return s.Name + " (single)";
		}

		/// <summary>Owner-draws a list entry, in grey when the source is switched off.</summary>
		private void lstSources_DrawItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index < 0 || e.Index >= this.lstSources.Items.Count)
			{
				return;
			}
			e.DrawBackground();
			Color textColor = ((e.Index < this._sources.Count && !this._sources[e.Index].SourceOn) ? Color.Gray : e.ForeColor);
			using (SolidBrush brush = new SolidBrush(textColor))
			{
				e.Graphics.DrawString(this.lstSources.Items[e.Index].ToString(), e.Font, brush, e.Bounds);
			}
			e.DrawFocusRectangle();
		}

		/// <summary>
		/// Selecting a list entry cancels any position pick and loads a clone of that source into
		/// the editor (unsaved edits of the previous source are discarded); deselecting loads an
		/// empty source. Ignored while the list is being synchronised programmatically.
		/// </summary>
		private void lstSources_SelectedIndexChanged(object sender, EventArgs e)
		{
			this.UpdateListButtonState();
			if (this._syncingSourceList)
			{
				return;
			}
			this.EndSourcePick();
			if (this.lstSources.SelectedIndex < 0)
			{
				this._currentSourceIndex = -1;
				this._editing = new SourceSettings();
			}
			else
			{
				this._currentSourceIndex = this.lstSources.SelectedIndex;
				this._editing = this._sources[this._currentSourceIndex].Clone();
			}
			this.ShowSourceInControls(this._editing);
			this.ClearSourceDirty();
			if (this._picSource != null)
			{
				this._picSource.Invalidate();
			}
			this.picPreviewSource.Invalidate();
			this.ToolStripStatusLabel1.Text = ((this._currentSourceIndex >= 0) ? ("Editing '" + this._editing.Name + "': press Save source to keep the changes.") : "");
		}

		/// <summary>Enables Copy and Delete only while a list entry is selected.</summary>
		private void UpdateListButtonState()
		{
			bool hasSelection = this.lstSources.SelectedIndex >= 0;
			this.btnCopy.Enabled = hasSelection;
			this.btnDelete.Enabled = hasSelection;
		}

		/// <summary>Starts a new, unsaved source named "Source N" in the editor, deselects the list and focuses the name box.</summary>
		private void btnNew_Click(object sender, EventArgs e)
		{
			this.EndSourcePick();
			this._syncingSourceList = true;
			try
			{
				this.lstSources.SelectedIndex = -1;
			}
			finally
			{
				this._syncingSourceList = false;
			}
			this._currentSourceIndex = -1;
			this._editing = new SourceSettings();
			this._editing.Name = "Source " + (this._sources.Count + 1).ToString(CultureInfo.InvariantCulture);
			this.ShowSourceInControls(this._editing);
			this.MarkSourceDirty();
			this.UpdateListButtonState();
			if (this._picSource != null)
			{
				this._picSource.Invalidate();
			}
			this.picPreviewSource.Invalidate();
			this.ToolStripStatusLabel1.Text = "New source: fill in its properties, then press Save source.";
			this.txtName.Focus();
			this.txtName.SelectAll();
		}

		/// <summary>Loads a clone of the selected source, named "... copy", into the editor as a new unsaved source (it is only added to the list on save).</summary>
		private void btnCopy_Click(object sender, EventArgs e)
		{
			if (this.lstSources.SelectedIndex < 0)
			{
				return;
			}
			this.EndSourcePick();
			string originalName = this._sources[this.lstSources.SelectedIndex].Name;
			this._editing = this._sources[this.lstSources.SelectedIndex].Clone();
			this._editing.Name = this._editing.Name + " copy";
			this._syncingSourceList = true;
			try
			{
				this.lstSources.SelectedIndex = -1;
			}
			finally
			{
				this._syncingSourceList = false;
			}
			this._currentSourceIndex = -1;
			this.ShowSourceInControls(this._editing);
			this.MarkSourceDirty();
			this.UpdateListButtonState();
			if (this._picSource != null)
			{
				this._picSource.Invalidate();
			}
			this.picPreviewSource.Invalidate();
			this.ToolStripStatusLabel1.Text = "Copy of '" + originalName + "': change it and press Save source to add it as a new source.";
			this.txtName.Focus();
			this.txtName.SelectAll();
		}

		/// <summary>After confirmation, removes the selected source from the list and marks the project dirty; clears the editor if that source was being edited.</summary>
		private void btnDelete_Click(object sender, EventArgs e)
		{
			if (this.lstSources.SelectedIndex < 0)
			{
				return;
			}
			int selectedIndex = this.lstSources.SelectedIndex;
			string name = this._sources[selectedIndex].Name;
			if (MessageBox.Show(this, "Delete the source '" + name + "'?", "Delete source", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
			{
				return;
			}
			this._sources.RemoveAt(selectedIndex);
			if (this._currentSourceIndex == selectedIndex)
			{
				this._currentSourceIndex = -1;
				this._editing = new SourceSettings();
				this.ShowSourceInControls(this._editing);
				this.ClearSourceDirty();
			}
			else if (this._currentSourceIndex > selectedIndex)
			{
				this._currentSourceIndex--;
			}
			this.RefreshSourceList();
			this.UpdateListButtonState();
			this.MarkProjectChanged();
			if (this._picSource != null)
			{
				this._picSource.Invalidate();
			}
			this.picPreviewSource.Invalidate();
			this.ToolStripStatusLabel1.Text = "Source '" + name + "' deleted.";
		}

		/// <summary>Saves the editor contents into the list (see <see cref="CommitSourceEdits"/>).</summary>
		private void btnSaveSource_Click(object sender, EventArgs e)
		{
			this.CommitSourceEdits();
		}

		/// <summary>
		/// Reads the editor controls into the source being edited, regenerates its elements
		/// (re-rolling any randomisation) and stores it in the list, replacing the selected entry
		/// or appending a new one. Marks the project dirty and leaves the editor on a fresh clone
		/// of the saved source. Returns false, without saving, if the source has no name.
		/// </summary>
		private bool CommitSourceEdits()
		{
			this.ReadSourceControls(this._editing);
			if (this._editing.Name.Trim().Length == 0)
			{
				MessageBox.Show(this, "Give the source a name first.", "Save source", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				this.txtName.Focus();
				return false;
			}
			this._editing.GenerateElements(this._sourceRandom);
			if (this._currentSourceIndex >= 0 && this._currentSourceIndex < this._sources.Count)
			{
				this._sources[this._currentSourceIndex] = this._editing;
			}
			else
			{
				this._sources.Add(this._editing);
				this._currentSourceIndex = this._sources.Count - 1;
			}
			this.RefreshSourceList();
			this._syncingSourceList = true;
			try
			{
				this.lstSources.SelectedIndex = this._currentSourceIndex;
			}
			finally
			{
				this._syncingSourceList = false;
			}
			this.UpdateListButtonState();
			this.MarkProjectChanged();
			if (this._picSource != null)
			{
				this._picSource.Invalidate();
			}
			this.picPreviewSource.Invalidate();
			this.ToolStripStatusLabel1.Text = "Source '" + this._editing.Name + "' saved (" + this._editing.Elements.Count.ToString(CultureInfo.InvariantCulture) + " element(s)).";
			this._editing = this._editing.Clone();
			this.ClearSourceDirty();
			return true;
		}

		/// <summary>
		/// Fills the editor controls from a source (after normalising it), with
		/// <c>_loadingSource</c> set so the change handlers neither mark the editor dirty nor
		/// write back into <c>_editing</c>. Values outside a control's range are clamped for
		/// display. The randomise limits are refreshed before the randomise amounts are set so
		/// they are not clipped by stale maxima; finally the enabled states and the wavelength
		/// label are updated.
		/// </summary>
		private void ShowSourceInControls(SourceSettings s)
		{
			s.Normalize();
			this._loadingSource = true;
			try
			{
				this.txtName.Text = s.Name;
				this.cmbMainType.SelectedIndex = (int)s.MainType;
				this.chkSourceOn.Checked = s.SourceOn;
				this.chkDetachSource.Checked = s.DetachSource;
				this.txtDetachAfterSteps.Text = s.DetachAfterSteps.ToString(CultureInfo.InvariantCulture);
				this.numSourcePositionX.Value = Form1.ClampToRange(this.numSourcePositionX, s.PosX);
				this.numSourcePositionY.Value = Form1.ClampToRange(this.numSourcePositionY, s.PosY);
				this.numFrequency.Value = Form1.ClampToRange(this.numFrequency, s.Frequency);
				this.numAmplitude.Value = Form1.ClampToRange(this.numAmplitude, s.Amplitude);
				this.numPhase.Value = Form1.ClampToRange(this.numPhase, s.PhaseDeg);
				this.radModePulsed.Checked = s.Mode == SourceMode.Pulsed;
				this.radModeContinuous.Checked = s.Mode == SourceMode.Continuous;
				this.cmbPulseShape.SelectedIndex = (int)s.PulseShape;
				this.numPulseWidthCycles.Value = Form1.ClampToRange(this.numPulseWidthCycles, s.PulseWidthCycles);
				this.numOffStateCycles.Value = Form1.ClampToRange(this.numOffStateCycles, s.OffStateCycles);
				this.numArraySizeX.Value = Form1.ClampToRange(this.numArraySizeX, s.ArraySizeX);
				this.numArraySizeY.Value = Form1.ClampToRange(this.numArraySizeY, s.ArraySizeY);
				this.numSpacingSizeX.Value = Form1.ClampToRange(this.numSpacingSizeX, s.SpacingX);
				this.numSpacingSizeY.Value = Form1.ClampToRange(this.numSpacingSizeY, s.SpacingY);
				this.UpdateRandomizeLimits();
				this.cmbCompositeProperties.SelectedIndex = (int)s.CompositeMode;
				this.chkRandomizeFrequency.Checked = s.RandomizeFrequency;
				this.numRandomizeFrequency.Value = Form1.ClampToRange(this.numRandomizeFrequency, s.RandomizeFrequencyAmount);
				this.chkRandomizePhase.Checked = s.RandomizePhase;
				this.NumRandomizePhase.Value = Form1.ClampToRange(this.NumRandomizePhase, s.RandomizePhaseAmount);
				this.chkOffWidth.Checked = s.RandomizeOffWidth;
				this.numRandomizeOffWidth.Value = Form1.ClampToRange(this.numRandomizeOffWidth, s.RandomizeOffWidthAmount);
				this.chkRandomizePosition.Checked = s.RandomizePosition;
				this.numRandomizePosition.Value = Form1.ClampToRange(this.numRandomizePosition, s.RandomizePositionAmount);
			}
			finally
			{
				this._loadingSource = false;
			}
			this.UpdateSourceControlState();
			this.UpdateLambdaLabel();
		}

		/// <summary>Hooks the change events of every input control below <paramref name="container"/> (recursively) to the dirty marker.</summary>
		private void WireSourceDirtyTracking(Control container)
		{
			foreach (Control control in container.Controls)
			{
				if (control is TextBox textBox)
				{
					textBox.TextChanged += this.SourceDetail_Changed;
				}
				else if (control is NumericUpDown numericUpDown)
				{
					numericUpDown.ValueChanged += this.SourceDetail_Changed;
				}
				else if (control is CheckBox checkBox)
				{
					checkBox.CheckedChanged += this.SourceDetail_Changed;
				}
				else if (control is RadioButton radioButton)
				{
					radioButton.CheckedChanged += this.SourceDetail_Changed;
				}
				else if (control is ComboBox comboBox)
				{
					comboBox.SelectedIndexChanged += this.SourceDetail_Changed;
				}
				if (control.HasChildren)
				{
					this.WireSourceDirtyTracking(control);
				}
			}
		}

		/// <summary>Any user edit in the source details marks the editor dirty, unless the controls are being filled programmatically.</summary>
		private void SourceDetail_Changed(object sender, EventArgs e)
		{
			if (!this._loadingSource)
			{
				this.MarkSourceDirty();
			}
		}

		/// <summary>Signals unsaved edits by colouring the save button orange.</summary>
		private void MarkSourceDirty()
		{
			this.btnSaveSource.BackColor = Color.Orange;
		}

		/// <summary>Restores the save button to its default colour once the editor matches the saved source.</summary>
		private void ClearSourceDirty()
		{
			this.btnSaveSource.BackColor = this._sourceSaveButtonDefaultBackColor;
		}

		/// <summary>
		/// Copies the editor controls into <paramref name="target"/> and normalises it. The
		/// detach step count is parsed with the invariant culture and left unchanged when the
		/// text is not a non-negative integer. Does not touch <c>HasPosition</c> or the elements.
		/// </summary>
		private void ReadSourceControls(SourceSettings target)
		{
			target.Name = this.txtName.Text.Trim();
			target.MainType = ((this.cmbMainType.SelectedIndex == 1) ? SourceMainType.Composite : SourceMainType.Simple);
			target.SourceOn = this.chkSourceOn.Checked;
			target.DetachSource = this.chkDetachSource.Checked;
			int detachAfterSteps;
			if (int.TryParse(this.txtDetachAfterSteps.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out detachAfterSteps) && detachAfterSteps >= 0)
			{
				target.DetachAfterSteps = detachAfterSteps;
			}
			target.PosX = Convert.ToInt32(this.numSourcePositionX.Value);
			target.PosY = Convert.ToInt32(this.numSourcePositionY.Value);
			target.Frequency = Convert.ToInt32(this.numFrequency.Value);
			target.Amplitude = Convert.ToInt32(this.numAmplitude.Value);
			target.PhaseDeg = Convert.ToInt32(this.numPhase.Value);
			target.Mode = (this.radModePulsed.Checked ? SourceMode.Pulsed : SourceMode.Continuous);
			target.PulseShape = ((this.cmbPulseShape.SelectedIndex == 1) ? SourcePulseShape.SmoothedSquare : SourcePulseShape.Gaussian);
			target.PulseWidthCycles = Convert.ToInt32(this.numPulseWidthCycles.Value);
			target.OffStateCycles = Convert.ToInt32(this.numOffStateCycles.Value);
			target.ArraySizeX = Convert.ToInt32(this.numArraySizeX.Value);
			target.ArraySizeY = Convert.ToInt32(this.numArraySizeY.Value);
			target.SpacingX = Convert.ToInt32(this.numSpacingSizeX.Value);
			target.SpacingY = Convert.ToInt32(this.numSpacingSizeY.Value);
			target.CompositeMode = ((this.cmbCompositeProperties.SelectedIndex == 1) ? SourceCompositeMode.InheritAndRandomize : SourceCompositeMode.InheritFromSingle);
			target.RandomizeFrequency = this.chkRandomizeFrequency.Checked;
			target.RandomizeFrequencyAmount = Convert.ToInt32(this.numRandomizeFrequency.Value);
			target.RandomizePhase = this.chkRandomizePhase.Checked;
			target.RandomizePhaseAmount = Convert.ToInt32(this.NumRandomizePhase.Value);
			target.RandomizeOffWidth = this.chkOffWidth.Checked;
			target.RandomizeOffWidthAmount = Convert.ToInt32(this.numRandomizeOffWidth.Value);
			target.RandomizePosition = this.chkRandomizePosition.Checked;
			target.RandomizePositionAmount = Convert.ToInt32(this.numRandomizePosition.Value);
			target.Normalize();
		}

		/// <summary>
		/// Enables or disables the dependent inputs: pulse controls only in pulsed mode, the
		/// detach step box only when detaching, the array and composite controls only for a
		/// composite source, and each randomise amount only when the composite mode is
		/// "randomize" and its own checkbox is ticked.
		/// </summary>
		private void UpdateSourceControlState()
		{
			bool isComposite = this.cmbMainType.SelectedIndex == 1;
			this.grpComposite.Enabled = isComposite;
			bool isPulsed = this.radModePulsed.Checked;
			this.cmbPulseShape.Enabled = isPulsed;
			this.numPulseWidthCycles.Enabled = isPulsed;
			this.numOffStateCycles.Enabled = isPulsed;
			this.txtDetachAfterSteps.Enabled = this.chkDetachSource.Checked;
			bool canRandomize = isComposite && this.cmbCompositeProperties.SelectedIndex == 1;
			this.cmbCompositeProperties.Enabled = isComposite;
			this.numArraySizeX.Enabled = isComposite;
			this.numArraySizeY.Enabled = isComposite;
			this.numSpacingSizeX.Enabled = isComposite;
			this.numSpacingSizeY.Enabled = isComposite;
			this.chkRandomizeFrequency.Enabled = canRandomize;
			this.chkRandomizePhase.Enabled = canRandomize;
			this.chkOffWidth.Enabled = canRandomize;
			this.chkRandomizePosition.Enabled = canRandomize;
			this.numRandomizeFrequency.Enabled = canRandomize && this.chkRandomizeFrequency.Checked;
			this.NumRandomizePhase.Enabled = canRandomize && this.chkRandomizePhase.Checked;
			this.numRandomizeOffWidth.Enabled = canRandomize && this.chkOffWidth.Checked;
			this.numRandomizePosition.Enabled = canRandomize && this.chkRandomizePosition.Checked;
		}

		/// <summary>Changing the main type or the composite mode combo box re-evaluates which inputs are enabled.</summary>
		private void SourceMainType_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (!this._loadingSource)
			{
				this.UpdateSourceControlState();
			}
		}

		/// <summary>Switching between continuous and pulsed enables or disables the pulse inputs.</summary>
		private void SourceMode_CheckedChanged(object sender, EventArgs e)
		{
			if (!this._loadingSource)
			{
				this.UpdateSourceControlState();
			}
		}

		/// <summary>Changing the frequency updates the wavelength label.</summary>
		private void SourceFrequency_ValueChanged(object sender, EventArgs e)
		{
			this.UpdateLambdaLabel();
		}

		/// <summary>
		/// Shows the wavelength in canvas pixels that follows from the frequency input and the
		/// simulation settings: angular frequency = level / 255 * 0.5 * SourceFrequencyScale
		/// (radians per step), wavelength = WaveSpeedC (px per step) / cycles per step. Falls back
		/// to a scale of 2.0 and c = 0.563 before the simulation settings exist.
		/// </summary>
		private void UpdateLambdaLabel()
		{
			if (this.lblLambdaIs == null)
			{
				return;
			}
			int frequency = Convert.ToInt32(this.numFrequency.Value);
			double frequencyScale = ((this._simulation != null) ? this._simulation.SourceFrequencyScale : 2.0);
			double angularFrequency = frequency / 255.0 * 0.5 * frequencyScale;
			double cyclesPerStep = angularFrequency / (2.0 * Math.PI);
			if (cyclesPerStep <= 0.0)
			{
				this.lblLambdaIs.Text = "λ=  -  px";
			}
			else
			{
				double waveSpeed = ((this._simulation != null) ? this._simulation.WaveSpeedC : 0.563);
				double wavelength = waveSpeed / cyclesPerStep;
				this.lblLambdaIs.Text = "λ=" + wavelength.ToString("0.0", CultureInfo.CurrentCulture) + " px";
			}
		}

		/// <summary>Ticking "detach" enables the detach step box.</summary>
		private void SourceDetach_CheckedChanged(object sender, EventArgs e)
		{
			if (!this._loadingSource)
			{
				this.UpdateSourceControlState();
			}
		}

		/// <summary>Ticking one of the randomise checkboxes enables its amount input.</summary>
		private void SourceRandomizeCheck_CheckedChanged(object sender, EventArgs e)
		{
			if (!this._loadingSource)
			{
				this.UpdateSourceControlState();
			}
		}

		/// <summary>Changing frequency, off-width, array size or spacing recomputes the maxima of the randomise amounts.</summary>
		private void SourceRandomizeLimitInput_ValueChanged(object sender, EventArgs e)
		{
			this.UpdateRandomizeLimits();
		}

		/// <summary>
		/// Limits the randomise amounts so that randomised values stay within their valid
		/// ranges: frequency offset at most the distance to 1 or 255, off-width offset at most
		/// off-width - 1 (keeps the gap at least 1 cycle), position jitter at most half the
		/// smallest spacing along an axis with more than one element (16384 px for a 1x1 array).
		/// Lowering a maximum also clips the current value of that input.
		/// </summary>
		private void UpdateRandomizeLimits()
		{
			int frequency = Convert.ToInt32(this.numFrequency.Value);
			this.numRandomizeFrequency.Maximum = new decimal(Math.Max(0, Math.Min(frequency - 1, 255 - frequency)));
			int offStateCycles = Convert.ToInt32(this.numOffStateCycles.Value);
			this.numRandomizeOffWidth.Maximum = new decimal(Math.Max(0, offStateCycles - 1));
			int arraySizeX = Convert.ToInt32(this.numArraySizeX.Value);
			int arraySizeY = Convert.ToInt32(this.numArraySizeY.Value);
			if (arraySizeX <= 1 && arraySizeY <= 1)
			{
				this.numRandomizePosition.Maximum = 16384m;
			}
			else
			{
				List<int> spacings = new List<int>();
				if (arraySizeX > 1)
				{
					spacings.Add(Convert.ToInt32(this.numSpacingSizeX.Value));
				}
				if (arraySizeY > 1)
				{
					spacings.Add(Convert.ToInt32(this.numSpacingSizeY.Value));
				}
				int minSpacing = spacings[0];
				for (int i = 1; i < spacings.Count; i++)
				{
					if (spacings[i] < minSpacing)
					{
						minSpacing = spacings[i];
					}
				}
				this.numRandomizePosition.Maximum = new decimal(Math.Max(0, minSpacing / 2));
			}
		}

		/// <summary>Leaving an array size input with an even value bumps it to the next odd number (capped at the maximum) and tells the user; the array must have a centre element.</summary>
		private void ArraySize_Validating(object sender, CancelEventArgs e)
		{
			NumericUpDown input = (NumericUpDown)sender;
			int value = Convert.ToInt32(input.Value);
			if (value % 2 != 0)
			{
				return;
			}
			int adjusted = Math.Min(Convert.ToInt32(input.Maximum), value + 1);
			input.Value = new decimal(adjusted);
			MessageBox.Show(this, "Use uneven numbers only - the array size has been increased to " + adjusted.ToString(CultureInfo.InvariantCulture) + ".", "Array size", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}

		/// <summary>Any main tab change cancels a running position pick; arriving on the source tab loads the scene picture if needed and re-syncs the list selection with the editor.</summary>
		private void SourceTabChanged(object sender, EventArgs e)
		{
			this.EndSourcePick();
			if (this.tabMainFunctions.SelectedTab == this.tabSourceList)
			{
				this.AutoLoadSceneImageIfEmpty(this._picSource);
				this.SyncSourceListSelectionToEditor();
			}
		}

		/// <summary>Makes the list selection match <c>_currentSourceIndex</c> without triggering the selection handler, then reloads the editor from <c>_editing</c>.</summary>
		private void SyncSourceListSelectionToEditor()
		{
			this._syncingSourceList = true;
			try
			{
				if (this._currentSourceIndex >= 0 && this._currentSourceIndex < this.lstSources.Items.Count)
				{
					this.lstSources.SelectedIndex = this._currentSourceIndex;
				}
				else
				{
					this.lstSources.SelectedIndex = -1;
				}
			}
			finally
			{
				this._syncingSourceList = false;
			}
			this.ShowSourceInControls(this._editing);
		}

		/// <summary>Toggles position picking on the scene picture; the button reads "Cancel" while picking.</summary>
		private void PickSourcePosition_Click(object sender, EventArgs e)
		{
			if (this._sourcePicking)
			{
				this.EndSourcePick();
			}
			else
			{
				this.StartSourcePick();
			}
		}

		/// <summary>Enters pick mode (cross cursor, "Cancel" button, status hint) after making sure the picture box has an image; shows a message and stays idle when the canvas size is unknown.</summary>
		private void StartSourcePick()
		{
			if (this._picSource.Image == null)
			{
				this.EnsureSourcePicture();
			}
			if (this._picSource.Image == null)
			{
				MessageBox.Show(this, "There is no picture to pick from yet, and the canvas size is not known either\r\n(choose a scene image, or a user defined width and height, on the Configuration tab first).", "Source position", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			this._sourcePicking = true;
			this._picSource.Cursor = Cursors.Cross;
			this.btnPickSourcePosition.Text = "Cancel";
			this.ToolStripStatusLabel1.Text = "Click in the picture for the position of the source (press Cancel to stop picking).";
		}

		/// <summary>Creates a black canvas-sized picture when no scene image is loaded yet; the bitmap is owned by the picture box. No-op when the canvas size is unknown.</summary>
		private void EnsureSourcePicture()
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
			this._picSource.Image = bitmap;
		}

		/// <summary>Leaves pick mode and restores cursor, button text and status bar; no-op when not picking.</summary>
		private void EndSourcePick()
		{
			if (!this._sourcePicking)
			{
				return;
			}
			this._sourcePicking = false;
			this.btnPickSourcePosition.Text = this._pickSourceButtonText;
			if (this._picSource != null)
			{
				this._picSource.Cursor = Cursors.Default;
			}
			this.ToolStripStatusLabel1.Text = "";
		}

		/// <summary>
		/// A left click on the scene picture while picking converts the click to canvas pixels,
		/// writes them into the position inputs and the edited source, ends the pick and, when
		/// an existing list entry is being edited, saves the source immediately. Clicks outside
		/// the image are ignored.
		/// </summary>
		private void SourcePicture_MouseClick(object sender, MouseEventArgs e)
		{
			if (!this._sourcePicking || e.Button != MouseButtons.Left)
			{
				return;
			}
			int canvasX = 0;
			int canvasY = 0;
			if (!this.TryPictureToCanvas(this._picSource, e.Location, ref canvasX, ref canvasY))
			{
				return;
			}
			this.numSourcePositionX.Value = Form1.ClampToRange(this.numSourcePositionX, canvasX);
			this.numSourcePositionY.Value = Form1.ClampToRange(this.numSourcePositionY, canvasY);
			if (!this._loadingSource)
			{
				this._editing.PosX = canvasX;
				this._editing.PosY = canvasY;
				this._editing.HasPosition = true;
				this._picSource.Invalidate();
			}
			this.EndSourcePick();
			if (this._currentSourceIndex >= 0)
			{
				this.CommitSourceEdits();
			}
		}

		/// <summary>Editing a position input moves the edited source (and flags it as positioned) so its marker follows on the scene picture before saving.</summary>
		private void SourcePosition_ValueChanged(object sender, EventArgs e)
		{
			if (this._loadingSource)
			{
				return;
			}
			this._editing.PosX = Convert.ToInt32(this.numSourcePositionX.Value);
			this._editing.PosY = Convert.ToInt32(this.numSourcePositionY.Value);
			this._editing.HasPosition = true;
			if (this._picSource != null)
			{
				this._picSource.Invalidate();
			}
		}

		/// <summary>
		/// Draws a marker for every positioned source over the scene picture: saved sources in
		/// blue, the one being edited (taken from <c>_editing</c>, so unsaved moves show) in
		/// yellow, plus the unsaved new source when nothing is selected. Canvas pixels are scaled
		/// to the zoomed image rectangle of the picture box.
		/// </summary>
		private void SourcePicture_Paint(object sender, PaintEventArgs e)
		{
			if (this._picSource.Image == null)
			{
				return;
			}
			Size canvas = this.EffectiveCanvas();
			if (canvas.Width <= 0)
			{
				canvas = this._picSource.Image.Size;
			}
			RectangleF imageRect = Form1.ZoomedImageRectangle(this._picSource);
			if (imageRect.Width <= 0f)
			{
				return;
			}
			float scaleX = imageRect.Width / canvas.Width;
			float scaleY = imageRect.Height / canvas.Height;
			for (int i = 0; i < this._sources.Count; i++)
			{
				bool isCurrent = i == this._currentSourceIndex;
				SourceSettings source = (isCurrent ? this._editing : this._sources[i]);
				if (source.HasPosition)
				{
					Form1.DrawSourceMarker(e.Graphics, source, imageRect, scaleX, scaleY, isCurrent);
				}
			}
			if (this._currentSourceIndex < 0 && this._editing.HasPosition)
			{
				Form1.DrawSourceMarker(e.Graphics, this._editing, imageRect, scaleX, scaleY, true);
			}
		}

		/// <summary>Draws a circle-and-crosshair marker with the source name at the source position; the current source is drawn larger and in yellow, others in blue.</summary>
		/// <param name="g">Target surface of the picture box's paint event.</param>
		/// <param name="s">Source whose canvas-pixel position and name are drawn.</param>
		/// <param name="area">Rectangle the canvas occupies inside the picture box, in picture-box pixels.</param>
		/// <param name="sx">Picture-box pixels per canvas pixel horizontally.</param>
		/// <param name="sy">Picture-box pixels per canvas pixel vertically.</param>
		/// <param name="isCurrent">True for the source being edited (larger yellow marker).</param>
		private static void DrawSourceMarker(Graphics g, SourceSettings s, RectangleF area, float sx, float sy, bool isCurrent)
		{
			Color color = (isCurrent ? Color.FromArgb(230, 255, 208, 64) : Color.FromArgb(200, 64, 180, 255));
			float radius = (isCurrent ? 5f : 4f);
			float cx = area.X + s.PosX * sx;
			float cy = area.Y + s.PosY * sy;
			using (Pen pen = new Pen(color, isCurrent ? 2f : 1.5f))
			{
				g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2f, radius * 2f);
				g.DrawLine(pen, cx - radius - 3f, cy, cx + radius + 3f, cy);
				g.DrawLine(pen, cx, cy - radius - 3f, cx, cy + radius + 3f);
			}
			using (Font font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
			{
				using (SolidBrush brush = new SolidBrush(color))
				{
					g.DrawString(s.Name, font, brush, cx + radius + 4f, cy - radius - 2f);
				}
			}
		}

		/// <summary>
		/// Draws the generated elements of the source being edited on black, centred in the
		/// preview box. With auto-rescale on, the element bounding box is scaled to 70 % of the
		/// smaller box dimension and each element is a 6 px dot; otherwise elements are drawn
		/// 1:1 as single pixels. Empty until the source has been saved once.
		/// </summary>
		private void picPreviewSource_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.Clear(Color.Black);
			List<SourceElement> elements = ((this._editing != null) ? this._editing.Elements : null);
			if (elements == null || elements.Count == 0)
			{
				return;
			}
			int minX = elements[0].PosX;
			int maxX = elements[0].PosX;
			int minY = elements[0].PosY;
			int maxY = elements[0].PosY;
			foreach (SourceElement element in elements)
			{
				if (element.PosX < minX)
				{
					minX = element.PosX;
				}
				if (element.PosX > maxX)
				{
					maxX = element.PosX;
				}
				if (element.PosY < minY)
				{
					minY = element.PosY;
				}
				if (element.PosY > maxY)
				{
					maxY = element.PosY;
				}
			}
			float width = this.picPreviewSource.ClientSize.Width;
			float height = this.picPreviewSource.ClientSize.Height;
			if (width <= 0f || height <= 0f)
			{
				return;
			}
			float centerX = (minX + maxX) / 2f;
			float centerY = (minY + maxY) / 2f;
			bool autoRescale = this.chkAutoRescaleSourceOn == null || this.chkAutoRescaleSourceOn.Checked;
			float scale;
			if (autoRescale)
			{
				float halfExtent = Math.Max(maxX - minX, maxY - minY) / 2f;
				if (halfExtent <= 0f)
				{
					halfExtent = 1f;
				}
				float targetSize = Math.Min(width, height) * 0.7f;
				scale = targetSize / (halfExtent * 2f);
			}
			else
			{
				scale = 1f;
			}
			using (SolidBrush brush = new SolidBrush(Color.White))
			{
				foreach (SourceElement element in elements)
				{
					float px = width / 2f + (element.PosX - centerX) * scale;
					float py = height / 2f + (element.PosY - centerY) * scale;
					if (autoRescale)
					{
						e.Graphics.FillEllipse(brush, px - 3f, py - 3f, 6f, 6f);
					}
					else
					{
						e.Graphics.FillRectangle(brush, px, py, 1f, 1f);
					}
				}
			}
		}

		/// <summary>Resizing the preview box repaints it so the elements stay centred.</summary>
		private void picPreviewSource_SizeChanged(object sender, EventArgs e)
		{
			this.picPreviewSource.Invalidate();
		}

		/// <summary>Toggling auto-rescale repaints the preview in the other scale mode.</summary>
		private void chkAutoRescaleSourceOn_CheckedChanged(object sender, EventArgs e)
		{
			this.picPreviewSource.Invalidate();
		}
	}
}
