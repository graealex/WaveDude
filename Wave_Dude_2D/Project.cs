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
			_folderPath = folderPath;
			_config = config;
			// the folder name is the one and only project name
			_config.ProjectName = Name;
		}

		/// <summary>Project name, i.e. the name of the project folder.</summary>
		public string Name => Path.GetFileName(_folderPath);

		/// <summary>Full path of the project folder.</summary>
		public string FolderPath => _folderPath;

		/// <summary>Parent of the project folder (the project root), or an empty string when the folder has no parent.</summary>
		public string RootFolder => Path.GetDirectoryName(_folderPath) ?? "";

		/// <summary>The configuration loaded from (or to be written to) <see cref="ConfigFilePath"/>; mutable, shared with the editor.</summary>
		public ProjectConfig Config => _config;

		/// <summary>Full path of the configuration file (<see cref="ProjectConfig.FileName"/>) inside the project folder.</summary>
		public string ConfigFilePath => Path.Combine(_folderPath, ProjectConfig.FileName);

		/// <summary>
		/// The <see cref="ProjectStore.AssetsFolderName"/> sub-folder: where the user can keep any third-party
		/// files used by the project (e.g. the original Photoshop file of a scene image). Not read or written by
		/// the simulator itself.
		/// </summary>
		public string AssetsFolder => Path.Combine(_folderPath, ProjectStore.AssetsFolderName);

		/// <summary>The <see cref="ProjectStore.OutputFramesFolderName"/> sub-folder where exported PNG frames are written.</summary>
		public string OutputFramesFolder => Path.Combine(_folderPath, ProjectStore.OutputFramesFolderName);

		/// <summary>The <see cref="ProjectStore.OutputVideoFolderName"/> sub-folder where exported videos are written.</summary>
		public string OutputVideoFolder => Path.Combine(_folderPath, ProjectStore.OutputVideoFolderName);

		/// <summary>The <see cref="ProjectStore.SceneImageFolderName"/> sub-folder into which scene images chosen from elsewhere are copied.</summary>
		public string SceneImageFolder => Path.Combine(_folderPath, ProjectStore.SceneImageFolderName);

		/// <summary>
		/// Re-stamps the project name and writes <see cref="Config"/> to <see cref="ConfigFilePath"/>
		/// (atomically, via <see cref="ProjectConfig.Save(string)"/>).
		/// </summary>
		public void Save()
		{
			_config.ProjectName = Name;
			_config.Save(ConfigFilePath);
		}

		/// <summary>Full path of the project folder; its last segment is the project name.</summary>
		private readonly string _folderPath;

		/// <summary>The project's configuration; never null.</summary>
		private readonly ProjectConfig _config;
	}
}
