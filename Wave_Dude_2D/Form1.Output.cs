using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Outputs tab: a second project list (<c>lstAllProjects3</c>) and, for the project selected
	/// there, three file lists (scene images, rendered frames, videos) with a shared preview box
	/// and delete/rename/open/copy commands. Only one of the three file lists has a selection at
	/// a time; selecting in one clears the other two. The lists show files on disk, not the open
	/// project's state, so they are refreshed from disk after every change. Everything here runs
	/// on the UI thread.
	/// </summary>
	public partial class Form1
	{
		/// <summary>
		/// Creates the preview <see cref="PictureBox"/> (zoom-to-fit, docked in <c>pnlPreview</c>),
		/// fills the Outputs-tab project list and sets the initial button state. Called once from
		/// <c>Form1_Load</c>.
		/// </summary>
		private void InitializeOutputsTab()
		{
			_outputPreviewPic = new PictureBox();
			_outputPreviewPic.Dock = DockStyle.Fill;
			_outputPreviewPic.SizeMode = PictureBoxSizeMode.Zoom;
			pnlPreview.Controls.Add(_outputPreviewPic);
			RefreshOutputProjectList("");
			UpdateShareWithEditorsButton();
		}

		/// <summary>
		/// Rebuilds the Outputs-tab project list from disk with its own sort state and selects
		/// <paramref name="preferredName"/>, or the previously selected project when that is "".
		/// Selecting an entry triggers <see cref="lstAllProjects3_SelectedIndexChanged"/>, which
		/// reloads the file lists.
		/// </summary>
		private void RefreshOutputProjectList(string preferredName = "")
		{
			string selectedName = (preferredName.Length > 0) ? preferredName : SelectedProjectName(lstAllProjects3);
			List<Form1.ProjectListEntry> entries = BuildProjectEntries();
			SortProjectEntries(entries, _outputProjectSortColumn, _outputProjectSortAscending);
			PopulateProjectListView(lstAllProjects3, entries);
			UpdateOutputProjectSortHeaders();
			SelectProjectInList(lstAllProjects3, selectedName);
		}

		/// <summary>Clicking a column header sorts the Outputs-tab project list by it; clicking the current sort column again flips the direction.</summary>
		private void lstAllProjects3_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			if (e.Column == _outputProjectSortColumn)
			{
				_outputProjectSortAscending = !_outputProjectSortAscending;
			}
			else
			{
				_outputProjectSortColumn = e.Column;
				_outputProjectSortAscending = true;
			}
			RefreshOutputProjectList("");
		}

		/// <summary>Appends " ^" (ascending) or " v" (descending) to the header of the column the Outputs-tab project list is sorted by.</summary>
		private void UpdateOutputProjectSortHeaders()
		{
			string arrow = _outputProjectSortAscending ? " ^" : " v";
			colProjectName2.Text = "Project name" + ((_outputProjectSortColumn == 0) ? arrow : "");
			colLastEdited2.Text = "Last edited" + ((_outputProjectSortColumn == 1) ? arrow : "");
		}

		/// <summary>Full folder path of the project selected in the Outputs-tab list, or "" when none is selected.</summary>
		private string SelectedOutputProjectFolder()
		{
			string name = SelectedProjectName(lstAllProjects3);
			return (name.Length == 0) ? "" : ProjectStore.FolderPathFor(_settings.ProjectRoot, name);
		}

		/// <summary>Selecting a project reloads the three file lists for it.</summary>
		private void lstAllProjects3_SelectedIndexChanged(object sender, EventArgs e)
		{
			RefreshOutputFileLists();
		}

		/// <summary>
		/// Refills the scene, image and video lists from the selected Outputs-tab project (empties
		/// them when none is selected), updates their sort headers, clears the preview and the
		/// copy-button state. Previous file selections are dropped; the <c>Refill*List</c> methods
		/// are the variants that keep them.
		/// </summary>
		private void RefreshOutputFileLists()
		{
			string projectFolder = SelectedOutputProjectFolder();
			FillFileList(lstScenes, projectFolder, ProjectStore.SceneImageFolderName, Form1.SceneImageExtensions, _sceneSortColumn, _sceneSortAscending);
			FillFileList(lstImages, projectFolder, ProjectStore.OutputFramesFolderName, Form1.OutputImageExtensions, _imageSortColumn, _imageSortAscending);
			FillFileList(lstVideo, projectFolder, ProjectStore.OutputVideoFolderName, Form1.OutputVideoExtensions, _videoSortColumn, _videoSortAscending);
			UpdateFileListSortHeaders(colSceneName, "Scene file name", colSceneCreated, "Created on", _sceneSortColumn, _sceneSortAscending);
			UpdateFileListSortHeaders(colImageName, "Image file name", colCreatedOn, "Created on", _imageSortColumn, _imageSortAscending);
			UpdateFileListSortHeaders(colVideoName, "Video file name", colCreatedOn2, "Created on", _videoSortColumn, _videoSortAscending);
			ClearOutputPreview();
			UpdateCopyToClipboardButton();
		}

		/// <summary>
		/// Replaces the items of <paramref name="list"/> with the files in
		/// <c>projectFolder\subfolderName</c> whose lower-cased extension (including the dot) is in
		/// <paramref name="extensions"/>. Sorted by file name, or by creation time when the list has
		/// a date column and <paramref name="sortColumn"/> is 1. Each item's <c>Tag</c> is the full
		/// path; the optional second column shows the local creation time as "yyyy-MM-dd HH:mm".
		/// The list is left empty when <paramref name="projectFolder"/> is "" or the sub-folder does
		/// not exist.
		/// </summary>
		private void FillFileList(ListView list, string projectFolder, string subfolderName, string[] extensions, int sortColumn, bool sortAscending)
		{
			list.BeginUpdate();
			try
			{
				list.Items.Clear();
				if (projectFolder.Length == 0)
				{
					return;
				}
				string folder = Path.Combine(projectFolder, subfolderName);
				if (!Directory.Exists(folder))
				{
					return;
				}
				List<string> files = new List<string>(Directory.GetFiles(folder));
				files.RemoveAll(f => Array.IndexOf(extensions, Path.GetExtension(f).ToLowerInvariant()) < 0);
				bool byCreatedDate = list.Columns.Count > 1 && sortColumn == 1;
				files.Sort((a, b) => CompareFileEntries(a, b, byCreatedDate, sortAscending));
				foreach (string file in files)
				{
					ListViewItem item = new ListViewItem(Path.GetFileName(file));
					if (list.Columns.Count > 1)
					{
						item.SubItems.Add(File.GetCreationTime(file).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
					}
					item.Tag = file;
					list.Items.Add(item);
				}
			}
			finally
			{
				list.EndUpdate();
			}
		}

		/// <summary>Sort comparison for two full file paths: by creation time (read from disk on every call) or by file name, case-insensitive; negated for descending order.</summary>
		private static int CompareFileEntries(string a, string b, bool byCreatedDate, bool ascending)
		{
			int order = byCreatedDate
				? File.GetCreationTime(a).CompareTo(File.GetCreationTime(b))
				: string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
			return ascending ? order : -order;
		}

		/// <summary>Writes the two header labels of a file list, appending " ^" or " v" to the column (0 = name, 1 = date) the list is sorted by.</summary>
		private void UpdateFileListSortHeaders(ColumnHeader nameColumn, string nameLabel, ColumnHeader dateColumn, string dateLabel, int sortColumn, bool ascending)
		{
			string arrow = ascending ? " ^" : " v";
			nameColumn.Text = nameLabel + ((sortColumn == 0) ? arrow : "");
			dateColumn.Text = dateLabel + ((sortColumn == 1) ? arrow : "");
		}

		/// <summary>Full paths (item tags) of the selected items in a file list; items without a path tag are skipped.</summary>
		private List<string> SelectedFilePaths(ListView list)
		{
			List<string> paths = new List<string>();
			foreach (ListViewItem item in list.SelectedItems)
			{
				string path = item.Tag as string;
				if (!string.IsNullOrEmpty(path))
				{
					paths.Add(path);
				}
			}
			return paths;
		}

		/// <summary>Re-selects every item of <paramref name="list"/> whose path is in <paramref name="paths"/> (case-insensitive); used to keep the selection across a refill.</summary>
		private void ReselectFiles(ListView list, List<string> paths)
		{
			if (paths.Count == 0)
			{
				return;
			}
			foreach (ListViewItem item in list.Items)
			{
				string path = item.Tag as string;
				if (path != null && paths.Contains(path, StringComparer.OrdinalIgnoreCase))
				{
					item.Selected = true;
				}
			}
		}

		/// <summary>Reloads and re-sorts the scene-image list, keeping the selected files selected; used after a header click.</summary>
		private void RefillSceneList()
		{
			List<string> selected = SelectedFilePaths(lstScenes);
			FillFileList(lstScenes, SelectedOutputProjectFolder(), ProjectStore.SceneImageFolderName, Form1.SceneImageExtensions, _sceneSortColumn, _sceneSortAscending);
			UpdateFileListSortHeaders(colSceneName, "Scene file name", colSceneCreated, "Created on", _sceneSortColumn, _sceneSortAscending);
			ReselectFiles(lstScenes, selected);
		}

		/// <summary>Reloads and re-sorts the output-frame list, keeping the selected files selected; used after a header click.</summary>
		private void RefillImageList()
		{
			List<string> selected = SelectedFilePaths(lstImages);
			FillFileList(lstImages, SelectedOutputProjectFolder(), ProjectStore.OutputFramesFolderName, Form1.OutputImageExtensions, _imageSortColumn, _imageSortAscending);
			UpdateFileListSortHeaders(colImageName, "Image file name", colCreatedOn, "Created on", _imageSortColumn, _imageSortAscending);
			ReselectFiles(lstImages, selected);
		}

		/// <summary>Reloads and re-sorts the video list, keeping the selected files selected; used after a header click.</summary>
		private void RefillVideoList()
		{
			List<string> selected = SelectedFilePaths(lstVideo);
			FillFileList(lstVideo, SelectedOutputProjectFolder(), ProjectStore.OutputVideoFolderName, Form1.OutputVideoExtensions, _videoSortColumn, _videoSortAscending);
			UpdateFileListSortHeaders(colVideoName, "Video file name", colCreatedOn2, "Created on", _videoSortColumn, _videoSortAscending);
			ReselectFiles(lstVideo, selected);
		}

		/// <summary>Clicking a header sorts the scene-image list by that column (toggling direction on a repeat click).</summary>
		private void lstScenes_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			Form1.ToggleFileSort(ref _sceneSortColumn, ref _sceneSortAscending, e.Column);
			RefillSceneList();
		}

		/// <summary>Clicking a header sorts the output-frame list by that column (toggling direction on a repeat click).</summary>
		private void lstImages_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			Form1.ToggleFileSort(ref _imageSortColumn, ref _imageSortAscending, e.Column);
			RefillImageList();
		}

		/// <summary>Clicking a header sorts the video list by that column (toggling direction on a repeat click).</summary>
		private void lstVideo_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			Form1.ToggleFileSort(ref _videoSortColumn, ref _videoSortAscending, e.Column);
			RefillVideoList();
		}

		/// <summary>Applies a header click to a sort column/direction pair: the same column flips the direction, a different column becomes the sort column in ascending order.</summary>
		private static void ToggleFileSort(ref int sortColumn, ref bool sortAscending, int clickedColumn)
		{
			if (clickedColumn == sortColumn)
			{
				sortAscending = !sortAscending;
			}
			else
			{
				sortColumn = clickedColumn;
				sortAscending = true;
			}
		}

		/// <summary>
		/// Clears the selection of the two file lists other than <paramref name="active"/> (the one the user
		/// just clicked in), so only one of the three ever shows a selection at a time - otherwise it is unclear
		/// which list's item <c>pnlPreview</c> is actually showing. The Projects list (<c>lstAllProjects3</c>) is
		/// not one of these three and keeps its own selection regardless. Sets
		/// <c>_clearingFileListSelection</c> while doing so, so that their
		/// <c>SelectedIndexChanged</c> handlers ignore the change and do not clear the preview.
		/// </summary>
		private void ClearOtherFileListSelections(ListView active)
		{
			_clearingFileListSelection = true;
			try
			{
				if (active != lstScenes)
				{
					lstScenes.SelectedItems.Clear();
				}
				if (active != lstImages)
				{
					lstImages.SelectedItems.Clear();
				}
				if (active != lstVideo)
				{
					lstVideo.SelectedItems.Clear();
				}
			}
			finally
			{
				_clearingFileListSelection = false;
			}
		}

		/// <summary>Selecting a scene image makes it the only selection across the three lists and shows it in the preview; ignored while another list is clearing selections.</summary>
		private void lstScenes_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_clearingFileListSelection)
			{
				return;
			}
			ClearOtherFileListSelections(lstScenes);
			ShowOutputPreview(lstScenes);
		}

		/// <summary>Selecting output frames makes them the only selection across the three lists, previews the first one and updates the copy button; ignored while another list is clearing selections.</summary>
		private void lstImages_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_clearingFileListSelection)
			{
				return;
			}
			ClearOtherFileListSelections(lstImages);
			ShowOutputPreview(lstImages);
			UpdateCopyToClipboardButton();
		}

		/// <summary>
		/// Shows the first selected file of <paramref name="list"/> in the preview as a still image
		/// (an in-memory copy, so the file is not locked). Clears the preview when the file is
		/// missing or cannot be decoded; leaves it untouched when nothing is selected.
		/// </summary>
		private void ShowOutputPreview(ListView list)
		{
			if (list.SelectedItems.Count == 0)
			{
				return;
			}
			string path = list.SelectedItems[0].Tag as string;
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				ClearOutputPreview();
				return;
			}
			try
			{
				SetOutputPreviewImage(Form1.LoadImageCopy(path), false);
			}
			catch (Exception)
			{
				ClearOutputPreview();
			}
		}

		/// <summary>
		/// Selecting exactly one video makes it the only selection across the three lists and shows
		/// a thumbnail-sized preview of it, via the operating system's own shell thumbnail cache
		/// (<see cref="GetShellThumbnail"/>, further down) - there is no dependency-free way to decode/draw
		/// an actual video frame with just System.Drawing/System.Windows.Forms. The thumbnail is requested
		/// at the preview box's current size (at least 64x64 pixels). Selecting zero or more than one video
		/// (<c>lstVideo</c> allows multi-select) clears the preview instead, since a single thumbnail cannot
		/// stand in for several files; a missing file or a shell failure clears it too.
		/// Ignored while another list is clearing selections.
		/// </summary>
		private void lstVideo_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_clearingFileListSelection)
			{
				return;
			}
			ClearOtherFileListSelections(lstVideo);
			if (lstVideo.SelectedItems.Count != 1)
			{
				ClearOutputPreview();
				return;
			}
			string path = lstVideo.SelectedItems[0].Tag as string;
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				ClearOutputPreview();
				return;
			}
			try
			{
				int width = Math.Max(64, _outputPreviewPic.ClientSize.Width);
				int height = Math.Max(64, _outputPreviewPic.ClientSize.Height);
				SetOutputPreviewImage(Form1.GetShellThumbnail(path, width, height), true);
			}
			catch (Exception)
			{
				ClearOutputPreview();
			}
		}

		/// <summary>
		/// Puts <paramref name="image"/> (may be null) into the preview box, disposing the image that
		/// was there before (the preview box owns its image), remembers whether it is a video
		/// thumbnail and updates the "share with editors" button accordingly.
		/// </summary>
		private void SetOutputPreviewImage(Image image, bool isVideo = false)
		{
			Image previous = _outputPreviewPic.Image;
			_outputPreviewPic.Image = image;
			if (previous != null)
			{
				previous.Dispose();
			}
			_outputPreviewIsVideo = isVideo;
			UpdateShareWithEditorsButton();
		}

		/// <summary>Removes and disposes the preview image; safe to call before <see cref="InitializeOutputsTab"/> has created the preview box.</summary>
		private void ClearOutputPreview()
		{
			if (_outputPreviewPic != null)
			{
				SetOutputPreviewImage(null, false);
			}
		}

		/// <summary>
		/// Permanently deletes the selected scene image files after confirmation. If the list shows
		/// the open project and one of the files is the scene image it currently uses, the question
		/// says so and the scene is switched to "User defined" after deleting. Files that could not
		/// be deleted are reported together at the end.
		/// </summary>
		private void btnDeleteScene_Click(object sender, EventArgs e)
		{
			if (lstScenes.SelectedItems.Count == 0)
			{
				MessageBox.Show(this, "Select a scene image in the list first.", "Delete scene", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			List<string> paths = SelectedFilePaths(lstScenes);

			bool listShowsOpenProject = _project != null && string.Equals(_project.FolderPath, SelectedOutputProjectFolder(), StringComparison.OrdinalIgnoreCase);
			string currentImagePath = (listShowsOpenProject && _scene.UseImage) ? SceneSettings.ToFullPath(_project.FolderPath, _scene.ImageFile) : "";
			bool deletesCurrentImage = currentImagePath.Length > 0 && paths.Exists(p => string.Equals(p, currentImagePath, StringComparison.OrdinalIgnoreCase));

			string question = (paths.Count == 1)
				? "Permanently delete '" + Path.GetFileName(paths[0]) + "'?"
				: "Permanently delete these " + paths.Count.ToString(CultureInfo.InvariantCulture) + " scene images?";
			if (deletesCurrentImage)
			{
				question += "\r\n\r\nThis is the scene image currently used by the open project. Deleting it will switch the scene to \"User defined\" instead.";
			}
			if (MessageBox.Show(this, question, "Delete scene", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
			{
				return;
			}

			List<string> errors = new List<string>();
			foreach (string path in paths)
			{
				try
				{
					File.Delete(path);
				}
				catch (Exception ex)
				{
					errors.Add(Path.GetFileName(path) + ": " + ex.Message);
				}
			}
			if (deletesCurrentImage)
			{
				radUserDefinedScene.Checked = true;
			}
			RefreshOutputFileLists();
			ShowDeleteErrorsIfAny(errors, "Delete scene");
		}

		/// <summary>
		/// Runs the Scene tab's "load scene image" dialog (which copies an outside image into the
		/// open project's scene_images folder and makes it the scene image), then refreshes the
		/// file lists if the Outputs tab currently shows the open project.
		/// </summary>
		private void btnLoadExternal_Click(object sender, EventArgs e)
		{
			btnLOadSceneImage_Click(sender, e);
			if (_project != null && string.Equals(_project.FolderPath, SelectedOutputProjectFolder(), StringComparison.OrdinalIgnoreCase))
			{
				RefreshOutputFileLists();
			}
		}

		/// <summary>Deletes the selected output frames (see <see cref="DeleteSelectedImages"/>).</summary>
		private void btnDeleteImages_Click(object sender, EventArgs e)
		{
			DeleteSelectedImages();
		}

		/// <summary>Pressing Delete in the output-frame list deletes the selected frames.</summary>
		private void lstImages_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				e.Handled = true;
				DeleteSelectedImages();
			}
		}

		/// <summary>Permanently deletes the selected output frame files after confirmation; files that could not be deleted are reported together at the end.</summary>
		private void DeleteSelectedImages()
		{
			if (lstImages.SelectedItems.Count == 0)
			{
				MessageBox.Show(this, "Select one or more images in the list first.", "Delete images", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			int count = lstImages.SelectedItems.Count;
			string question = (count == 1)
				? "Permanently delete '" + lstImages.SelectedItems[0].Text + "'?"
				: "Permanently delete these " + count.ToString(CultureInfo.InvariantCulture) + " images?";
			if (MessageBox.Show(this, question, "Delete images", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
			{
				return;
			}
			List<string> errors = new List<string>();
			foreach (ListViewItem item in lstImages.SelectedItems)
			{
				string path = (string)item.Tag;
				try
				{
					File.Delete(path);
				}
				catch (Exception ex)
				{
					errors.Add(item.Text + ": " + ex.Message);
				}
			}
			RefreshOutputFileLists();
			ShowDeleteErrorsIfAny(errors, "Delete images");
		}

		/// <summary>Copies the single selected output frame to the clipboard as a bitmap (the clipboard takes its own copy, so the loaded bitmap is disposed immediately).</summary>
		private void btnCopyToClipboard_Click(object sender, EventArgs e)
		{
			if (lstImages.SelectedItems.Count != 1)
			{
				return;
			}
			string path = lstImages.SelectedItems[0].Tag as string;
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return;
			}
			try
			{
				using (Bitmap bitmap = Form1.LoadImageCopy(path))
				{
					Clipboard.SetImage(bitmap);
				}
				ToolStripStatusLabel1.Text = "Copied '" + Path.GetFileName(path) + "' to the clipboard.";
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The image could not be copied to the clipboard:\r\n\r\n" + ex.Message, "Copy to clipboard", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Enables "Copy to clipboard" only when exactly one output frame is selected.</summary>
		private void UpdateCopyToClipboardButton()
		{
			btnCopyToClipboard.Enabled = lstImages.SelectedItems.Count == 1;
		}

		/// <summary>Opens the selected project's output_frames folder in Explorer (icon view; the folder is created if missing).</summary>
		private void btnOpenImageFolder2_Click(object sender, EventArgs e)
		{
			string folder = SelectedOutputProjectFolder();
			if (folder.Length == 0)
			{
				MessageBox.Show(this, "Select a project in the list first.", "Open image folder", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			try
			{
				OpenFolderInExplorer(Path.Combine(folder, ProjectStore.OutputFramesFolderName), FVM_ICON);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The image folder could not be opened:\r\n\r\n" + ex.Message, "Open image folder", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Deletes the selected videos (see <see cref="DeleteSelectedVideos"/>).</summary>
		private void btnDeleteVideos_Click(object sender, EventArgs e)
		{
			DeleteSelectedVideos();
		}

		/// <summary>Pressing Delete in the video list deletes the selected videos.</summary>
		private void lstVideo_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
			{
				e.Handled = true;
				DeleteSelectedVideos();
			}
		}

		/// <summary>Permanently deletes the selected video files after confirmation; files that could not be deleted are reported together at the end.</summary>
		private void DeleteSelectedVideos()
		{
			if (lstVideo.SelectedItems.Count == 0)
			{
				MessageBox.Show(this, "Select one or more videos in the list first.", "Delete videos", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			int count = lstVideo.SelectedItems.Count;
			string question = (count == 1)
				? "Permanently delete '" + lstVideo.SelectedItems[0].Text + "'?"
				: "Permanently delete these " + count.ToString(CultureInfo.InvariantCulture) + " videos?";
			if (MessageBox.Show(this, question, "Delete videos", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
			{
				return;
			}
			List<string> errors = new List<string>();
			foreach (ListViewItem item in lstVideo.SelectedItems)
			{
				string path = (string)item.Tag;
				try
				{
					File.Delete(path);
				}
				catch (Exception ex)
				{
					errors.Add(item.Text + ": " + ex.Message);
				}
			}
			RefreshOutputFileLists();
			ShowDeleteErrorsIfAny(errors, "Delete videos");
		}

		/// <summary>Opens the first selected video in the system's default player.</summary>
		private void btnViewVIdeoInPlayer2_Click(object sender, EventArgs e)
		{
			if (lstVideo.SelectedItems.Count == 0)
			{
				MessageBox.Show(this, "Select a video in the list first.", "View in player", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			OpenVideo((string)lstVideo.SelectedItems[0].Tag);
		}

		/// <summary>Double-clicking a video opens it in the system's default player.</summary>
		private void lstVideo_DoubleClick(object sender, EventArgs e)
		{
			if (lstVideo.SelectedItems.Count > 0)
			{
				OpenVideo((string)lstVideo.SelectedItems[0].Tag);
			}
		}

		/// <summary>
		/// Renames the single selected video file inside its folder after prompting for a new file
		/// name; an empty or unchanged name is a no-op, invalid characters and an existing target
		/// are refused. The extension is not enforced, so a file renamed to a non-video extension
		/// disappears from the list.
		/// </summary>
		private void btnRenameVideo_Click(object sender, EventArgs e)
		{
			if (lstVideo.SelectedItems.Count != 1)
			{
				MessageBox.Show(this, "Select a single video in the list first.", "Rename video", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			string oldPath = (string)lstVideo.SelectedItems[0].Tag;
			string fileName = Path.GetFileName(oldPath);
			string newName = fileName;
			if (!PromptForText("Rename video", "New name for '" + fileName + "':", fileName, ref newName))
			{
				return;
			}
			newName = newName.Trim();
			if (newName.Length == 0 || newName == fileName)
			{
				return;
			}
			if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			{
				MessageBox.Show(this, "That name contains characters that are not allowed in a file name.", "Rename video", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			string newPath = Path.Combine(Path.GetDirectoryName(oldPath), newName);
			if (File.Exists(newPath))
			{
				MessageBox.Show(this, "A file named '" + newName + "' already exists.", "Rename video", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			try
			{
				File.Move(oldPath, newPath);
				RefreshOutputFileLists();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The video could not be renamed:\r\n\r\n" + ex.Message, "Rename video", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Opens the selected project's output_video folder in Explorer (details view; the folder is created if missing).</summary>
		private void btnOpenVIdeoFolder2_Click(object sender, EventArgs e)
		{
			string folder = SelectedOutputProjectFolder();
			if (folder.Length == 0)
			{
				MessageBox.Show(this, "Select a project in the list first.", "Open video folder", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			try
			{
				OpenFolderInExplorer(Path.Combine(folder, ProjectStore.OutputVideoFolderName), FVM_DETAILS);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "The video folder could not be opened:\r\n\r\n" + ex.Message, "Open video folder", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		/// <summary>Copies the previewed still image into the Sources and Probes tab preview boxes.</summary>
		private void btnShareWithEditors_Click(object sender, EventArgs e)
		{
			CopyPreviewToTabs(_outputPreviewPic);
		}

		/// <summary>Enables "Share with editors" only while the preview shows a still image (not a video thumbnail).</summary>
		private void UpdateShareWithEditorsButton()
		{
			btnShareWithEditors.Enabled = !_outputPreviewIsVideo && _outputPreviewPic != null && _outputPreviewPic.Image != null;
		}

		/// <summary>Shows one message box listing <paramref name="errors"/> (one per line) under <paramref name="title"/>; does nothing for an empty list.</summary>
		private void ShowDeleteErrorsIfAny(List<string> errors, string title)
		{
			if (errors.Count > 0)
			{
				MessageBox.Show(this, "Some files could not be deleted:\r\n\r\n" + string.Join("\r\n", errors), title, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		// ---- the video preview thumbnail, via the Windows shell (no video codec of our own)
		//
		// Windows shell thumbnail interop (IShellItemImageFactory, declared with NativeSize and ThumbnailOptions in
		// Form1.cs) - plain P/Invoke/COM interop against shell32.dll and gdi32.dll, no NuGet package or project
		// reference needed, but genuine COM interop nonetheless, which could not be exercised on an actual Windows
		// machine while writing it. GetShellThumbnail is written so a wrong detail here surfaces as "no thumbnail
		// for this file" (via the try/catch in lstVideo_SelectedIndexChanged) rather than a crash - if the preview
		// comes out consistently missing for videos, this is the place to revisit.

		/// <summary>
		/// shell32: creates the shell item for a file system path and returns the interface
		/// identified by <paramref name="riid"/>. <c>PreserveSig = false</c> turns a failing HRESULT
		/// into a <see cref="COMException"/>.
		/// </summary>
		[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
		private static extern void SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr pbc, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

		/// <summary>gdi32: frees a GDI object; used for the HBITMAP the shell hands back in <see cref="GetShellThumbnail"/>.</summary>
		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr hObject);

		/// <summary>
		/// Asks the Windows shell (<c>IShellItemImageFactory</c>) for the thumbnail it would show for
		/// a file, requesting <paramref name="width"/> x <paramref name="height"/> pixels but
		/// accepting a larger one. Used for video previews. The returned bitmap is an independent
		/// managed copy; the shell's HBITMAP and the COM object are released before returning.
		/// </summary>
		/// <returns>The thumbnail, or null when the shell does not provide one for the file.</returns>
		/// <exception cref="COMException">The shell could not create the item or extract the thumbnail.</exception>
		private static Bitmap GetShellThumbnail(string filePath, int width, int height)
		{
			Guid factoryGuid = typeof(Form1.IShellItemImageFactory).GUID;
			object shellItem;
			Form1.SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref factoryGuid, out shellItem);
			Form1.IShellItemImageFactory factory = shellItem as Form1.IShellItemImageFactory;
			if (factory == null)
			{
				return null;
			}
			IntPtr bitmapHandle = IntPtr.Zero;
			try
			{
				factory.GetImage(new Form1.NativeSize
				{
					cx = width,
					cy = height
				}, Form1.ThumbnailOptions.BiggerSizeOk | Form1.ThumbnailOptions.ThumbnailOnly, out bitmapHandle);
				if (bitmapHandle == IntPtr.Zero)
				{
					return null;
				}
				using (Bitmap wrapper = Image.FromHbitmap(bitmapHandle))
				{
					return new Bitmap(wrapper);
				}
			}
			finally
			{
				if (bitmapHandle != IntPtr.Zero)
				{
					Form1.DeleteObject(bitmapHandle);
				}
				Marshal.ReleaseComObject(factory);
			}
		}
	}
}
