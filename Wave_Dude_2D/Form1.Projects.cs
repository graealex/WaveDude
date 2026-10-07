using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Projects tab: the sortable list of every project folder found under the configured project
	/// root (<see cref="AppSettings.ProjectRoot"/>) and the new/load/delete/rename commands that act
	/// on the selected entry. The list-building helpers here (<see cref="BuildProjectEntries"/>,
	/// <see cref="SortProjectEntries"/>, <see cref="PopulateProjectListView"/>,
	/// <see cref="SelectedProjectName"/>, <see cref="SelectProjectInList"/>) are shared with the
	/// second project list on the Outputs tab (<c>Form1.Output.cs</c>). Everything here runs on
	/// the UI thread.
	/// </summary>
	public partial class Form1
	{
		/// <summary>
		/// Scans the project root for project folders and returns one unsorted entry per project.
		/// The "last modified" time is the write time of the project's configuration.json, or of
		/// the folder itself when that file is missing (legacy folders).
		/// </summary>
		/// <returns>An empty list when no project root is configured or it cannot be read.</returns>
		private List<Form1.ProjectListEntry> BuildProjectEntries()
		{
			List<Form1.ProjectListEntry> entries = new List<Form1.ProjectListEntry>();
			foreach (string name in ProjectStore.ListProjects(this._settings.ProjectRoot))
			{
				string folder = ProjectStore.FolderPathFor(this._settings.ProjectRoot, name);
				string configPath = Path.Combine(folder, ProjectConfig.FileName);
				DateTime lastModified = File.Exists(configPath) ? File.GetLastWriteTime(configPath) : Directory.GetLastWriteTime(folder);
				entries.Add(new Form1.ProjectListEntry(name, lastModified));
			}
			return entries;
		}

		/// <summary>
		/// Sorts <paramref name="entries"/> in place: column 0 by name (case-insensitive), any other
		/// column by last-modified time; the list is reversed afterwards for descending order.
		/// </summary>
		private void SortProjectEntries(List<Form1.ProjectListEntry> entries, int sortColumn, bool ascending)
		{
			if (sortColumn == 0)
			{
				entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
			}
			else
			{
				entries.Sort((a, b) => DateTime.Compare(a.LastModified, b.LastModified));
			}
			if (!ascending)
			{
				entries.Reverse();
			}
		}

		/// <summary>
		/// Replaces the items of <paramref name="list"/> with one row per entry: the name and the
		/// last-modified time as local "yyyy-MM-dd HH:mm". Each item's <c>Tag</c> holds its
		/// <see cref="ProjectListEntry"/>.
		/// </summary>
		private void PopulateProjectListView(ListView list, List<Form1.ProjectListEntry> entries)
		{
			list.BeginUpdate();
			try
			{
				list.Items.Clear();
				foreach (Form1.ProjectListEntry entry in entries)
				{
					ListViewItem item = new ListViewItem(entry.Name);
					item.SubItems.Add(entry.LastModified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
					item.Tag = entry;
					list.Items.Add(item);
				}
			}
			finally
			{
				list.EndUpdate();
			}
		}

		/// <summary>Name of the first selected project in <paramref name="list"/>, or "" when nothing is selected.</summary>
		private string SelectedProjectName(ListView list)
		{
			if (list.SelectedItems.Count == 0)
			{
				return "";
			}
			Form1.ProjectListEntry entry = list.SelectedItems[0].Tag as Form1.ProjectListEntry;
			return (entry != null) ? entry.Name : "";
		}

		/// <summary>
		/// Adds the entry named <paramref name="name"/> (case-insensitive) to the selection of
		/// <paramref name="list"/> and scrolls it into view; does nothing for "" or an unknown name.
		/// </summary>
		private void SelectProjectInList(ListView list, string name)
		{
			if (name.Length == 0)
			{
				return;
			}
			foreach (ListViewItem item in list.Items)
			{
				Form1.ProjectListEntry entry = (Form1.ProjectListEntry)item.Tag;
				if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					item.Selected = true;
					item.EnsureVisible();
					break;
				}
			}
		}

		/// <summary>
		/// Rebuilds the Projects-tab list from disk with the current sort, keeps the previously
		/// selected project selected when it still exists, and refreshes the sort arrows and buttons.
		/// Called after every project command and whenever the root folder changes.
		/// </summary>
		private void RefreshProjectList()
		{
			string selectedName = this.SelectedProjectName(this.lstAllProjects);
			List<Form1.ProjectListEntry> entries = this.BuildProjectEntries();
			this.SortProjectEntries(entries, this._projectSortColumn, this._projectSortAscending);
			this.PopulateProjectListView(this.lstAllProjects, entries);
			this.UpdateProjectListSortHeaders();
			this.SelectProjectInList(this.lstAllProjects, selectedName);
			this.UpdateProjectListButtons();
		}

		/// <summary>Clicking a column header sorts the project list by it; clicking the current sort column again flips the direction.</summary>
		private void lstAllProjects_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			if (e.Column == this._projectSortColumn)
			{
				this._projectSortAscending = !this._projectSortAscending;
			}
			else
			{
				this._projectSortColumn = e.Column;
				this._projectSortAscending = true;
			}
			this.RefreshProjectList();
		}

		/// <summary>Appends " ^" (ascending) or " v" (descending) to the header text of the column the project list is sorted by.</summary>
		private void UpdateProjectListSortHeaders()
		{
			string arrow = this._projectSortAscending ? " ^" : " v";
			this.colProjectName.Text = "Project name" + ((this._projectSortColumn == 0) ? arrow : "");
			this.colLastEdit.Text = "Last edited" + ((this._projectSortColumn == 1) ? arrow : "");
		}

		/// <summary>Enables "New" only when a project root is configured, and Load/Delete/Rename only when a project is selected.</summary>
		private void UpdateProjectListButtons()
		{
			bool hasSelection = this.lstAllProjects.SelectedItems.Count > 0;
			this.btnNewProject.Enabled = this._settings != null && this._settings.ProjectRoot.Length > 0;
			this.btnLoadProject.Enabled = hasSelection;
			this.btnDeleteProject.Enabled = hasSelection;
			this.btnRenameProject.Enabled = hasSelection;
		}

		/// <summary>
		/// Creates a new project (wired to both the "New project" button and File &gt; New): offers
		/// to save the open project, then asks for a name until it is valid and unused or the prompt
		/// is cancelled. On success the editor is reset to defaults, those defaults are written into
		/// the new project's configuration.json, and the project becomes the current one.
		/// </summary>
		private void btnNewProject2_Click(object sender, EventArgs e)
		{
			if (this._settings.ProjectRoot.Length == 0)
			{
				MessageBox.Show(this, "Choose a project root folder first (File > Choose project root).", "New project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			if (!this.ConfirmSaveProject())
			{
				return;
			}
			string name = "";
			while (this.PromptForText("New project", "Name for the new project:", name, ref name))
			{
				name = name.Trim();
				string error = ProjectStore.ValidateName(name);
				if (error.Length > 0)
				{
					MessageBox.Show(this, error, "New project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					continue;
				}
				if (ProjectStore.IsProjectFolder(ProjectStore.FolderPathFor(this._settings.ProjectRoot, name)))
				{
					MessageBox.Show(this, "A project named '" + name + "' already exists in this root folder. Choose a different name.", "New project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					continue;
				}
				try
				{
					Project project = ProjectStore.Create(this._settings.ProjectRoot, name);
					this.StartNewProject();
					this._scene.ToConfig(project.Config);
					this._simulation.ToConfig(project.Config);
					ProbeSettings.ListToConfig(this._probes, project.Config);
					SourceSettings.ListToConfig(this._sources, project.Config);
					project.Save();
					this.SetCurrentProject(project);
					this.ToolStripStatusLabel1.Text = "Project '" + name + "' created.";
					break;
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "The project could not be created:\r\n\r\n" + ex.Message, "New project", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
			}
		}

		/// <summary>Changing the selection updates which project buttons are enabled.</summary>
		private void lstAllProjects_SelectedIndexChanged(object sender, EventArgs e)
		{
			this.UpdateProjectListButtons();
		}

		/// <summary>Double-clicking a project loads it, exactly like the Load button.</summary>
		private void lstAllProjects_DoubleClick(object sender, EventArgs e)
		{
			this.btnLoadProject_Click(sender, e);
		}

		/// <summary>Full folder path of the project selected in the Projects-tab list, or "" when none is selected.</summary>
		private string SelectedProjectFolder()
		{
			string name = this.SelectedProjectName(this.lstAllProjects);
			return (name.Length == 0) ? "" : ProjectStore.FolderPathFor(this._settings.ProjectRoot, name);
		}

		/// <summary>
		/// True when <paramref name="folder"/> is the open project's folder and a
		/// <see cref="SimulationRunner"/> still exists for it (a run is in progress), in which case
		/// the folder must not be deleted or renamed.
		/// </summary>
		private bool IsOpenProjectRunning(string folder)
		{
			return this._project != null && this._runner != null && string.Equals(this._project.FolderPath, folder, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Loads the selected project into the editor after offering to save unsaved changes; read errors are shown in a message box.</summary>
		private void btnLoadProject_Click(object sender, EventArgs e)
		{
			string folder = this.SelectedProjectFolder();
			if (folder.Length == 0)
			{
				return;
			}
			try
			{
				if (this.ConfirmSaveProject())
				{
					this.SetCurrentProject(ProjectStore.Open(folder));
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The project could not be opened:\r\n\r\n" + ex.Message, "Load project", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>
		/// Permanently deletes the selected project folder after a confirmation that also mentions
		/// how many files its assets folder holds. Refused while a simulation is running for that
		/// project. If the deleted project was the open one, the editor is reset to a new, unsaved
		/// project.
		/// </summary>
		private void btnDeleteProject_Click(object sender, EventArgs e)
		{
			string name = this.SelectedProjectName(this.lstAllProjects);
			string folder = this.SelectedProjectFolder();
			if (folder.Length == 0)
			{
				return;
			}
			if (this.IsOpenProjectRunning(folder))
			{
				MessageBox.Show(this, "A simulation is running for this project. Stop it first.", "Delete project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			string question = "Permanently delete the project '" + name + "' and everything in its folder?\r\n\r\n" + folder;
			int assetCount = Form1.CountFilesIn(Path.Combine(folder, ProjectStore.AssetsFolderName));
			if (assetCount > 0)
			{
				question += "\r\n\r\nThis includes " + assetCount.ToString(CultureInfo.InvariantCulture) + " file(s) in its assets folder - copy them elsewhere first if you want to keep them.";
			}
			if (MessageBox.Show(this, question, "Delete project", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
			{
				return;
			}
			bool deletesOpenProject = this._project != null && string.Equals(this._project.FolderPath, folder, StringComparison.OrdinalIgnoreCase);
			try
			{
				ProjectStore.Delete(folder);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The project could not be deleted:\r\n\r\n" + ex.Message, "Delete project", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				this.RefreshProjectList();
				return;
			}
			if (deletesOpenProject)
			{
				this.StartNewProject();
				this.UpdateProjectAccess();
			}
			this.RefreshProjectList();
			this.ToolStripStatusLabel1.Text = "Project '" + name + "' deleted.";
		}

		/// <summary>Number of files anywhere below <paramref name="folderPath"/>; 0 when the folder is missing or unreadable.</summary>
		private static int CountFilesIn(string folderPath)
		{
			if (!Directory.Exists(folderPath))
			{
				return 0;
			}
			try
			{
				return Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).Length;
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				return 0;
			}
		}

		/// <summary>
		/// Renames the selected project folder after prompting for a new name (validated by
		/// <see cref="ProjectStore.Rename"/>). Refused while a simulation is running for it; an
		/// unchanged name is a no-op.
		/// </summary>
		/// <remarks>
		/// When the renamed project is the open one it is re-opened from the new folder so that all
		/// project-relative paths stay valid. That re-reads configuration.json, so unsaved edits to
		/// the open project are discarded without asking.
		/// </remarks>
		private void btnRenameProject_Click(object sender, EventArgs e)
		{
			string oldName = this.SelectedProjectName(this.lstAllProjects);
			string folder = this.SelectedProjectFolder();
			if (folder.Length == 0)
			{
				return;
			}
			if (this.IsOpenProjectRunning(folder))
			{
				MessageBox.Show(this, "A simulation is running for this project. Stop it first.", "Rename project", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			string newName = oldName;
			if (!this.PromptForText("Rename project", "New name for '" + oldName + "':", oldName, ref newName))
			{
				return;
			}
			newName = newName.Trim();
			if (newName == oldName)
			{
				return;
			}
			bool renamesOpenProject = this._project != null && string.Equals(this._project.FolderPath, folder, StringComparison.OrdinalIgnoreCase);
			try
			{
				string newFolder = ProjectStore.Rename(folder, newName);
				if (renamesOpenProject)
				{
					this.SetCurrentProject(ProjectStore.Open(newFolder));
				}
				this.RefreshProjectList();
				this.SelectProjectInList(this.lstAllProjects, newName);
				this.ToolStripStatusLabel1.Text = "Project '" + oldName + "' renamed to '" + newName + "'.";
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The project could not be renamed:\r\n\r\n" + ex.Message, "Rename project", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}
	}
}
