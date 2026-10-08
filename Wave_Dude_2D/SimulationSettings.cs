using System;
using System.Collections.Generic;
using System.Globalization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The "simulation" section of a project (Configuration tab): step count, visualisation and export options,
	/// and solver parameters. Edited by <c>Form1</c> on the UI thread, persisted through <see cref="ProjectConfig"/>
	/// and handed to the runner as a <see cref="Clone"/> inside the <see cref="SimulationJob"/>.
	/// A few keys have no control on the screen yet but can be edited in the file: <c>field_brightness</c>,
	/// <c>intensity_brightness</c>, <c>field_black_level</c>, <c>intensity_black_level</c>,
	/// <c>source_frequency_scale</c>, <c>video_fps</c>. The defaults are those of the Python example
	/// (<c>main_curr.py</c>).
	/// </summary>
	public sealed class SimulationSettings
	{
		/// <summary>
		/// Creates settings with the defaults for a new project (preview on, export off, 4th-order stencil, CPU
		/// engine). <see cref="Steps"/> and <see cref="TimeDecayFactor"/> deliberately have no software-chosen
		/// default and stay 0: a new project starts at whatever the Designer value of <c>numSimulationSteps</c> /
		/// <c>numTimeDecayFactor</c> is, which <c>StartNewProject</c> (Form1) reads right after creating this
		/// instance. <see cref="DefaultSteps"/> is only the <see cref="FromConfig"/> fallback for a project saved
		/// before the key existed.
		/// </summary>
		public SimulationSettings()
		{
			VisualizeEvery = 2;
			ExportField = false;
			ExportIntensity = false;
			PreviewField = true;
			PreviewIntensity = true;
			FieldColorScheme = "Default_Field";
			IntensityColorScheme = "Default_intensity";
			FieldBrightness = 30.0;
			IntensityBrightness = 30.0;
			FieldBlackLevel = -0.05;
			IntensityBlackLevel = 0.0;
			SourceFrequencyScale = 2.0;
			VideoFps = 60;
			VideoQuality = DefaultVideoQuality;
			UseGpu = false;
			Use4thOrderLaplacian = true;
			WaveSpeedC = 1.0;
			IntensityScale = 1.0;
		}

		/// <summary>
		/// Total number of simulation steps to run. No software-chosen default of its own, the same as
		/// <see cref="TimeDecayFactor"/>: a new project starts at whatever <c>numSimulationSteps</c>' own Designer
		/// value is (see <c>StartNewProject</c>'s <c>new SimulationSettings()</c>); <see cref="DefaultSteps"/> is
		/// only the fallback for an existing project saved before "steps" existed (<see cref="FromConfig"/>).
		/// </summary>
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

		/// <summary>
		/// Name of the <see cref="ColorScheme"/> used for field frames (empty: grey levels). The name a new project
		/// starts with is selected by <c>FillSchemeCombo</c> (Form1) only when a scheme with that exact name
		/// (case-insensitive) and matching <see cref="ColorScheme.SchemeType"/> exists; otherwise the combo just
		/// shows nothing selected, the same as for any other scheme name that is not found.
		/// </summary>
		public string FieldColorScheme { get; set; }

		/// <summary>Name of the <see cref="ColorScheme"/> used for intensity frames (empty: grey levels); see <see cref="FieldColorScheme"/> for how the name is matched.</summary>
		public string IntensityColorScheme { get; set; }

		/// <summary>
		/// Display gain for field frames (multiplies the field value before the colour lookup). This is
		/// <c>render_field(7)</c> in the Python example; 30.0 here is high enough to see a single source in the
		/// simulation, which is what <c>numFieldScaling</c>'s own Designer default also shows for a new source.
		/// </summary>
		public double FieldBrightness { get; set; }

		/// <summary>Display gain for intensity frames (multiplies the square root of the intensity before the colour lookup); <c>render_intensity(7)</c> in the Python example, see <see cref="FieldBrightness"/>.</summary>
		public double IntensityBrightness { get; set; }

		/// <summary>Offset baked into the field colour LUT (the "offset" spinner in the UI); see <see cref="ColorLut.Build"/>.</summary>
		public double FieldBlackLevel { get; set; }

		/// <summary>Offset baked into the intensity colour LUT (the "offset" spinner in the UI); see <see cref="ColorLut.Build"/>.</summary>
		public double IntensityBlackLevel { get; set; }

		/// <summary>Multiplies the frequency of the sources that come from the green channel of the scene image (and of the object sources, which use the same level scale): angular frequency = level/255 × 0.5 × this, in radians per step.</summary>
		public double SourceFrequencyScale { get; set; }

		/// <summary>Frame rate of the exported videos.</summary>
		public int VideoFps { get; set; }

		/// <summary>Encoder quality of the exported videos, 1 (small files) to 100 (best picture, large files).</summary>
		public int VideoQuality { get; set; }

		/// <summary>Run the simulation on the graphics card (OpenCL); falls back to the processor when that is not possible.</summary>
		public bool UseGpu { get; set; }

		/// <summary>
		/// Use a 4th-order accurate discrete Laplacian (a 5-point "cross" in each of x and y, reaching 2 cells away)
		/// instead of the standard 3x3 kernel, everywhere it is safe to (see <c>WaveSimulator.UpdateField</c> /
		/// <c>GpuWaveSimulator.KernelSource</c>; the outermost ring of the domain still falls back to the 3x3 kernel,
		/// since it does not have 2 cells of margin). This reduces numerical dispersion (short wavelengths,
		/// under-resolved by the grid, are otherwise slowed down relative to long ones; see <see cref="WaveSpeedC"/>,
		/// whose legacy fallback is itself a symptom of this) at the cost of roughly twice the memory reads per step.
		/// On by default for a new project (the constructor). An existing project saved before this property existed
		/// still loads as Off (<see cref="FromConfig"/>'s own separate, hardcoded fallback), so it keeps its exact
		/// previous behaviour instead of silently picking up the new project default.
		/// </summary>
		public bool Use4thOrderLaplacian { get; set; }

		/// <summary>
		/// The propagation speed of waves through the medium, in canvas pixels per step (λ = c / frequency). Set
		/// from the "Set c-value" entry on the File menu. This is only used to estimate and display a source's
		/// wavelength (<c>lblLambdaIs</c> on the Sources tab); it does not affect the simulation itself, which
		/// derives the real, per-pixel wave speed from the scene's refractive index (the red channel of the scene
		/// image; see <c>SceneData.C2</c>) instead, and can vary from pixel to pixel while this single value cannot.
		/// A new project starts at 1.0, the solver's own default speed for a uniform medium (the constructor). An
		/// existing project saved before this field existed still loads at 0.563 instead (<see cref="FromConfig"/>'s
		/// own separate, hardcoded fallback): that value was found experimentally to be the correct one at around
		/// a source frequency of 100, and is kept only so such a project's displayed wavelength estimate does not
		/// change out from under it. Like every other property here, this belongs to one project's settings and is
		/// saved/loaded with it (<c>wave_speed_c</c>); it is not a global setting, so different projects can each
		/// keep their own value.
		/// </summary>
		public double WaveSpeedC { get; set; }

		/// <summary>
		/// "Intensity decay factor" (Configuration tab, <c>numTimeDecayFactor</c>): the intensity shown/exported is
		/// not the raw field², it is an exponential running average of it (see <c>WaveSimulator.UpdateField</c> /
		/// <c>GpuWaveSimulator.KernelSource</c>), because a source oscillates every step and the raw squared
		/// amplitude would flicker at twice the source frequency instead of showing a steady intensity. The
		/// averaging needs a time constant of at least a few periods of the slowest active source, or that flicker
		/// "prints through"; a fixed time constant long enough for a low-frequency source is unnecessarily long for
		/// a high-frequency one and shows as a visible trail behind anything that moves. Both engines therefore
		/// scale the time constant inversely with the frequency of the slowest active source (image or object)
		/// instead of using one fixed value, and this property is the number of steps that time constant works out
		/// to at a nominal medium source (green level <c>WaveSimulator.NominalDecayLevel</c>, 100). Raising it
		/// lengthens the trail on every source (helps a low-frequency source avoid print-through); lowering it
		/// shortens the trail (helps a high-frequency source look crisp) at the cost of print-through if a
		/// low-frequency source is also present in the same scene.
		/// This class does not itself pick a number for a new project; that would just be a second, competing
		/// default fighting the one already set on <c>numTimeDecayFactor</c> in the Designer. <c>StartNewProject</c>
		/// (Form1) instead reads the control's own value right after creating a new instance, and that is the only
		/// place a new project's starting value is decided; changing it means changing <c>numTimeDecayFactor</c>'s
		/// Value in the Designer. The 10.0 in <see cref="FromConfig"/> is only the fallback for a project saved
		/// before the key existed.
		/// </summary>
		public double TimeDecayFactor { get; set; }

		/// <summary>
		/// A plain multiplier on the tracked intensity itself (not just how it is displayed): both engines apply it
		/// to every intensity value they compute, so it changes the actual numbers in the preview, the exported
		/// videos, the probe data and any exported files, unlike <see cref="FieldBrightness"/> /
		/// <see cref="IntensityBrightness"/>, which only scale how an image is drawn from the numbers and leave
		/// the numbers themselves alone. It exists because, with the filtering <see cref="TimeDecayFactor"/>
		/// controls, the raw intensity ends up landing around 1e-5 for typical scenes (an inconvenient scale for an
		/// axis or a data file), and the right multiplier to bring that into a convenient range depends on the
		/// source amplitudes and pulse shapes actually used, which this class has no way to predict. The default
		/// of 1.0 leaves the raw numbers exactly as computed; <c>numIntensityScale</c> (Configuration tab) lets the
		/// value be tuned by eye once the actual scale in use is visible.
		/// </summary>
		public double IntensityScale { get; set; }

		/// <summary>Shallow copy that a running simulation can keep while the user goes on editing; all members are value types or immutable strings, so the copy is independent.</summary>
		public SimulationSettings Clone()
		{
			return (SimulationSettings)base.MemberwiseClone();
		}

		/// <summary>
		/// Reads the "simulation" section of <paramref name="config"/>. Missing keys and out-of-range values fall
		/// back to per-key defaults; a missing section yields a default instance.
		/// </summary>
		/// <remarks>
		/// The per-key fallbacks are deliberately separate from the constructor defaults and differ for four keys
		/// (steps 2500 vs 0, use_4th_order_laplacian false vs true, wave_speed_c 0.563 vs 1.0, time_decay_factor
		/// 10 vs 0): an existing project saved before a key existed keeps its exact previous behaviour, while a new
		/// project takes the constructor / Designer values via <c>StartNewProject</c> (Form1). See
		/// <see cref="Steps"/>, <see cref="Use4thOrderLaplacian"/>, <see cref="WaveSpeedC"/> and
		/// <see cref="TimeDecayFactor"/> for the reasoning behind each.
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
			section["steps"] = Steps;
			section["visualize_every"] = VisualizeEvery;
			section["export_field"] = ExportField;
			section["export_intensity"] = ExportIntensity;
			section["preview_field"] = PreviewField;
			section["preview_intensity"] = PreviewIntensity;
			section["field_color_scheme"] = FieldColorScheme;
			section["intensity_color_scheme"] = IntensityColorScheme;
			section["field_brightness"] = FieldBrightness;
			section["intensity_brightness"] = IntensityBrightness;
			section["field_black_level"] = FieldBlackLevel;
			section["intensity_black_level"] = IntensityBlackLevel;
			section["source_frequency_scale"] = SourceFrequencyScale;
			section["video_fps"] = VideoFps;
			section["video_quality"] = VideoQuality;
			section["use_gpu"] = UseGpu;
			section["use_4th_order_laplacian"] = Use4thOrderLaplacian;
			section["wave_speed_c"] = WaveSpeedC;
			section["time_decay_factor"] = TimeDecayFactor;
			section["intensity_scale"] = IntensityScale;
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
					// use the fallback
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
					// use the fallback
				}
			}
			return fallback;
		}

		/// <summary>Name of this section in the project file.</summary>
		public const string SectionName = "simulation";

		/// <summary>
		/// Only the <see cref="FromConfig"/> fallback for an existing project saved before "steps" existed.
		/// <see cref="Steps"/> itself has no software-chosen default of its own, the same as
		/// <see cref="TimeDecayFactor"/>: a new project starts at whatever <c>numSimulationSteps</c>' own Designer
		/// value is (see <c>StartNewProject</c>'s <c>new SimulationSettings()</c>).
		/// </summary>
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
