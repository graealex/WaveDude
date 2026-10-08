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
	/// <remarks>
	/// <para>
	/// An object source is a point (or, for a composite source, an X-Y array of points) that drives
	/// the field directly, as opposed to the image-derived sources (the green channel of the scene
	/// image). Unlike an image source it has its own frequency, amplitude and phase, and it can be
	/// continuous or pulsed (on for a number of cycles, then off for a number of cycles). A composite
	/// source is a set of point sources built from the same base ("single source") parameters,
	/// optionally randomized per element.
	/// </para>
	/// <para>
	/// Editing a source is a two step process. First, <c>txtName</c>/<c>cmbMainType</c>/
	/// <c>grpSingleSource</c>/<c>grpComposite</c> describe the source; the position is typed or
	/// picked with the Pick button in the picture (a copy of a Preview tab picture, "Copy Image to
	/// probe and source tab"). A freshly created source has no position yet
	/// (<see cref="SourceSettings.HasPosition"/> is false): the picture shows no marker for it until
	/// one is picked or typed, so an unpositioned source is never mistaken for one at (0, 0) or
	/// wherever the numeric fields default to. Second, "Save source" resolves those parameters (for a
	/// composite source, including a fresh random draw when "Inherit + randomize" is chosen) into a
	/// fixed list of point-source elements (<c>_editing.Elements</c>), then adds the result to
	/// <c>lstSources</c> (a new source) or updates the selected one. Only saved sources are used in a
	/// simulation (when "Use object sources" is ticked in the scene properties): the engine always
	/// runs the already-generated element list, never re-randomizing, so a run is reproducible.
	/// </para>
	/// <para>
	/// Selecting a source in <c>lstSources</c> loads it straight into the editor (there is no separate
	/// Edit step/button). New and Copy load a (blank, respectively copied) source into the editor and
	/// clear the list selection; neither touches <c>lstSources</c> itself until Save is pressed, which
	/// adds or updates the entry.
	/// </para>
	/// </remarks>
	public partial class Form1
	{
		/// <summary>
		/// One-time set-up at form load: remembers the "Pick" button text and the save button
		/// colour, sets the numeric input limits (positions 0..<see cref="SceneSettings.MaxCanvasSize"/> - 1
		/// canvas px, array sizes 1..999 stepping by 2, spacings 0..<see cref="SceneSettings.MaxCanvasSize"/> px),
		/// wires dirty tracking and the scene picture events, switches the list to owner drawing and
		/// shows the initial source.
		/// </summary>
		private void InitializeSourceControls()
		{
			_pickSourceButtonText = btnPickSourcePosition.Text;
			_sourceSaveButtonDefaultBackColor = btnSaveSource.BackColor;
			WireSourceDirtyTracking(grpSourceDetails);
			numSourcePositionX.DecimalPlaces = 0;
			numSourcePositionX.Minimum = 0m;
			numSourcePositionX.Maximum = (decimal)(SceneSettings.MaxCanvasSize - 1);
			numSourcePositionY.DecimalPlaces = 0;
			numSourcePositionY.Minimum = 0m;
			numSourcePositionY.Maximum = (decimal)(SceneSettings.MaxCanvasSize - 1);
			// Minimum (1) and Maximum (999) are both odd, and Increment is 2, so the up/down spinner buttons can only
			// ever land on an odd array size - see ArraySize_Validating for what catches an even value typed in
			// directly (the spinner can't produce one, but the keyboard can).
			numArraySizeX.DecimalPlaces = 0;
			numArraySizeX.Minimum = 1m;
			numArraySizeX.Maximum = 999m;
			numArraySizeX.Increment = 2m;
			numArraySizeY.DecimalPlaces = 0;
			numArraySizeY.Minimum = 1m;
			numArraySizeY.Maximum = 999m;
			numArraySizeY.Increment = 2m;
			numSpacingSizeX.DecimalPlaces = 0;
			numSpacingSizeX.Minimum = 0m;
			numSpacingSizeX.Maximum = (decimal)SceneSettings.MaxCanvasSize;
			numSpacingSizeY.DecimalPlaces = 0;
			numSpacingSizeY.Minimum = 0m;
			numSpacingSizeY.Maximum = (decimal)SceneSettings.MaxCanvasSize;
			numAmplitude.DecimalPlaces = 0;
			numPulseWidthCycles.DecimalPlaces = 0;
			if (numPulseWidthCycles.Minimum < 1m)
			{
				numPulseWidthCycles.Minimum = 1m;
			}
			// Designer limit is too small for long pulses; widen it without touching the Designer.
			if (numPulseWidthCycles.Maximum < 100m)
			{
				numPulseWidthCycles.Maximum = 1000000m;
			}
			numOffStateCycles.DecimalPlaces = 0;
			numRandomizeFrequency.DecimalPlaces = 0;
			numRandomizeOffWidth.DecimalPlaces = 0;
			numRandomizePosition.DecimalPlaces = 0;
			NumRandomizePhase.DecimalPlaces = 0;
			_picSource.Paint += SourcePicture_Paint;
			_picSource.MouseClick += SourcePicture_MouseClick;
			// owner-drawn so a switched-off source (chkSourceOn unchecked) can be shown greyed out in the list -
			// it stays fully selectable, only its text color changes (see lstSources_DrawItem)
			lstSources.DrawMode = DrawMode.OwnerDrawFixed;
			RefreshSourceList();
			ShowSourceInControls(_editing);
			ClearSourceDirty();
			UpdateListButtonState();
		}

		/// <summary>
		/// Replaces the source list with the sources stored in the open project and shows the first
		/// one (if any) selected and loaded into the editor, rather than always starting the editor on
		/// a blank "new source" template: opening a project that already has sources shows one of them,
		/// not a to-be-inserted one. "First" is whichever generate/save order the list is in; there is
		/// no separate "last edited" tracking kept across a save/reload to prefer instead. An empty
		/// project gets an empty editor. No-op without a project.
		/// </summary>
		private void LoadSourcesFromProject()
		{
			if (_project == null)
			{
				return;
			}
			_sources = SourceSettings.ListFromConfig(_project.Config);
			if (_sources.Count > 0)
			{
				_currentSourceIndex = 0;
				_editing = _sources[0].Clone();
			}
			else
			{
				_currentSourceIndex = -1;
				_editing = new SourceSettings();
			}
			RefreshSourceList();
			_syncingSourceList = true;
			try
			{
				lstSources.SelectedIndex = _currentSourceIndex;
			}
			finally
			{
				_syncingSourceList = false;
			}
			ShowSourceInControls(_editing);
			ClearSourceDirty();
			UpdateListButtonState();
		}

		/// <summary>
		/// Empties the source list and the editor for a new project. The editor is only
		/// refreshed once the source picture box exists, i.e. after the form has been built.
		/// </summary>
		private void ResetSources()
		{
			_sources = new List<SourceSettings>();
			_currentSourceIndex = -1;
			_editing = new SourceSettings();
			RefreshSourceList();
			if (_picSource != null)
			{
				ShowSourceInControls(_editing);
				ClearSourceDirty();
			}
			UpdateListButtonState();
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
			if (_scene == null || !_scene.UseObjectSources || _sources.Count == 0)
			{
				return null;
			}
			List<SourceElement> elements = new List<SourceElement>();
			foreach (SourceSettings source in _sources)
			{
				// switched off (chkSourceOn unchecked): keep the source and its parameters, just leave it out of the
				// run entirely, so it neither drives the field nor gets in the way of waves passing through it
				if (!source.SourceOn)
				{
					continue;
				}
				if (source.Elements == null || source.Elements.Count == 0)
				{
					// a source saved before it was ever generated (should not normally happen) - generate it now, once
					source.GenerateElements(_sourceRandom);
				}
				elements.AddRange(source.Elements);
			}
			return elements;
		}

		/// <summary>Rebuilds the list box items from <c>_sources</c>, keeping the current selection index when it is still valid; the selection handler is suppressed meanwhile.</summary>
		private void RefreshSourceList()
		{
			int selectedIndex = lstSources.SelectedIndex;
			_syncingSourceList = true;
			try
			{
				lstSources.Items.Clear();
				foreach (SourceSettings source in _sources)
				{
					lstSources.Items.Add(Form1.SourceListText(source));
				}
				if (selectedIndex >= 0 && selectedIndex < lstSources.Items.Count)
				{
					lstSources.SelectedIndex = selectedIndex;
				}
			}
			finally
			{
				_syncingSourceList = false;
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

		/// <summary>Owner-draws a list entry: a switched-off source (<c>chkSourceOn</c> unchecked) is drawn greyed out; it stays fully selectable, only the text colour changes, so the selection highlight still shows normally.</summary>
		private void lstSources_DrawItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index < 0 || e.Index >= lstSources.Items.Count)
			{
				return;
			}
			e.DrawBackground();
			Color textColor = ((e.Index < _sources.Count && !_sources[e.Index].SourceOn) ? Color.Gray : e.ForeColor);
			using (SolidBrush brush = new SolidBrush(textColor))
			{
				e.Graphics.DrawString(lstSources.Items[e.Index].ToString(), e.Font, brush, e.Bounds);
			}
			e.DrawFocusRectangle();
		}

		/// <summary>
		/// Selecting a list entry cancels any position pick and loads a clone of that source straight
		/// into the editor (unsaved edits of the previous source are discarded); clearing the selection
		/// (New/Copy, or the list becoming empty) loads a blank/default source instead. Programmatic
		/// selection changes (<see cref="RefreshSourceList"/>, <see cref="CommitSourceEdits"/>) set
		/// <c>_syncingSourceList</c> first so they do not re-trigger this.
		/// </summary>
		private void lstSources_SelectedIndexChanged(object sender, EventArgs e)
		{
			UpdateListButtonState();
			if (_syncingSourceList)
			{
				return;
			}
			EndSourcePick();
			if (lstSources.SelectedIndex < 0)
			{
				_currentSourceIndex = -1;
				_editing = new SourceSettings();
			}
			else
			{
				_currentSourceIndex = lstSources.SelectedIndex;
				_editing = _sources[_currentSourceIndex].Clone();
			}
			ShowSourceInControls(_editing);
			ClearSourceDirty();
			if (_picSource != null)
			{
				_picSource.Invalidate();
			}
			picPreviewSource.Invalidate();
			ToolStripStatusLabel1.Text = ((_currentSourceIndex >= 0) ? ("Editing '" + _editing.Name + "': press Save source to keep the changes.") : "");
		}

		/// <summary>Enables Copy and Delete only while a list entry is selected.</summary>
		private void UpdateListButtonState()
		{
			bool hasSelection = lstSources.SelectedIndex >= 0;
			btnCopy.Enabled = hasSelection;
			btnDelete.Enabled = hasSelection;
		}

		/// <summary>Starts a new, unsaved source named "Source N" in the editor, deselects the list and focuses the name box.</summary>
		private void btnNew_Click(object sender, EventArgs e)
		{
			EndSourcePick();
			_syncingSourceList = true;
			try
			{
				lstSources.SelectedIndex = -1;
			}
			finally
			{
				_syncingSourceList = false;
			}
			_currentSourceIndex = -1;
			_editing = new SourceSettings();
			_editing.Name = "Source " + (_sources.Count + 1).ToString(CultureInfo.InvariantCulture);
			ShowSourceInControls(_editing);
			MarkSourceDirty(); // not saved yet - pressing Save source is what actually adds it
			UpdateListButtonState();
			if (_picSource != null)
			{
				_picSource.Invalidate();
			}
			picPreviewSource.Invalidate();
			ToolStripStatusLabel1.Text = "New source: fill in its properties, then press Save source.";
			txtName.Focus();
			txtName.SelectAll();
		}

		/// <summary>Loads a clone of the selected source, named "... copy", into the editor as a new unsaved source (it is only added to the list on save).</summary>
		private void btnCopy_Click(object sender, EventArgs e)
		{
			if (lstSources.SelectedIndex < 0)
			{
				return;
			}
			EndSourcePick();
			string originalName = _sources[lstSources.SelectedIndex].Name;
			_editing = _sources[lstSources.SelectedIndex].Clone();
			_editing.Name = _editing.Name + " copy";
			_syncingSourceList = true;
			try
			{
				lstSources.SelectedIndex = -1;
			}
			finally
			{
				_syncingSourceList = false;
			}
			_currentSourceIndex = -1;
			ShowSourceInControls(_editing);
			MarkSourceDirty(); // not saved yet - pressing Save source is what actually adds it
			UpdateListButtonState();
			if (_picSource != null)
			{
				_picSource.Invalidate();
			}
			picPreviewSource.Invalidate();
			ToolStripStatusLabel1.Text = "Copy of '" + originalName + "': change it and press Save source to add it as a new source.";
			txtName.Focus();
			txtName.SelectAll();
		}

		/// <summary>After confirmation, removes the selected source from the list and marks the project dirty; clears the editor if that source was being edited.</summary>
		private void btnDelete_Click(object sender, EventArgs e)
		{
			if (lstSources.SelectedIndex < 0)
			{
				return;
			}
			int selectedIndex = lstSources.SelectedIndex;
			string name = _sources[selectedIndex].Name;
			if (MessageBox.Show(this, "Delete the source '" + name + "'?", "Delete source", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
			{
				return;
			}
			_sources.RemoveAt(selectedIndex);
			if (_currentSourceIndex == selectedIndex)
			{
				_currentSourceIndex = -1;
				_editing = new SourceSettings();
				ShowSourceInControls(_editing);
				ClearSourceDirty();
			}
			else if (_currentSourceIndex > selectedIndex)
			{
				_currentSourceIndex--;
			}
			RefreshSourceList();
			UpdateListButtonState();
			MarkProjectChanged();
			if (_picSource != null)
			{
				_picSource.Invalidate();
			}
			picPreviewSource.Invalidate();
			ToolStripStatusLabel1.Text = "Source '" + name + "' deleted.";
		}

		/// <summary>Saves the editor contents into the list (see <see cref="CommitSourceEdits"/>).</summary>
		private void btnSaveSource_Click(object sender, EventArgs e)
		{
			CommitSourceEdits();
		}

		/// <summary>
		/// Reads the editor controls into the source being edited, regenerates its elements
		/// (re-rolling any randomisation) and stores it in the list, replacing the selected entry
		/// or appending a new one, then clears the "unsaved changes" indicator on <c>btnSaveSource</c>.
		/// Shared by "Save source" itself and by completing a position pick for an already-selected
		/// source (see <see cref="SourcePicture_MouseClick"/>), so picking a new position for an
		/// existing source is never left stale until a separate press of Save source. Marks the
		/// project dirty and leaves the editor on a fresh clone of the saved source. Returns false,
		/// without committing anything, if the source has no name yet, the same as "Save source"
		/// always required.
		/// </summary>
		private bool CommitSourceEdits()
		{
			ReadSourceControls(_editing);
			if (_editing.Name.Trim().Length == 0)
			{
				MessageBox.Show(this, "Give the source a name first.", "Save source", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				txtName.Focus();
				return false;
			}
			// always (re)generate the elements from the current editor values, so a changed position (or any other
			// parameter) is never left stale in a source that already had elements from an earlier generate/save
			_editing.GenerateElements(_sourceRandom);
			if (_currentSourceIndex >= 0 && _currentSourceIndex < _sources.Count)
			{
				_sources[_currentSourceIndex] = _editing;
			}
			else
			{
				_sources.Add(_editing);
				_currentSourceIndex = _sources.Count - 1;
			}
			RefreshSourceList();
			_syncingSourceList = true;
			try
			{
				lstSources.SelectedIndex = _currentSourceIndex;
			}
			finally
			{
				_syncingSourceList = false;
			}
			UpdateListButtonState();
			MarkProjectChanged();
			if (_picSource != null)
			{
				_picSource.Invalidate();
			}
			picPreviewSource.Invalidate();
			ToolStripStatusLabel1.Text = "Source '" + _editing.Name + "' saved (" + _editing.Elements.Count.ToString(CultureInfo.InvariantCulture) + " element(s)).";
			// the editor keeps editing the source that was just saved, so pressing Save again updates it in place
			_editing = _editing.Clone();
			ClearSourceDirty();
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
			_loadingSource = true;
			try
			{
				txtName.Text = s.Name;
				cmbMainType.SelectedIndex = (int)s.MainType;
				chkSourceOn.Checked = s.SourceOn;
				chkDetachSource.Checked = s.DetachSource;
				txtDetachAfterSteps.Text = s.DetachAfterSteps.ToString(CultureInfo.InvariantCulture);
				numSourcePositionX.Value = Form1.ClampToRange(numSourcePositionX, s.PosX);
				numSourcePositionY.Value = Form1.ClampToRange(numSourcePositionY, s.PosY);
				numFrequency.Value = Form1.ClampToRange(numFrequency, s.Frequency);
				numAmplitude.Value = Form1.ClampToRange(numAmplitude, s.Amplitude);
				numPhase.Value = Form1.ClampToRange(numPhase, s.PhaseDeg);
				radModePulsed.Checked = s.Mode == SourceMode.Pulsed;
				radModeContinuous.Checked = s.Mode == SourceMode.Continuous;
				cmbPulseShape.SelectedIndex = (int)s.PulseShape;
				numPulseWidthCycles.Value = Form1.ClampToRange(numPulseWidthCycles, s.PulseWidthCycles);
				numOffStateCycles.Value = Form1.ClampToRange(numOffStateCycles, s.OffStateCycles);
				numArraySizeX.Value = Form1.ClampToRange(numArraySizeX, s.ArraySizeX);
				numArraySizeY.Value = Form1.ClampToRange(numArraySizeY, s.ArraySizeY);
				numSpacingSizeX.Value = Form1.ClampToRange(numSpacingSizeX, s.SpacingX);
				numSpacingSizeY.Value = Form1.ClampToRange(numSpacingSizeY, s.SpacingY);
				// the three "amount" fields below are clamped against the just-loaded values above (see
				// UpdateRandomizeLimits) - called explicitly here rather than relying only on the ValueChanged handler
				// further down, since a value that happens to equal what a control already shows does not raise that event
				UpdateRandomizeLimits();
				cmbCompositeProperties.SelectedIndex = (int)s.CompositeMode;
				chkRandomizeFrequency.Checked = s.RandomizeFrequency;
				numRandomizeFrequency.Value = Form1.ClampToRange(numRandomizeFrequency, s.RandomizeFrequencyAmount);
				chkRandomizePhase.Checked = s.RandomizePhase;
				NumRandomizePhase.Value = Form1.ClampToRange(NumRandomizePhase, s.RandomizePhaseAmount);
				chkOffWidth.Checked = s.RandomizeOffWidth;
				numRandomizeOffWidth.Value = Form1.ClampToRange(numRandomizeOffWidth, s.RandomizeOffWidthAmount);
				chkRandomizePosition.Checked = s.RandomizePosition;
				numRandomizePosition.Value = Form1.ClampToRange(numRandomizePosition, s.RandomizePositionAmount);
			}
			finally
			{
				_loadingSource = false;
			}
			UpdateSourceControlState();
			UpdateLambdaLabel();
		}

		/// <summary>
		/// Hooks the change events of every input control below <paramref name="container"/> to
		/// <see cref="MarkSourceDirty"/>, recursively since <c>grpSourceDetails</c> nests
		/// <c>grpSingleSource</c> and <c>grpComposite</c>, so any change the user makes to a source's
		/// properties there turns <c>btnSaveSource</c> orange until the change is saved (see
		/// <see cref="MarkSourceDirty"/>/<see cref="ClearSourceDirty"/>). Called once, from
		/// <see cref="InitializeSourceControls"/>. The source's position (<c>numSourcePositionX</c>/<c>Y</c>,
		/// in <c>grpPlaceSource</c>, not <c>grpSourceDetails</c>) is deliberately not included here:
		/// picking or typing a position is committed through its own path instead
		/// (<see cref="SourcePicture_MouseClick"/>/<see cref="SourcePosition_ValueChanged"/>).
		/// </summary>
		private void WireSourceDirtyTracking(Control container)
		{
			foreach (Control control in container.Controls)
			{
				if (control is TextBox textBox)
				{
					textBox.TextChanged += SourceDetail_Changed;
				}
				else if (control is NumericUpDown numericUpDown)
				{
					numericUpDown.ValueChanged += SourceDetail_Changed;
				}
				else if (control is CheckBox checkBox)
				{
					checkBox.CheckedChanged += SourceDetail_Changed;
				}
				else if (control is RadioButton radioButton)
				{
					radioButton.CheckedChanged += SourceDetail_Changed;
				}
				else if (control is ComboBox comboBox)
				{
					comboBox.SelectedIndexChanged += SourceDetail_Changed;
				}
				if (control.HasChildren)
				{
					WireSourceDirtyTracking(control);
				}
			}
		}

		/// <summary>Common handler for every control wired by <see cref="WireSourceDirtyTracking"/>. Ignored while the code itself is filling the controls (<see cref="ShowSourceInControls"/> sets <c>_loadingSource</c> first), so only an actual user edit counts.</summary>
		private void SourceDetail_Changed(object sender, EventArgs e)
		{
			if (!_loadingSource)
			{
				MarkSourceDirty();
			}
		}

		/// <summary>Signals unsaved edits by colouring the save button orange.</summary>
		private void MarkSourceDirty()
		{
			btnSaveSource.BackColor = Color.Orange;
		}

		/// <summary>Restores <c>btnSaveSource</c> to its own Designer colour (captured in <c>_sourceSaveButtonDefaultBackColor</c>), once its pending changes are saved or discarded by loading a different/blank source into the editor.</summary>
		private void ClearSourceDirty()
		{
			btnSaveSource.BackColor = _sourceSaveButtonDefaultBackColor;
		}

		/// <summary>
		/// Copies the editor controls into <paramref name="target"/> and normalises it. The
		/// detach step count is parsed with the invariant culture and left unchanged when the
		/// text is not a non-negative integer. Does not touch <c>HasPosition</c> or the elements.
		/// </summary>
		private void ReadSourceControls(SourceSettings target)
		{
			target.Name = txtName.Text.Trim();
			target.MainType = ((cmbMainType.SelectedIndex == 1) ? SourceMainType.Composite : SourceMainType.Simple);
			target.SourceOn = chkSourceOn.Checked;
			target.DetachSource = chkDetachSource.Checked;
			int detachAfterSteps;
			if (int.TryParse(txtDetachAfterSteps.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out detachAfterSteps) && detachAfterSteps >= 0)
			{
				target.DetachAfterSteps = detachAfterSteps;
			}
			target.PosX = Convert.ToInt32(numSourcePositionX.Value);
			target.PosY = Convert.ToInt32(numSourcePositionY.Value);
			target.Frequency = Convert.ToInt32(numFrequency.Value);
			target.Amplitude = Convert.ToInt32(numAmplitude.Value);
			target.PhaseDeg = Convert.ToInt32(numPhase.Value);
			target.Mode = (radModePulsed.Checked ? SourceMode.Pulsed : SourceMode.Continuous);
			target.PulseShape = ((cmbPulseShape.SelectedIndex == 1) ? SourcePulseShape.SmoothedSquare : SourcePulseShape.Gaussian);
			target.PulseWidthCycles = Convert.ToInt32(numPulseWidthCycles.Value);
			target.OffStateCycles = Convert.ToInt32(numOffStateCycles.Value);
			target.ArraySizeX = Convert.ToInt32(numArraySizeX.Value);
			target.ArraySizeY = Convert.ToInt32(numArraySizeY.Value);
			target.SpacingX = Convert.ToInt32(numSpacingSizeX.Value);
			target.SpacingY = Convert.ToInt32(numSpacingSizeY.Value);
			target.CompositeMode = ((cmbCompositeProperties.SelectedIndex == 1) ? SourceCompositeMode.InheritAndRandomize : SourceCompositeMode.InheritFromSingle);
			target.RandomizeFrequency = chkRandomizeFrequency.Checked;
			target.RandomizeFrequencyAmount = Convert.ToInt32(numRandomizeFrequency.Value);
			target.RandomizePhase = chkRandomizePhase.Checked;
			target.RandomizePhaseAmount = Convert.ToInt32(NumRandomizePhase.Value);
			target.RandomizeOffWidth = chkOffWidth.Checked;
			target.RandomizeOffWidthAmount = Convert.ToInt32(numRandomizeOffWidth.Value);
			target.RandomizePosition = chkRandomizePosition.Checked;
			target.RandomizePositionAmount = Convert.ToInt32(numRandomizePosition.Value);
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
			bool isComposite = cmbMainType.SelectedIndex == 1;
			grpComposite.Enabled = isComposite;
			bool isPulsed = radModePulsed.Checked;
			cmbPulseShape.Enabled = isPulsed;
			numPulseWidthCycles.Enabled = isPulsed;
			numOffStateCycles.Enabled = isPulsed;
			txtDetachAfterSteps.Enabled = chkDetachSource.Checked;
			bool canRandomize = isComposite && cmbCompositeProperties.SelectedIndex == 1;
			cmbCompositeProperties.Enabled = isComposite;
			numArraySizeX.Enabled = isComposite;
			numArraySizeY.Enabled = isComposite;
			numSpacingSizeX.Enabled = isComposite;
			numSpacingSizeY.Enabled = isComposite;
			chkRandomizeFrequency.Enabled = canRandomize;
			chkRandomizePhase.Enabled = canRandomize;
			chkOffWidth.Enabled = canRandomize;
			chkRandomizePosition.Enabled = canRandomize;
			numRandomizeFrequency.Enabled = canRandomize && chkRandomizeFrequency.Checked;
			NumRandomizePhase.Enabled = canRandomize && chkRandomizePhase.Checked;
			numRandomizeOffWidth.Enabled = canRandomize && chkOffWidth.Checked;
			numRandomizePosition.Enabled = canRandomize && chkRandomizePosition.Checked;
		}

		/// <summary>Changing the main type or the composite mode combo box re-evaluates which inputs are enabled.</summary>
		private void SourceMainType_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (!_loadingSource)
			{
				UpdateSourceControlState();
			}
		}

		/// <summary>Switching between continuous and pulsed enables or disables the pulse inputs.</summary>
		private void SourceMode_CheckedChanged(object sender, EventArgs e)
		{
			if (!_loadingSource)
			{
				UpdateSourceControlState();
			}
		}

		/// <summary>Keeps <c>lblLambdaIs</c> in sync with <c>numFrequency</c>. Not guarded by <c>_loadingSource</c>, since it must also refresh when a source is loaded into the editor (<see cref="ShowSourceInControls"/>), not only on a user edit.</summary>
		private void SourceFrequency_ValueChanged(object sender, EventArgs e)
		{
			UpdateLambdaLabel();
		}

		/// <summary>
		/// Shows an estimated wavelength for the current <c>numFrequency</c> value in <c>lblLambdaIs</c>,
		/// as "λ=xx,x px" (using the current culture's decimal separator). lambda = c / f: f is the
		/// cyclic frequency (cycles/step) that a source's level (1-255, the <c>numFrequency</c> control)
		/// maps to via <see cref="SimulationSettings.SourceFrequencyScale"/>, the same formula the engines
		/// use (level / 255 * 0.5 * SourceFrequencyScale is the <em>angular</em> frequency in radians per
		/// step, so dividing by 2π gives cycles/step), and c is the propagation speed set via File &gt;
		/// Set c-value (<see cref="SimulationSettings.WaveSpeedC"/>), in canvas pixels per step. Falls
		/// back to a scale of 2.0 and c = 0.563 before the simulation settings exist. This is only ever
		/// an estimate/display value: the real per-pixel wave speed the simulation actually uses comes
		/// from the scene's refractive index instead (see <see cref="SceneData.C2"/>) and is not
		/// necessarily uniform.
		/// </summary>
		private void UpdateLambdaLabel()
		{
			if (lblLambdaIs == null)
			{
				return;
			}
			int frequency = Convert.ToInt32(numFrequency.Value);
			double frequencyScale = ((_simulation != null) ? _simulation.SourceFrequencyScale : 2.0);
			double angularFrequency = frequency / 255.0 * 0.5 * frequencyScale;
			double cyclesPerStep = angularFrequency / (2.0 * Math.PI);
			if (cyclesPerStep <= 0.0)
			{
				lblLambdaIs.Text = "λ=  -  px";
			}
			else
			{
				double waveSpeed = ((_simulation != null) ? _simulation.WaveSpeedC : 0.563);
				double wavelength = waveSpeed / cyclesPerStep;
				// the current culture's own decimal separator (a comma in nl-NL, for instance), not a fixed "."
				lblLambdaIs.Text = "λ=" + wavelength.ToString("0.0", CultureInfo.CurrentCulture) + " px";
			}
		}

		/// <summary>Ticking "detach" enables the detach step box.</summary>
		private void SourceDetach_CheckedChanged(object sender, EventArgs e)
		{
			if (!_loadingSource)
			{
				UpdateSourceControlState();
			}
		}

		/// <summary>Ticking one of the randomise checkboxes enables its amount input.</summary>
		private void SourceRandomizeCheck_CheckedChanged(object sender, EventArgs e)
		{
			if (!_loadingSource)
			{
				UpdateSourceControlState();
			}
		}

		/// <summary>
		/// Keeps the three randomise "amount" fields' Maximum in step with the single-source values they
		/// are added to (see <see cref="UpdateRandomizeLimits"/>) whenever frequency, off-width, array
		/// size or spacing changes. Not guarded by <c>_loadingSource</c>, since <see cref="ShowSourceInControls"/>
		/// also relies on this to be current before it sets the amount fields' Value (though it
		/// additionally calls <see cref="UpdateRandomizeLimits"/> directly itself, since a value that
		/// happens to already match a control's current Value does not raise ValueChanged).
		/// </summary>
		private void SourceRandomizeLimitInput_ValueChanged(object sender, EventArgs e)
		{
			UpdateRandomizeLimits();
		}

		/// <summary>
		/// Caps how large a "randomize amount" can be set to, so <see cref="SourceSettings.GenerateElements"/>'
		/// inherited ± amount can never be forced to clip against a hard limit and so end up biased
		/// instead of a true symmetric spread:
		/// <list type="bullet">
		/// <item><description>frequency (<c>numRandomizeFrequency</c>): the source's level must stay within
		/// 1-255, so the amount cannot exceed the smaller of (Frequency - 1) and (255 - Frequency).</description></item>
		/// <item><description>off-width cycles (<c>numRandomizeOffWidth</c>): GenerateElements floors the result
		/// at 1 cycle, so the amount cannot exceed (OffStateCycles - 1).</description></item>
		/// <item><description>position (<c>numRandomizePosition</c>): position has no natural upper/lower bound
		/// the way frequency and off-width do, so instead the amount is capped at half the array's own
		/// spacing along whichever axis (or axes) actually separate elements (ArraySizeX/Y &gt; 1); a
		/// randomized element's jitter can then never overshoot as far as a neighbouring (unrandomized)
		/// grid position. An array that is only one element wide in both directions (no neighbour to
		/// protect against) is left unbounded at <see cref="SceneSettings.MaxCanvasSize"/>.</description></item>
		/// </list>
		/// Phase (<c>NumRandomizePhase</c>) is not limited here: its result is already clamped to -180..180
		/// in GenerateElements, and since ±180° is the same angle either way, that clipping does not
		/// bias the result the way it would for frequency or off-width. Lowering a Maximum below a
		/// field's current Value clamps that Value down to it automatically (standard NumericUpDown
		/// behaviour), so the amount fields never need to be set here, only their Maximum.
		/// </summary>
		private void UpdateRandomizeLimits()
		{
			int frequency = Convert.ToInt32(numFrequency.Value);
			numRandomizeFrequency.Maximum = new decimal(Math.Max(0, Math.Min(frequency - 1, 255 - frequency)));
			int offStateCycles = Convert.ToInt32(numOffStateCycles.Value);
			numRandomizeOffWidth.Maximum = new decimal(Math.Max(0, offStateCycles - 1));
			int arraySizeX = Convert.ToInt32(numArraySizeX.Value);
			int arraySizeY = Convert.ToInt32(numArraySizeY.Value);
			if (arraySizeX <= 1 && arraySizeY <= 1)
			{
				numRandomizePosition.Maximum = (decimal)SceneSettings.MaxCanvasSize;
			}
			else
			{
				// the smallest spacing among the axes that actually separate elements (Count > 1), without relying on
				// System.Linq (not imported by this file) for a two/one-element Min()
				List<int> relevantSpacing = new List<int>();
				if (arraySizeX > 1)
				{
					relevantSpacing.Add(Convert.ToInt32(numSpacingSizeX.Value));
				}
				if (arraySizeY > 1)
				{
					relevantSpacing.Add(Convert.ToInt32(numSpacingSizeY.Value));
				}
				int minSpacing = relevantSpacing[0];
				for (int i = 1; i < relevantSpacing.Count; i++)
				{
					if (relevantSpacing[i] < minSpacing)
					{
						minSpacing = relevantSpacing[i];
					}
				}
				numRandomizePosition.Maximum = new decimal(Math.Max(0, minSpacing / 2));
			}
		}

		/// <summary>
		/// <c>numArraySizeX</c>/<c>numArraySizeY</c> must stay odd: an even-sized array has its centre
		/// between two pixels instead of on one, which (especially at small spacings) places the whole
		/// array off-grid in an irregular way. The spinner buttons can never produce an even value
		/// (Minimum/Maximum are both odd and Increment is 2, see <see cref="InitializeSourceControls"/>),
		/// but typing a value directly can, so this catches that on Validating (the point where a typed
		/// value is finalized: on Tab/click-away, or on Enter via the global <c>NumericUpDown_KeyDown</c>
		/// hook that moves focus off the control) and bumps it up by one (capped at the maximum), with a
		/// warning so the change is not a silent surprise. Not guarded by <c>_loadingSource</c>:
		/// Validating only fires from an actual focus change, never from a plain Value assignment in
		/// code (e.g. <see cref="ShowSourceInControls"/> loading a project), so it can only ever be the
		/// user's own keyboard entry.
		/// </summary>
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
			EndSourcePick();
			if (tabMainFunctions.SelectedTab == tabSourceList)
			{
				AutoLoadSceneImageIfEmpty(_picSource);
				SyncSourceListSelectionToEditor();
			}
		}

		/// <summary>
		/// Makes <c>lstSources</c>' highlighted item and <c>grpSourceDetails</c> agree with the source
		/// actually loaded in the editor (<c>_editing</c>/<c>_currentSourceIndex</c>, the last source
		/// that was selected or edited) whenever the tab is opened, without triggering the selection
		/// handler. Without this, coming back from another tab could show some list item highlighted
		/// (left over from before, or the list's own default) while the detail controls still showed a
		/// different source, or one that no longer matched the highlighted entry at all.
		/// </summary>
		private void SyncSourceListSelectionToEditor()
		{
			_syncingSourceList = true;
			try
			{
				if (_currentSourceIndex >= 0 && _currentSourceIndex < lstSources.Items.Count)
				{
					lstSources.SelectedIndex = _currentSourceIndex;
				}
				else
				{
					lstSources.SelectedIndex = -1;
				}
			}
			finally
			{
				_syncingSourceList = false;
			}
			ShowSourceInControls(_editing);
		}

		/// <summary>Toggles position picking on the scene picture; the button reads "Cancel" while picking.</summary>
		private void PickSourcePosition_Click(object sender, EventArgs e)
		{
			if (_sourcePicking)
			{
				EndSourcePick();
			}
			else
			{
				StartSourcePick();
			}
		}

		/// <summary>Enters pick mode (cross cursor, "Cancel" button, status hint) after making sure the picture box has an image; shows a message and stays idle when the canvas size is unknown.</summary>
		private void StartSourcePick()
		{
			if (_picSource.Image == null)
			{
				EnsureSourcePicture();
			}
			if (_picSource.Image == null)
			{
				MessageBox.Show(this, "There is no picture to pick from yet, and the canvas size is not known either\r\n(choose a scene image, or a user defined width and height, on the Configuration tab first).", "Source position", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			_sourcePicking = true;
			_picSource.Cursor = Cursors.Cross;
			btnPickSourcePosition.Text = "Cancel";
			ToolStripStatusLabel1.Text = "Click in the picture for the position of the source (press Cancel to stop picking).";
		}

		/// <summary>
		/// When there is no picture yet to pick from (no simulation has run), makes a plain black one at
		/// the canvas size that the Configuration tab currently specifies (the scene image's size, or the
		/// user defined width/height), so a source position can still be picked before ever running a
		/// simulation. Replaced by the real picture as soon as one is copied over from the Preview tab.
		/// No-op when the canvas size is unknown. The bitmap is owned by the picture box.
		/// </summary>
		private void EnsureSourcePicture()
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
			_picSource.Image = placeholder;
		}

		/// <summary>Leaves pick mode and restores cursor, button text and status bar; no-op when not picking.</summary>
		private void EndSourcePick()
		{
			if (!_sourcePicking)
			{
				return;
			}
			_sourcePicking = false;
			btnPickSourcePosition.Text = _pickSourceButtonText;
			if (_picSource != null)
			{
				_picSource.Cursor = Cursors.Default;
			}
			ToolStripStatusLabel1.Text = "";
		}

		/// <summary>
		/// A left click on the scene picture while picking converts the click to canvas pixels,
		/// writes them into the position inputs and the edited source, ends the pick and, when
		/// an existing list entry is being edited, saves the source immediately. Clicks outside
		/// the image are ignored.
		/// </summary>
		private void SourcePicture_MouseClick(object sender, MouseEventArgs e)
		{
			if (!_sourcePicking || e.Button != MouseButtons.Left)
			{
				return;
			}
			int canvasX = 0;
			int canvasY = 0;
			if (!TryPictureToCanvas(_picSource, e.Location, ref canvasX, ref canvasY))
			{
				return;
			}
			numSourcePositionX.Value = Form1.ClampToRange(numSourcePositionX, canvasX);
			numSourcePositionY.Value = Form1.ClampToRange(numSourcePositionY, canvasY);
			// SourcePosition_ValueChanged updates _editing and invalidates the picture, but not when the values above
			// did not actually change (e.g. clicking back on the same pixel), so do it here too, explicitly
			if (!_loadingSource)
			{
				_editing.PosX = canvasX;
				_editing.PosY = canvasY;
				_editing.HasPosition = true;
				_picSource.Invalidate();
			}
			EndSourcePick();
			// picking a position for a source that is already in the list commits the editor's current data (the
			// picked position and anything else changed since the last save) straight to it, the same as pressing Save
			// source - so the pick is never left stale waiting for a separate save. A brand new/copied source (not
			// selected in the list yet) is left as before: still just a live, unsaved position until Save source is
			// pressed, since adding it here could otherwise silently add an incomplete/unnamed source to the list.
			if (_currentSourceIndex >= 0)
			{
				CommitSourceEdits();
			}
		}

		/// <summary>Keeps the live marker in the picture in sync while typing a new position, same as picking one does: moves the edited source (and flags it as positioned) so its marker follows on the scene picture before saving.</summary>
		private void SourcePosition_ValueChanged(object sender, EventArgs e)
		{
			if (_loadingSource)
			{
				return;
			}
			_editing.PosX = Convert.ToInt32(numSourcePositionX.Value);
			_editing.PosY = Convert.ToInt32(numSourcePositionY.Value);
			_editing.HasPosition = true;
			if (_picSource != null)
			{
				_picSource.Invalidate();
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
			if (_picSource.Image == null)
			{
				return;
			}
			Size canvas = EffectiveCanvas();
			if (canvas.Width <= 0)
			{
				canvas = _picSource.Image.Size;
			}
			RectangleF imageRect = Form1.ZoomedImageRectangle(_picSource);
			if (imageRect.Width <= 0f)
			{
				return;
			}
			float scaleX = imageRect.Width / canvas.Width;
			float scaleY = imageRect.Height / canvas.Height;
			for (int i = 0; i < _sources.Count; i++)
			{
				bool isCurrent = i == _currentSourceIndex;
				// the source being edited is shown at its live (not yet saved) position, so moving it in the picture or
				// the X/Y boxes is reflected immediately instead of only after Save source
				SourceSettings shown = (isCurrent ? _editing : _sources[i]);
				if (shown.HasPosition)
				{
					Form1.DrawSourceMarker(e.Graphics, shown, imageRect, scaleX, scaleY, isCurrent);
				}
			}
			// the source being created or copied (not in _sources yet) is shown too, so its position can be seen while
			// picking - but only once it actually has one (Pick or a typed X/Y), never at the unset default
			if (_currentSourceIndex < 0 && _editing.HasPosition)
			{
				Form1.DrawSourceMarker(e.Graphics, _editing, imageRect, scaleX, scaleY, true);
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
		/// Shows the layout of the source currently loaded in the editor (<c>_editing</c>) as white dots
		/// on a black background, centred in the preview box. The source picture above is at canvas
		/// scale, so a source (especially a composite source's individual elements) can be too small or
		/// too close together to make out there. <c>chkAutoRescaleSourceOn</c> (checked by default) picks
		/// which of two views this shows instead: checked, it zooms in on just the elements themselves
		/// (centred, the element bounding box scaled to fill the box minus a <c>marginFraction</c> margin
		/// on each side, each element shown as a dot of radius <c>dotRadius</c> so it stays visible), so
		/// their relative arrangement is always visible regardless of where the source sits on the canvas
		/// or how tightly its array is spaced; unchecked, the true, unscaled size (one simulation pixel is
		/// one screen pixel, and each element is drawn as exactly that: a single pixel, the same size it
		/// actually is in the simulation), so a composite source's real physical footprint (how many
		/// actual simulation pixels its array spans) can be judged directly. Either way it shows
		/// <c>_editing.Elements</c>, the last generated list, so it is blank for a brand new/copied source
		/// until it is generated by Save source, and otherwise reflects whatever was last saved (or, for
		/// the source just saved, just regenerated). Kept in sync by a <c>picPreviewSource.Invalidate()</c>
		/// call everywhere <c>_editing</c>/the currently edited source changes (loading a different one,
		/// New/Copy/Delete, and Save source), the same places <c>_picSource.Invalidate()</c> is called for
		/// the position marker above, and by <see cref="chkAutoRescaleSourceOn_CheckedChanged"/> when the
		/// view itself is switched.
		/// </summary>
		private void picPreviewSource_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.Clear(Color.Black);
			List<SourceElement> elements = ((_editing != null) ? _editing.Elements : null);
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
			float boxW = picPreviewSource.ClientSize.Width;
			float boxH = picPreviewSource.ClientSize.Height;
			if (boxW <= 0f || boxH <= 0f)
			{
				return;
			}
			float centerX = (minX + maxX) / 2f;
			float centerY = (minY + maxY) / 2f;
			bool autoRescale = chkAutoRescaleSourceOn == null || chkAutoRescaleSourceOn.Checked;
			float scale;
			if (autoRescale)
			{
				// a single element (or several coinciding at the same spot) has no span to scale to - fall back to a
				// fixed half-extent instead of dividing by zero, so it still just shows one dot in the middle of the box
				float halfExtent = Math.Max(maxX - minX, maxY - minY) / 2f;
				if (halfExtent <= 0f)
				{
					halfExtent = 1f;
				}
				const float marginFraction = 0.15f; // keeps outer dots from touching/clipping at the edge of the box
				float usable = Math.Min(boxW, boxH) * (1f - 2f * marginFraction);
				scale = usable / (halfExtent * 2f);
			}
			else
			{
				scale = 1f; // true size: one simulation pixel is one screen pixel
			}
			const float dotRadius = 3f;
			using (SolidBrush brush = new SolidBrush(Color.White))
			{
				foreach (SourceElement element in elements)
				{
					float px = boxW / 2f + (element.PosX - centerX) * scale;
					float py = boxH / 2f + (element.PosY - centerY) * scale;
					if (autoRescale)
					{
						e.Graphics.FillEllipse(brush, px - dotRadius, py - dotRadius, 2f * dotRadius, 2f * dotRadius);
					}
					else
					{
						e.Graphics.FillRectangle(brush, px, py, 1f, 1f);
					}
				}
			}
		}

		/// <summary>Redraws at the (centred, rescaled) size whenever the box itself is resized, the same as any other owner-drawn control that scales to its client area.</summary>
		private void picPreviewSource_SizeChanged(object sender, EventArgs e)
		{
			picPreviewSource.Invalidate();
		}

		/// <summary>Toggling auto-rescale repaints the preview in the other scale mode.</summary>
		private void chkAutoRescaleSourceOn_CheckedChanged(object sender, EventArgs e)
		{
			picPreviewSource.Invalidate();
		}
	}
}
