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
			base.Load += this.Form1_Load;
			base.FormClosed += this.Form1_FormClosed;
			base.FormClosing += this.Form1_FormClosing;
			this._schemes = new List<ColorScheme>();
			this._selectedStop = -1;
			this._thumbs = new ImageList();
			this._projectSortColumn = 1;
			this._projectSortAscending = false;
			this._outputProjectSortColumn = 1;
			this._outputProjectSortAscending = false;
			this._schemeSortColumn = 2;
			this._schemeSortAscending = true;
			this._sceneSortColumn = 1;
			this._sceneSortAscending = false;
			this._imageSortColumn = 1;
			this._imageSortAscending = false;
			this._videoSortColumn = 1;
			this._videoSortAscending = false;
			this._thumbBitmaps = new List<Bitmap>();
			this._settings = new AppSettings();
			this._rootBlinkTimer = new global::System.Windows.Forms.Timer
			{
				Interval = 700
			};
			this._rootBlinkTimer.Tick += this.RootBlinkTimer_Tick;
			this._scene = new SceneSettings();
			this._sceneTip = new ToolTip();
			this._simulation = new SimulationSettings();
			this._simStatus = "";
			this._fieldVideoFile = "";
			this._intensityVideoFile = "";
			this._runInfo = "";
			this._progressText = "";
			this._probes = Form1.NewProbeList();
			this._probeTraces = new List<ProbeTrace>();
			this._sources = new List<SourceSettings>();
			this._currentSourceIndex = -1;
			this._editing = new SourceSettings();
			this._pickSourceButtonText = "Pick";
			this._sourceRandom = new Random();
			this.InitializeComponent();
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
			this._stopButtons = new Button[] { this.btnColor1, this.btnCOlor2, this.btnColor3, this.btnColor4, this.btnColor5, this.btnColor6, this.btnColor7, this.btnColor8, this.btnColor9 };
			Debug.Assert(this._stopButtons.Length == 9, "The number of Color buttons must equal ColorScheme.StopCount.");
			foreach (Button button in this._stopButtons)
			{
				button.UseVisualStyleBackColor = false;
				button.FlatStyle = FlatStyle.Flat;
				button.FlatAppearance.BorderSize = 1;
				button.FlatAppearance.BorderColor = Color.Black;
			}
			this._thumbs.ImageSize = new Size(120, 16);
			this._thumbs.ColorDepth = ColorDepth.Depth32Bit;
			this.ListView1.View = View.Details;
			this.ListView1.FullRowSelect = true;
			this.ListView1.MultiSelect = false;
			this.ListView1.HideSelection = false;
			this.ListView1.HeaderStyle = ColumnHeaderStyle.Clickable;
			this.ListView1.SmallImageList = this._thumbs;
			this.ListView1.Columns.Clear();
			this.ListView1.Columns.Add("Scheme", 250);
			this.ListView1.Columns.Add("Kind", 55);
			this.ListView1.Columns.Add("Type", 62);
			this.UpdateSchemeListSortHeaders();
			this.pnlScheme.BorderStyle = BorderStyle.FixedSingle;
			this.Panel2.BorderStyle = BorderStyle.FixedSingle;
			this._picker = new ColorPickerControl();
			this._picker.Dock = DockStyle.Fill;
			this.Panel2.Controls.Add(this._picker);
			this._picker.ColorChanged += this.Picker_ColorChanged;
			this.txtCurrentColorScheme.ReadOnly = true;
			this.tabMainFunctions.DrawMode = TabDrawMode.OwnerDrawFixed;
			this.UpdateProjectListSortHeaders();
			this.txtSchemeFolder.Text = ColorSchemeStore.FindDefaultFolder();
			if (Directory.Exists(this.txtSchemeFolder.Text))
			{
				this.LoadSchemeList();
			}
			this.RefreshEditorState();
			this.lblInfo.ForeColor = Color.Blue;
			this.lblInfo.Cursor = Cursors.Hand;
			this.InitializeSceneControls();
			this.InitializeSimulationControls();
			this.InitializeProbeControls();
			this.InitializeSourceControls();
			// The designer places btnPickRoot inside grpProjects, but UpdateProjectAccess disables
			// that group while no root is chosen - which is exactly when the button is needed.
			// Move it to the tab page at the same screen position so it stays enabled.
			Point location = this.grpProjects.Location;
			location.Offset(this.btnPickRoot.Location);
			this.grpProjects.Controls.Remove(this.btnPickRoot);
			this.tabConfiguration.Controls.Add(this.btnPickRoot);
			this.btnPickRoot.Location = location;
			this.btnPickRoot.BringToFront();
			this.InitializeProject();
			this.InitializeOutputsTab();
			this.HookNumericUpDownEnterKey(this);
		}

		/// <summary>Recursively attaches <see cref="NumericUpDown_KeyDown"/> to every <see cref="NumericUpDown"/> below <paramref name="root"/>.</summary>
		private void HookNumericUpDownEnterKey(Control root)
		{
			foreach (Control control in root.Controls)
			{
				NumericUpDown numericUpDown = control as NumericUpDown;
				if (numericUpDown != null)
				{
					numericUpDown.KeyDown += this.NumericUpDown_KeyDown;
				}
				if (control.HasChildren)
				{
					this.HookNumericUpDownEnterKey(control);
				}
			}
		}

		/// <summary>Enter in a spinner commits its value by moving focus to the next control (and suppresses the beep).</summary>
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
			this.ClearPreviews();
			foreach (Bitmap bitmap in this._thumbBitmaps)
			{
				bitmap.Dispose();
			}
			this._thumbBitmaps.Clear();
			this.ReplaceSceneImage(null);
			this.DisposeEmptyPreview();
			this.ClearOutputPreview();
		}

		/// <summary>
		/// Vetoes closing when the user declines the save-project prompt, declines stopping a
		/// running simulation (otherwise it is cancelled and given up to 15 s to exit), or
		/// declines discarding unsaved colour-scheme edits.
		/// </summary>
		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!this.ConfirmSaveProject())
			{
				e.Cancel = true;
				return;
			}
			if (this._runner != null)
			{
				if (MessageBox.Show(this, "A simulation is running. Stop it and close the program?", "Simulation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					e.Cancel = true;
					return;
				}
				this._runner.Cancel();
				this._runner.WaitForExit(15000);
			}
			if (this._dirty)
			{
				if (MessageBox.Show(this, "The current color scheme has unsaved changes. Close anyway?", "Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
				{
					e.Cancel = true;
				}
			}
		}

		/// <summary>
		/// Enables the project list and output tab only when a project root is chosen (and blinks
		/// <c>btnPickRoot</c> while none is), and the editing tabs, buttons and groups only when
		/// a project is open. Repaints the tab headers so disabled tabs grey out.
		/// </summary>
		private void UpdateProjectAccess()
		{
			bool hasRoot = this._settings.ProjectRoot.Length > 0;
			bool hasProject = this._project != null;
			this.grpProjects.Enabled = hasRoot;
			this.tabOutput.Enabled = hasRoot;
			if (hasRoot)
			{
				this._rootBlinkTimer.Stop();
				this._rootBlinkOn = false;
				this.btnPickRoot.UseVisualStyleBackColor = true;
			}
			else
			{
				this._rootBlinkTimer.Start();
			}
			this.grpSceneProperties.Enabled = hasProject;
			this.grpSimulationSettings.Enabled = hasProject;
			this.tabSourceList.Enabled = hasProject;
			this.tabProbeList.Enabled = hasProject;
			this.tabViewer.Enabled = hasProject;
			this.btnGotoPreview.Enabled = hasProject;
			this.btnGotoProbes.Enabled = hasProject;
			this.btnGotoSources.Enabled = hasProject;
			this.grpSimulationControlButtons.Enabled = hasProject;
			this.grpShareImage.Enabled = hasProject;
			this.btnSaveConfiguration.Enabled = hasProject;
			this.btnSaveConfiguration2.Enabled = hasProject;
			this.btnSaveAs.Enabled = hasProject;
			this.btnDescription.Enabled = hasProject;
			this.tabMainFunctions.Invalidate();
		}

		/// <summary>Every 700 ms while no root is chosen: toggles <c>btnPickRoot</c> between light blue and the normal button colour.</summary>
		private void RootBlinkTimer_Tick(object sender, EventArgs e)
		{
			this._rootBlinkOn = !this._rootBlinkOn;
			this.btnPickRoot.UseVisualStyleBackColor = false;
			this.btnPickRoot.BackColor = (this._rootBlinkOn ? Color.LightBlue : SystemColors.Control);
		}

		/// <summary>Refuses to switch to a tab that <see cref="UpdateProjectAccess"/> disabled and tells the user to open a project first.</summary>
		private void tabMainFunctions_Selecting(object sender, TabControlCancelEventArgs e)
		{
			if (!e.TabPage.Enabled)
			{
				e.Cancel = true;
				MessageBox.Show(this, "Please open or create a project first.", "No project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}

		/// <summary>Owner-draws a tab header: window colour for the selected tab, control colour otherwise, silver text when the page is disabled.</summary>
		private void tabMainFunctions_DrawItem(object sender, DrawItemEventArgs e)
		{
			TabPage tabPage = this.tabMainFunctions.TabPages[e.Index];
			Rectangle tabRect = this.tabMainFunctions.GetTabRect(e.Index);
			Color backColor = ((e.Index == this.tabMainFunctions.SelectedIndex) ? SystemColors.Window : SystemColors.Control);
			using (SolidBrush brush = new SolidBrush(backColor))
			{
				e.Graphics.FillRectangle(brush, tabRect);
			}
			Color textColor = (tabPage.Enabled ? SystemColors.ControlText : Color.Silver);
			TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
			TextRenderer.DrawText(e.Graphics, tabPage.Text, this.tabMainFunctions.Font, tabRect, textColor, flags);
		}

		/// <summary>
		/// Replaces the placeholder <see cref="_settings"/> with the values read from disk, shows
		/// the stored project root, and refreshes access state, title and project list. No
		/// project is opened automatically.
		/// </summary>
		private void InitializeProject()
		{
			this._settings = AppSettings.Load();
			this.txtRootFolder.Text = this._settings.ProjectRoot;
			this.txtProjectName.Text = "";
			this.UpdateProjectAccess();
			this.UpdateTitle();
			this.RefreshProjectList();
		}

		/// <summary>File &gt; Open: asks for an existing project folder, offers to save the current project, then opens the chosen one.</summary>
		private void OpenProject_Click(object sender, EventArgs e)
		{
			try
			{
				string folder = this.BrowseForFolder("Choose the main folder of the project (the folder that contains configuration.json)", this.txtRootFolder.Text.Trim(), false);
				if (folder.Length > 0 && this.ConfirmSaveProject())
				{
					this.SetCurrentProject(ProjectStore.Open(folder));
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
			this.SaveProject();
		}

		/// <summary>"Save as": asks for a new project name and saves the current settings as that project in the current root folder.</summary>
		private void SaveAs_Click(object sender, EventArgs e)
		{
			string root = this.txtRootFolder.Text.Trim();
			if (root.Length == 0)
			{
				MessageBox.Show(this, "Enter a root folder first (double-click the box to browse).", "Save project as", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			string projectName = this.txtProjectName.Text.Trim();
			if (!this.PromptForText("Save project as", "New project name (saved in the current root folder):", projectName, ref projectName))
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
			this.SaveProjectTo(root, projectName);
		}

		/// <summary>Opens <see cref="frmDescription"/> for the open project and, on OK, writes the new description to disk at once (without touching <see cref="_projectDirty"/>).</summary>
		private void btnDescription_Click(object sender, EventArgs e)
		{
			if (this._project == null)
			{
				return;
			}
			using (frmDescription dialog = new frmDescription())
			{
				dialog.DescriptionText = this._project.Config.Description;
				if (dialog.ShowDialog(this) == DialogResult.OK)
				{
					this._project.Config.Description = dialog.DescriptionText;
					this._project.Save();
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
		/// File &gt; Choose project root (also the Browse/Pick root buttons and a double-click on the
		/// root text box): asks for a folder, stores it in the user settings immediately and
		/// refreshes the project list. The open project, if any, is left as it is.
		/// </summary>
		private void ChooseProjectRootToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string startFolder = ((this._settings.ProjectRoot.Length > 0) ? this._settings.ProjectRoot : this.txtRootFolder.Text.Trim());
			string folder = this.BrowseForFolder("Choose the root folder that contains your project folders", startFolder, true);
			if (folder.Length == 0)
			{
				return;
			}
			this._settings.ProjectRoot = folder;
			this._settings.Save();
			this.txtRootFolder.Text = folder;
			this.UpdateProjectAccess();
			this.RefreshProjectList();
		}

		/// <summary>Clicking the "(Info)" label shows the explanation of 4th-order processing, dispersion and the c-value.</summary>
		private void lblInfo_Click(object sender, EventArgs e)
		{
			MessageBox.Show(this, "The standard 3rd order Laplacian processing inherently leads to large dispersion, meaning that longer wavelengths propagate faster through the medium. Choosing 4th order processing (including sub-step sampling) reduces this effect significantly. However, processing is somewhat slower. Another effect of 4th order processing is that it requires a different propagation speed (c). The value can be specified per project and set in the File menu. For 4th order processing choose 1.00 for c. For standard processing, this value can be quite a bit different. For example, with standard processing for waves with a frequency of 100, c = 0.56, leading in this case to a wavelength of only 9 pixels (instead of 15). Because of the dispersion present in standard processing, the exact value of c is strongly dependent on the frequency. (lower frequencies -> higher c value; higher frequencies -> lower c value).", "4th order Laplacian and wave speed (c)", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}

		/// <summary>
		/// File &gt; Set c-value: prompts for the wave speed (pixels per step), stores it in
		/// <see cref="SimulationSettings.WaveSpeedC"/>, marks the project changed if it differs,
		/// and refreshes the wavelength read-out on the Sources tab.
		/// </summary>
		private void SetCValueToolStripMenuItem_Click(object sender, EventArgs e)
		{
			double cValue = 0.0;
			if (!this.PromptForCValue(this._simulation.WaveSpeedC, ref cValue))
			{
				return;
			}
			if (cValue != this._simulation.WaveSpeedC)
			{
				this._simulation.WaveSpeedC = cValue;
				this.MarkProjectChanged();
			}
			this.UpdateLambdaLabel();
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
			string root = this.txtRootFolder.Text.Trim();
			string projectName = this.txtProjectName.Text.Trim();
			if (root.Length == 0)
			{
				MessageBox.Show(this, "Enter a root folder first (double-click the box to browse).", "Save project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				this.txtRootFolder.Focus();
				return false;
			}
			string error = ProjectStore.ValidateName(projectName);
			if (error.Length > 0)
			{
				MessageBox.Show(this, error, "Save project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				this.txtProjectName.Focus();
				return false;
			}
			return this.SaveProjectTo(root, projectName);
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
				Project previous = this._project;
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
				this.PrepareSceneImageFor(target, previous);
				this._scene.ToConfig(target.Config);
				this._simulation.ToConfig(target.Config);
				ProbeSettings.ListToConfig(this._probes, target.Config);
				SourceSettings.ListToConfig(this._sources, target.Config);
				target.Save();
				this.SetCurrentProject(target);
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
		/// </summary>
		/// <param name="target">The project about to be saved.</param>
		/// <param name="previous">The project the image path is currently relative to; null for an unsaved project.</param>
		private void PrepareSceneImageFor(Project target, Project previous)
		{
			if (this._scene.ImageFile.Length == 0 || object.ReferenceEquals(target, previous))
			{
				return;
			}
			string previousFolder = ((previous == null) ? "" : previous.FolderPath);
			string imagePath = SceneSettings.ToFullPath(previousFolder, this._scene.ImageFile);
			if (File.Exists(imagePath) && !Form1.IsInsideFolder(imagePath, target.FolderPath))
			{
				imagePath = Form1.CopyIntoSceneImageFolder(target, imagePath);
			}
			this._scene.ImageFile = SceneSettings.ToStoredPath(target.FolderPath, imagePath);
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
			if (this._project == null || !this._projectDirty)
			{
				return true;
			}
			DialogResult answer = MessageBox.Show(this, "There are unsaved changes in the project '" + this._project.Name + "'. Save them first?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
			if (answer == DialogResult.Yes)
			{
				return this.SaveProject();
			}
			return answer == DialogResult.No;
		}

		/// <summary>
		/// Resets the editor to a fresh, unsaved project: new scene and simulation settings
		/// (keeping the damping-edge and object-source check boxes and the steps/decay spinners as
		/// they are), no scene image, default probes, no sources, no video files, not dirty.
		/// </summary>
		private void StartNewProject()
		{
			this._project = null;
			this._scene = new SceneSettings();
			this._scene.DampingEdgeEnabled = this.chkDampingEdge.Checked;
			this._scene.UseObjectSources = this.chkUseObjectSources.Checked;
			this.ReplaceSceneImage(null);
			this.txtProjectName.Text = "";
			this.ShowSceneInControls();
			this._simulation = new SimulationSettings();
			this._simulation.Steps = Convert.ToInt32(this.numSimulationSteps.Value);
			this._simulation.TimeDecayFactor = Convert.ToDouble(this.numTimeDecayFactor.Value);
			this.ShowSimulationInControls();
			this.ResetProbes();
			this.ResetSources();
			this._fieldVideoFile = "";
			this._intensityVideoFile = "";
			this.UpdateVideoButtons();
			this._projectDirty = false;
			this.UpdateTitle();
			this.txtProjectName.Focus();
		}

		/// <summary>
		/// Makes <paramref name="opened"/> the current project: shows its root and name, persists
		/// them in the user settings, clears the Sources/Probes background pictures when the
		/// folder differs from the previous project, loads scene, simulation, probes and sources
		/// from its configuration, clears the dirty flag and refreshes the project list.
		/// </summary>
		private void SetCurrentProject(Project opened)
		{
			bool projectChanged = this._project == null || !string.Equals(this._project.FolderPath, opened.FolderPath, StringComparison.OrdinalIgnoreCase);
			this._project = opened;
			this.txtRootFolder.Text = opened.RootFolder;
			this.txtProjectName.Text = opened.Name;
			this.UpdateProjectAccess();
			this._settings.LastRootFolder = opened.RootFolder;
			this._settings.LastProjectName = opened.Name;
			this._settings.Save();
			if (projectChanged)
			{
				this.ClearPreviewImage(this._picSource);
				this.ClearPreviewImage(this._picProbe);
			}
			this.LoadSceneFromProject();
			this._simulation = SimulationSettings.FromConfig(this._project.Config);
			this.ShowSimulationInControls();
			this.LoadProbesFromProject();
			this.LoadSourcesFromProject();
			this._projectDirty = false;
			this.UpdateTitle();
			this.RefreshProjectList();
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
			if (this._loadingScene || this._loadingSim || this._loadingProbe || this._loadingSource)
			{
				return;
			}
			this._projectDirty = true;
			this.UpdateTitle();
		}

		/// <summary>Sets the window title to "2D wave simulation - name", with " *" when dirty and the run status appended while a simulation runs.</summary>
		private void UpdateTitle()
		{
			string projectName = ((this._project == null) ? "(new project)" : this._project.Name);
			this.Text = "2D wave simulation - " + projectName + (this._projectDirty ? " *" : "") + ((this._simStatus.Length > 0) ? ("   |   " + this._simStatus) : "");
		}

		/// <summary>
		/// Shows a hand-built modal dialog with a path text box, a "Browse..." button (a
		/// <see cref="FolderBrowserDialog"/> rooted at My Computer) and OK/Cancel. OK is refused
		/// for an empty path, and for a non-existent one unless <paramref name="allowNewFolder"/>.
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

		/// <summary>Index (0..8) of the colour stop of <see cref="_current"/> that the picker is editing; -1 when none.</summary>
		private int _selectedStop;

		/// <summary>Re-entrancy guard: set while <see cref="RefreshEditorState"/> programmatically checks the scheme-type radio buttons.</summary>
		private bool _updatingRadios;

		/// <summary>The nine "Color 1".."Color 9" buttons in stop order (filled in <see cref="Form1_Load"/>).</summary>
		private Button[] _stopButtons;

		/// <summary>The colour picker embedded in <c>Panel2</c> (created in <see cref="Form1_Load"/>).</summary>
		private ColorPickerControl _picker;

		/// <summary>Image list of the 120 x 16 scheme thumbnails shown in <c>ListView1</c>; the bitmaps themselves are owned by <see cref="_thumbBitmaps"/>.</summary>
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

		/// <summary>Owner of the thumbnail bitmaps added to <see cref="_thumbs"/>; disposed on every reload and on closing.</summary>
		private readonly List<Bitmap> _thumbBitmaps;

		/// <summary>Nominal scheme thumbnail width in pixels. Not referenced at present: the Colors part uses the literal 120.</summary>
		private const int ThumbWidth = 120;

		/// <summary>Nominal scheme thumbnail height in pixels. Not referenced at present: the Colors part uses the literal 16.</summary>
		private const int ThumbHeight = 16;

		// ----- Project state -----

		/// <summary>The open project; null while editing a new project that has never been saved.</summary>
		private Project _project;

		/// <summary>Per-user settings (project root, last project). A placeholder from the constructor until <see cref="InitializeProject"/> loads the real values.</summary>
		private AppSettings _settings;

		/// <summary>True when scene, simulation, probe or source settings differ from what is saved (title shows " *").</summary>
		private bool _projectDirty;

		/// <summary>Current phase of the <c>btnPickRoot</c> blink (true = highlighted).</summary>
		private bool _rootBlinkOn;

		/// <summary>700 ms timer that blinks <c>btnPickRoot</c> while no project root has been chosen; started/stopped by <see cref="UpdateProjectAccess"/>.</summary>
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

		/// <summary><see cref="_glowEdgesImage"/> resized to <see cref="_glowOverlayCacheSize"/> (owned); null until first requested, rebuilt when the preview size changes.</summary>
		private Bitmap _glowOverlayCache;

		/// <summary>Pixel size of <see cref="_glowOverlayCache"/>.</summary>
		private Size _glowOverlayCacheSize;

		/// <summary>Background picture box on the Sources tab (filling <c>pnlSourceImage</c>). Receives copies of the scene, field or intensity picture via the share buttons; source markers are painted on top and clicks pick a source position.</summary>
		private PictureBox _picSource;

		/// <summary>Background picture box on the Probes tab (filling <c>pnlProbeImage</c>). Receives copies of the scene, field or intensity picture via the share buttons; probe markers and graph boxes are painted on top and clicks pick probe positions.</summary>
		private PictureBox _picProbe;

		/// <summary>Set once any simulation has finished. Not read anywhere at present.</summary>
		private bool _hasRun;

		/// <summary>Explorer <c>CurrentViewMode</c> value for the icons view (FOLDERVIEWMODE). Not referenced at present: callers of <see cref="OpenFolderInExplorer"/> pass the literal 1.</summary>
		private const int FVM_ICON = 1;

		/// <summary>Explorer <c>CurrentViewMode</c> value for the details view (FOLDERVIEWMODE). Not referenced at present: callers of <see cref="OpenFolderInExplorer"/> pass the literal 4.</summary>
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

		/// <summary>Normal back colour of <c>btnSaveSource</c>; the button turns orange while the editor has unsaved edits.</summary>
		private Color _sourceSaveButtonDefaultBackColor;

		/// <summary>One row of the project list views (stored in the item's <c>Tag</c>).</summary>
		private sealed class ProjectListEntry
		{
			public ProjectListEntry(string name, DateTime lastModified)
			{
				this.Name = name;
				this.LastModified = lastModified;
			}

			/// <summary>Project (folder) name.</summary>
			public readonly string Name;

			/// <summary>Last write time of the project's configuration file, or of the folder when the file is missing (local time).</summary>
			public readonly DateTime LastModified;
		}

		/// <summary>Win32 <c>SIZE</c> structure passed to <see cref="IShellItemImageFactory.GetImage"/>.</summary>
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
				this.Index = index;
				this.Title = title;
				this.ChkOn = chkOn;
				this.CmbPlaceOn = cmbPlaceOn;
				this.CmbMeasurement = cmbMeasurement;
				this.CmbProbeType = cmbProbeType;
				this.CmbBoxSize = cmbBoxSize;
				this.NumAPosX = numAPosX;
				this.NumAPosY = numAPosY;
				this.NumBPosX = numBPosX;
				this.NumBPosY = numBPosY;
				this.ButPick = butPick;
				this.RadRunning = radRunning;
				this.RadFull = radFull;
				this.ChkAutoX = chkAutoX;
				this.ChkAutoY = chkAutoY;
				this.TxtMinY = txtMinY;
				this.TxtMaxY = txtMaxY;
				this.ChkStartAfter = chkStartAfter;
				this.TxtStartAfter = txtStartAfter;
				this.NumRunningWindow = numRunningWindow;
				this.BOnlyControls = bOnlyControls;
				this.PickButtonText = butPick.Text;
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
