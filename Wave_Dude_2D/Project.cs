using System;
using System.IO;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// An opened project on disk: its folder, the well-known sub-folders inside it and the
	/// in-memory <see cref="ProjectConfig"/>. Instances are created only by
	/// <see cref="ProjectStore"/> (open, create, rename); <see cref="Form1"/> holds the current
	/// one, edits <see cref="Config"/> and calls <see cref="Save"/>. The project name is always
	/// the folder name. None of the folder properties create the directory they point to.
	/// </summary>
	public sealed class Project
	{
		/// <summary>
		/// Wraps an existing folder and its configuration, and immediately stamps
		/// <see cref="ProjectConfig.ProjectName"/> with the folder name so a copied or renamed
		/// project folder is self-consistent even before it is saved.
		/// </summary>
		/// <param name="folderPath">Full path of the project folder; its last segment is the project name.</param>
		/// <param name="config">The configuration loaded from, or to be written to, that folder.</param>
		internal Project(string folderPath, ProjectConfig config)
		{
			this._folderPath = folderPath;
			this._config = config;
			this._config.ProjectName = this.Name;
		}

		/// <summary>Project name, i.e. the name of the project folder.</summary>
		public string Name
		{
			get
			{
				return Path.GetFileName(this._folderPath);
			}
		}

		/// <summary>Full path of the project folder.</summary>
		public string FolderPath
		{
			get
			{
				return this._folderPath;
			}
		}

		/// <summary>Parent of the project folder (the project root), or an empty string when the folder has no parent.</summary>
		public string RootFolder
		{
			get
			{
				return Path.GetDirectoryName(this._folderPath) ?? "";
			}
		}

		/// <summary>The configuration loaded from (or to be written to) <see cref="ConfigFilePath"/>; mutable, shared with the editor.</summary>
		public ProjectConfig Config
		{
			get
			{
				return this._config;
			}
		}

		/// <summary>Full path of the <c>configuration.json</c> file inside the project folder.</summary>
		public string ConfigFilePath
		{
			get
			{
				return Path.Combine(this._folderPath, "configuration.json");
			}
		}

		/// <summary>The <c>assets</c> sub-folder (user files kept with the project).</summary>
		public string AssetsFolder
		{
			get
			{
				return Path.Combine(this._folderPath, "assets");
			}
		}

		/// <summary>The <c>output_frames</c> sub-folder where exported PNG frames are written.</summary>
		public string OutputFramesFolder
		{
			get
			{
				return Path.Combine(this._folderPath, "output_frames");
			}
		}

		/// <summary>The <c>output_video</c> sub-folder where exported videos are written.</summary>
		public string OutputVideoFolder
		{
			get
			{
				return Path.Combine(this._folderPath, "output_video");
			}
		}

		/// <summary>The <c>scene_images</c> sub-folder into which scene images chosen from elsewhere are copied.</summary>
		public string SceneImageFolder
		{
			get
			{
				return Path.Combine(this._folderPath, "scene_images");
			}
		}

		/// <summary>
		/// Re-stamps the project name and writes <see cref="Config"/> to <see cref="ConfigFilePath"/>
		/// (atomically, via <see cref="ProjectConfig.Save(string)"/>).
		/// </summary>
		public void Save()
		{
			this._config.ProjectName = this.Name;
			this._config.Save(this.ConfigFilePath);
		}

		/// <summary>Full path of the project folder; its last segment is the project name.</summary>
		private readonly string _folderPath;

		/// <summary>The project's configuration; never null.</summary>
		private readonly ProjectConfig _config;
	}
}
