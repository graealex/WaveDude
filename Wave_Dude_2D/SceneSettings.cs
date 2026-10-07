using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The "scene" section of a project: where the canvas comes from (an image or a blank user-sized canvas),
	/// the damping border and the source options. Edited by <c>Form1</c> on the UI thread and persisted through
	/// <see cref="ProjectConfig"/>; the runner turns it into a <see cref="SceneData"/> when a run starts.
	/// </summary>
	public sealed class SceneSettings
	{
		/// <summary>Creates settings with the defaults: a blank 1920 × 1080 canvas, 64 px damping edge, no sources.</summary>
		public SceneSettings()
		{
			this.UseImage = false;
			this.ImageFile = "";
			this.UserWidth = DefaultWidth;
			this.UserHeight = DefaultHeight;
			this.DampingEdgeSize = DefaultDampingEdgeSize;
			this.UseSources = false;
			this.ImageSourceScale = DefaultImageSourceScale;
			this.BackgroundRefractiveIndex = DefaultBackgroundRefractiveIndex;
		}

		/// <summary>True to build the canvas from <see cref="ImageFile"/>; false for a blank <see cref="UserWidth"/> × <see cref="UserHeight"/> canvas.</summary>
		public bool UseImage { get; set; }

		/// <summary>Scene image path as stored in the project (relative to the project folder when inside it; see <see cref="ToStoredPath"/>), or "".</summary>
		public string ImageFile { get; set; }

		/// <summary>Width in canvas pixels of a user-defined canvas.</summary>
		public int UserWidth { get; set; }

		/// <summary>Height in canvas pixels of a user-defined canvas.</summary>
		public int UserHeight { get; set; }

		/// <summary>Whether a damping frame is applied around the canvas edge.</summary>
		public bool DampingEdgeEnabled { get; set; }

		/// <summary>Thickness of the damping frame in canvas pixels.</summary>
		public int DampingEdgeSize { get; set; }

		/// <summary>Whether green pixels of the scene image act as wave sources.</summary>
		public bool UseSources { get; set; }

		/// <summary>Peak amplitude of the image sources (passed to the engine as the source amplitude).</summary>
		public double ImageSourceScale { get; set; }

		/// <summary>Whether the interactive object sources of the project are used in the run.</summary>
		public bool UseObjectSources { get; set; }

		/// <summary>Refractive index of a user-defined canvas (c = 1/n cells per step); 1.0 to 2.55.</summary>
		public double BackgroundRefractiveIndex { get; set; }

		/// <summary>
		/// Reads the "scene" section of <paramref name="config"/>. Missing keys and out-of-range values fall back to
		/// the defaults; a missing section yields a default instance.
		/// </summary>
		public static SceneSettings FromConfig(ProjectConfig config)
		{
			SceneSettings settings = new SceneSettings();
			if (!config.ContainsKey(SectionName))
			{
				return settings;
			}
			Dictionary<string, object> section = config.GetSection(SectionName);
			settings.UseImage = string.CompareOrdinal(SceneSettings.ReadText(section, "mode", ModeUserDefined), ModeImage) == 0;
			settings.ImageFile = SceneSettings.ReadText(section, "image_file", "");
			settings.UserWidth = SceneSettings.ReadInt(section, "user_width", DefaultWidth, MinCanvasSize, MaxCanvasSize);
			settings.UserHeight = SceneSettings.ReadInt(section, "user_height", DefaultHeight, MinCanvasSize, MaxCanvasSize);
			settings.DampingEdgeEnabled = SceneSettings.ReadBool(section, "damping_edge_enabled", false);
			settings.DampingEdgeSize = SceneSettings.ReadInt(section, "damping_edge_size_px", DefaultDampingEdgeSize, 1, MaxDampingEdgeSize);
			settings.UseSources = SceneSettings.ReadBool(section, "use_sources", false);
			settings.ImageSourceScale = SceneSettings.ReadDouble(section, "image_source_scale", DefaultImageSourceScale, 0.0, MaxImageSourceScale);
			settings.UseObjectSources = SceneSettings.ReadBool(section, "use_object_sources", false);
			settings.BackgroundRefractiveIndex = SceneSettings.ReadDouble(section, "background_refractive_index", DefaultBackgroundRefractiveIndex, MinBackgroundRefractiveIndex, MaxBackgroundRefractiveIndex);
			return settings;
		}

		/// <summary>Writes every value into the "scene" section of <paramref name="config"/>, creating the section if needed.</summary>
		public void ToConfig(ProjectConfig config)
		{
			Dictionary<string, object> section = config.GetSection(SectionName);
			section["mode"] = (this.UseImage ? ModeImage : ModeUserDefined);
			section["image_file"] = this.ImageFile;
			section["user_width"] = this.UserWidth;
			section["user_height"] = this.UserHeight;
			section["damping_edge_enabled"] = this.DampingEdgeEnabled;
			section["damping_edge_size_px"] = this.DampingEdgeSize;
			section["use_sources"] = this.UseSources;
			section["image_source_scale"] = this.ImageSourceScale;
			section["use_object_sources"] = this.UseObjectSources;
			section["background_refractive_index"] = this.BackgroundRefractiveIndex;
		}

		/// <summary>
		/// Converts a full path to the form stored in the project file: relative to the project folder
		/// (with forward slashes) when it lies inside it (case-insensitive prefix match), otherwise unchanged.
		/// </summary>
		/// <param name="projectFolder">Project folder; when null or empty <paramref name="fullPath"/> is returned as is.</param>
		/// <param name="fullPath">Absolute path of the scene image.</param>
		public static string ToStoredPath(string projectFolder, string fullPath)
		{
			if (string.IsNullOrEmpty(projectFolder))
			{
				return fullPath;
			}
			string folderPrefix = projectFolder.TrimEnd(new char[]
			{
				Path.DirectorySeparatorChar,
				Path.AltDirectorySeparatorChar
			}) + Path.DirectorySeparatorChar.ToString();
			if (fullPath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
			{
				return fullPath.Substring(folderPrefix.Length).Replace(Path.DirectorySeparatorChar, '/');
			}
			return fullPath;
		}

		/// <summary>
		/// Inverse of <see cref="ToStoredPath"/>: resolves a stored (possibly relative, forward-slash) path
		/// against the project folder. Rooted paths are returned unchanged.
		/// </summary>
		/// <returns>The full path, or "" when <paramref name="storedPath"/> is null or empty.</returns>
		public static string ToFullPath(string projectFolder, string storedPath)
		{
			if (string.IsNullOrEmpty(storedPath))
			{
				return "";
			}
			if (Path.IsPathRooted(storedPath))
			{
				return storedPath;
			}
			return Path.Combine(projectFolder, storedPath.Replace('/', Path.DirectorySeparatorChar));
		}

		/// <summary>The value under <paramref name="key"/> converted to text (invariant culture), or the fallback when absent or null.</summary>
		private static string ReadText(Dictionary<string, object> section, string key, string fallback)
		{
			object value;
			if (section.TryGetValue(key, out value) && value != null)
			{
				return Convert.ToString(value, CultureInfo.InvariantCulture);
			}
			return fallback;
		}

		/// <summary>The value under <paramref name="key"/> if it is an actual <see cref="bool"/>; anything else yields the fallback.</summary>
		private static bool ReadBool(Dictionary<string, object> section, string key, bool fallback)
		{
			object value;
			if (section.TryGetValue(key, out value) && value is bool)
			{
				return Convert.ToBoolean(value);
			}
			return fallback;
		}

		/// <summary>
		/// The value under <paramref name="key"/> converted to a double (invariant culture) when it converts and
		/// lies within [<paramref name="minValue"/>, <paramref name="maxValue"/>]; otherwise the fallback.
		/// </summary>
		private static double ReadDouble(Dictionary<string, object> section, string key, double fallback, double minValue, double maxValue)
		{
			object value;
			if (section.TryGetValue(key, out value) && value != null)
			{
				try
				{
					double parsed = Convert.ToDouble(value, CultureInfo.InvariantCulture);
					if (parsed >= minValue && parsed <= maxValue)
					{
						return parsed;
					}
				}
				catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
				{
				}
			}
			return fallback;
		}

		/// <summary>
		/// The value under <paramref name="key"/> converted to an int (invariant culture) when it converts and
		/// lies within [<paramref name="minValue"/>, <paramref name="maxValue"/>]; otherwise the fallback.
		/// </summary>
		private static int ReadInt(Dictionary<string, object> section, string key, int fallback, int minValue, int maxValue)
		{
			object value;
			if (section.TryGetValue(key, out value) && value != null)
			{
				try
				{
					int parsed = Convert.ToInt32(value, CultureInfo.InvariantCulture);
					if (parsed >= minValue && parsed <= maxValue)
					{
						return parsed;
					}
				}
				catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
				{
				}
			}
			return fallback;
		}

		/// <summary>Name of this section in the project file.</summary>
		public const string SectionName = "scene";

		/// <summary>Value of the "mode" key when the canvas comes from an image.</summary>
		public const string ModeImage = "image";

		/// <summary>Value of the "mode" key for a blank user-sized canvas.</summary>
		public const string ModeUserDefined = "user_defined";

		/// <summary>Default <see cref="UserWidth"/> in canvas pixels.</summary>
		public const int DefaultWidth = 1920;

		/// <summary>Default <see cref="UserHeight"/> in canvas pixels.</summary>
		public const int DefaultHeight = 1080;

		/// <summary>Default <see cref="DampingEdgeSize"/> in canvas pixels.</summary>
		public const int DefaultDampingEdgeSize = 64;

		/// <summary>Smallest accepted canvas width or height in pixels.</summary>
		public const int MinCanvasSize = 8;

		/// <summary>Largest accepted canvas width or height in pixels.</summary>
		public const int MaxCanvasSize = 16384;

		/// <summary>Largest accepted <see cref="DampingEdgeSize"/> in pixels.</summary>
		public const int MaxDampingEdgeSize = 8192;

		/// <summary>Default <see cref="ImageSourceScale"/>.</summary>
		public const double DefaultImageSourceScale = 1.0;

		/// <summary>Largest accepted <see cref="ImageSourceScale"/>.</summary>
		public const double MaxImageSourceScale = 1000.0;

		/// <summary>Default <see cref="BackgroundRefractiveIndex"/>.</summary>
		public const double DefaultBackgroundRefractiveIndex = 1.0;

		/// <summary>Smallest accepted <see cref="BackgroundRefractiveIndex"/>.</summary>
		public const double MinBackgroundRefractiveIndex = 1.0;

		/// <summary>Largest accepted <see cref="BackgroundRefractiveIndex"/>.</summary>
		public const double MaxBackgroundRefractiveIndex = 2.55;
	}
}
