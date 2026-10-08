using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Color scheme tab: scheme list, stop editor and preview bar.
	/// </summary>
	/// <remarks>
	/// Two schemes matter here: <c>_previewScheme</c> is the (read-only) list selection and <c>_current</c>
	/// is the editable copy loaded from it. The editor always shows the preview when one is selected,
	/// otherwise the current scheme (<see cref="DisplayedScheme"/>). Everything in this file runs on
	/// the UI thread; the simulation never sees these objects, only the <see cref="ColorLut"/> built
	/// from them when a run starts.
	/// </remarks>
	public partial class Form1
	{
		/// <summary>Lets the user pick a scheme folder in a folder dialog and reloads the list from it.</summary>
		private void btbChooseFFolder_Click(object sender, EventArgs e)
		{
			using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
			{
				folderBrowserDialog.Description = "Choose the folder that contains the color scheme (.json) files";
				if (Directory.Exists(txtSchemeFolder.Text))
				{
					folderBrowserDialog.SelectedPath = txtSchemeFolder.Text;
				}
				if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
				{
					txtSchemeFolder.Text = folderBrowserDialog.SelectedPath;
					LoadSchemeList();
				}
			}
		}

		/// <summary>Enter in the folder box reloads the scheme list from the typed folder (and swallows the beep).</summary>
		private void txtSchemeFolder_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
			{
				e.SuppressKeyPress = true;
				LoadSchemeList();
			}
		}

		/// <summary>
		/// Reloads all scheme files from the folder in <c>txtSchemeFolder</c> and rebuilds the list view.
		/// Also sorts by the current column, regenerates the thumbnails (disposing the old bitmaps),
		/// refills the field/intensity combo boxes on the simulation tab, reports unreadable files in one
		/// message box and clears the preview selection. The current (editable) scheme is left alone.
		/// </summary>
		private void LoadSchemeList()
		{
			string folder = txtSchemeFolder.Text.Trim();
			if (!Directory.Exists(folder))
			{
				MessageBox.Show(this, "The folder '" + folder + "' does not exist.", "Color schemes", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			List<string> errors = new List<string>();
			_schemes = ColorSchemeStore.LoadFolder(folder, errors);
			SortSchemes(_schemes, _schemeSortColumn, _schemeSortAscending);
			ListView1.BeginUpdate();
			try
			{
				ListView1.Items.Clear();
				_thumbs.Images.Clear();
				foreach (Bitmap thumb in _thumbBitmaps)
				{
					thumb.Dispose();
				}
				_thumbBitmaps.Clear();
				foreach (ColorScheme scheme in _schemes)
				{
					Bitmap thumb = Form1.MakeThumbnail(scheme);
					_thumbBitmaps.Add(thumb);
					_thumbs.Images.Add(thumb);
					ListViewItem item = new ListViewItem(scheme.Name, _thumbs.Images.Count - 1);
					item.SubItems.Add(scheme.HasStops ? (ColorScheme.StopCount.ToString() + "-stop") : "table");
					item.SubItems.Add(string.IsNullOrEmpty(scheme.SchemeType) ? "-" : scheme.SchemeType);
					item.Tag = scheme;
					ListView1.Items.Add(item);
				}
			}
			finally
			{
				ListView1.EndUpdate();
			}
			UpdateSchemeListSortHeaders();
			if (errors.Count > 0)
			{
				// Show at most 8 file errors; the rest is summarised as a count.
				int shown = Math.Min(errors.Count, 8);
				string message = string.Join("\r\n", errors.GetRange(0, shown));
				if (errors.Count > shown)
				{
					message = message + "\r\n... and " + (errors.Count - shown).ToString() + " more";
				}
				MessageBox.Show(this, "Some files could not be read as color schemes:\r\n\r\n" + message, "Color schemes", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			RefreshSchemeCombos();
			_previewScheme = null;
			RefreshEditorState();
		}

		/// <summary>
		/// Sorts <paramref name="schemes"/> in place, case-insensitively: column 0 by name, any other
		/// column by scheme type and then name. <paramref name="ascending"/> false reverses the result.
		/// </summary>
		/// <param name="schemes">The list to sort.</param>
		/// <param name="sortColumn">List view column index; 0 = name, otherwise type then name.</param>
		/// <param name="ascending">False reverses the sorted order.</param>
		private void SortSchemes(List<ColorScheme> schemes, int sortColumn, bool ascending)
		{
			if (sortColumn == 0)
			{
				schemes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
			}
			else
			{
				schemes.Sort((a, b) =>
				{
					int byType = string.Compare(a.SchemeType, b.SchemeType, StringComparison.OrdinalIgnoreCase);
					return byType != 0 ? byType : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
				});
			}
			if (!ascending)
			{
				schemes.Reverse();
			}
		}

		/// <summary>
		/// Clicking the Scheme (0) or Type (2) header sorts by that column, or flips the direction when it
		/// is already the sort column; the Kind column is ignored. Sorting re-reads the folder from disk.
		/// </summary>
		private void ListView1_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			if (e.Column != 0 && e.Column != 2)
			{
				return;
			}
			if (e.Column == _schemeSortColumn)
			{
				_schemeSortAscending = !_schemeSortAscending;
			}
			else
			{
				_schemeSortColumn = e.Column;
				_schemeSortAscending = true;
			}
			LoadSchemeList();
		}

		/// <summary>Appends a " ^" / " v" direction marker to the header of the column the list is sorted by.</summary>
		private void UpdateSchemeListSortHeaders()
		{
			if (ListView1.Columns.Count < 3)
			{
				return;
			}
			string arrow = _schemeSortAscending ? " ^" : " v";
			ListView1.Columns[0].Text = "Scheme" + (_schemeSortColumn == 0 ? arrow : "");
			ListView1.Columns[2].Text = "Type" + (_schemeSortColumn == 2 ? arrow : "");
		}

		/// <summary>Selecting a list row previews that scheme in the editor; clearing the selection shows the current scheme again.</summary>
		private void ListView1_SelectedIndexChanged(object sender, EventArgs e)
		{
			_previewScheme = ListView1.SelectedItems.Count == 0 ? null : (ColorScheme)ListView1.SelectedItems[0].Tag;
			RefreshEditorState();
		}

		/// <summary>Double-clicking a row loads it as the current scheme.</summary>
		private void ListView1_DoubleClick(object sender, EventArgs e)
		{
			LoadPreviewedScheme();
		}

		/// <summary>Loads the previewed scheme as the current one, or asks the user to select a row first.</summary>
		private void btnLoadScheme_Click(object sender, EventArgs e)
		{
			if (_previewScheme == null)
			{
				MessageBox.Show(this, "Select a scheme in the list first.", "Load scheme", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
			else
			{
				LoadPreviewedScheme();
			}
		}

		/// <summary>Escape in the list clears the selection, which ends the preview.</summary>
		private void ListView1_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				ListView1.SelectedItems.Clear();
				e.Handled = true;
			}
		}

		/// <summary>
		/// Makes a copy of the previewed scheme the current (editable) one, after asking to discard unsaved
		/// changes. The copy keeps the file path of the original (<see cref="ColorScheme.Clone"/> does not),
		/// so a later Save overwrites that file. Clears the stop selection and the list selection.
		/// </summary>
		/// <returns>False if there is no preview or the user kept the unsaved current scheme.</returns>
		private bool LoadPreviewedScheme()
		{
			if (_previewScheme == null)
			{
				return false;
			}
			if (!ConfirmDiscardChanges())
			{
				return false;
			}
			ColorScheme previewScheme = _previewScheme;
			_current = previewScheme.Clone(previewScheme.Name);
			_current.FilePath = previewScheme.FilePath;
			_dirty = false;
			_selectedStop = -1;
			ListView1.SelectedItems.Clear(); // ends the preview; also refreshes the display
			RefreshEditorState();
			return true;
		}

		/// <summary>Asks whether unsaved edits of the current scheme may be thrown away.</summary>
		/// <returns>True if there are no unsaved changes or the user answered Yes.</returns>
		private bool ConfirmDiscardChanges()
		{
			if (!_dirty)
			{
				return true;
			}
			DialogResult answer = MessageBox.Show(this, "The current color scheme '" + _current.Name + "' has unsaved changes. Discard them?", "Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
			return answer == DialogResult.Yes;
		}

		/// <summary>
		/// Clicking one of the <see cref="ColorScheme.StopCount"/> Color buttons selects that stop for editing and
		/// loads its color into the picker. While previewing, the preview is first loaded as the current scheme
		/// (may be cancelled). A table-only scheme is converted to an unsaved
		/// <see cref="ColorScheme.StopCount"/>-stop approximation after confirmation.
		/// </summary>
		private void StopButton_Click(object sender, EventArgs e)
		{
			int stopIndex = Array.IndexOf(_stopButtons, (Button)sender);
			if (stopIndex < 0)
			{
				return;
			}
			// clicking a Color button while a list item is only previewed loads that scheme first
			if (_previewScheme != null && !LoadPreviewedScheme())
			{
				return;
			}
			if (_current == null)
			{
				return;
			}
			// an imported scheme is a full table without stops: editing it means making a stop-based approximation
			if (!_current.HasStops)
			{
				DialogResult answer = MessageBox.Show(this, "The scheme '" + _current.Name + "' is a full table of 255 colors without " + ColorScheme.StopCount.ToString() + " editable stops.\r\n\r\nEditing it creates a smooth " + ColorScheme.StopCount.ToString() + "-stop approximation as a new, unsaved scheme (the original file stays unchanged). Continue?", "Edit scheme", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
				if (answer != DialogResult.Yes)
				{
					return;
				}
				_current = _current.ToStopScheme(_current.Name + " (" + ColorScheme.StopCount.ToString() + "-stop)");
				_dirty = true;
			}
			_selectedStop = stopIndex;
			Color color = _current.Stops[stopIndex];
			_picker.OriginalColor = color;
			_picker.SelectedColor = color;
			RefreshEditorState();
		}

		/// <summary>
		/// A color change in the picker writes the new color into the selected stop of the current scheme
		/// (which regenerates its whole table), marks it dirty and repaints. Raised by
		/// <see cref="ColorPickerControl.ColorChanged"/>; ignored when no editable stop is selected.
		/// </summary>
		private void Picker_ColorChanged(object sender, EventArgs e)
		{
			if (_current == null || !_current.HasStops || _selectedStop < 0)
			{
				return;
			}
			_current.SetStop(_selectedStop, _picker.SelectedColor);
			_dirty = true;
			RefreshEditorState();
		}

		/// <summary>
		/// Saves the current scheme to its own file, or behaves like Save As when it has never been saved.
		/// Reloads the list afterwards so the list reflects the file.
		/// </summary>
		private void btnSaveCurrentScheme_Click(object sender, EventArgs e)
		{
			if (_current == null)
			{
				return;
			}
			if (string.IsNullOrEmpty(_current.FilePath))
			{
				SaveAsNew();
				return;
			}
			try
			{
				ColorSchemeStore.Save(_current, _current.FilePath);
				_dirty = false;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The scheme could not be saved:\r\n" + ex.Message, "Save scheme", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			ReloadListAfterSave();
		}

		/// <summary>Saves the current scheme under a new name (see <see cref="SaveAsNew"/>).</summary>
		private void btnSaveAsColorScheme_Click(object sender, EventArgs e)
		{
			SaveAsNew();
		}

		/// <summary>
		/// Asks for a name (VB <see cref="Interaction.InputBox"/>), writes a copy of the current scheme as
		/// <c>&lt;folder&gt;/&lt;safe name&gt;.json</c> in the scheme folder (confirming an overwrite) and makes
		/// that copy the current scheme. A stop scheme without a category is saved as "custom".
		/// </summary>
		private void SaveAsNew()
		{
			if (_current == null)
			{
				return;
			}
			string folder = txtSchemeFolder.Text.Trim();
			if (!Directory.Exists(folder))
			{
				MessageBox.Show(this, "Choose an existing scheme folder first.", "Save scheme", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			string newName = Interaction.InputBox("Name for the new color scheme:", "Save as new scheme", _current.Name, -1, -1).Trim();
			if (newName.Length == 0)
			{
				return;
			}
			string targetPath = ColorSchemeStore.PathFor(folder, newName);
			if (File.Exists(targetPath))
			{
				DialogResult answer = MessageBox.Show(this, "A scheme file named '" + Path.GetFileName(targetPath) + "' already exists. Overwrite it?", "Save scheme", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
				if (answer != DialogResult.Yes)
				{
					return;
				}
			}
			try
			{
				ColorScheme copy = _current.Clone(newName);
				if (copy.HasStops && string.IsNullOrEmpty(copy.Category))
				{
					copy.Category = "custom";
				}
				ColorSchemeStore.Save(copy, targetPath);
				_current = copy;
				_dirty = false;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The scheme could not be saved:\r\n" + ex.Message, "Save scheme", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			ReloadListAfterSave();
		}

		/// <summary>Re-reads the scheme folder after a save so the list and combo boxes show the new file.</summary>
		private void ReloadListAfterSave()
		{
			LoadSchemeList();
		}

		/// <summary>
		/// Renames the previewed scheme: prompts for a new name, saves the scheme under the new file name,
		/// deletes the old file and, if the current scheme was loaded from that file, renames it too.
		/// Refuses to overwrite a different existing file. Reloads the list and reselects the renamed row.
		/// </summary>
		private void btnRenameColorScheme_Click(object sender, EventArgs e)
		{
			if (_previewScheme == null)
			{
				MessageBox.Show(this, "Select a scheme in the list first.", "Rename color scheme", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			ColorScheme previewScheme = _previewScheme;
			string oldName = previewScheme.Name;
			string oldPath = previewScheme.FilePath;
			string newName = oldName;
			if (!PromptForText("Rename color scheme", "New name for '" + oldName + "':", oldName, ref newName))
			{
				return;
			}
			newName = newName.Trim();
			if (newName.Length == 0 || newName == oldName)
			{
				return;
			}
			string folder = string.IsNullOrEmpty(oldPath) ? txtSchemeFolder.Text.Trim() : Path.GetDirectoryName(oldPath);
			string newPath = ColorSchemeStore.PathFor(folder, newName);
			if (!string.Equals(newPath, oldPath, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
			{
				MessageBox.Show(this, "A scheme file named '" + Path.GetFileName(newPath) + "' already exists.", "Rename color scheme", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			try
			{
				previewScheme.Name = newName;
				ColorSchemeStore.Save(previewScheme, newPath);
				if (!string.IsNullOrEmpty(oldPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
				{
					File.Delete(oldPath);
				}
				if (_current != null && !string.IsNullOrEmpty(oldPath) && string.Equals(_current.FilePath, oldPath, StringComparison.OrdinalIgnoreCase))
				{
					_current.Name = newName;
					_current.FilePath = newPath;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The scheme could not be renamed:\r\n" + ex.Message, "Rename color scheme", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			LoadSchemeList();
			SelectSchemeInList(newName);
			ToolStripStatusLabel1.Text = "Color scheme '" + oldName + "' renamed to '" + newName + "'.";
		}

		/// <summary>
		/// Deletes the previewed scheme's file from disk after confirmation. If the current scheme came from
		/// that file it stays loaded but loses its path (the next Save becomes Save As). Reloads the list.
		/// </summary>
		private void btnDeleteColorScheme_Click(object sender, EventArgs e)
		{
			if (_previewScheme == null)
			{
				MessageBox.Show(this, "Select a scheme in the list first.", "Delete color scheme", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			ColorScheme previewScheme = _previewScheme;
			string name = previewScheme.Name;
			string filePath = previewScheme.FilePath;
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
			{
				MessageBox.Show(this, "That scheme has no file on disk (nothing to delete).", "Delete color scheme", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			DialogResult answer = MessageBox.Show(this, "Permanently delete the color scheme '" + name + "'?\r\n\r\n" + filePath, "Delete color scheme", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
			if (answer != DialogResult.Yes)
			{
				return;
			}
			try
			{
				File.Delete(filePath);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The scheme could not be deleted:\r\n" + ex.Message, "Delete color scheme", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			if (_current != null && string.Equals(_current.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
			{
				_current.FilePath = null;
			}
			_previewScheme = null;
			LoadSchemeList();
			ToolStripStatusLabel1.Text = "Color scheme '" + name + "' deleted.";
		}

		/// <summary>Selects and scrolls to the first list row whose scheme name matches <paramref name="name"/> (case-insensitive); does nothing if there is none.</summary>
		private void SelectSchemeInList(string name)
		{
			foreach (ListViewItem item in ListView1.Items)
			{
				ColorScheme scheme = item.Tag as ColorScheme;
				if (scheme != null && string.Equals(scheme.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					item.Selected = true;
					item.EnsureVisible();
					break;
				}
			}
		}

		/// <summary>
		/// Checking the "field" or "intensity" radio button changes the type of the current scheme and marks it
		/// dirty. The radio buttons say what the current scheme is meant for; the choice is saved in the scheme
		/// file as <c>"scheme_type": "field"</c> or <c>"scheme_type": "intensity"</c>
		/// (<see cref="ColorScheme.SchemeTypeField"/> / <see cref="ColorScheme.SchemeTypeIntensity"/>) and is shown
		/// again when the file is loaded. Ignored while <see cref="RefreshEditorState"/> is setting the radios
		/// programmatically (<c>_updatingRadios</c>), while previewing, and for the radio that just became unchecked.
		/// </summary>
		private void SchemeType_CheckedChanged(object sender, EventArgs e)
		{
			if (_updatingRadios)
			{
				return;
			}
			if (_current == null || _previewScheme != null)
			{
				return;
			}
			RadioButton radio = (RadioButton)sender;
			if (!radio.Checked)
			{
				return;
			}
			string schemeType = radio == radSchemeTypeField ? ColorScheme.SchemeTypeField : ColorScheme.SchemeTypeIntensity;
			if (_current.SchemeType != schemeType)
			{
				_current.SchemeType = schemeType;
				_dirty = true;
				RefreshEditorState();
			}
		}

		/// <summary>The scheme shown in the editor: the previewed list selection if any, otherwise the current one.</summary>
		/// <returns>Null when nothing is previewed and no scheme has been loaded.</returns>
		private ColorScheme DisplayedScheme()
		{
			return _previewScheme ?? _current;
		}

		/// <summary>
		/// Brings every editor control in line with the current/preview state: name box (with an
		/// "(unsaved changes)" suffix), the <see cref="ColorScheme.StopCount"/> stop buttons (color, contrasting
		/// text, thick border on the selected stop), picker enabling and caption, Load/Save button enabling, the
		/// type radios (set with <c>_updatingRadios</c> raised so <see cref="SchemeType_CheckedChanged"/> ignores
		/// it) and finally a repaint of the preview bar.
		/// </summary>
		private void RefreshEditorState()
		{
			bool previewing = _previewScheme != null;
			ColorScheme scheme = DisplayedScheme();
			bool hasCurrent = _current != null;
			bool editingStop = hasCurrent && !previewing && _selectedStop >= 0;
			if (hasCurrent)
			{
				txtCurrentColorScheme.Text = _current.Name + (_dirty ? "   (unsaved changes)" : "");
			}
			else
			{
				txtCurrentColorScheme.Text = "(none loaded)";
			}
			for (int k = 0; k < ColorScheme.StopCount; k++)
			{
				Button button = _stopButtons[k];
				button.Enabled = scheme != null;
				if (scheme != null)
				{
					Color color = Form1.StopColorOf(scheme, k);
					button.BackColor = color;
					button.ForeColor = Form1.ContrastColor(color);
				}
				else
				{
					button.BackColor = SystemColors.Control;
					button.ForeColor = SystemColors.ControlText;
				}
				button.FlatAppearance.BorderSize = (editingStop && k == _selectedStop) ? 4 : 1;
			}
			_picker.Enabled = editingStop;
			if (editingStop)
			{
				lblColorPicker.Text = "Color Picker - editing Color " + (_selectedStop + 1).ToString() + Form1.StopRole(_selectedStop);
			}
			else
			{
				lblColorPicker.Text = "Color Picker (click a Color button to edit it)";
			}
			btnLoadScheme.Enabled = previewing;
			btnSaveCurrentScheme.Enabled = hasCurrent && _dirty;
			btnSaveAsColorScheme.Enabled = hasCurrent;
			// radio buttons show the type of the displayed scheme; they can only be changed for the current scheme
			_updatingRadios = true;
			try
			{
				string schemeType = scheme == null ? "" : scheme.SchemeType;
				radSchemeTypeField.Checked = schemeType == ColorScheme.SchemeTypeField;
				radSchemeTypeIntensity.Checked = schemeType == ColorScheme.SchemeTypeIntensity;
				radSchemeTypeField.Enabled = hasCurrent && !previewing;
				radSchemeTypeIntensity.Enabled = hasCurrent && !previewing;
			}
			finally
			{
				_updatingRadios = false;
			}
			pnlScheme.Invalidate();
		}

		/// <summary>
		/// Paints the vertical preview bar: entry 0 (Color 1) at the top, the last entry (last Color) at the
		/// bottom and the middle entry (centre Color) half-way, stretched from a 1 x <see cref="ColorScheme.EntryCount"/>
		/// strip without smoothing; white-on-black tick marks at the <see cref="ColorScheme.StopCount"/> stop
		/// positions (a full-width thick line for the stop being edited) and the scale labels for the scheme type
		/// ("-1 / 0 / +1" for field, "0 / max" for intensity). A plain light fill when there is nothing to show.
		/// </summary>
		private void pnlScheme_Paint(object sender, PaintEventArgs e)
		{
			ColorScheme scheme = DisplayedScheme();
			Graphics g = e.Graphics;
			Rectangle bar = pnlScheme.ClientRectangle;
			if (scheme == null)
			{
				g.Clear(SystemColors.ControlLight);
				return;
			}
			// vertical gradient: top = entry 0 (Color 1), bottom = entry EntryCount - 1 (last Color), middle = entry EntryCount / 2 (centre Color)
			using (Bitmap strip = new Bitmap(1, ColorScheme.EntryCount, PixelFormat.Format32bppArgb))
			{
				for (int i = 0; i < ColorScheme.EntryCount; i++)
				{
					strip.SetPixel(0, i, scheme.GetEntry(i));
				}
				g.InterpolationMode = InterpolationMode.NearestNeighbor;
				g.PixelOffsetMode = PixelOffsetMode.Half;
				g.DrawImage(strip, bar);
			}
			// tick marks at the stop positions (dark and light so they show on any color)
			bool editingStop = _previewScheme == null && _selectedStop >= 0;
			for (int k = 0; k < ColorScheme.StopCount; k++)
			{
				// Stop k sits at fraction k/(StopCount - 1) of the bar height (StopCount stops = StopCount - 1 segments).
				int y = (int)Math.Round((k * (bar.Height - 1)) / (double)(ColorScheme.StopCount - 1));
				bool highlighted = editingStop && k == _selectedStop;
				int tickLength = highlighted ? bar.Width : 9;
				int tickWidth = highlighted ? 3 : 1;
				using (Pen outline = new Pen(Color.Black, tickWidth + 2))
				{
					g.DrawLine(outline, 0, y, tickLength, y);
					g.DrawLine(outline, bar.Width - tickLength, y, bar.Width, y);
				}
				using (Pen tick = new Pen(Color.White, tickWidth))
				{
					g.DrawLine(tick, 0, y, tickLength, y);
					g.DrawLine(tick, bar.Width - tickLength, y, bar.Width, y);
				}
			}
			// scale labels that depend on the scheme type
			if (scheme.SchemeType == ColorScheme.SchemeTypeField)
			{
				DrawScaleLabel(g, bar, "-1", 0, scheme);
				DrawScaleLabel(g, bar, "0", ColorScheme.EntryCount / 2, scheme);
				DrawScaleLabel(g, bar, "+1", ColorScheme.EntryCount - 1, scheme);
			}
			else if (scheme.SchemeType == ColorScheme.SchemeTypeIntensity)
			{
				DrawScaleLabel(g, bar, "0", 0, scheme);
				DrawScaleLabel(g, bar, "max", ColorScheme.EntryCount - 1, scheme);
			}
		}

		/// <summary>
		/// Draws <paramref name="text"/> horizontally centred on the preview bar at the height of table entry
		/// <paramref name="entryIndex"/> (0..<see cref="ColorScheme.EntryCount"/> - 1 mapped onto the bar height),
		/// kept at least 12 px away from the top and bottom edges (clear of the end ticks), in black or white
		/// depending on the entry color underneath, so it is readable on any color.
		/// </summary>
		/// <param name="g">Target graphics of the preview panel.</param>
		/// <param name="bar">Client rectangle of the preview panel.</param>
		/// <param name="text">Label text.</param>
		/// <param name="entryIndex">Table entry (0..<see cref="ColorScheme.EntryCount"/> - 1) whose vertical position the label is centred on.</param>
		/// <param name="scheme">Scheme being painted; used only to pick a contrasting text color.</param>
		private void DrawScaleLabel(Graphics g, Rectangle bar, string text, int entryIndex, ColorScheme scheme)
		{
			Size textSize = TextRenderer.MeasureText(g, text, pnlScheme.Font);
			int centerY = (int)Math.Round((entryIndex * (bar.Height - 1)) / (double)(ColorScheme.EntryCount - 1));
			int top = centerY - textSize.Height / 2;
			if (top < 12)
			{
				top = 12; // keep clear of the end ticks
			}
			if (top + textSize.Height > bar.Height - 12)
			{
				top = bar.Height - 12 - textSize.Height;
			}
			Rectangle labelBounds = new Rectangle(0, top, bar.Width, textSize.Height);
			TextRenderer.DrawText(g, text, pnlScheme.Font, labelBounds, Form1.ContrastColor(scheme.GetEntry(entryIndex)), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}

		/// <summary>
		/// Color shown on stop button <paramref name="k"/>: the real stop for a stop scheme, otherwise the table
		/// sampled at the position the stop would have (k/(<see cref="ColorScheme.StopCount"/> - 1) of the
		/// 0..<see cref="ColorScheme.EntryCount"/> - 1 range).
		/// </summary>
		private static Color StopColorOf(ColorScheme scheme, int k)
		{
			return scheme.HasStops ? scheme.Stops[k] : scheme.SampleAt((k * (ColorScheme.EntryCount - 1)) / (double)(ColorScheme.StopCount - 1));
		}

		/// <summary>
		/// Suffix for the picker caption describing what stop <paramref name="k"/> represents (first, centre, last);
		/// empty for the others. There is a centre stop only while <see cref="ColorScheme.StopCount"/> is odd.
		/// </summary>
		private static string StopRole(int k)
		{
			if (k == 0)
			{
				return " (start of the scale)";
			}
			if (k == ColorScheme.StopCount - 1)
			{
				return " (end of the scale)";
			}
			if (ColorScheme.StopCount % 2 == 1 && k == ColorScheme.StopCount / 2)
			{
				return " (centre = zero field)";
			}
			return "";
		}

		/// <summary>Black or white, whichever reads better on top of <paramref name="c"/> (Rec. 601 luma, threshold 140 of 255).</summary>
		private static Color ContrastColor(Color c)
		{
			double luma = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
			return luma > 140.0 ? Color.Black : Color.White;
		}

		/// <summary>
		/// Renders a <see cref="ThumbWidth"/> x <see cref="ThumbHeight"/> horizontal strip of the scheme (entry 0
		/// left, the last entry right) with a gray border, for the list view image list. The caller owns the
		/// returned bitmap; <see cref="LoadSchemeList"/> keeps them in <c>_thumbBitmaps</c> and disposes them on
		/// the next reload.
		/// </summary>
		private static Bitmap MakeThumbnail(ColorScheme scheme)
		{
			Bitmap thumb = new Bitmap(ThumbWidth, ThumbHeight, PixelFormat.Format32bppArgb);
			using (Graphics g = Graphics.FromImage(thumb))
			{
				for (int x = 0; x < ThumbWidth; x++)
				{
					int entryIndex = (int)Math.Round((x * (ColorScheme.EntryCount - 1)) / (double)(ThumbWidth - 1));
					using (SolidBrush brush = new SolidBrush(scheme.GetEntry(entryIndex)))
					{
						g.FillRectangle(brush, x, 0, 1, ThumbHeight);
					}
				}
				g.DrawRectangle(Pens.Gray, 0, 0, ThumbWidth - 1, ThumbHeight - 1);
			}
			return thumb;
		}
	}
}
