using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Static helper that creates, opens, lists, renames and deletes project folders under a root
	/// folder, and is the only code that constructs <see cref="Project"/> instances. A project folder
	/// is any folder directly below the root that contains a configuration.json (or an empty legacy
	/// Configuration.txt, which <see cref="Open"/> upgrades in place); the folder name is the project name.
	/// The main folder of a project contains configuration.json and the sub-folders
	/// <see cref="AssetsFolderName"/>, <see cref="OutputFramesFolderName"/>, <see cref="OutputVideoFolderName"/>
	/// and <see cref="SceneImageFolderName"/>. The configuration is entirely in configuration.json; assets is
	/// instead where the user can keep any third-party files used by the project, such as the original
	/// Photoshop file of a scene image. Used by the Projects and Outputs tabs and by the File menu; all
	/// methods are synchronous and do their own disk I/O.
	/// </summary>
	public sealed class ProjectStore
	{
		/// <summary>Not instantiable; all members are static.</summary>
		private ProjectStore()
		{
		}

		/// <summary>
		/// Checks that <paramref name="projectName"/> can be used as a folder name on Windows: not
		/// blank, no surrounding whitespace, no trailing dot, at most <see cref="MaxNameLength"/>
		/// characters, no characters from <see cref="Path.GetInvalidFileNameChars"/>, and not a
		/// reserved device name (CON, NUL, COM1, ...; the part before the first dot is checked).
		/// The name is never changed silently.
		/// </summary>
		/// <returns>A user-facing error message, or "" when the name is acceptable.</returns>
		public static string ValidateName(string projectName)
		{
			if (string.IsNullOrWhiteSpace(projectName))
			{
				return "Enter a project name.";
			}
			if (projectName != projectName.Trim())
			{
				return "The project name must not start or end with a space.";
			}
			if (projectName.EndsWith(".", StringComparison.Ordinal))
			{
				return "The project name must not end with a dot.";
			}
			if (projectName.Length > ProjectStore.MaxNameLength)
			{
				return "The project name is too long (at most " + ProjectStore.MaxNameLength.ToString() + " characters).";
			}
			char[] invalidChars = Path.GetInvalidFileNameChars();
			foreach (char ch in projectName)
			{
				if (Array.IndexOf(invalidChars, ch) >= 0)
				{
					if (ch < ' ')
					{
						return "The project name contains a control character.";
					}
					return "The project name contains the character '" + ch.ToString() + "', which is not allowed in a folder name.";
				}
			}
			string baseName = projectName.Split('.')[0].ToUpperInvariant();
			if (Array.IndexOf(ProjectStore.ReservedNames, baseName) >= 0)
			{
				return "'" + projectName + "' is a reserved name in Windows and cannot be used as a folder name.";
			}
			return "";
		}

		/// <summary>Full path of the folder a project named <paramref name="projectName"/> has (or would have) under <paramref name="rootFolder"/>. Does not touch the disk.</summary>
		public static string FolderPathFor(string rootFolder, string projectName)
		{
			return Path.Combine(ProjectStore.NormalizeFolder(rootFolder), projectName);
		}

		/// <summary>True when <paramref name="folderPath"/> exists and contains a configuration.json or a legacy Configuration.txt.</summary>
		public static bool IsProjectFolder(string folderPath)
		{
			if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
			{
				return false;
			}
			return File.Exists(Path.Combine(folderPath, ProjectConfig.FileName)) || File.Exists(Path.Combine(folderPath, ProjectConfig.LegacyFileName));
		}

		/// <summary>
		/// Opens the project in <paramref name="folderPath"/> and makes sure its standard sub-folders
		/// exist. A folder that only has an empty legacy Configuration.txt is upgraded:
		/// a fresh configuration.json is written and the legacy file deleted.
		/// </summary>
		/// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
		/// <exception cref="InvalidDataException">The folder has no configuration file, its legacy
		/// Configuration.txt is not empty, or configuration.json is malformed or from a newer format
		/// version (see <see cref="ProjectConfig.Load"/>).</exception>
		public static Project Open(string folderPath)
		{
			string folder = ProjectStore.NormalizeFolder(folderPath);
			if (!Directory.Exists(folder))
			{
				throw new DirectoryNotFoundException("The folder '" + folder + "' does not exist.");
			}
			string configPath = Path.Combine(folder, ProjectConfig.FileName);
			string legacyPath = Path.Combine(folder, ProjectConfig.LegacyFileName);
			Project project;
			if (File.Exists(configPath))
			{
				project = new Project(folder, ProjectConfig.Load(configPath));
			}
			else
			{
				if (!File.Exists(legacyPath))
				{
					throw new InvalidDataException("The folder '" + Path.GetFileName(folder) + "' is not a project folder: it contains no " + ProjectConfig.FileName + ".");
				}
				if (File.ReadAllText(legacyPath).Trim().Length > 0)
				{
					throw new InvalidDataException("The file " + ProjectConfig.LegacyFileName + " is not empty and has no known format, so it was not replaced.");
				}
				project = new Project(folder, new ProjectConfig(Path.GetFileName(folder)));
				project.Save();
				File.Delete(legacyPath);
			}
			ProjectStore.EnsureSubFolders(folder);
			return project;
		}

		/// <summary>
		/// Creates the folder <c>rootFolder\projectName</c> with its standard sub-folders and an
		/// initial configuration.json, and returns the opened project. An existing folder is
		/// accepted only when it is completely empty.
		/// </summary>
		/// <exception cref="ArgumentException">The name fails <see cref="ValidateName"/> or the root folder is blank.</exception>
		/// <exception cref="IOException">A project of that name already exists, or the folder exists and is not empty.</exception>
		public static Project Create(string rootFolder, string projectName)
		{
			string error = ProjectStore.ValidateName(projectName);
			if (error.Length > 0)
			{
				throw new ArgumentException(error, "projectName");
			}
			if (string.IsNullOrWhiteSpace(rootFolder))
			{
				throw new ArgumentException("Enter a root folder.", "rootFolder");
			}
			string folder = ProjectStore.FolderPathFor(rootFolder, projectName);
			if (ProjectStore.IsProjectFolder(folder))
			{
				throw new IOException("A project named '" + projectName + "' already exists in this root folder. Use 'Open existing' to open it.");
			}
			if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
			{
				throw new IOException("The folder '" + folder + "' already exists and is not empty, so it cannot be used for a new project.");
			}
			Directory.CreateDirectory(folder);
			ProjectStore.EnsureSubFolders(folder);
			Project project = new Project(folder, new ProjectConfig(projectName));
			project.Save();
			return project;
		}

		/// <summary>
		/// Names (folder names, not paths) of all project folders directly below
		/// <paramref name="rootFolder"/>, sorted case-insensitively.
		/// </summary>
		/// <returns>An empty list when the root is blank, missing or cannot be enumerated (I/O or access errors are swallowed).</returns>
		public static List<string> ListProjects(string rootFolder)
		{
			List<string> names = new List<string>();
			if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
			{
				return names;
			}
			try
			{
				foreach (string folder in Directory.GetDirectories(rootFolder))
				{
					if (ProjectStore.IsProjectFolder(folder))
					{
						names.Add(Path.GetFileName(folder));
					}
				}
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				return new List<string>();
			}
			names.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
			return names;
		}

		/// <summary>Deletes a project folder and everything inside it.</summary>
		/// <exception cref="InvalidDataException">The folder is not a project folder (safety check so arbitrary folders cannot be removed).</exception>
		public static void Delete(string folderPath)
		{
			string folder = ProjectStore.NormalizeFolder(folderPath);
			if (!ProjectStore.IsProjectFolder(folder))
			{
				throw new InvalidDataException("The folder '" + Path.GetFileName(folder) + "' is not a project folder, so it was not deleted.");
			}
			Directory.Delete(folder, true);
		}

		/// <summary>
		/// Renames the project folder <paramref name="folderPath"/> to <paramref name="newProjectName"/>
		/// within the same root and returns the new folder path. A name that differs only in case
		/// is treated as unchanged and the original path is returned without touching the disk.
		/// </summary>
		/// <exception cref="InvalidDataException">The folder is not a project folder.</exception>
		/// <exception cref="ArgumentException">The new name fails <see cref="ValidateName"/>.</exception>
		/// <exception cref="IOException">A file or folder with the new name already exists.</exception>
		public static string Rename(string folderPath, string newProjectName)
		{
			string folder = ProjectStore.NormalizeFolder(folderPath);
			if (!ProjectStore.IsProjectFolder(folder))
			{
				throw new InvalidDataException("The folder '" + Path.GetFileName(folder) + "' is not a project folder, so it was not renamed.");
			}
			string error = ProjectStore.ValidateName(newProjectName);
			if (error.Length > 0)
			{
				throw new ArgumentException(error, "newProjectName");
			}
			string rootFolder = Path.GetDirectoryName(folder);
			string newFolder = ProjectStore.FolderPathFor(rootFolder, newProjectName);
			if (string.Equals(folder, newFolder, StringComparison.OrdinalIgnoreCase))
			{
				// only a case change, or unchanged
				return folder;
			}
			if (Directory.Exists(newFolder) || File.Exists(newFolder))
			{
				throw new IOException("'" + newProjectName + "' already exists in this root folder. Choose a different name.");
			}
			Directory.Move(folder, newFolder);
			return newFolder;
		}

		/// <summary>Creates the standard sub-folders (<see cref="SubFolderNames"/>) inside a project folder; I/O and access errors are not fatal here.</summary>
		private static void EnsureSubFolders(string folder)
		{
			try
			{
				foreach (string name in ProjectStore.SubFolderNames)
				{
					Directory.CreateDirectory(Path.Combine(folder, name));
				}
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				// not fatal: a folder that cannot be created is reported when it is needed
			}
		}

		/// <summary>
		/// Canonical form of a folder path for comparisons: trimmed, made absolute, and without a
		/// trailing separator (except for a bare drive root such as <c>C:\</c>).
		/// </summary>
		internal static string NormalizeFolder(string folderPath)
		{
			string path = Path.GetFullPath(folderPath.Trim());
			string pathRoot = Path.GetPathRoot(path);
			if (path.Length > pathRoot.Length)
			{
				path = path.TrimEnd(new char[]
				{
					Path.DirectorySeparatorChar,
					Path.AltDirectorySeparatorChar
				});
			}
			return path;
		}

		/// <summary>Sub-folder where the user keeps any third-party files that belong to the project, such as the original Photoshop file of a scene image; never read or written by the simulator itself (counted in the delete warning).</summary>
		public const string AssetsFolderName = "assets";

		/// <summary>Sub-folder the simulation writes rendered PNG frames into.</summary>
		public const string OutputFramesFolderName = "output_frames";

		/// <summary>Sub-folder the simulation writes video files into.</summary>
		public const string OutputVideoFolderName = "output_video";

		/// <summary>Sub-folder that holds the project's scene images (imported images are copied here).</summary>
		public const string SceneImageFolderName = "scene_images";

		/// <summary>Maximum length of a project name in characters.</summary>
		private const int MaxNameLength = 100;

		/// <summary>The sub-folders every project folder gets, in creation order.</summary>
		private static readonly string[] SubFolderNames = new string[] { AssetsFolderName, OutputFramesFolderName, OutputVideoFolderName, SceneImageFolderName };

		/// <summary>Names that Windows does not allow for a file or folder (compared upper-case against the part before the first dot).</summary>
		private static readonly string[] ReservedNames = new string[]
		{
			"CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6",
			"COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
			"LPT8", "LPT9"
		};
	}
}
