using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The "scene" section of a project (Configuration tab): where the canvas comes from (an image or a blank
	/// user-sized canvas), the damping border and the source options. Edited by <c>Form1</c> on the UI thread and
	/// persisted through <see cref="ProjectConfig"/>; the runner turns it into a <see cref="SceneData"/> when a run
	/// starts. The size of a scene image is not stored: it is read from the image itself. There is no scene-level
	/// "use probes" switch: each probe has its own "On" checkbox (<see cref="ProbeSettings.Active"/>) instead.
	/// </summary>
	public sealed class SceneSettings
	{
		/// <summary>
		/// Creates settings with the defaults of the Python example: a blank 1920 × 1080 canvas, 64 px damping
		/// edge, no sources. <see cref="DampingEdgeEnabled"/> and <see cref="UseObjectSources"/> deliberately have
		/// no software-chosen default of their own and stay false here, the same as
		/// <see cref="SimulationSettings.Steps"/> / <see cref="SimulationSettings.TimeDecayFactor"/>: a new project
		/// starts at whatever the Designer Checked value of <c>chkDampingEdge</c> / <c>chkUseObjectSources</c> is,
		/// which <c>StartNewProject</c> (Form1) reads right after creating this instance.
		/// </summary>
		public SceneSettings()
		{
			UseImage = false;
			ImageFile = "";
			UserWidth = DefaultWidth;
			UserHeight = DefaultHeight;
			DampingEdgeSize = DefaultDampingEdgeSize;
			UseSources = false;
			ImageSourceScale = DefaultImageSourceScale;
			BackgroundRefractiveIndex = DefaultBackgroundRefractiveIndex;
		}

		/// <summary>True to build the canvas from <see cref="ImageFile"/>; false for a blank <see cref="UserWidth"/> × <see cref="UserHeight"/> canvas.</summary>
		public bool UseImage { get; set; }

		/// <summary>Scene image path as stored in the project: relative to the project folder when inside it (forward slashes; see <see cref="ToStoredPath"/>), otherwise a full path; "" when none.</summary>
		public string ImageFile { get; set; }

		/// <summary>Width in canvas pixels of a user-defined canvas.</summary>
		public int UserWidth { get; set; }

		/// <summary>Height in canvas pixels of a user-defined canvas.</summary>
		public int UserHeight { get; set; }

		/// <summary>
		/// Whether a damping frame is applied around the canvas edge. No software-chosen default of its own, the
		/// same as <see cref="SimulationSettings.Steps"/> / <see cref="SimulationSettings.TimeDecayFactor"/>: a new
		/// project starts at whatever <c>chkDampingEdge</c>'s own Designer Checked value is (see
		/// <c>StartNewProject</c>, Form1). The false used by <see cref="FromConfig"/> is only the fallback for an
		/// existing project saved before this setting existed.
		/// </summary>
		public bool DampingEdgeEnabled { get; set; }

		/// <summary>Thickness of the damping frame in canvas pixels.</summary>
		public int DampingEdgeSize { get; set; }

		/// <summary>Whether green pixels of the scene image act as wave sources.</summary>
		public bool UseSources { get; set; }

		/// <summary>Peak amplitude of the image sources (passed to the engine as the source amplitude; <c>source_amplitude</c> of <c>StaticImageScene</c> in the Python code).</summary>
		public double ImageSourceScale { get; set; }

		/// <summary>
		/// Whether the object sources of the source list are used in the run. No software-chosen default of its
		/// own, the same as <see cref="DampingEdgeEnabled"/>: a new project starts at whatever
		/// <c>chkUseObjectSources</c>' own Designer Checked value is (see <c>StartNewProject</c>, Form1); the false
		/// used by <see cref="FromConfig"/> is only the fallback for a project saved before this setting existed.
		/// </summary>
		public bool UseObjectSources { get; set; }

		/// <summary>Refractive index of a user-defined canvas's background (c = 1/n cells per step), 1.0 to 2.55 (a red channel value of 100 to 255, as in a scene image, which has its own value per pixel from its red channel).</summary>
		public double BackgroundRefractiveIndex { get; set; }

		/// <summary>
		/// Reads the "scene" section of <paramref name="config"/>. Missing keys and out-of-range values fall back to
		/// the defaults; a missing section yields a default instance. The false fallbacks for
		/// <c>damping_edge_enabled</c> and <c>use_object_sources</c> are deliberate: they keep a project saved before
		/// those keys existed at its previous behaviour (see <see cref="DampingEdgeEnabled"/>).
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
			section["mode"] = (UseImage ? ModeImage : ModeUserDefined);
			section["image_file"] = ImageFile;
			section["user_width"] = UserWidth;
			section["user_height"] = UserHeight;
			section["damping_edge_enabled"] = DampingEdgeEnabled;
			section["damping_edge_size_px"] = DampingEdgeSize;
			section["use_sources"] = UseSources;
			section["image_source_scale"] = ImageSourceScale;
			section["use_object_sources"] = UseObjectSources;
			section["background_refractive_index"] = BackgroundRefractiveIndex;
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
					// use the fallback
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
					// use the fallback
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

		// the defaults are those of the Python example (1920 x 1080 canvas, 64 px damping edge)

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

		/// <summary>Default <see cref="BackgroundRefractiveIndex"/> of a user-defined (image-less) scene's background; the range is 1.0 to 2.55 (a red channel value of 100 to 255, as in a scene image).</summary>
		public const double DefaultBackgroundRefractiveIndex = 1.0;

		/// <summary>Smallest accepted <see cref="BackgroundRefractiveIndex"/>.</summary>
		public const double MinBackgroundRefractiveIndex = 1.0;

		/// <summary>Largest accepted <see cref="BackgroundRefractiveIndex"/>.</summary>
		public const double MaxBackgroundRefractiveIndex = 2.55;
	}
}
