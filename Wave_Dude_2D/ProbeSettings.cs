using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// User-editable configuration of one probe: where it measures, what it measures and how its
	/// graph is shown. Edited on the Probes tab (<c>Form1.Probes.cs</c>), persisted in the project
	/// config (<see cref="ListToConfig"/> / <see cref="ListFromConfig"/>) and handed to the
	/// simulation as a <see cref="Clone"/> so the worker thread never sees the copy being edited.
	/// All positions are canvas pixels (0-based column/row of the simulation grid, origin top-left).
	/// </summary>
	public sealed class ProbeSettings
	{
		/// <summary>Creates an inactive point probe on the field at (100,100) with a medium graph box at (450,20) and auto scaling on both axes.</summary>
		public ProbeSettings()
		{
			PlaceOn = ProbePlaceOn.Field;
			Measurement = ProbeMeasurement.Amplitude;
			Kind = ProbeKind.Point;
			PosAX = 100;
			PosAY = 100;
			PosBX = 300;
			PosBY = 100;
			BoxSize = ProbeBoxSize.Medium;
			BoxX = 450;
			BoxY = 20;
			Active = false;
			ScaleMode = ProbeScaleMode.Running;
			AutoScaleX = true;
			AutoScaleY = true;
			YMin = -1.0;
			YMax = 1.0;
			RunningWindow = ProbeSettings.DefaultRunningWindow;
			UseStartAfterStep = false;
			StartAfterStep = 0;
		}

		/// <summary>Gets or sets the output (field or intensity) the probe samples and is drawn on.</summary>
		public ProbePlaceOn PlaceOn { get; set; }

		/// <summary>Gets or sets the recorded quantity. Must be consistent with <see cref="PlaceOn"/>; <see cref="Normalize"/> enforces that.</summary>
		public ProbeMeasurement Measurement { get; set; }

		/// <summary>Gets or sets whether this is a point probe (A only) or a line probe (A to B).</summary>
		public ProbeKind Kind { get; set; }

		/// <summary>Gets or sets the X of position A in canvas pixels (the probe pixel, or the start of the line).</summary>
		public int PosAX { get; set; }

		/// <summary>Gets or sets the Y of position A in canvas pixels.</summary>
		public int PosAY { get; set; }

		/// <summary>Gets or sets the X of position B in canvas pixels (end of the line; unused for point probes).</summary>
		public int PosBX { get; set; }

		/// <summary>Gets or sets the Y of position B in canvas pixels (end of the line; unused for point probes).</summary>
		public int PosBY { get; set; }

		/// <summary>Gets or sets the nominal size of the graph box drawn over the output picture.</summary>
		public ProbeBoxSize BoxSize { get; set; }

		/// <summary>Gets or sets the left edge of the graph box in canvas pixels. May lie outside the canvas; the painter clamps the box to stay inside.</summary>
		public int BoxX { get; set; }

		/// <summary>Gets or sets the top edge of the graph box in canvas pixels (clamped by the painter like <see cref="BoxX"/>).</summary>
		public int BoxY { get; set; }

		/// <summary>
		/// Gets or sets whether the probe is switched on: when false it is not sampled or shown in a simulation,
		/// but its settings are kept ("On" checkbox). Off by default (the constructor's default, used for both
		/// probes of a new project - see <c>NewProbeList</c> in <c>Form1.Probes.cs</c>). A probe loaded from a
		/// project saved before <c>active</c> was written for it still comes back on (<see cref="FromDictionary"/>'s
		/// own separate, hard-coded fallback), so that project keeps its exact previous behaviour instead of
		/// silently picking up the new-project default.
		/// </summary>
		public bool Active { get; set; }

		/// <summary>Gets or sets how the step axis of a point probe's time series is chosen; ignored for line probes.</summary>
		public ProbeScaleMode ScaleMode { get; set; }

		/// <summary>Gets or sets whether the step axis follows the samples recorded so far instead of the full step count / window; ignored for line probes.</summary>
		public bool AutoScaleX { get; set; }

		/// <summary>Gets or sets whether the value axis is fitted to the visible data; when false <see cref="YMin"/>/<see cref="YMax"/> are used.</summary>
		public bool AutoScaleY { get; set; }

		/// <summary>Gets or sets the lower bound of the value axis, in the units of <see cref="Measurement"/>. Only used when <see cref="AutoScaleY"/> is false.</summary>
		public double YMin { get; set; }

		/// <summary>Gets or sets the upper bound of the value axis; must be greater than <see cref="YMin"/> or <see cref="Normalize"/> resets both to -1..1.</summary>
		public double YMax { get; set; }

		/// <summary>Gets or sets the width of the sliding step window for <see cref="ProbeScaleMode.Running"/>, in simulation steps (10 .. <see cref="MaxRunningWindow"/>).</summary>
		public int RunningWindow { get; set; }

		/// <summary>
		/// Gets or sets whether the graph shows no data from before step <see cref="StartAfterStep"/> (0 = from the
		/// start). Sampling itself is not gated: the probe keeps being sampled, a point probe's graph is trimmed to
		/// start there and a line probe's stays blank until then (see <see cref="ProbeGraphPainter.DrawGraph"/>),
		/// and the running total of an integrated probe is kept at zero before it (<see cref="ProbeCollector.Collect"/>).
		/// </summary>
		public bool UseStartAfterStep { get; set; }

		/// <summary>Gets or sets the simulation step (0-based, never negative) at which recording starts when <see cref="UseStartAfterStep"/> is set (0 = from the start).</summary>
		public int StartAfterStep { get; set; }

		/// <summary>Returns an independent copy. All members are value types, so the memberwise clone is a full copy.</summary>
		public ProbeSettings Clone()
		{
			return (ProbeSettings)base.MemberwiseClone();
		}

		/// <summary>
		/// Copies only the graph/display settings (active flag, box size and position, scaling, Y range,
		/// running window, start-after gating) from <paramref name="other"/>, leaving position, kind and
		/// measurement untouched. Used to show a running simulation's probes with the display settings
		/// the user is editing right now.
		/// </summary>
		public void CopyDisplayFrom(ProbeSettings other)
		{
			Active = other.Active;
			BoxSize = other.BoxSize;
			BoxX = other.BoxX;
			BoxY = other.BoxY;
			ScaleMode = other.ScaleMode;
			AutoScaleX = other.AutoScaleX;
			AutoScaleY = other.AutoScaleY;
			YMin = other.YMin;
			YMax = other.YMax;
			RunningWindow = other.RunningWindow;
			UseStartAfterStep = other.UseStartAfterStep;
			StartAfterStep = other.StartAfterStep;
		}

		/// <summary>
		/// Forces the settings into a consistent state: the field only allows
		/// <see cref="ProbeMeasurement.Amplitude"/>, the intensity output never does (amplitude is
		/// turned into <see cref="ProbeMeasurement.Intensity"/>); an empty, inverted or NaN Y range becomes
		/// -1..1; <see cref="RunningWindow"/> is clamped to 10..<see cref="MaxRunningWindow"/> and
		/// <see cref="StartAfterStep"/> to zero or more.
		/// </summary>
		public void Normalize()
		{
			if (PlaceOn == ProbePlaceOn.Field)
			{
				Measurement = ProbeMeasurement.Amplitude;
			}
			else if (Measurement == ProbeMeasurement.Amplitude)
			{
				Measurement = ProbeMeasurement.Intensity;
			}
			// written this way round so a NaN limit also falls back
			if (!(YMax > YMin))
			{
				YMin = -1.0;
				YMax = 1.0;
			}
			if (RunningWindow < 10)
			{
				RunningWindow = 10;
			}
			if (RunningWindow > ProbeSettings.MaxRunningWindow)
			{
				RunningWindow = ProbeSettings.MaxRunningWindow;
			}
			if (StartAfterStep < 0)
			{
				StartAfterStep = 0;
			}
		}

		/// <summary>The UI label of <see cref="Measurement"/> ("Amplitude", "Intensity" or "Integrated intensity").</summary>
		public string MeasurementText()
		{
			switch (Measurement)
			{
			case ProbeMeasurement.Amplitude:
				return "Amplitude";
			case ProbeMeasurement.IntegratedIntensity:
				return "Integrated intensity";
			default:
				return "Intensity";
			}
		}

		/// <summary>Title shown in the graph box, e.g. "Probe 1 - Amplitude". The number is also drawn at the probe in the picture.</summary>
		/// <param name="index">0-based probe index; the title shows it 1-based.</param>
		public string Title(int index)
		{
			return "Probe " + (index + 1).ToString(CultureInfo.InvariantCulture) + " - " + MeasurementText();
		}

		/// <summary>
		/// Reads the probe list from the <c>probes</c> section of a project config. Entries in the
		/// section's <c>items</c> list that are not dictionaries are skipped; each read probe is
		/// normalized. Never returns an empty list: a single default probe is returned when the
		/// section is missing or holds no usable entries.
		/// </summary>
		public static List<ProbeSettings> ListFromConfig(ProjectConfig config)
		{
			List<ProbeSettings> probes = new List<ProbeSettings>();
			if (config.ContainsKey(ProbeSettings.SectionName))
			{
				Dictionary<string, object> section = config.GetSection(ProbeSettings.SectionName);
				object items;
				if (section.TryGetValue("items", out items))
				{
					IEnumerable sequence = items as IEnumerable;
					if (sequence != null && !(items is string))
					{
						foreach (object entry in sequence)
						{
							Dictionary<string, object> values = entry as Dictionary<string, object>;
							if (values != null)
							{
								probes.Add(ProbeSettings.FromDictionary(values));
							}
						}
					}
				}
			}
			if (probes.Count == 0)
			{
				probes.Add(new ProbeSettings());
			}
			return probes;
		}

		/// <summary>Writes <paramref name="probes"/> into the <c>probes</c> section of <paramref name="config"/> as <c>count</c> plus an <c>items</c> list of dictionaries.</summary>
		public static void ListToConfig(List<ProbeSettings> probes, ProjectConfig config)
		{
			Dictionary<string, object> section = config.GetSection(ProbeSettings.SectionName);
			List<object> items = new List<object>();
			foreach (ProbeSettings probe in probes)
			{
				items.Add(probe.ToDictionary());
			}
			section["count"] = items.Count;
			section["items"] = items;
		}

		/// <summary>
		/// Serializes the probe to the config dictionary form. Enum values are stored as lower-case
		/// snake_case strings (<c>field</c>/<c>intensity</c>, <c>amplitude</c>/<c>intensity</c>/
		/// <c>integrated_intensity</c>, <c>point</c>/<c>line</c>, <c>tiny</c>..<c>extra_large</c>,
		/// <c>running</c>/<c>full</c>); numbers and booleans as-is.
		/// </summary>
		private Dictionary<string, object> ToDictionary()
		{
			Dictionary<string, object> values = new Dictionary<string, object>();
			values["active"] = Active;
			values["place_on"] = ((PlaceOn == ProbePlaceOn.Field) ? "field" : "intensity");
			switch (Measurement)
			{
			case ProbeMeasurement.Amplitude:
				values["measurement"] = "amplitude";
				break;
			case ProbeMeasurement.IntegratedIntensity:
				values["measurement"] = "integrated_intensity";
				break;
			default:
				values["measurement"] = "intensity";
				break;
			}
			values["probe_type"] = ((Kind == ProbeKind.Line) ? "line" : "point");
			values["ax"] = PosAX;
			values["ay"] = PosAY;
			values["bx"] = PosBX;
			values["by"] = PosBY;
			switch (BoxSize)
			{
			case ProbeBoxSize.Tiny:
				values["box_size"] = "tiny";
				break;
			case ProbeBoxSize.Small:
				values["box_size"] = "small";
				break;
			case ProbeBoxSize.Large:
				values["box_size"] = "large";
				break;
			case ProbeBoxSize.ExtraLarge:
				values["box_size"] = "extra_large";
				break;
			default:
				values["box_size"] = "medium";
				break;
			}
			values["box_x"] = BoxX;
			values["box_y"] = BoxY;
			values["scale_mode"] = ((ScaleMode == ProbeScaleMode.Full) ? "full" : "running");
			values["auto_scale_x"] = AutoScaleX;
			values["auto_scale_y"] = AutoScaleY;
			values["y_min"] = YMin;
			values["y_max"] = YMax;
			values["running_window"] = RunningWindow;
			values["use_start_after_step"] = UseStartAfterStep;
			values["start_after_step"] = StartAfterStep;
			return values;
		}

		/// <summary>
		/// Inverse of <see cref="ToDictionary"/>. Missing, unparsable or out-of-range values fall back to
		/// defaults (note that a missing <c>active</c> defaults to <c>true</c>, unlike the constructor),
		/// unknown enum strings map to the default enum value, and the result is normalized.
		/// </summary>
		private static ProbeSettings FromDictionary(Dictionary<string, object> d)
		{
			ProbeSettings probe = new ProbeSettings();
			probe.Active = ProbeSettings.ReadBool(d, "active", true);
			probe.PlaceOn = ((ProbeSettings.ReadText(d, "place_on", "field") == "intensity") ? ProbePlaceOn.Intensity : ProbePlaceOn.Field);
			switch (ProbeSettings.ReadText(d, "measurement", "amplitude"))
			{
			case "intensity":
				probe.Measurement = ProbeMeasurement.Intensity;
				break;
			case "integrated_intensity":
				probe.Measurement = ProbeMeasurement.IntegratedIntensity;
				break;
			default:
				probe.Measurement = ProbeMeasurement.Amplitude;
				break;
			}
			probe.Kind = ((ProbeSettings.ReadText(d, "probe_type", "point") == "line") ? ProbeKind.Line : ProbeKind.Point);
			probe.PosAX = ProbeSettings.ReadInt(d, "ax", 100, 0, 100000);
			probe.PosAY = ProbeSettings.ReadInt(d, "ay", 100, 0, 100000);
			probe.PosBX = ProbeSettings.ReadInt(d, "bx", 300, 0, 100000);
			probe.PosBY = ProbeSettings.ReadInt(d, "by", 100, 0, 100000);
			switch (ProbeSettings.ReadText(d, "box_size", "medium"))
			{
			case "tiny":
				probe.BoxSize = ProbeBoxSize.Tiny;
				break;
			case "small":
				probe.BoxSize = ProbeBoxSize.Small;
				break;
			case "large":
				probe.BoxSize = ProbeBoxSize.Large;
				break;
			case "extra_large":
				probe.BoxSize = ProbeBoxSize.ExtraLarge;
				break;
			default:
				probe.BoxSize = ProbeBoxSize.Medium;
				break;
			}
			probe.BoxX = ProbeSettings.ReadInt(d, "box_x", 450, 0, 100000);
			probe.BoxY = ProbeSettings.ReadInt(d, "box_y", 20, 0, 100000);
			probe.ScaleMode = ((ProbeSettings.ReadText(d, "scale_mode", "running") == "full") ? ProbeScaleMode.Full : ProbeScaleMode.Running);
			probe.AutoScaleX = ProbeSettings.ReadBool(d, "auto_scale_x", true);
			probe.AutoScaleY = ProbeSettings.ReadBool(d, "auto_scale_y", true);
			probe.YMin = ProbeSettings.ReadDouble(d, "y_min", -1.0, -1E+30, 1E+30);
			probe.YMax = ProbeSettings.ReadDouble(d, "y_max", 1.0, -1E+30, 1E+30);
			probe.RunningWindow = ProbeSettings.ReadInt(d, "running_window", ProbeSettings.DefaultRunningWindow, 10, ProbeSettings.MaxRunningWindow);
			probe.UseStartAfterStep = ProbeSettings.ReadBool(d, "use_start_after_step", false);
			probe.StartAfterStep = ProbeSettings.ReadInt(d, "start_after_step", 0, 0, 1000000000);
			probe.Normalize();
			return probe;
		}

		/// <summary>Reads a config value as text (invariant culture for non-strings); <paramref name="fallback"/> when missing or null.</summary>
		private static string ReadText(Dictionary<string, object> d, string key, string fallback)
		{
			object value;
			if (d.TryGetValue(key, out value) && value != null)
			{
				return Convert.ToString(value, CultureInfo.InvariantCulture);
			}
			return fallback;
		}

		/// <summary>Reads a config value that must already be a <see cref="bool"/>; anything else yields <paramref name="fallback"/>.</summary>
		private static bool ReadBool(Dictionary<string, object> d, string key, bool fallback)
		{
			object value;
			if (d.TryGetValue(key, out value) && value is bool)
			{
				return (bool)value;
			}
			return fallback;
		}

		/// <summary>Reads an integer config value (invariant-culture conversion). Missing, unconvertible or out-of-range values yield <paramref name="fallback"/>.</summary>
		private static int ReadInt(Dictionary<string, object> d, string key, int fallback, int minValue, int maxValue)
		{
			object raw;
			if (d.TryGetValue(key, out raw) && raw != null)
			{
				try
				{
					int value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
					if (value >= minValue && value <= maxValue)
					{
						return value;
					}
				}
				catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
				{
					// use the fallback
				}
			}
			return fallback;
		}

		/// <summary>Reads a double config value (invariant-culture conversion). Missing, unconvertible or out-of-range values yield <paramref name="fallback"/>.</summary>
		private static double ReadDouble(Dictionary<string, object> d, string key, double fallback, double minValue, double maxValue)
		{
			object raw;
			if (d.TryGetValue(key, out raw) && raw != null)
			{
				try
				{
					double value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
					if (value >= minValue && value <= maxValue)
					{
						return value;
					}
				}
				catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
				{
					// use the fallback
				}
			}
			return fallback;
		}

		/// <summary>Name of the project config section that holds the probe list.</summary>
		public const string SectionName = "probes";

		/// <summary>Default <see cref="RunningWindow"/>, in simulation steps.</summary>
		public const int DefaultRunningWindow = 500;

		/// <summary>Largest allowed <see cref="RunningWindow"/>, in simulation steps (also the spinner maximum on the Probes tab).</summary>
		public const int MaxRunningWindow = 1000000;
	}
}
