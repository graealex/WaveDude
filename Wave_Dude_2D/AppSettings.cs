using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Per-user application settings, persisted as a small JSON object in
	/// <c>%AppData%\2D_wave_sim_V01\settings.json</c>. <see cref="Form1"/> loads one copy at
	/// start-up with <see cref="Load"/> and calls <see cref="Save"/> whenever the project root
	/// or the open project changes. Loading and saving never throw: on any error the defaults
	/// are used or the write is silently skipped.
	/// </summary>
	public sealed class AppSettings
	{
		/// <summary>Starts with all three values empty (no root chosen, no project remembered).</summary>
		public AppSettings()
		{
			this.LastRootFolder = "";
			this.LastProjectName = "";
			this.ProjectRoot = "";
		}

		/// <summary>
		/// Root folder of the project opened most recently. Written on every project open and
		/// persisted, but not read back by the application at present.
		/// </summary>
		public string LastRootFolder { get; set; }

		/// <summary>
		/// Name (folder name) of the project opened most recently. Written on every project open
		/// and persisted, but not read back by the application at present.
		/// </summary>
		public string LastProjectName { get; set; }

		/// <summary>
		/// The folder that contains the user's project folders (File &gt; Choose project root).
		/// Empty until the user has chosen one; <see cref="Form1"/> keeps most of the UI disabled
		/// while it is empty.
		/// </summary>
		public string ProjectRoot { get; set; }

		/// <summary>Full path of the settings file under the roaming application-data folder.</summary>
		private static string SettingsFilePath()
		{
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "2D_wave_sim_V01", "settings.json");
		}

		/// <summary>
		/// Reads the settings file. Missing file, unreadable JSON or missing keys all yield the
		/// corresponding default (empty string); nothing is thrown.
		/// </summary>
		/// <returns>A new instance; never null.</returns>
		public static AppSettings Load()
		{
			AppSettings settings = new AppSettings();
			try
			{
				string path = AppSettings.SettingsFilePath();
				if (File.Exists(path))
				{
					JavaScriptSerializer serializer = new JavaScriptSerializer();
					Dictionary<string, object> values = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
					if (values != null)
					{
						settings.LastRootFolder = AppSettings.ReadText(values, KeyRootFolder);
						settings.LastProjectName = AppSettings.ReadText(values, KeyProjectName);
						settings.ProjectRoot = AppSettings.ReadText(values, KeyProjectRoot);
					}
				}
			}
			catch
			{
			}
			return settings;
		}

		/// <summary>
		/// Writes the three values as a flat JSON object (UTF-8 without BOM), creating the folder
		/// if needed. Any I/O or serialisation error is swallowed.
		/// </summary>
		public void Save()
		{
			try
			{
				string path = AppSettings.SettingsFilePath();
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				Dictionary<string, string> values = new Dictionary<string, string>();
				values[KeyRootFolder] = this.LastRootFolder;
				values[KeyProjectName] = this.LastProjectName;
				values[KeyProjectRoot] = this.ProjectRoot;
				JavaScriptSerializer serializer = new JavaScriptSerializer();
				File.WriteAllText(path, serializer.Serialize(values), new UTF8Encoding(false));
			}
			catch
			{
			}
		}

		/// <summary>
		/// Returns the value stored under <paramref name="key"/> as text, or an empty string when
		/// the key is absent or null. Non-string JSON values are converted with the current culture.
		/// </summary>
		private static string ReadText(Dictionary<string, object> root, string key)
		{
			object value;
			if (root.TryGetValue(key, out value) && value != null)
			{
				return Convert.ToString(value);
			}
			return "";
		}

		/// <summary>JSON key of <see cref="LastRootFolder"/>.</summary>
		private const string KeyRootFolder = "last_root_folder";

		/// <summary>JSON key of <see cref="LastProjectName"/>.</summary>
		private const string KeyProjectName = "last_project_name";

		/// <summary>JSON key of <see cref="ProjectRoot"/>.</summary>
		private const string KeyProjectRoot = "project_root";
	}
}
