using System;
using System.Collections.Generic;
using System.Globalization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The "simulation" section of a project: step count, visualisation and export options, and solver
	/// parameters. Edited by <c>Form1</c> on the UI thread, persisted through <see cref="ProjectConfig"/> and
	/// handed to the runner as a <see cref="Clone"/> inside the <see cref="SimulationJob"/>.
	/// </summary>
	public sealed class SimulationSettings
	{
		/// <summary>Creates settings with the UI defaults (preview on, export off, 4th-order stencil, CPU engine); <see cref="Steps"/> and <see cref="TimeDecayFactor"/> stay 0.</summary>
		public SimulationSettings()
		{
			this.VisualizeEvery = 2;
			this.ExportField = false;
			this.ExportIntensity = false;
			this.PreviewField = true;
			this.PreviewIntensity = true;
			this.FieldColorScheme = "Default_Field";
			this.IntensityColorScheme = "Default_intensity";
			this.FieldBrightness = 30.0;
			this.IntensityBrightness = 30.0;
			this.FieldBlackLevel = -0.05;
			this.IntensityBlackLevel = 0.0;
			this.SourceFrequencyScale = 2.0;
			this.VideoFps = 60;
			this.VideoQuality = DefaultVideoQuality;
			this.UseGpu = false;
			this.Use4thOrderLaplacian = true;
			this.WaveSpeedC = 1.0;
			this.IntensityScale = 1.0;
		}

		/// <summary>Total number of simulation steps to run.</summary>
		public int Steps { get; set; }

		/// <summary>Render, export and collect probe data every this many steps.</summary>
		public int VisualizeEvery { get; set; }

		/// <summary>Whether to write the field frames to a video file.</summary>
		public bool ExportField { get; set; }

		/// <summary>Whether to write the intensity frames to a video file.</summary>
		public bool ExportIntensity { get; set; }

		/// <summary>Whether to show the field in the live preview.</summary>
		public bool PreviewField { get; set; }

		/// <summary>Whether to show the intensity in the live preview.</summary>
		public bool PreviewIntensity { get; set; }

		/// <summary>Name of the <see cref="ColorScheme"/> used for field frames.</summary>
		public string FieldColorScheme { get; set; }

		/// <summary>Name of the <see cref="ColorScheme"/> used for intensity frames.</summary>
		public string IntensityColorScheme { get; set; }

		/// <summary>Display gain for field frames (multiplies the field value before the colour lookup).</summary>
		public double FieldBrightness { get; set; }

		/// <summary>Display gain for intensity frames (multiplies the square root of the intensity before the colour lookup).</summary>
		public double IntensityBrightness { get; set; }

		/// <summary>Offset baked into the field colour LUT (the "offset" spinner in the UI); see <see cref="ColorLut.Build"/>.</summary>
		public double FieldBlackLevel { get; set; }

		/// <summary>Offset baked into the intensity colour LUT (the "offset" spinner in the UI); see <see cref="ColorLut.Build"/>.</summary>
		public double IntensityBlackLevel { get; set; }

		/// <summary>Multiplier in the source level-to-frequency mapping: angular frequency = level/255 × 0.5 × this, in radians per step.</summary>
		public double SourceFrequencyScale { get; set; }

		/// <summary>Frame rate of the exported videos.</summary>
		public int VideoFps { get; set; }

		/// <summary>Encoder quality of the exported videos, 1..100.</summary>
		public int VideoQuality { get; set; }

		/// <summary>Prefer the OpenCL engine; the CPU engine is used when no usable GPU is found.</summary>
		public bool UseGpu { get; set; }

		/// <summary>Use the 4th-order Laplacian stencil (with sub-stepping) instead of the 9-point one.</summary>
		public bool Use4thOrderLaplacian { get; set; }

		/// <summary>
		/// Nominal wave speed c in canvas pixels per step, used only to estimate a source's wavelength in the UI
		/// (λ = c / frequency); the engines derive the actual speed from the refractive index map.
		/// </summary>
		public double WaveSpeedC { get; set; }

		/// <summary>Intensity averaging time factor: the engines average over timeDecayFactor × 100 / (lowest source level) steps.</summary>
		public double TimeDecayFactor { get; set; }

		/// <summary>Multiplier applied to the intensity values produced by the engine.</summary>
		public double IntensityScale { get; set; }

		/// <summary>Shallow copy; all members are value types or immutable strings, so the copy is independent.</summary>
		public SimulationSettings Clone()
		{
			return (SimulationSettings)base.MemberwiseClone();
		}

		/// <summary>
		/// Reads the "simulation" section of <paramref name="config"/>. Missing keys and out-of-range values fall
		/// back to per-key defaults; a missing section yields a default instance.
		/// </summary>
		/// <remarks>
		/// The per-key fallbacks differ from the constructor defaults for some keys (steps 2500 vs 0,
		/// use_4th_order_laplacian false vs true, wave_speed_c 0.563 vs 1.0, time_decay_factor 10 vs 0).
		/// </remarks>
		public static SimulationSettings FromConfig(ProjectConfig config)
		{
			SimulationSettings settings = new SimulationSettings();
			if (!config.ContainsKey(SectionName))
			{
				return settings;
			}
			Dictionary<string, object> section = config.GetSection(SectionName);
			settings.Steps = SimulationSettings.ReadInt(section, "steps", DefaultSteps, 1, MaxSteps);
			settings.VisualizeEvery = SimulationSettings.ReadInt(section, "visualize_every", 2, 1, MaxVisualizeEvery);
			settings.ExportField = SimulationSettings.ReadBool(section, "export_field", false);
			settings.ExportIntensity = SimulationSettings.ReadBool(section, "export_intensity", false);
			settings.PreviewField = SimulationSettings.ReadBool(section, "preview_field", true);
			settings.PreviewIntensity = SimulationSettings.ReadBool(section, "preview_intensity", true);
			settings.FieldColorScheme = SimulationSettings.ReadText(section, "field_color_scheme", "Default_Field");
			settings.IntensityColorScheme = SimulationSettings.ReadText(section, "intensity_color_scheme", "Default_intensity");
			settings.FieldBrightness = SimulationSettings.ReadDouble(section, "field_brightness", 30.0, 0.0001, 100000.0);
			settings.IntensityBrightness = SimulationSettings.ReadDouble(section, "intensity_brightness", 30.0, 0.0001, 100000.0);
			settings.FieldBlackLevel = SimulationSettings.ReadDouble(section, "field_black_level", -0.05, -10.0, 1.0);
			settings.IntensityBlackLevel = SimulationSettings.ReadDouble(section, "intensity_black_level", 0.0, -10.0, 1.0);
			settings.SourceFrequencyScale = SimulationSettings.ReadDouble(section, "source_frequency_scale", 2.0, 0.0, 1000.0);
			settings.VideoFps = SimulationSettings.ReadInt(section, "video_fps", 60, 1, MaxVideoFps);
			settings.VideoQuality = SimulationSettings.ReadInt(section, "video_quality", DefaultVideoQuality, 1, 100);
			settings.UseGpu = SimulationSettings.ReadBool(section, "use_gpu", false);
			settings.Use4thOrderLaplacian = SimulationSettings.ReadBool(section, "use_4th_order_laplacian", false);
			settings.WaveSpeedC = SimulationSettings.ReadDouble(section, "wave_speed_c", 0.563, 1E-06, 1000000.0);
			settings.TimeDecayFactor = SimulationSettings.ReadDouble(section, "time_decay_factor", 10.0, 1.0, 100.0);
			settings.IntensityScale = SimulationSettings.ReadDouble(section, "intensity_scale", 1.0, 1E-07, 1000000.0);
			return settings;
		}

		/// <summary>Writes every value into the "simulation" section of <paramref name="config"/>, creating the section if needed.</summary>
		public void ToConfig(ProjectConfig config)
		{
			Dictionary<string, object> section = config.GetSection(SectionName);
			section["steps"] = this.Steps;
			section["visualize_every"] = this.VisualizeEvery;
			section["export_field"] = this.ExportField;
			section["export_intensity"] = this.ExportIntensity;
			section["preview_field"] = this.PreviewField;
			section["preview_intensity"] = this.PreviewIntensity;
			section["field_color_scheme"] = this.FieldColorScheme;
			section["intensity_color_scheme"] = this.IntensityColorScheme;
			section["field_brightness"] = this.FieldBrightness;
			section["intensity_brightness"] = this.IntensityBrightness;
			section["field_black_level"] = this.FieldBlackLevel;
			section["intensity_black_level"] = this.IntensityBlackLevel;
			section["source_frequency_scale"] = this.SourceFrequencyScale;
			section["video_fps"] = this.VideoFps;
			section["video_quality"] = this.VideoQuality;
			section["use_gpu"] = this.UseGpu;
			section["use_4th_order_laplacian"] = this.Use4thOrderLaplacian;
			section["wave_speed_c"] = this.WaveSpeedC;
			section["time_decay_factor"] = this.TimeDecayFactor;
			section["intensity_scale"] = this.IntensityScale;
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

		/// <summary>Name of this section in the project file.</summary>
		public const string SectionName = "simulation";

		/// <summary>Fallback for <see cref="Steps"/> when the key is missing from a project file.</summary>
		public const int DefaultSteps = 2500;

		/// <summary>Largest accepted <see cref="Steps"/>.</summary>
		public const int MaxSteps = 10000000;

		/// <summary>Largest accepted <see cref="VisualizeEvery"/>.</summary>
		public const int MaxVisualizeEvery = 1000;

		/// <summary>Largest accepted <see cref="VideoFps"/>.</summary>
		public const int MaxVideoFps = 240;

		/// <summary>Default <see cref="VideoQuality"/>.</summary>
		public const int DefaultVideoQuality = 70;
	}
}
