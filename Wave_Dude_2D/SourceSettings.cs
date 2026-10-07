using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// User-editable description of one wave source (single or composite array) plus the
	/// generated <see cref="SourceElement"/> list that the simulation actually consumes.
	/// Edited on the source tab of <c>Form1</c> (UI thread only), persisted in the
	/// <see cref="SectionName"/> section of the project config, and expanded into elements by
	/// <see cref="GenerateElements"/> whenever the user saves the source. Units are the ones
	/// shown in the UI: positions in canvas pixels, frequency as a 1..255 level, amplitude in
	/// percent, phase in degrees, pulse widths in cycles, detach time in simulation steps.
	/// </summary>
	public sealed class SourceSettings
	{
		/// <summary>Creates a source with the editor defaults: "Source 1", simple, continuous Gaussian at (100, 100) but not yet positioned, frequency level 100, 100 % amplitude, 10-cycle widths, 1x1 array with 10 px spacing, nothing randomised, no elements.</summary>
		public SourceSettings()
		{
			this.Name = "Source 1";
			this.MainType = SourceMainType.Simple;
			this.PosX = 100;
			this.PosY = 100;
			this.HasPosition = false;
			this.SourceOn = true;
			this.DetachSource = false;
			this.DetachAfterSteps = 1000;
			this.Frequency = 100;
			this.Amplitude = 100;
			this.PhaseDeg = 0;
			this.Mode = SourceMode.Continuous;
			this.PulseShape = SourcePulseShape.Gaussian;
			this.PulseWidthCycles = 10;
			this.OffStateCycles = 10;
			this.ArraySizeX = 1;
			this.ArraySizeY = 1;
			this.SpacingX = 10;
			this.SpacingY = 10;
			this.CompositeMode = SourceCompositeMode.InheritFromSingle;
			this.RandomizeFrequency = false;
			this.RandomizeFrequencyAmount = 0;
			this.RandomizePhase = false;
			this.RandomizePhaseAmount = 0;
			this.RandomizeOffWidth = false;
			this.RandomizeOffWidthAmount = 0;
			this.RandomizePosition = false;
			this.RandomizePositionAmount = 0;
			this.Elements = new List<SourceElement>();
		}

		/// <summary>Display name shown in the source list and next to the marker on the scene picture; must be non-blank to save.</summary>
		public string Name { get; set; }

		/// <summary>Single point source or composite array.</summary>
		public SourceMainType MainType { get; set; }

		/// <summary>X position in canvas pixels of the source, or of the array centre for a composite source.</summary>
		public int PosX { get; set; }

		/// <summary>Y position in canvas pixels of the source, or of the array centre for a composite source.</summary>
		public int PosY { get; set; }

		/// <summary>
		/// True once the user has placed the source (by picking on the picture or editing the
		/// position inputs); only sources with a position get a marker drawn on the scene picture.
		/// Defaults to false for a new source but to true when loaded from a project without the key.
		/// </summary>
		public bool HasPosition { get; set; }

		/// <summary>Whether the source takes part in simulation runs; disabled sources are listed in grey and skipped.</summary>
		public bool SourceOn { get; set; }

		/// <summary>Whether the source is switched off during the run after <see cref="DetachAfterSteps"/>.</summary>
		public bool DetachSource { get; set; }

		/// <summary>Simulation step at which the source starts fading out when <see cref="DetachSource"/> is set; 0 or greater.</summary>
		public int DetachAfterSteps { get; set; }

		/// <summary>
		/// Frequency as a source level 1..255, the same scale as the brightness of image-based
		/// sources (angular frequency = level / 255 * 0.5 * <c>SimulationSettings.SourceFrequencyScale</c>
		/// radians per step). Copied to <see cref="SourceElement.Level"/>.
		/// </summary>
		public int Frequency { get; set; }

		/// <summary>Amplitude in percent, 0..100; becomes 0..1 on the element.</summary>
		public int Amplitude { get; set; }

		/// <summary>Phase offset in degrees, -180..180; converted to radians on the element.</summary>
		public int PhaseDeg { get; set; }

		/// <summary>Continuous or pulsed emission.</summary>
		public SourceMode Mode { get; set; }

		/// <summary>Burst envelope shape; only used in pulsed mode.</summary>
		public SourcePulseShape PulseShape { get; set; }

		/// <summary>Length of each "on" burst in cycles of the source frequency, at least 1 (pulsed mode only).</summary>
		public int PulseWidthCycles { get; set; }

		/// <summary>Length of the "off" gap between bursts in cycles, at least 1 (pulsed mode only).</summary>
		public int OffStateCycles { get; set; }

		/// <summary>Number of array columns for a composite source, at least 1; the UI enforces odd values so the centre element sits on (PosX, PosY).</summary>
		public int ArraySizeX { get; set; }

		/// <summary>Number of array rows for a composite source, at least 1; the UI enforces odd values.</summary>
		public int ArraySizeY { get; set; }

		/// <summary>Horizontal distance between neighbouring array elements in canvas pixels.</summary>
		public int SpacingX { get; set; }

		/// <summary>Vertical distance between neighbouring array elements in canvas pixels.</summary>
		public int SpacingY { get; set; }

		/// <summary>Whether composite elements are exact copies or get per-element random offsets.</summary>
		public SourceCompositeMode CompositeMode { get; set; }

		/// <summary>Enables the per-element frequency offset (composite + randomize mode only).</summary>
		public bool RandomizeFrequency { get; set; }

		/// <summary>Half-range of the random frequency offset in levels: each element gets Frequency +/- up to this, clamped to 1..255.</summary>
		public int RandomizeFrequencyAmount { get; set; }

		/// <summary>Enables the per-element phase offset (composite + randomize mode only).</summary>
		public bool RandomizePhase { get; set; }

		/// <summary>Half-range of the random phase offset in degrees: each element gets PhaseDeg +/- up to this, clamped to -180..180.</summary>
		public int RandomizePhaseAmount { get; set; }

		/// <summary>Enables the per-element and per-period off-width randomisation (composite + randomize mode only).</summary>
		public bool RandomizeOffWidth { get; set; }

		/// <summary>Half-range of the random off-gap offset in cycles; also passed on to the elements so the engine re-draws the gap every period.</summary>
		public int RandomizeOffWidthAmount { get; set; }

		/// <summary>Enables the per-element position jitter (composite + randomize mode only).</summary>
		public bool RandomizePosition { get; set; }

		/// <summary>Half-range of the position jitter in canvas pixels, drawn independently for X and Y of each element; the UI caps it at half the smallest spacing.</summary>
		public int RandomizePositionAmount { get; set; }

		/// <summary>
		/// The point sources generated from these settings by <see cref="GenerateElements"/>;
		/// empty until the source has been saved at least once. Persisted with the project so a
		/// randomised layout survives reloads. Handed to the simulation by reference.
		/// </summary>
		public List<SourceElement> Elements { get; set; }

		/// <summary>Deep copy: the element list and every element are cloned as well.</summary>
		public SourceSettings Clone()
		{
			SourceSettings copy = (SourceSettings)base.MemberwiseClone();
			copy.Elements = new List<SourceElement>();
			foreach (SourceElement element in this.Elements)
			{
				copy.Elements.Add(element.Clone());
			}
			return copy;
		}

		/// <summary>
		/// Clamps every setting into its valid range (frequency 1..255, amplitude 0..100, phase
		/// -180..180, widths and array sizes at least 1, detach steps at least 0). Positions,
		/// spacings and randomise amounts are not touched.
		/// </summary>
		public void Normalize()
		{
			if (this.Frequency < 1)
			{
				this.Frequency = 1;
			}
			if (this.Frequency > 255)
			{
				this.Frequency = 255;
			}
			if (this.Amplitude < 0)
			{
				this.Amplitude = 0;
			}
			if (this.Amplitude > 100)
			{
				this.Amplitude = 100;
			}
			if (this.PhaseDeg < -180)
			{
				this.PhaseDeg = -180;
			}
			if (this.PhaseDeg > 180)
			{
				this.PhaseDeg = 180;
			}
			if (this.PulseWidthCycles < 1)
			{
				this.PulseWidthCycles = 1;
			}
			if (this.OffStateCycles < 1)
			{
				this.OffStateCycles = 1;
			}
			if (this.ArraySizeX < 1)
			{
				this.ArraySizeX = 1;
			}
			if (this.ArraySizeY < 1)
			{
				this.ArraySizeY = 1;
			}
			if (this.DetachAfterSteps < 0)
			{
				this.DetachAfterSteps = 0;
			}
		}

		/// <summary>
		/// Rebuilds <see cref="Elements"/> from the settings: one element for a simple source,
		/// or an ArraySizeX x ArraySizeY grid centred on (PosX, PosY) for a composite source,
		/// optionally randomised per element. Calls <see cref="Normalize"/> first. Random offsets
		/// are only applied when <see cref="CompositeMode"/> is
		/// <see cref="SourceCompositeMode.InheritAndRandomize"/>, the matching Randomize flag is
		/// set and its amount is positive; frequency and phase offsets are clamped back into
		/// their valid ranges, the off gap is kept at least 1 cycle. Element positions are not
		/// clamped to the canvas. Every call replaces the list, so a randomised layout changes
		/// each time the source is saved.
		/// </summary>
		/// <param name="rng">Random generator for the per-element offsets; may be any instance when nothing is randomised.</param>
		public void GenerateElements(Random rng)
		{
			this.Normalize();
			List<SourceElement> elements = new List<SourceElement>();
			if (this.MainType == SourceMainType.Simple)
			{
				elements.Add(this.MakeBaseElement(this.PosX, this.PosY));
			}
			else
			{
				int arraySizeX = this.ArraySizeX;
				int arraySizeY = this.ArraySizeY;
				bool randomize = this.CompositeMode == SourceCompositeMode.InheritAndRandomize;
				for (int i = 0; i < arraySizeY; i++)
				{
					for (int j = 0; j < arraySizeX; j++)
					{
						// Grid is centred on (PosX, PosY); with an even size the centre falls between two elements.
						int x = this.PosX + (int)Math.Round((j - (arraySizeX - 1) / 2.0) * this.SpacingX);
						int y = this.PosY + (int)Math.Round((i - (arraySizeY - 1) / 2.0) * this.SpacingY);
						if (randomize && this.RandomizePosition && this.RandomizePositionAmount > 0)
						{
							x += SourceSettings.RandomInt(rng, this.RandomizePositionAmount);
							y += SourceSettings.RandomInt(rng, this.RandomizePositionAmount);
						}
						SourceElement element = this.MakeBaseElement(x, y);
						if (randomize && this.RandomizeFrequency && this.RandomizeFrequencyAmount > 0)
						{
							element.Level = SourceSettings.Clamp(this.Frequency + SourceSettings.RandomInt(rng, this.RandomizeFrequencyAmount), 1, 255);
						}
						if (randomize && this.RandomizePhase && this.RandomizePhaseAmount > 0)
						{
							int phaseDeg = SourceSettings.Clamp(this.PhaseDeg + SourceSettings.RandomInt(rng, this.RandomizePhaseAmount), -180, 180);
							element.PhaseRad = phaseDeg * Math.PI / 180.0;
						}
						if (randomize && this.RandomizeOffWidth && this.RandomizeOffWidthAmount > 0)
						{
							element.OffWidthCycles = Math.Max(1, this.OffStateCycles + SourceSettings.RandomInt(rng, this.RandomizeOffWidthAmount));
							element.RandomizeOffWidthAmount = this.RandomizeOffWidthAmount;
						}
						elements.Add(element);
					}
				}
			}
			this.Elements = elements;
		}

		/// <summary>
		/// Creates an element at the given canvas position with the non-randomised settings,
		/// converting amplitude to 0..1 and phase to radians; <c>DetachAfterStep</c> is -1 when
		/// <see cref="DetachSource"/> is off.
		/// </summary>
		private SourceElement MakeBaseElement(int x, int y)
		{
			return new SourceElement
			{
				PosX = x,
				PosY = y,
				Level = this.Frequency,
				Amplitude = this.Amplitude / 100.0,
				PhaseRad = this.PhaseDeg * Math.PI / 180.0,
				Mode = this.Mode,
				PulseShape = this.PulseShape,
				OnWidthCycles = this.PulseWidthCycles,
				OffWidthCycles = this.OffStateCycles,
				OffWidthBase = this.OffStateCycles,
				RandomizeOffWidthAmount = 0,
				DetachAfterStep = this.DetachSource ? this.DetachAfterSteps : -1
			};
		}

		/// <summary>Uniform random integer in [-amount, +amount]; 0 when amount is not positive. Also used by <c>WaveSimulator</c> for the per-period off-width.</summary>
		internal static int RandomInt(Random rng, int amount)
		{
			return amount <= 0 ? 0 : rng.Next(-amount, amount + 1);
		}

		/// <summary>Clamps <paramref name="value"/> into [minValue, maxValue].</summary>
		private static int Clamp(int value, int minValue, int maxValue)
		{
			return Math.Max(minValue, Math.Min(maxValue, value));
		}

		/// <summary>
		/// Reads all sources from the <see cref="SectionName"/> section of a project config.
		/// Returns an empty list when the section or its <c>"items"</c> list is missing; entries
		/// that are not dictionaries are skipped. Each source is clamped via <see cref="Normalize"/>.
		/// </summary>
		public static List<SourceSettings> ListFromConfig(ProjectConfig config)
		{
			List<SourceSettings> sources = new List<SourceSettings>();
			if (!config.ContainsKey(SourceSettings.SectionName))
			{
				return sources;
			}
			Dictionary<string, object> section = config.GetSection(SourceSettings.SectionName);
			if (!section.TryGetValue("items", out object itemsValue))
			{
				return sources;
			}
			IEnumerable items = itemsValue as IEnumerable;
			// A string is IEnumerable too; it is not a list of sources.
			if (items == null || itemsValue is string)
			{
				return sources;
			}
			foreach (object item in items)
			{
				Dictionary<string, object> itemDict = item as Dictionary<string, object>;
				if (itemDict != null)
				{
					sources.Add(SourceSettings.FromDictionary(itemDict));
				}
			}
			return sources;
		}

		/// <summary>Writes all sources (settings and generated elements) into the <see cref="SectionName"/> section of a project config, replacing its <c>"count"</c> and <c>"items"</c> entries.</summary>
		public static void ListToConfig(List<SourceSettings> sources, ProjectConfig config)
		{
			Dictionary<string, object> section = config.GetSection(SourceSettings.SectionName);
			List<object> items = new List<object>();
			foreach (SourceSettings source in sources)
			{
				items.Add(source.ToDictionary());
			}
			section["count"] = items.Count;
			section["items"] = items;
		}

		/// <summary>Serialises the settings and the generated elements into the key/value form stored in the project file (enums as lower-case strings, phase of elements in radians).</summary>
		private Dictionary<string, object> ToDictionary()
		{
			Dictionary<string, object> data = new Dictionary<string, object>();
			data["name"] = this.Name;
			data["main_type"] = ((this.MainType == SourceMainType.Composite) ? "composite" : "single");
			data["x"] = this.PosX;
			data["y"] = this.PosY;
			data["has_position"] = this.HasPosition;
			data["source_on"] = this.SourceOn;
			data["detach_source"] = this.DetachSource;
			data["detach_after_steps"] = this.DetachAfterSteps;
			data["frequency"] = this.Frequency;
			data["amplitude"] = this.Amplitude;
			data["phase_deg"] = this.PhaseDeg;
			data["mode"] = ((this.Mode == SourceMode.Pulsed) ? "pulsed" : "continuous");
			data["pulse_shape"] = ((this.PulseShape == SourcePulseShape.SmoothedSquare) ? "smoothed_square" : "gaussian");
			data["pulse_width_cycles"] = this.PulseWidthCycles;
			data["off_state_cycles"] = this.OffStateCycles;
			data["array_size_x"] = this.ArraySizeX;
			data["array_size_y"] = this.ArraySizeY;
			data["spacing_x"] = this.SpacingX;
			data["spacing_y"] = this.SpacingY;
			data["composite_mode"] = ((this.CompositeMode == SourceCompositeMode.InheritAndRandomize) ? "randomize" : "inherit");
			data["randomize_frequency"] = this.RandomizeFrequency;
			data["randomize_frequency_amount"] = this.RandomizeFrequencyAmount;
			data["randomize_phase"] = this.RandomizePhase;
			data["randomize_phase_amount"] = this.RandomizePhaseAmount;
			data["randomize_off_width"] = this.RandomizeOffWidth;
			data["randomize_off_width_amount"] = this.RandomizeOffWidthAmount;
			data["randomize_position"] = this.RandomizePosition;
			data["randomize_position_amount"] = this.RandomizePositionAmount;
			List<object> elements = new List<object>();
			foreach (SourceElement element in this.Elements)
			{
				Dictionary<string, object> elementData = new Dictionary<string, object>();
				elementData["x"] = element.PosX;
				elementData["y"] = element.PosY;
				elementData["level"] = element.Level;
				elementData["amplitude"] = element.Amplitude;
				elementData["phase_rad"] = element.PhaseRad;
				elementData["mode"] = ((element.Mode == SourceMode.Pulsed) ? "pulsed" : "continuous");
				elementData["pulse_shape"] = ((element.PulseShape == SourcePulseShape.SmoothedSquare) ? "smoothed_square" : "gaussian");
				elementData["on_width_cycles"] = element.OnWidthCycles;
				elementData["off_width_cycles"] = element.OffWidthCycles;
				elementData["off_width_base"] = element.OffWidthBase;
				elementData["randomize_off_width_amount"] = element.RandomizeOffWidthAmount;
				elementData["detach_after_step"] = element.DetachAfterStep;
				elements.Add(elementData);
			}
			data["elements"] = elements;
			return data;
		}

		/// <summary>
		/// Inverse of <see cref="ToDictionary"/>. Missing or out-of-range values fall back to the
		/// constructor defaults, except <c>has_position</c> which defaults to true; unknown enum
		/// strings map to the first enum value. Stored elements are restored as they are (they are
		/// not regenerated), with a 0.5 amplitude fallback and the phase range left wide open.
		/// </summary>
		private static SourceSettings FromDictionary(Dictionary<string, object> d)
		{
			SourceSettings s = new SourceSettings();
			s.Name = SourceSettings.ReadText(d, "name", s.Name);
			s.MainType = ((SourceSettings.ReadText(d, "main_type", "single") == "composite") ? SourceMainType.Composite : SourceMainType.Simple);
			s.PosX = SourceSettings.ReadInt(d, "x", s.PosX, -1000000, 1000000);
			s.PosY = SourceSettings.ReadInt(d, "y", s.PosY, -1000000, 1000000);
			s.HasPosition = SourceSettings.ReadBool(d, "has_position", true);
			s.SourceOn = SourceSettings.ReadBool(d, "source_on", true);
			s.DetachSource = SourceSettings.ReadBool(d, "detach_source", false);
			s.DetachAfterSteps = SourceSettings.ReadInt(d, "detach_after_steps", s.DetachAfterSteps, 0, 1000000000);
			s.Frequency = SourceSettings.ReadInt(d, "frequency", s.Frequency, 1, 255);
			s.Amplitude = SourceSettings.ReadInt(d, "amplitude", s.Amplitude, 0, 100);
			s.PhaseDeg = SourceSettings.ReadInt(d, "phase_deg", s.PhaseDeg, -180, 180);
			s.Mode = ((SourceSettings.ReadText(d, "mode", "continuous") == "pulsed") ? SourceMode.Pulsed : SourceMode.Continuous);
			s.PulseShape = ((SourceSettings.ReadText(d, "pulse_shape", "gaussian") == "smoothed_square") ? SourcePulseShape.SmoothedSquare : SourcePulseShape.Gaussian);
			s.PulseWidthCycles = SourceSettings.ReadInt(d, "pulse_width_cycles", s.PulseWidthCycles, 1, 1000000);
			s.OffStateCycles = SourceSettings.ReadInt(d, "off_state_cycles", s.OffStateCycles, 1, 1000000);
			s.ArraySizeX = SourceSettings.ReadInt(d, "array_size_x", s.ArraySizeX, 1, 100000);
			s.ArraySizeY = SourceSettings.ReadInt(d, "array_size_y", s.ArraySizeY, 1, 100000);
			s.SpacingX = SourceSettings.ReadInt(d, "spacing_x", s.SpacingX, -1000000, 1000000);
			s.SpacingY = SourceSettings.ReadInt(d, "spacing_y", s.SpacingY, -1000000, 1000000);
			s.CompositeMode = ((SourceSettings.ReadText(d, "composite_mode", "inherit") == "randomize") ? SourceCompositeMode.InheritAndRandomize : SourceCompositeMode.InheritFromSingle);
			s.RandomizeFrequency = SourceSettings.ReadBool(d, "randomize_frequency", false);
			s.RandomizeFrequencyAmount = SourceSettings.ReadInt(d, "randomize_frequency_amount", 0, 0, 1000000);
			s.RandomizePhase = SourceSettings.ReadBool(d, "randomize_phase", false);
			s.RandomizePhaseAmount = SourceSettings.ReadInt(d, "randomize_phase_amount", 0, 0, 1000000);
			s.RandomizeOffWidth = SourceSettings.ReadBool(d, "randomize_off_width", false);
			s.RandomizeOffWidthAmount = SourceSettings.ReadInt(d, "randomize_off_width_amount", 0, 0, 1000000);
			s.RandomizePosition = SourceSettings.ReadBool(d, "randomize_position", false);
			s.RandomizePositionAmount = SourceSettings.ReadInt(d, "randomize_position_amount", 0, 0, 1000000);
			List<SourceElement> elements = new List<SourceElement>();
			if (d.TryGetValue("elements", out object elementsValue))
			{
				IEnumerable items = elementsValue as IEnumerable;
				if (items != null && !(elementsValue is string))
				{
					foreach (object item in items)
					{
						Dictionary<string, object> elementDict = item as Dictionary<string, object>;
						if (elementDict == null)
						{
							continue;
						}
						SourceElement element = new SourceElement();
						element.PosX = SourceSettings.ReadInt(elementDict, "x", 0, -1000000, 1000000);
						element.PosY = SourceSettings.ReadInt(elementDict, "y", 0, -1000000, 1000000);
						element.Level = SourceSettings.ReadInt(elementDict, "level", 100, 1, 255);
						element.Amplitude = SourceSettings.ReadDouble(elementDict, "amplitude", 0.5, 0.0, 1.0);
						element.PhaseRad = SourceSettings.ReadDouble(elementDict, "phase_rad", 0.0, -1000.0, 1000.0);
						element.Mode = ((SourceSettings.ReadText(elementDict, "mode", "continuous") == "pulsed") ? SourceMode.Pulsed : SourceMode.Continuous);
						element.PulseShape = ((SourceSettings.ReadText(elementDict, "pulse_shape", "gaussian") == "smoothed_square") ? SourcePulseShape.SmoothedSquare : SourcePulseShape.Gaussian);
						element.OnWidthCycles = SourceSettings.ReadInt(elementDict, "on_width_cycles", 10, 1, 1000000);
						element.OffWidthCycles = SourceSettings.ReadInt(elementDict, "off_width_cycles", 10, 1, 1000000);
						element.OffWidthBase = SourceSettings.ReadInt(elementDict, "off_width_base", element.OffWidthCycles, 1, 1000000);
						element.RandomizeOffWidthAmount = SourceSettings.ReadInt(elementDict, "randomize_off_width_amount", 0, 0, 1000000);
						element.DetachAfterStep = SourceSettings.ReadInt(elementDict, "detach_after_step", -1, -1, 1000000000);
						elements.Add(element);
					}
				}
			}
			s.Elements = elements;
			s.Normalize();
			return s;
		}

		/// <summary>Reads a value as text (invariant culture); <paramref name="fallback"/> when the key is missing or null.</summary>
		private static string ReadText(Dictionary<string, object> d, string key, string fallback)
		{
			return (d.TryGetValue(key, out object value) && value != null) ? Convert.ToString(value, CultureInfo.InvariantCulture) : fallback;
		}

		/// <summary>Reads a bool; <paramref name="fallback"/> unless the stored value is an actual <see cref="bool"/> (no string parsing).</summary>
		private static bool ReadBool(Dictionary<string, object> d, string key, bool fallback)
		{
			return (d.TryGetValue(key, out object value) && value is bool) ? (bool)value : fallback;
		}

		/// <summary>Reads an int (invariant culture); <paramref name="fallback"/> when the key is missing, not convertible or outside [minValue, maxValue].</summary>
		private static int ReadInt(Dictionary<string, object> d, string key, int fallback, int minValue, int maxValue)
		{
			if (d.TryGetValue(key, out object value) && value != null)
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

		/// <summary>Reads a double (invariant culture); <paramref name="fallback"/> when the key is missing, not convertible or outside [minValue, maxValue].</summary>
		private static double ReadDouble(Dictionary<string, object> d, string key, double fallback, double minValue, double maxValue)
		{
			if (d.TryGetValue(key, out object value) && value != null)
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

		/// <summary>Name of the project config section that holds the source list.</summary>
		public const string SectionName = "object_sources";
	}
}
