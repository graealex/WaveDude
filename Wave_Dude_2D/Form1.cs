using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Main window of the wave simulator. This part holds start-up and shutdown, the File/Info
	/// menus, project open/save, the hand-built modal prompts, and every private field and
	/// nested type shared by the other <c>Form1.*.cs</c> parts (Colors, Projects, Output, Scene,
	/// Simulation, Probes, Sources). All of it runs on the UI thread; the only other thread is
	/// the <see cref="SimulationRunner"/> worker, whose events are marshalled back through
	/// <see cref="PostToUi"/> in the Simulation part.
	/// </summary>
	public partial class Form1 : Form
	{
		/// <summary>
		/// Wires the form lifetime events, gives every shared field its start value (empty
		/// settings objects, default sort orders, the root-blink timer, an unnamed source being
		/// edited) and then runs the designer code. The real set-up continues in
		/// <see cref="Form1_Load"/>.
		/// </summary>
		public Form1()
		{
			base.Load += Form1_Load;
			base.FormClosed += Form1_FormClosed;
			base.FormClosing += Form1_FormClosing;
			_schemes = new List<ColorScheme>();
			_selectedStop = -1;
			_thumbs = new ImageList();
			_projectSortColumn = 1;
			_projectSortAscending = false;
			_outputProjectSortColumn = 1;
			_outputProjectSortAscending = false;
			_schemeSortColumn = 2;
			_schemeSortAscending = true;
			_sceneSortColumn = 1;
			_sceneSortAscending = false;
			_imageSortColumn = 1;
			_imageSortAscending = false;
			_videoSortColumn = 1;
			_videoSortAscending = false;
			_thumbBitmaps = new List<Bitmap>();
			_settings = new AppSettings();
			_rootBlinkTimer = new global::System.Windows.Forms.Timer
			{
				Interval = 700
			};
			_rootBlinkTimer.Tick += RootBlinkTimer_Tick;
			_scene = new SceneSettings();
			_sceneTip = new ToolTip();
			_simulation = new SimulationSettings();
			_simStatus = "";
			_fieldVideoFile = "";
			_intensityVideoFile = "";
			_runInfo = "";
			_progressText = "";
			_probes = Form1.NewProbeList();
			_probeTraces = new List<ProbeTrace>();
			_sources = new List<SourceSettings>();
			_currentSourceIndex = -1;
			_editing = new SourceSettings();
			_pickSourceButtonText = "Pick";
			_sourceRandom = new Random();
			InitializeComponent();
		}

		/// <summary>
		/// Finishes what the designer cannot express: collects the nine colour-stop buttons,
		/// configures the scheme list view and its thumbnail list, embeds the
		/// <see cref="ColorPickerControl"/>, switches the main tab control to owner drawing,
		/// loads the default scheme folder, runs the per-tab initialisers, re-parents
		/// <c>btnPickRoot</c> so it stays clickable while <c>grpProjects</c> is disabled, loads the
		/// user settings and project list, and makes Enter in any <see cref="NumericUpDown"/>
		/// move the focus on.
		/// </summary>
		private void Form1_Load(object sender, EventArgs e)
		{
			// one button per stop, in scale order (Color 1 = first entry ... last button = last entry)
			_stopButtons = new Button[] { btnColor1, btnCOlor2, btnColor3, btnColor4, btnColor5, btnColor6, btnColor7, btnColor8, btnColor9 };
			Debug.Assert(_stopButtons.Length == ColorScheme.StopCount, "The number of Color buttons must equal ColorScheme.StopCount.");
			foreach (Button button in _stopButtons)
			{
				button.UseVisualStyleBackColor = false;
				button.FlatStyle = FlatStyle.Flat;
				button.FlatAppearance.BorderSize = 1;
				button.FlatAppearance.BorderColor = Color.Black;
			}
			// scheme list: thumbnail + name, grouped by category
			_thumbs.ImageSize = new Size(ThumbWidth, ThumbHeight);
			_thumbs.ColorDepth = ColorDepth.Depth32Bit;
			ListView1.View = View.Details;
			ListView1.FullRowSelect = true;
			ListView1.MultiSelect = false;
			ListView1.HideSelection = false;
			ListView1.HeaderStyle = ColumnHeaderStyle.Clickable;
			ListView1.SmallImageList = _thumbs;
			ListView1.Columns.Clear();
			ListView1.Columns.Add("Scheme", 250);
			ListView1.Columns.Add("Kind", 55);
			ListView1.Columns.Add("Type", 62);
			UpdateSchemeListSortHeaders();
			pnlScheme.BorderStyle = BorderStyle.FixedSingle;
			Panel2.BorderStyle = BorderStyle.FixedSingle;
			_picker = new ColorPickerControl();
			_picker.Dock = DockStyle.Fill;
			Panel2.Controls.Add(_picker);
			_picker.ColorChanged += Picker_ColorChanged;
			txtCurrentColorScheme.ReadOnly = true;
			// tabMainFunctions: owner-drawn only so a disabled tab's own label can be shown in a lighter gray
			// (WinForms does not do this by itself - a disabled TabPage still draws its label at full strength) -
			// see tabMainFunctions_DrawItem.
			tabMainFunctions.DrawMode = TabDrawMode.OwnerDrawFixed;
			// lstAllProjects (Configuration tab project list): sortable by clicking colProjectName/colLastEdit, see
			// lstAllProjects_ColumnClick. Show the initial sort direction in the header the same way a click would.
			UpdateProjectListSortHeaders();
			// the program's own scheme folder (ColorSchemeList in the project, or next to the exe when deployed)
			txtSchemeFolder.Text = ColorSchemeStore.FindDefaultFolder();
			if (Directory.Exists(txtSchemeFolder.Text))
			{
				LoadSchemeList();
			}
			RefreshEditorState();
			// lblInfo (Settings tab): a clickable "hyperlink" style label - see lblInfo_Click for what it shows
			lblInfo.ForeColor = Color.Blue;
			lblInfo.Cursor = Cursors.Hand;
			InitializeSceneControls();
			InitializeSimulationControls();
			InitializeProbeControls();
			InitializeSourceControls();
			// btnPickRoot must stay clickable even when no project root is known yet (that is the one situation it
			// exists for), but the Designer put it inside grpProjects, and WinForms disables an enabled child along
			// with a disabled parent - grpProjects.Enabled = false (UpdateProjectAccess, no root) would silently
			// swallow its clicks right when they matter most. Move it onto tabConfiguration itself, in the same
			// screen position, so it is never caught by that.
			Point rootButtonSpot = grpProjects.Location;
			rootButtonSpot.Offset(btnPickRoot.Location);
			grpProjects.Controls.Remove(btnPickRoot);
			tabConfiguration.Controls.Add(btnPickRoot);
			btnPickRoot.Location = rootButtonSpot;
			btnPickRoot.BringToFront();
			InitializeProject();
			InitializeOutputsTab();
			// Enter in a NumericUpDown's edit box otherwise just beeps (Windows beeps on an unhandled Enter
			// keystroke) and leaves the typed value sitting there uncommitted - HookNumericUpDownEnterKey wires
			// every NumericUpDown on the form, wherever it lives, to NumericUpDown_KeyDown instead.
			HookNumericUpDownEnterKey(this);
		}

		/// <summary>
		/// Recursively attaches <see cref="NumericUpDown_KeyDown"/> to every <see cref="NumericUpDown"/> below
		/// <paramref name="root"/> (recursing into GroupBox/TabPage/Panel/... containers), so Enter commits the
		/// typed value and moves on instead of beeping. Called once, from <see cref="Form1_Load"/>, on the whole
		/// form - a plain loop over Controls rather than a fixed list, so it also covers any NumericUpDown added
		/// later without needing to come back and wire it up by hand.
		/// </summary>
		private void HookNumericUpDownEnterKey(Control root)
		{
			foreach (Control control in root.Controls)
			{
				NumericUpDown numericUpDown = control as NumericUpDown;
				if (numericUpDown != null)
				{
					numericUpDown.KeyDown += NumericUpDown_KeyDown;
				}
				if (control.HasChildren)
				{
					HookNumericUpDownEnterKey(control);
				}
			}
		}

		/// <summary>
		/// Enter in a NumericUpDown: stops the beep (<c>e.SuppressKeyPress</c> - Enter is not a character its edit
		/// box accepts, which is what Windows was beeping about) and moves focus on to the next control, the same
		/// as Tab would - which also commits whatever was typed, since a NumericUpDown parses/clamps its text on
		/// losing focus regardless of how focus was lost.
		/// </summary>
		private void NumericUpDown_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Return)
			{
				return;
			}
			e.SuppressKeyPress = true;
			e.Handled = true;
			Control control = sender as Control;
			if (control != null)
			{
				base.SelectNextControl(control, true, true, true, true);
			}
		}

		/// <summary>Disposes every bitmap the form owns: preview frames, scheme thumbnails, the scene image, the empty-canvas placeholder and the output preview.</summary>
		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
			ClearPreviews();
			foreach (Bitmap bitmap in _thumbBitmaps)
			{
				bitmap.Dispose();
			}
			_thumbBitmaps.Clear();
			ReplaceSceneImage(null);
			DisposeEmptyPreview();
			ClearOutputPreview();
		}

		/// <summary>
		/// Vetoes closing when the user declines the save-project prompt, declines stopping a
		/// running simulation (otherwise it is cancelled and given up to 15 s to exit), or
		/// declines discarding unsaved colour-scheme edits.
		/// </summary>
		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!ConfirmSaveProject())
			{
				e.Cancel = true;
				return;
			}
			if (_runner != null)
			{
				if (MessageBox.Show(this, "A simulation is running. Stop it and close the program?", "Simulation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					e.Cancel = true;
					return;
				}
				// the runner closes the video files before it ends
				_runner.Cancel();
				_runner.WaitForExit(15000);
			}
			if (_dirty)
			{
				if (MessageBox.Show(this, "The current color scheme has unsaved changes. Close anyway?", "Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
				{
					e.Cancel = true;
				}
			}
		}

		/// <summary>
		/// Enables/disables everything that depends on a project root being chosen and/or an actual, named
		/// project being the current one. <c>grpProjects</c> and the Outputs tab (its own project list, not
		/// necessarily the one that is open) only need a root. The scene/simulation groups, the
		/// Sources/Probes/Viewer tabs, most of "Quick navigate to" (all but <c>btnGotoConfiguration</c>, which
		/// jumps to the always-available Configuration tab), "Simulation Control", "Share with source / probe"
		/// and Save all need a real project. The Configuration tab (where the root and the project list
		/// themselves live) and the color scheme tab manage the application's own settings, not a project's,
		/// so they are never restricted. Disabling a TabPage here is not enough by itself to keep the user out
		/// of it - see <see cref="tabMainFunctions_Selecting"/>, which refuses to switch to a disabled tab at
		/// all instead of just showing its (disabled) contents. Blinks <c>btnPickRoot</c> while no root is chosen.
		/// </summary>
		private void UpdateProjectAccess()
		{
			bool hasRoot = _settings.ProjectRoot.Length > 0;
			bool hasProject = _project != null;
			grpProjects.Enabled = hasRoot;
			tabOutput.Enabled = hasRoot;
			// btnPickRoot (moved onto tabConfiguration itself in Form1_Load, so grpProjects.Enabled above cannot
			// disable it) blinks light blue, slowly, for exactly the situation it exists for: no project root known
			// yet - a fresh install on another computer, before AppSettings.ProjectRoot is set and everything else
			// on screen is still disabled.
			if (hasRoot)
			{
				_rootBlinkTimer.Stop();
				_rootBlinkOn = false;
				btnPickRoot.UseVisualStyleBackColor = true;
			}
			else
			{
				_rootBlinkTimer.Start();
			}
			grpSceneProperties.Enabled = hasProject;
			grpSimulationSettings.Enabled = hasProject;
			tabSourceList.Enabled = hasProject;
			tabProbeList.Enabled = hasProject;
			tabViewer.Enabled = hasProject;
			// grpNavigateTabs itself stays enabled: btnGotoConfiguration jumps to the always-available Configuration
			// tab, so only the other three "quick navigate" buttons need a real project.
			btnGotoPreview.Enabled = hasProject;
			btnGotoProbes.Enabled = hasProject;
			btnGotoSources.Enabled = hasProject;
			grpSimulationControlButtons.Enabled = hasProject;
			grpShareImage.Enabled = hasProject;
			btnSaveConfiguration.Enabled = hasProject;
			btnSaveConfiguration2.Enabled = hasProject;
			btnSaveAs.Enabled = hasProject;
			btnDescription.Enabled = hasProject;
			// Changing a TabPage's Enabled does not by itself repaint the tab strip, so a label's color
			// (tabMainFunctions_DrawItem) would otherwise stay stuck at its old, disabled gray until something
			// unrelated forced a redraw (resizing the window, switching tabs, ...) - force it here instead.
			tabMainFunctions.Invalidate();
		}

		/// <summary>Every 700 ms while no root is chosen: toggles <c>btnPickRoot</c> between light blue and the normal button colour.</summary>
		private void RootBlinkTimer_Tick(object sender, EventArgs e)
		{
			_rootBlinkOn = !_rootBlinkOn;
			btnPickRoot.UseVisualStyleBackColor = false;
			btnPickRoot.BackColor = (_rootBlinkOn ? Color.LightBlue : SystemColors.Control);
		}

		/// <summary>
		/// Refuses to switch to a tab that <see cref="UpdateProjectAccess"/> disabled and tells the user to open a
		/// project first. A disabled TabPage still lets itself be clicked into and shows its (disabled) contents
		/// unless refused here - WinForms' TabControl has no built-in notion of a locked tab.
		/// </summary>
		private void tabMainFunctions_Selecting(object sender, TabControlCancelEventArgs e)
		{
			if (!e.TabPage.Enabled)
			{
				e.Cancel = true;
				MessageBox.Show(this, "Please open or create a project first.", "No project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}

		/// <summary>
		/// Owner-draws a tab header: window colour for the selected tab, control colour otherwise, silver text when
		/// the page is disabled. Needed only so a disabled tab's label can be shown in a lighter gray than an
		/// enabled one's (<see cref="Form1_Load"/> sets <c>tabMainFunctions.DrawMode</c> to OwnerDrawFixed for this).
		/// </summary>
		private void tabMainFunctions_DrawItem(object sender, DrawItemEventArgs e)
		{
			TabPage tabPage = tabMainFunctions.TabPages[e.Index];
			Rectangle tabRect = tabMainFunctions.GetTabRect(e.Index);
			// the selected tab a shade lighter than the rest, so which one is open is still visible without the
			// native visual style this drawing replaces
			Color backColor = ((e.Index == tabMainFunctions.SelectedIndex) ? SystemColors.Window : SystemColors.Control);
			using (SolidBrush brush = new SolidBrush(backColor))
			{
				e.Graphics.FillRectangle(brush, tabRect);
			}
			Color textColor = (tabPage.Enabled ? SystemColors.ControlText : Color.Silver);
			TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
			TextRenderer.DrawText(e.Graphics, tabPage.Text, tabMainFunctions.Font, tabRect, textColor, flags);
		}

		/// <summary>
		/// Startup: replaces the placeholder <see cref="_settings"/> with the values read from disk, shows
		/// the stored project root, and refreshes access state, title and project list. Nothing from any
		/// previous project is filled into the form: only the chosen project root folder
		/// (<see cref="AppSettings.ProjectRoot"/>) is restored, if one was already chosen on an earlier run -
		/// everything that needs a root or a real project stays disabled until then (<see cref="UpdateProjectAccess"/>).
		/// </summary>
		private void InitializeProject()
		{
			_settings = AppSettings.Load();
			txtRootFolder.Text = _settings.ProjectRoot;
			txtProjectName.Text = "";
			UpdateProjectAccess();
			UpdateTitle();
			RefreshProjectList();
		}

		/// <summary>
		/// File &gt; Open: asks for an existing project folder (browse directly to the main folder of a project,
		/// without going through the project list), offers to save the current project, then opens the chosen one.
		/// Browsing for the project ROOT folder instead is <c>btnBrowseRoot</c> / File &gt; Choose project root /
		/// double-clicking <c>txtRootFolder</c> - see <see cref="ChooseProjectRootToolStripMenuItem_Click"/>.
		/// </summary>
		private void OpenProject_Click(object sender, EventArgs e)
		{
			try
			{
				string folder = BrowseForFolder("Choose the main folder of the project (the folder that contains configuration.json)", txtRootFolder.Text.Trim(), false);
				if (folder.Length > 0 && ConfirmSaveProject())
				{
					SetCurrentProject(ProjectStore.Open(folder));
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The project could not be opened:\r\n\r\n" + ex.Message, "Open project", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>File &gt; Save and the "Save changes to project" button: saves the open project (see <see cref="SaveProject"/>).</summary>
		private void SaveProject_Click(object sender, EventArgs e)
		{
			SaveProject();
		}

		/// <summary>
		/// "Save as": a simple name prompt (<see cref="PromptForText"/>, the same small dialog <c>btnRenameProject</c>
		/// uses), not a file dialog - the new project always goes into the current root folder (<c>txtRootFolder</c>),
		/// never somewhere else, and there is no file extension to worry about (a project is just a folder, see
		/// <see cref="ProjectStore"/>). Saving under a name that already exists in the root is not refused up front:
		/// <see cref="SaveProjectTo"/> itself asks to overwrite in that case, same as plain Save would for an existing project.
		/// </summary>
		private void SaveAs_Click(object sender, EventArgs e)
		{
			string root = txtRootFolder.Text.Trim();
			if (root.Length == 0)
			{
				MessageBox.Show(this, "Enter a root folder first (double-click the box to browse).", "Save project as", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			string projectName = txtProjectName.Text.Trim();
			if (!PromptForText("Save project as", "New project name (saved in the current root folder):", projectName, ref projectName))
			{
				return;
			}
			projectName = projectName.Trim();
			string error = ProjectStore.ValidateName(projectName);
			if (error.Length > 0)
			{
				MessageBox.Show(this, error, "Save project as", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			SaveProjectTo(root, projectName);
		}

		/// <summary>
		/// Opens <see cref="frmDescription"/> so the user can add/edit a free-form description of the open project,
		/// stored in the project's own configuration.json (<see cref="ProjectConfig.Description"/>) - written to disk
		/// at once on OK (without touching <see cref="_projectDirty"/>), the same as <c>btnRenameProject</c>'s rename
		/// takes effect right away rather than waiting for "Save project". Only enabled while a real (saved) project
		/// is open (<see cref="UpdateProjectAccess"/>) - a new, unsaved project has no configuration.json yet to hold it.
		/// </summary>
		private void btnDescription_Click(object sender, EventArgs e)
		{
			if (_project == null)
			{
				return;
			}
			using (frmDescription dialog = new frmDescription())
			{
				dialog.DescriptionText = _project.Config.Description;
				if (dialog.ShowDialog(this) == DialogResult.OK)
				{
					_project.Config.Description = dialog.DescriptionText;
					_project.Save();
				}
			}
		}

		/// <summary>File &gt; Exit: closes the window (the closing checks run in <see cref="Form1_FormClosing"/>).</summary>
		private void ExitProgram_Click(object sender, EventArgs e)
		{
			base.Close();
		}

		/// <summary>Info &gt; Help: shows the <see cref="HelpForm"/> modally.</summary>
		private void HelpToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (HelpForm helpForm = new HelpForm())
			{
				helpForm.ShowDialog(this);
			}
		}

		/// <summary>Info &gt; About: shows the <see cref="AboutForm"/> modally.</summary>
		private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (AboutForm aboutForm = new AboutForm())
			{
				aboutForm.ShowDialog(this);
			}
		}

		/// <summary>
		/// File &gt; Choose project root, the <c>btnBrowseRoot</c> button, <c>btnPickRoot</c> (the blinking button that
		/// is always reachable even with no root yet - see <see cref="Form1_Load"/>/<see cref="UpdateProjectAccess"/>)
		/// and a double-click on the (read-only) root text box: all four let the user pick the folder that contains
		/// the project folders (<c>txtRootFolder</c> just displays it - <see cref="AppSettings.ProjectRoot"/> is the
		/// real, persisted value, restored again on the next start). Once a root is chosen, <c>grpProjects</c> and
		/// the Outputs tab become usable and the project list is (re)populated. The open project, if any, is left as it is.
		/// </summary>
		private void ChooseProjectRootToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string startFolder = ((_settings.ProjectRoot.Length > 0) ? _settings.ProjectRoot : txtRootFolder.Text.Trim());
			string folder = BrowseForFolder("Choose the root folder that contains your project folders", startFolder, true);
			if (folder.Length == 0)
			{
				return;
			}
			_settings.ProjectRoot = folder;
			_settings.Save();
			txtRootFolder.Text = folder;
			UpdateProjectAccess();
			RefreshProjectList();
		}

		/// <summary>Clicking the "(Info)" label shows the explanation of 4th-order processing, dispersion and the c-value.</summary>
		private void lblInfo_Click(object sender, EventArgs e)
		{
			MessageBox.Show(this, "The standard 3rd order Laplacian processing inherently leads to large dispersion, meaning that longer wavelengths propagate faster through the medium. Choosing 4th order processing (including sub-step sampling) reduces this effect significantly. However, processing is somewhat slower. Another effect of 4th order processing is that it requires a different propagation speed (c). The value can be specified per project and set in the File menu. For 4th order processing choose 1.00 for c. For standard processing, this value can be quite a bit different. For example, with standard processing for waves with a frequency of 100, c = 0.56, leading in this case to a wavelength of only 9 pixels (instead of 15). Because of the dispersion present in standard processing, the exact value of c is strongly dependent on the frequency. (lower frequencies -> higher c value; higher frequencies -> lower c value).", "4th order Laplacian and wave speed (c)", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}

		/// <summary>
		/// File &gt; Set c-value: prompts for the wave speed (pixels per step), stores it in
		/// <see cref="SimulationSettings.WaveSpeedC"/>, marks the project changed if it differs,
		/// and refreshes the wavelength read-out on the Sources tab. c is only used to estimate a
		/// source's wavelength (<c>lblLambdaIs</c> on the Sources tab).
		/// </summary>
		private void SetCValueToolStripMenuItem_Click(object sender, EventArgs e)
		{
			double cValue = 0.0;
			if (!PromptForCValue(_simulation.WaveSpeedC, ref cValue))
			{
				return;
			}
			if (cValue != _simulation.WaveSpeedC)
			{
				_simulation.WaveSpeedC = cValue;
				MarkProjectChanged();
			}
			UpdateLambdaLabel();
		}

		/// <summary>
		/// Shows a hand-built modal dialog with a 3-decimal spinner (0.001 to 1,000,000) for the
		/// wave speed c. <paramref name="current"/> is clamped into that range before display.
		/// </summary>
		/// <param name="current">Value shown when the dialog opens.</param>
		/// <param name="result">Receives the entered value on OK, or <paramref name="current"/> on cancel.</param>
		/// <returns>True when the user pressed OK.</returns>
		private bool PromptForCValue(double current, ref double result)
		{
			using (Form dlg = new Form())
			{
				dlg.Text = "Set c-value";
				dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
				dlg.StartPosition = FormStartPosition.CenterParent;
				dlg.MinimizeBox = false;
				dlg.MaximizeBox = false;
				dlg.ShowInTaskbar = false;
				dlg.ClientSize = new Size(340, 116);
				Label label = new Label();
				label.Text = "Wave propagation speed c (in pixels per step)\r\n(lambda = c / frequency).\r\nOnly used to estimate a source's wavelength in screen pixels";
				label.SetBounds(12, 10, 316, 44);
				NumericUpDown numValue = new NumericUpDown();
				numValue.DecimalPlaces = 3;
				numValue.Minimum = 0.001m;
				numValue.Maximum = 1000000m;
				numValue.Increment = 0.1m;
				numValue.SetBounds(12, 60, 110, 22);
				double clamped = Math.Max(Convert.ToDouble(numValue.Minimum), Math.Min(Convert.ToDouble(numValue.Maximum), current));
				numValue.Value = new decimal(clamped);
				Button btnOk = new Button();
				btnOk.Text = "OK";
				btnOk.DialogResult = DialogResult.OK;
				btnOk.SetBounds(152, 80, 84, 26);
				Button btnCancel = new Button();
				btnCancel.Text = "Cancel";
				btnCancel.DialogResult = DialogResult.Cancel;
				btnCancel.SetBounds(244, 80, 84, 26);
				dlg.Controls.AddRange(new Control[] { label, numValue, btnOk, btnCancel });
				dlg.AcceptButton = btnOk;
				dlg.CancelButton = btnCancel;
				if (dlg.ShowDialog(this) == DialogResult.OK)
				{
					result = Convert.ToDouble(numValue.Value);
					return true;
				}
			}
			result = current;
			return false;
		}

		/// <summary>
		/// Shows a hand-built modal dialog with one single-line text box (pre-filled and selected).
		/// Used for new/renamed project, scheme and video names.
		/// </summary>
		/// <param name="title">Dialog caption.</param>
		/// <param name="prompt">Label shown above the text box.</param>
		/// <param name="defaultValue">Initial text, shown selected.</param>
		/// <param name="result">Receives the untrimmed text on OK, or <paramref name="defaultValue"/> on cancel.</param>
		/// <returns>True when the user pressed OK.</returns>
		private bool PromptForText(string title, string prompt, string defaultValue, ref string result)
		{
			using (Form dlg = new Form())
			{
				dlg.Text = title;
				dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
				dlg.StartPosition = FormStartPosition.CenterParent;
				dlg.MinimizeBox = false;
				dlg.MaximizeBox = false;
				dlg.ShowInTaskbar = false;
				dlg.ClientSize = new Size(340, 110);
				Label label = new Label();
				label.Text = prompt;
				label.SetBounds(12, 10, 316, 20);
				TextBox txt = new TextBox();
				txt.SetBounds(12, 34, 316, 22);
				txt.Text = defaultValue;
				txt.SelectAll();
				Button btnOk = new Button();
				btnOk.Text = "OK";
				btnOk.DialogResult = DialogResult.OK;
				btnOk.SetBounds(152, 68, 84, 26);
				Button btnCancel = new Button();
				btnCancel.Text = "Cancel";
				btnCancel.DialogResult = DialogResult.Cancel;
				btnCancel.SetBounds(244, 68, 84, 26);
				dlg.Controls.AddRange(new Control[] { label, txt, btnOk, btnCancel });
				dlg.AcceptButton = btnOk;
				dlg.CancelButton = btnCancel;
				dlg.ActiveControl = txt;
				if (dlg.ShowDialog(this) == DialogResult.OK)
				{
					result = txt.Text;
					return true;
				}
			}
			result = defaultValue;
			return false;
		}

		/// <summary>
		/// Saves the current settings under the root folder and project name shown in the text
		/// boxes, after validating both (focusing the offending box on failure).
		/// </summary>
		/// <returns>True when the project was written; false when validation failed, the user cancelled or saving failed.</returns>
		private bool SaveProject()
		{
			string root = txtRootFolder.Text.Trim();
			string projectName = txtProjectName.Text.Trim();
			if (root.Length == 0)
			{
				MessageBox.Show(this, "Enter a root folder first (double-click the box to browse).", "Save project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				txtRootFolder.Focus();
				return false;
			}
			string error = ProjectStore.ValidateName(projectName);
			if (error.Length > 0)
			{
				MessageBox.Show(this, error, "Save project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				txtProjectName.Focus();
				return false;
			}
			return SaveProjectTo(root, projectName);
		}

		/// <summary>
		/// Writes the scene, simulation, probe and source settings into the project
		/// <c>&lt;root&gt;\&lt;projectName&gt;</c> and makes it the current project. The target is the
		/// open project when it is that same folder; an existing project there after an overwrite
		/// confirmation; otherwise a newly created one (offering to create a missing root folder).
		/// A scene image from outside the target folder is copied in first
		/// (<see cref="PrepareSceneImageFor"/>).
		/// </summary>
		/// <returns>True when saved; false when the user declined a prompt or an error was shown.</returns>
		private bool SaveProjectTo(string root, string projectName)
		{
			try
			{
				string rootFolder = ProjectStore.NormalizeFolder(root);
				string projectFolder = Path.Combine(rootFolder, projectName);
				Project previous = _project;
				Project target;
				if (previous != null && string.Equals(projectFolder, previous.FolderPath, StringComparison.OrdinalIgnoreCase))
				{
					target = previous;
				}
				else if (ProjectStore.IsProjectFolder(projectFolder))
				{
					if (MessageBox.Show(this, "A project named '" + projectName + "' already exists in the root folder\r\n" + rootFolder + "\r\n\r\nOverwrite its configuration with the current settings?", "Save project", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
					{
						return false;
					}
					target = ProjectStore.Open(projectFolder);
				}
				else
				{
					if (!Directory.Exists(rootFolder))
					{
						if (MessageBox.Show(this, "The root folder does not exist:\r\n" + rootFolder + "\r\n\r\nCreate it?", "Save project", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
						{
							return false;
						}
					}
					target = ProjectStore.Create(rootFolder, projectName);
				}
				PrepareSceneImageFor(target, previous);
				_scene.ToConfig(target.Config);
				_simulation.ToConfig(target.Config);
				ProbeSettings.ListToConfig(_probes, target.Config);
				SourceSettings.ListToConfig(_sources, target.Config);
				target.Save();
				SetCurrentProject(target);
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The project could not be saved:\r\n\r\n" + ex.Message, "Save project", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return false;
			}
		}

		/// <summary>
		/// When saving into a different project than the scene image came from, copies the image
		/// into <paramref name="target"/>'s scene-image folder (unless it already lies inside the
		/// target) and rewrites <see cref="SceneSettings.ImageFile"/> as a path stored relative to
		/// the target. No-op when there is no scene image or the target is the previous project.
		/// This is the same copy that <see cref="btnLOadSceneImage_Click"/> already does when a project
		/// exists; this one catches an image that was picked before the (new) project was ever saved.
		/// </summary>
		/// <param name="target">The project about to be saved.</param>
		/// <param name="previous">The project the image path is currently relative to; null for an unsaved project.</param>
		private void PrepareSceneImageFor(Project target, Project previous)
		{
			if (_scene.ImageFile.Length == 0 || object.ReferenceEquals(target, previous))
			{
				return;
			}
			string previousFolder = ((previous == null) ? "" : previous.FolderPath);
			string imagePath = SceneSettings.ToFullPath(previousFolder, _scene.ImageFile);
			if (File.Exists(imagePath) && !Form1.IsInsideFolder(imagePath, target.FolderPath))
			{
				imagePath = Form1.CopyIntoSceneImageFolder(target, imagePath);
			}
			_scene.ImageFile = SceneSettings.ToStoredPath(target.FolderPath, imagePath);
		}

		/// <summary>Case-insensitive test whether <paramref name="fullPath"/> lies below <paramref name="folder"/> (both resolved to full paths); false for an empty folder.</summary>
		private static bool IsInsideFolder(string fullPath, string folder)
		{
			if (string.IsNullOrEmpty(folder))
			{
				return false;
			}
			string prefix = folder.TrimEnd(new char[]
			{
				Path.DirectorySeparatorChar,
				Path.AltDirectorySeparatorChar
			}) + Path.DirectorySeparatorChar.ToString();
			return Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(prefix), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Copies <paramref name="fullPath"/> into the project's <c>scene_images</c> folder. A file
		/// of the same name and the same length is assumed to be the same image and reused; one
		/// with a different length gets a " (2)", " (3)", ... suffix instead.
		/// </summary>
		/// <returns>Full path of the file inside the scene-image folder.</returns>
		private static string CopyIntoSceneImageFolder(Project project, string fullPath)
		{
			Directory.CreateDirectory(project.SceneImageFolder);
			string fileName = Path.GetFileName(fullPath);
			string destination = Path.Combine(project.SceneImageFolder, fileName);
			int suffix = 2;
			while (File.Exists(destination) && new FileInfo(destination).Length != new FileInfo(fullPath).Length)
			{
				destination = Path.Combine(project.SceneImageFolder, Path.GetFileNameWithoutExtension(fileName) + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")" + Path.GetExtension(fileName));
				suffix++;
			}
			if (!File.Exists(destination))
			{
				File.Copy(fullPath, destination);
			}
			return destination;
		}

		/// <summary>
		/// Before discarding the open project: offers Yes/No/Cancel when it has unsaved changes.
		/// </summary>
		/// <returns>True when the caller may proceed (nothing to save, saved successfully, or "No"); false on Cancel or a failed save.</returns>
		private bool ConfirmSaveProject()
		{
			// Nothing to save when no named project exists yet (a project only exists once it has been loaded from
			// the list or created and named through btnNewProject's dialog) - there is no such thing as an
			// "unsaved new project" to ask about any more.
			if (_project == null || !_projectDirty)
			{
				return true;
			}
			DialogResult answer = MessageBox.Show(this, "There are unsaved changes in the project '" + _project.Name + "'. Save them first?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
			if (answer == DialogResult.Yes)
			{
				return SaveProject();
			}
			return answer == DialogResult.No;
		}

		/// <summary>
		/// Resets the in-memory project state to blank defaults: new scene and simulation settings
		/// (keeping the damping-edge and object-source check boxes and the steps/decay spinners as
		/// they are), no scene image, default probes, no sources, no video files, not dirty.
		/// Does not touch <c>grpSceneProperties</c>/<c>grpSimulationSettings</c>/<c>grpProjects.Enabled</c> - a
		/// caller that goes on to make a real, named project current (<see cref="SetCurrentProject"/>) re-enables
		/// them there; a caller that ends up with no project at all (e.g. deleting the one that was open) should
		/// instead disable them, since - per the startup design - there is no such thing as an editable "new
		/// project" that does not exist on disk yet.
		/// </summary>
		private void StartNewProject()
		{
			_project = null;
			_scene = new SceneSettings();
			// chkDampingEdge and chkUseObjectSources have no software-chosen default of their own (see their
			// docs in SceneSettings) - a new project starts at whatever the controls' own Designer values are
			_scene.DampingEdgeEnabled = chkDampingEdge.Checked;
			_scene.UseObjectSources = chkUseObjectSources.Checked;
			ReplaceSceneImage(null);
			txtProjectName.Text = "";
			ShowSceneInControls();
			_simulation = new SimulationSettings();
			// numSimulationSteps and numTimeDecayFactor have no software-chosen default of their own (see their
			// docs in SimulationSettings) - a new project starts at whatever the controls' own Designer values are
			_simulation.Steps = Convert.ToInt32(numSimulationSteps.Value);
			_simulation.TimeDecayFactor = Convert.ToDouble(numTimeDecayFactor.Value);
			ShowSimulationInControls();
			ResetProbes();
			ResetSources();
			_fieldVideoFile = "";
			_intensityVideoFile = "";
			UpdateVideoButtons();
			_projectDirty = false;
			UpdateTitle();
			txtProjectName.Focus();
		}

		/// <summary>
		/// Makes <paramref name="opened"/> the current project: shows its root and name, persists
		/// them in the user settings, clears the Sources/Probes background pictures when the
		/// folder differs from the previous project, loads scene, simulation, probes and sources
		/// from its configuration, clears the dirty flag and refreshes the project list.
		/// Also called by <see cref="SaveProjectTo"/> for a plain "Save project" of the project that is already
		/// open (target and the previous <see cref="_project"/> are then the same folder) - the preview-clearing
		/// must only run when the project is actually changing, or every "Save configuration" would blank the
		/// Sources/Probes tab's picture (see <see cref="ClearPreviewImage"/>) even though nothing about that
		/// picture is out of date.
		/// </summary>
		private void SetCurrentProject(Project opened)
		{
			bool projectChanged = _project == null || !string.Equals(_project.FolderPath, opened.FolderPath, StringComparison.OrdinalIgnoreCase);
			_project = opened;
			txtRootFolder.Text = opened.RootFolder;
			txtProjectName.Text = opened.Name;
			UpdateProjectAccess();
			_settings.LastRootFolder = opened.RootFolder;
			_settings.LastProjectName = opened.Name;
			_settings.Save();
			if (projectChanged)
			{
				// a preview picture (Sources/Probes tab) loaded for the previous project has no relevance to this one -
				// clear it so the tab shows this project's own scene image (or a blank placeholder) the next time it is
				// visited, rather than a leftover picture from whatever was open before (AutoLoadSceneImageIfEmpty only
				// fills a picture that is empty, so a stale one left behind would otherwise never be replaced)
				ClearPreviewImage(_picSource);
				ClearPreviewImage(_picProbe);
			}
			LoadSceneFromProject();
			_simulation = SimulationSettings.FromConfig(_project.Config);
			ShowSimulationInControls();
			LoadProbesFromProject();
			LoadSourcesFromProject();
			_projectDirty = false;
			UpdateTitle();
			RefreshProjectList();
		}

		/// <summary>Removes and disposes the image of <paramref name="pic"/>; tolerates a null box or no image.</summary>
		private void ClearPreviewImage(PictureBox pic)
		{
			if (pic == null || pic.Image == null)
			{
				return;
			}
			Image image = pic.Image;
			pic.Image = null;
			image.Dispose();
			pic.Invalidate();
		}

		/// <summary>
		/// Flags the project as having unsaved changes and updates the title, unless one of the
		/// <c>_loading*</c> re-entrancy guards is set (controls are being filled from settings).
		/// </summary>
		private void MarkProjectChanged()
		{
			if (_loadingScene || _loadingSim || _loadingProbe || _loadingSource)
			{
				return;
			}
			_projectDirty = true;
			UpdateTitle();
		}

		/// <summary>Sets the window title to "2D wave simulation - name", with " *" when dirty and the run status appended while a simulation runs.</summary>
		private void UpdateTitle()
		{
			string projectLabel = ((_project == null) ? "(new project)" : _project.Name);
			Text = "2D wave simulation - " + projectLabel + (_projectDirty ? " *" : "") + ((_simStatus.Length > 0) ? ("   |   " + _simStatus) : "");
		}

		/// <summary>
		/// Lets the user choose a folder: type or paste a full path directly into the box, or click "Browse..."
		/// for the usual Windows tree picker (a <see cref="FolderBrowserDialog"/>) - rooted at "This PC" rather
		/// than the Desktop, so every drive (C:\, D:\, a network drive, anywhere) is one click away instead of
		/// buried under Desktop. This replaces a plain <see cref="FolderBrowserDialog"/> call, which on this
		/// machine shows no address bar to type a path into and makes other drives awkward to reach from its
		/// Desktop-rooted tree. OK is refused for an empty path, and for a non-existent one unless
		/// <paramref name="allowNewFolder"/> (a new, not-yet-created one is then fine - the caller decides
		/// whether/how to create it).
		/// </summary>
		/// <param name="description">Text shown above the path box and in the folder browser.</param>
		/// <param name="startFolder">Initial contents of the path box.</param>
		/// <param name="allowNewFolder">Accept a folder that does not exist yet (and show the "New folder" button in the browser).</param>
		/// <returns>The trimmed folder path, or an empty string when cancelled.</returns>
		private string BrowseForFolder(string description, string startFolder, bool allowNewFolder)
		{
			using (Form dlg = new Form())
			{
				dlg.Text = "Choose a folder";
				dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
				dlg.StartPosition = FormStartPosition.CenterParent;
				dlg.MinimizeBox = false;
				dlg.MaximizeBox = false;
				dlg.ShowInTaskbar = false;
				dlg.ClientSize = new Size(480, 124);

				Label label = new Label();
				label.Text = description;
				label.SetBounds(12, 10, 456, 34);

				TextBox txt = new TextBox();
				txt.SetBounds(12, 50, 362, 22);
				txt.Text = startFolder;

				Button btnBrowse = new Button();
				btnBrowse.Text = "Browse...";
				btnBrowse.SetBounds(380, 49, 88, 24);
				btnBrowse.Click += (sender, e) =>
				{
					using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
					{
						folderDialog.Description = description;
						folderDialog.ShowNewFolderButton = allowNewFolder;
						// MyComputer (not the default Desktop) so every drive is a top-level node, reachable in
						// one click instead of nested under Desktop/This PC
						folderDialog.RootFolder = Environment.SpecialFolder.MyComputer;
						if (Directory.Exists(txt.Text.Trim()))
						{
							folderDialog.SelectedPath = txt.Text.Trim();
						}
						if (folderDialog.ShowDialog(dlg) == DialogResult.OK)
						{
							txt.Text = folderDialog.SelectedPath;
							txt.SelectAll();
						}
					}
				};

				Button btnOk = new Button();
				btnOk.Text = "OK";
				btnOk.SetBounds(228, 86, 84, 26);
				btnOk.Click += (sender, e) =>
				{
					string folder = txt.Text.Trim();
					if (folder.Length == 0)
					{
						MessageBox.Show(dlg, "Enter or browse to a folder first.", "Choose a folder", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					}
					else if (!allowNewFolder && !Directory.Exists(folder))
					{
						MessageBox.Show(dlg, "This folder does not exist:\r\n" + folder, "Choose a folder", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					}
					else
					{
						dlg.DialogResult = DialogResult.OK;
						dlg.Close();
					}
				};

				Button btnCancel = new Button();
				btnCancel.Text = "Cancel";
				btnCancel.DialogResult = DialogResult.Cancel;
				btnCancel.SetBounds(320, 86, 84, 26);

				dlg.Controls.AddRange(new Control[] { label, txt, btnBrowse, btnOk, btnCancel });
				dlg.AcceptButton = btnOk;
				dlg.CancelButton = btnCancel;
				dlg.ActiveControl = txt;
				if (dlg.ShowDialog(this) == DialogResult.OK)
				{
					return txt.Text.Trim();
				}
			}
			return "";
		}

		// ----- Colors tab (Form1.Colors.cs) -----

		/// <summary>All schemes read from the folder in <c>txtSchemeFolder</c>; feeds the Colors tab list and the field/intensity scheme combos. Replaced wholesale by <see cref="LoadSchemeList"/>.</summary>
		private List<ColorScheme> _schemes;

		/// <summary>The scheme loaded into the editor (a clone of a list entry, or a new unsaved one); null until a scheme is loaded.</summary>
		private ColorScheme _current;

		/// <summary>True when <see cref="_current"/> has edits that are not saved to its file; checked again on closing.</summary>
		private bool _dirty;

		/// <summary>The scheme selected in <c>ListView1</c>, shown read-only in the editor and preview bar; null when nothing is selected.</summary>
		private ColorScheme _previewScheme;

		/// <summary>Index (0 to <see cref="ColorScheme.StopCount"/> - 1) of the colour stop of <see cref="_current"/> that the picker is editing; -1 when none.</summary>
		private int _selectedStop;

		/// <summary>Re-entrancy guard: set while <see cref="RefreshEditorState"/> programmatically checks the scheme-type radio buttons.</summary>
		private bool _updatingRadios;

		/// <summary>The nine "Color 1".."Color 9" buttons in stop order (filled in <see cref="Form1_Load"/>).</summary>
		private Button[] _stopButtons;

		/// <summary>The colour picker embedded in <c>Panel2</c> (created in <see cref="Form1_Load"/>).</summary>
		private ColorPickerControl _picker;

		/// <summary>Image list of the <see cref="ThumbWidth"/> x <see cref="ThumbHeight"/> scheme thumbnails shown in <c>ListView1</c>; the bitmaps themselves are owned by <see cref="_thumbBitmaps"/>.</summary>
		private readonly ImageList _thumbs;

		// ----- Project lists (Form1.Projects.cs / Form1.Output.cs) -----

		/// <summary>Sort column of the Settings-tab project list: 0 = name, 1 = last edited (default).</summary>
		private int _projectSortColumn;

		/// <summary>Sort direction of the Settings-tab project list; default descending (newest first).</summary>
		private bool _projectSortAscending;

		/// <summary>Sort column of the Manage-output project list: 0 = name, 1 = last edited (default).</summary>
		private int _outputProjectSortColumn;

		/// <summary>Sort direction of the Manage-output project list; default descending (newest first).</summary>
		private bool _outputProjectSortAscending;

		/// <summary>Sort column of the scheme list: 0 = name, 2 = type (default); column 1 ("Kind") is not sortable.</summary>
		private int _schemeSortColumn;

		/// <summary>Sort direction of the scheme list; default ascending.</summary>
		private bool _schemeSortAscending;

		/// <summary>Zoomed picture box filling <c>pnlPreview</c> on the Manage-output tab (created in <see cref="InitializeOutputsTab"/>); it owns its image.</summary>
		private PictureBox _outputPreviewPic;

		/// <summary>True when <see cref="_outputPreviewPic"/> shows a shell thumbnail of a video (such a picture cannot be shared to the Sources/Probes tabs).</summary>
		private bool _outputPreviewIsVideo;

		/// <summary>Sort column of the scene-image file list: 0 = file name, 1 = created date (default).</summary>
		private int _sceneSortColumn;

		/// <summary>Sort direction of the scene-image file list; default descending (newest first).</summary>
		private bool _sceneSortAscending;

		/// <summary>Sort column of the exported-image file list: 0 = file name, 1 = created date (default).</summary>
		private int _imageSortColumn;

		/// <summary>Sort direction of the exported-image file list; default descending (newest first).</summary>
		private bool _imageSortAscending;

		/// <summary>Sort column of the exported-video file list: 0 = file name, 1 = created date (default).</summary>
		private int _videoSortColumn;

		/// <summary>Sort direction of the exported-video file list; default descending (newest first).</summary>
		private bool _videoSortAscending;

		/// <summary>
		/// Owner of the thumbnail bitmaps added to <see cref="_thumbs"/>; disposed on every reload and on closing.
		/// The ImageList keeps using the bitmaps that were added to it (it re-creates its native image list from
		/// them, e.g. after Clear or when the ListView on a hidden tab is created). They must therefore stay
		/// alive until the list is rebuilt or the form is closed, and must not be disposed right after Add.
		/// </summary>
		private readonly List<Bitmap> _thumbBitmaps;

		/// <summary>Scheme thumbnail width in pixels (<see cref="_thumbs"/> image size and <see cref="MakeThumbnail"/>).</summary>
		private const int ThumbWidth = 120;

		/// <summary>Scheme thumbnail height in pixels (<see cref="_thumbs"/> image size and <see cref="MakeThumbnail"/>).</summary>
		private const int ThumbHeight = 16;

		// ----- Project state -----
		//
		// A project is a folder inside the root folder; the folder name is the project name (txtProjectName).
		// The main folder of a project holds configuration.json and the standard subfolders (see ProjectStore).
		//
		// The settings on the tabs are kept in memory and only written to configuration.json by "Save project"
		// and "Save as". _project is null for a new project that has not been saved yet.

		/// <summary>The open project; null while editing a new project that has never been saved.</summary>
		private Project _project;

		/// <summary>Per-user settings (project root, last project). A placeholder from the constructor until <see cref="InitializeProject"/> loads the real values.</summary>
		private AppSettings _settings;

		/// <summary>True when scene, simulation, probe or source settings differ from what is saved (title shows " *").</summary>
		private bool _projectDirty;

		/// <summary>Current phase of the <c>btnPickRoot</c> blink (true = highlighted).</summary>
		private bool _rootBlinkOn;

		/// <summary>
		/// 700 ms timer that blinks <c>btnPickRoot</c> light blue, slowly, while no project root is known yet - e.g.
		/// right after installing on a different computer, when <see cref="AppSettings.ProjectRoot"/> is still empty
		/// and nothing on the Sources/Probes/Viewer tabs works yet. <see cref="UpdateProjectAccess"/> starts/stops
		/// this alongside the rest of the "has a root" state; <see cref="RootBlinkTimer_Tick"/> does the actual blinking.
		/// </summary>
		private readonly global::System.Windows.Forms.Timer _rootBlinkTimer;

		// ----- Manage output tab (Form1.Output.cs) -----

		/// <summary>Lower-case extensions listed as scene images on the Manage-output tab.</summary>
		private static readonly string[] SceneImageExtensions = new string[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif" };

		/// <summary>Lower-case extensions listed as exported frames on the Manage-output tab.</summary>
		private static readonly string[] OutputImageExtensions = new string[] { ".png" };

		/// <summary>Lower-case extensions listed as exported videos on the Manage-output tab.</summary>
		private static readonly string[] OutputVideoExtensions = new string[] { ".mp4", ".avi" };

		/// <summary>Re-entrancy guard: set while <see cref="ClearOtherFileListSelections"/> clears the two inactive file lists, so their selection handlers return early.</summary>
		private bool _clearingFileListSelection;

		// ----- Scene (Form1.Scene.cs) -----

		/// <summary>The scene settings being edited (image or user-defined canvas, damping edge, image sources, refractive index); replaced on project load and new project.</summary>
		private SceneSettings _scene;

		/// <summary>Re-entrancy guard: set while scene controls are being filled from <see cref="_scene"/>, so their change handlers do not write back or mark the project dirty.</summary>
		private bool _loadingScene;

		/// <summary>The decoded scene image (owned; disposed by <see cref="ReplaceSceneImage"/>); null when none is loaded or the file was unreadable. Its size is the canvas size when <see cref="SceneSettings.UseImage"/> is set.</summary>
		private Bitmap _sceneImage;

		/// <summary>Placeholder bitmap shown in <c>picImagePreview</c> for a user-defined (empty) canvas; owned and replaced by <see cref="UpdateScenePreview"/>.</summary>
		private Bitmap _emptyPreview;

		/// <summary>Tooltip used to explain rejected canvas-size and damping-edge entries next to their text boxes.</summary>
		private readonly ToolTip _sceneTip;

		// ----- Simulation (Form1.Simulation.cs) -----

		/// <summary>The simulation settings being edited (steps, brightness/offset, export and preview flags, engine options, wave speed c); replaced on project load and new project.</summary>
		private SimulationSettings _simulation;

		/// <summary>Re-entrancy guard: set while simulation controls are filled from <see cref="_simulation"/>, and while a linked control on the other tab is mirrored silently.</summary>
		private bool _loadingSim;

		/// <summary>The active simulation runner; null when nothing is running. Its worker-thread events reach the form through <see cref="PostToUi"/>, and every callback compares the runner identity to ignore events from a stale run.</summary>
		private SimulationRunner _runner;

		/// <summary>Short run status shown in the window title ("starting...", "step n of m (p%)", "paused - ...", "stopping..."); empty when idle.</summary>
		private string _simStatus;

		/// <summary>Full path of the field video written by the last run; empty when none. Enables the "View field video" button.</summary>
		private string _fieldVideoFile;

		/// <summary>Full path of the intensity video written by the last run; empty when none. Enables the "View intensity video" button.</summary>
		private string _intensityVideoFile;

		/// <summary>Multi-line summary of the brightness, offset and image-source scale used by the last started run; appended to the status tooltip when the run finishes.</summary>
		private string _runInfo;

		/// <summary>True while the current run is paused with the Pause button.</summary>
		private bool _paused;

		/// <summary>True after Stop was pressed, until the runner reports that it has finished.</summary>
		private bool _stopRequested;

		/// <summary>Latest progress text, "step n of m (p%)"; <see cref="_simStatus"/> is derived from it.</summary>
		private string _progressText;

		/// <summary>Steps completed according to the latest progress report or run result; used in exported frame file names.</summary>
		private int _lastStepsDone;

		/// <summary>ARGB look-up table (from <see cref="ColorLut"/>) used to colour the field in the current or last run; null when the field view was not wanted. Used to render a frame while paused.</summary>
		private int[] _lastFieldLut;

		/// <summary>ARGB look-up table (from <see cref="ColorLut"/>) used to colour the intensity in the current or last run; null when the intensity view was not wanted. Used to render a frame while paused.</summary>
		private int[] _lastIntensityLut;

		/// <summary>Full canvas-size field frame delivered with the last run's <see cref="SimulationResult"/> (owned); null until a run has finished. Source for "Export frame" when not paused.</summary>
		private Bitmap _lastFullFieldFrame;

		/// <summary>Full canvas-size intensity frame delivered with the last run's <see cref="SimulationResult"/> (owned); null until a run has finished. Source for "Export frame" when not paused.</summary>
		private Bitmap _lastFullIntensityFrame;

		/// <summary>Live field preview box filling <c>pnlPreviewField</c> (created in <see cref="InitializeSimulationControls"/>). <c>Tag</c> holds the raw preview frame, <c>Image</c> that frame or its overlay composite (see <see cref="SetPreviewFrame"/>).</summary>
		private PictureBox _picField;

		/// <summary>Live intensity preview box filling <c>pnlPreviewIntensity</c> (created in <see cref="InitializeSimulationControls"/>). <c>Tag</c> holds the raw preview frame, <c>Image</c> that frame or its overlay composite (see <see cref="SetPreviewFrame"/>).</summary>
		private PictureBox _picIntensity;

		/// <summary>Sobel edge image of <see cref="_sceneImage"/> at canvas size (owned; see <see cref="ComputeGlowingEdges"/>); null when there is no scene image. Added on top of previews and exports as the "glowing edges" overlay.</summary>
		private Bitmap _glowEdgesImage;

		/// <summary>
		/// <see cref="_glowEdgesImage"/> resized to <see cref="_glowOverlayCacheSize"/>, whatever size a preview frame
		/// last asked for (<see cref="GetGlowOverlayResized"/>; owned). Rebuilding this by resampling the full
		/// scene-image-sized picture on every single visualized step of a run would be wasteful, so it is cached
		/// until either the target size or <see cref="_glowEdgesImage"/> itself changes. Null until first requested.
		/// </summary>
		private Bitmap _glowOverlayCache;

		/// <summary>Pixel size of <see cref="_glowOverlayCache"/>.</summary>
		private Size _glowOverlayCacheSize;

		/// <summary>Background picture box on the Sources tab (filling <c>pnlSourceImage</c>). Receives copies of the scene, field or intensity picture via the share buttons; source markers are painted on top and clicks pick a source position.</summary>
		private PictureBox _picSource;

		/// <summary>Background picture box on the Probes tab (filling <c>pnlProbeImage</c>). Receives copies of the scene, field or intensity picture via the share buttons; probe markers and graph boxes are painted on top and clicks pick probe positions.</summary>
		private PictureBox _picProbe;

		/// <summary>Set once any simulation has finished (originally meant to turn the stop button into a restart button). Not read anywhere at present.</summary>
		private bool _hasRun;

		// Windows Explorer's own view-mode numbers (the classic Shell "FOLDERVIEWMODE" enum), used by
		// OpenFolderInExplorer and its callers.

		/// <summary>Explorer <c>CurrentViewMode</c> value for the "Large icons" view (FOLDERVIEWMODE <c>FVM_ICON</c>).</summary>
		private const int FVM_ICON = 1;

		/// <summary>Explorer <c>CurrentViewMode</c> value for the "Details" view (FOLDERVIEWMODE <c>FVM_DETAILS</c>).</summary>
		private const int FVM_DETAILS = 4;

		// ----- Probes (Form1.Probes.cs) -----

		/// <summary>Number of probes the UI supports.</summary>
		private const int ProbeCount = 2;

		/// <summary>Probe settings being edited, one per probe panel (always exactly two after <see cref="NormalizeProbeCount"/>); replaced on project load and new project.</summary>
		private List<ProbeSettings> _probes;

		/// <summary>The designer controls of each probe panel, indexed by probe (built in <see cref="InitializeProbeControls"/>); null before that.</summary>
		private Form1.ProbeControlSet[] _probeUi;

		/// <summary>Re-entrancy guard: set while probe controls are filled from <see cref="_probes"/>, so their change handlers do not write back or mark the project dirty.</summary>
		private bool _loadingProbe;

		/// <summary>State of position picking on <see cref="_picProbe"/>: 0 = not picking, 1 = waiting for position A, 2 = position B (line probes only), 3 = top-left corner of the graph box.</summary>
		private int _pickStep;

		/// <summary>Index of the probe whose positions are being picked; meaningful only while <see cref="_pickStep"/> is non-zero.</summary>
		private int _pickIndex;

		/// <summary>Last mouse position (picture-box pixels) over <see cref="_picProbe"/> during pick step 3, used to draw the graph box where it would land; null otherwise.</summary>
		private Point? _pickHover;

		/// <summary>Copies of the probe settings handed to the running or last run; preview markers and graphs are drawn from these (with display settings copied from <see cref="_probes"/>). Null before the first run.</summary>
		private List<ProbeSettings> _runProbes;

		/// <summary>Canvas size (pixels) of the running or last run, used to map probe positions onto the zoomed previews.</summary>
		private Size _runCanvas;

		/// <summary>Total step count of the running or last run (x-axis of full-scale probe graphs).</summary>
		private int _runSteps;

		/// <summary>Per-probe samples and line profiles collected during the running or last run; cleared by <see cref="BeginProbeRun"/>, appended on the UI thread in <see cref="ShowProbeSamples"/>.</summary>
		private readonly List<ProbeTrace> _probeTraces;

		// ----- Sources (Form1.Sources.cs) -----

		/// <summary>The project's object sources, in list order; replaced on project load and new project.</summary>
		private List<SourceSettings> _sources;

		/// <summary>Index in <see cref="_sources"/> of the source the editor is bound to; -1 while editing a new, not yet saved source.</summary>
		private int _currentSourceIndex;

		/// <summary>Working copy the editor controls are bound to (a clone of the selected source, or a fresh one); stored back into <see cref="_sources"/> by <see cref="CommitSourceEdits"/>.</summary>
		private SourceSettings _editing;

		/// <summary>Re-entrancy guard: set while source controls are filled from <see cref="_editing"/>, so they do not mark the source dirty.</summary>
		private bool _loadingSource;

		/// <summary>Re-entrancy guard: set while <c>lstSources</c>'s selection is changed programmatically, so the selection handler does not reload the editor.</summary>
		private bool _syncingSourceList;

		/// <summary>True while a click on <see cref="_picSource"/> is expected to set the source position.</summary>
		private bool _sourcePicking;

		/// <summary>Original caption of <c>btnPickSourcePosition</c>, restored after it has shown "Cancel" during picking.</summary>
		private string _pickSourceButtonText;

		/// <summary>Random generator used when composite sources generate randomised elements.</summary>
		private readonly Random _sourceRandom;

		/// <summary>
		/// <c>btnSaveSource</c>'s own Designer back colour, captured once at startup so <see cref="ClearSourceDirty"/>
		/// can restore exactly that (rather than a hardcoded colour) once the pending changes are saved; the button
		/// turns orange while the editor has unsaved edits.
		/// </summary>
		private Color _sourceSaveButtonDefaultBackColor;

		/// <summary>One row of the project list views (stored in the item's <c>Tag</c>).</summary>
		private sealed class ProjectListEntry
		{
			public ProjectListEntry(string name, DateTime lastModified)
			{
				Name = name;
				LastModified = lastModified;
			}

			/// <summary>Project (folder) name.</summary>
			public readonly string Name;

			/// <summary>Last write time of the project's configuration file, or of the folder when the file is missing (local time).</summary>
			public readonly DateTime LastModified;
		}

		/// <summary>Win32 <c>SIZE</c> structure passed to <see cref="IShellItemImageFactory.GetImage"/>.</summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct NativeSize
		{
			/// <summary>Width in pixels.</summary>
			public int cx;

			/// <summary>Height in pixels.</summary>
			public int cy;
		}

		/// <summary>Subset of the shell's <c>SIIGBF</c> flags for <see cref="IShellItemImageFactory.GetImage"/>.</summary>
		[Flags]
		private enum ThumbnailOptions
		{
			/// <summary>No options.</summary>
			None = 0,
			/// <summary><c>SIIGBF_BIGGERSIZEOK</c>: a larger image than requested is acceptable.</summary>
			BiggerSizeOk = 1,
			/// <summary><c>SIIGBF_THUMBNAILONLY</c>: return a thumbnail only, never an icon.</summary>
			ThumbnailOnly = 8
		}

		/// <summary>COM interface used to ask the Windows shell for a file's thumbnail (video previews on the Manage-output tab).</summary>
		[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[ComImport]
		private interface IShellItemImageFactory
		{
			/// <summary>Returns a GDI bitmap handle of the requested size; the caller owns it and must release it with <c>DeleteObject</c>. Throws on failure (PreserveSig is off).</summary>
			void GetImage([In] Form1.NativeSize size, [In] Form1.ThumbnailOptions flags, out IntPtr phbm);
		}

		/// <summary>
		/// Bundles the designer controls of one probe panel so the two identical panels share the
		/// same handler code (see <see cref="InitializeProbeControls"/>). All members are set once
		/// in the constructor.
		/// </summary>
		private sealed class ProbeControlSet
		{
			public ProbeControlSet(int index, string title, CheckBox chkOn, ComboBox cmbPlaceOn, ComboBox cmbMeasurement, ComboBox cmbProbeType, ComboBox cmbBoxSize, NumericUpDown numAPosX, NumericUpDown numAPosY, NumericUpDown numBPosX, NumericUpDown numBPosY, Button butPick, RadioButton radRunning, RadioButton radFull, CheckBox chkAutoX, CheckBox chkAutoY, TextBox txtMinY, TextBox txtMaxY, CheckBox chkStartAfter, TextBox txtStartAfter, NumericUpDown numRunningWindow, Control[] bOnlyControls)
			{
				Index = index;
				Title = title;
				ChkOn = chkOn;
				CmbPlaceOn = cmbPlaceOn;
				CmbMeasurement = cmbMeasurement;
				CmbProbeType = cmbProbeType;
				CmbBoxSize = cmbBoxSize;
				NumAPosX = numAPosX;
				NumAPosY = numAPosY;
				NumBPosX = numBPosX;
				NumBPosY = numBPosY;
				ButPick = butPick;
				RadRunning = radRunning;
				RadFull = radFull;
				ChkAutoX = chkAutoX;
				ChkAutoY = chkAutoY;
				TxtMinY = txtMinY;
				TxtMaxY = txtMaxY;
				ChkStartAfter = chkStartAfter;
				TxtStartAfter = txtStartAfter;
				NumRunningWindow = numRunningWindow;
				BOnlyControls = bOnlyControls;
				PickButtonText = butPick.Text;
			}

			/// <summary>Probe index (0 or 1), also the index into <see cref="_probes"/>.</summary>
			public readonly int Index;

			/// <summary>Display name ("Probe 1" / "Probe 2") used in status messages.</summary>
			public readonly string Title;

			/// <summary>"Probe on" check box.</summary>
			public readonly CheckBox ChkOn;

			/// <summary>Picture the probe samples: Field or Intensity.</summary>
			public readonly ComboBox CmbPlaceOn;

			/// <summary>Measured quantity (depends on <see cref="CmbPlaceOn"/>): amplitude, intensity or integrated intensity.</summary>
			public readonly ComboBox CmbMeasurement;

			/// <summary>Probe kind: point (A) or line (A-B).</summary>
			public readonly ComboBox CmbProbeType;

			/// <summary>Size preset of the graph box drawn on the previews.</summary>
			public readonly ComboBox CmbBoxSize;

			/// <summary>X of position A in canvas pixels.</summary>
			public readonly NumericUpDown NumAPosX;

			/// <summary>Y of position A in canvas pixels.</summary>
			public readonly NumericUpDown NumAPosY;

			/// <summary>X of position B (line probes) in canvas pixels.</summary>
			public readonly NumericUpDown NumBPosX;

			/// <summary>Y of position B (line probes) in canvas pixels.</summary>
			public readonly NumericUpDown NumBPosY;

			/// <summary>"Pick" button that starts/cancels picking positions on the Probes picture.</summary>
			public readonly Button ButPick;

			/// <summary>Running-scale radio button (graph shows a recent window of steps).</summary>
			public readonly RadioButton RadRunning;

			/// <summary>Full-scale radio button (graph shows the whole run).</summary>
			public readonly RadioButton RadFull;

			/// <summary>Auto-scale X check box.</summary>
			public readonly CheckBox ChkAutoX;

			/// <summary>Auto-scale Y check box; when off, <see cref="TxtMinY"/>/<see cref="TxtMaxY"/> apply.</summary>
			public readonly CheckBox ChkAutoY;

			/// <summary>Manual Y-axis minimum (invariant-culture number).</summary>
			public readonly TextBox TxtMinY;

			/// <summary>Manual Y-axis maximum (invariant-culture number).</summary>
			public readonly TextBox TxtMaxY;

			/// <summary>"Start recording at step #" check box.</summary>
			public readonly CheckBox ChkStartAfter;

			/// <summary>Step number at which recording starts (integer text).</summary>
			public readonly TextBox TxtStartAfter;

			/// <summary>Width of the running-scale window in steps.</summary>
			public readonly NumericUpDown NumRunningWindow;

			/// <summary>Controls that are only enabled for line probes (the position-B spinners and their labels).</summary>
			public readonly Control[] BOnlyControls;

			/// <summary>Original caption of <see cref="ButPick"/>, restored after it has shown "Cancel".</summary>
			public readonly string PickButtonText;
		}
	}
}
