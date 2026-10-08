using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// User-editable description of one wave source (single point source or composite array of point sources
	/// built from the same base parameters, optionally randomised) plus the generated
	/// <see cref="SourceElement"/> list that the simulation actually consumes.
	/// Edited on the source tab of <c>Form1</c> (UI thread only) and persisted in the
	/// <see cref="SectionName"/> section of the project config. "Generate composite source" resolves the
	/// current parameters (and, for a composite source, a fresh random draw) into a fixed list of elements via
	/// <see cref="GenerateElements"/>, which is what the simulation runs; "Save source" keeps both the
	/// authoring parameters (so the source can be reopened and edited or regenerated) and that generated list
	/// (so a saved project runs exactly what was generated, even before it is regenerated again). Units are the
	/// ones shown in the UI: positions in canvas pixels, frequency as a 1..255 level, amplitude in
	/// percent, phase in degrees, pulse widths in cycles, detach time in simulation steps.
	/// </summary>
	public sealed class SourceSettings
	{
		/// <summary>Creates a source with the editor defaults: "Source 1", simple, continuous Gaussian at (100, 100) but not yet positioned, frequency level 100, 100 % amplitude, 10-cycle widths, 1x1 array with 10 px spacing, nothing randomised, no elements.</summary>
		public SourceSettings()
		{
			Name = "Source 1";
			MainType = SourceMainType.Simple;
			PosX = 100;
			PosY = 100;
			HasPosition = false;
			SourceOn = true;
			DetachSource = false;
			DetachAfterSteps = 1000;
			Frequency = 100;
			Amplitude = 100;
			PhaseDeg = 0;
			Mode = SourceMode.Continuous;
			PulseShape = SourcePulseShape.Gaussian;
			PulseWidthCycles = 10;
			OffStateCycles = 10;
			ArraySizeX = 1;
			ArraySizeY = 1;
			SpacingX = 10;
			SpacingY = 10;
			CompositeMode = SourceCompositeMode.InheritFromSingle;
			RandomizeFrequency = false;
			RandomizeFrequencyAmount = 0;
			RandomizePhase = false;
			RandomizePhaseAmount = 0;
			RandomizeOffWidth = false;
			RandomizeOffWidthAmount = 0;
			RandomizePosition = false;
			RandomizePositionAmount = 0;
			Elements = new List<SourceElement>();
		}

		/// <summary>Display name shown in the source list and next to the marker on the scene picture; must be non-blank to save.</summary>
		public string Name { get; set; }

		/// <summary>Single point source or composite array.</summary>
		public SourceMainType MainType { get; set; }

		/// <summary>X position in canvas pixels of the source, or of the array centre for a composite source. Only meaningful once <see cref="HasPosition"/> is true; until then it is just the unset default and the Sources tab must not draw a marker for it.</summary>
		public int PosX { get; set; }

		/// <summary>Y position in canvas pixels of the source, or of the array centre for a composite source; see <see cref="PosX"/>.</summary>
		public int PosY { get; set; }

		/// <summary>
		/// True once the user has placed the source (by picking on the picture or editing the
		/// position inputs) rather than still holding the default; only sources with a position get
		/// a marker drawn on the scene picture. Defaults to false for a new source but to true when
		/// loaded from a project saved before this flag existed (see <see cref="FromDictionary"/>),
		/// since its sources were positioned under the old rules.
		/// </summary>
		public bool HasPosition { get; set; }

		/// <summary>
		/// Whether the source interacts with the simulation at all; a new source starts on. Switching it off
		/// keeps every parameter (it can still be edited, and switched back on) but the source takes no part
		/// in a run: it is listed in grey and <c>SourcesForRun</c> leaves it out entirely, so it neither drives
		/// the field nor gets in the way of waves passing through its position.
		/// </summary>
		public bool SourceOn { get; set; }

		/// <summary>
		/// When true, the source stops driving the field after <see cref="DetachAfterSteps"/> simulation steps
		/// (it runs normally up to that point). A source that keeps driving the field also holds it at a fixed
		/// value there, which diffracts waves that later pass through that point; detaching releases that hold
		/// once the source has done its job, so the field becomes free of it again for the rest of the run.
		/// </summary>
		public bool DetachSource { get; set; }

		/// <summary>Simulation step (0-based) at which the source detaches and starts fading out (over <see cref="SourceElement.DetachRampSteps"/> steps) when <see cref="DetachSource"/> is set; 0 or greater.</summary>
		public int DetachAfterSteps { get; set; }

		/// <summary>
		/// Frequency as a source level 1..255, the same scale as the brightness of image-based
		/// sources (angular frequency = level / 255 * 0.5 * <c>SimulationSettings.SourceFrequencyScale</c>
		/// radians per step). Copied to <see cref="SourceElement.Level"/>.
		/// </summary>
		public int Frequency { get; set; }

		/// <summary>Amplitude in percent of the source amplitude, 0..100; becomes 0..1 on the element. 100 matches <c>numAmplitude</c>'s own Designer default for a new source.</summary>
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
			foreach (SourceElement element in Elements)
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
			if (Frequency < 1)
			{
				Frequency = 1;
			}
			if (Frequency > 255)
			{
				Frequency = 255;
			}
			if (Amplitude < 0)
			{
				Amplitude = 0;
			}
			if (Amplitude > 100)
			{
				Amplitude = 100;
			}
			if (PhaseDeg < -180)
			{
				PhaseDeg = -180;
			}
			if (PhaseDeg > 180)
			{
				PhaseDeg = 180;
			}
			if (PulseWidthCycles < 1)
			{
				PulseWidthCycles = 1;
			}
			if (OffStateCycles < 1)
			{
				OffStateCycles = 1;
			}
			if (ArraySizeX < 1)
			{
				ArraySizeX = 1;
			}
			if (ArraySizeY < 1)
			{
				ArraySizeY = 1;
			}
			if (DetachAfterSteps < 0)
			{
				DetachAfterSteps = 0;
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
			Normalize();
			List<SourceElement> elements = new List<SourceElement>();
			if (MainType == SourceMainType.Simple)
			{
				elements.Add(MakeBaseElement(PosX, PosY));
			}
			else
			{
				int cols = ArraySizeX;
				int rows = ArraySizeY;
				bool randomize = CompositeMode == SourceCompositeMode.InheritAndRandomize;
				for (int row = 0; row < rows; row++)
				{
					for (int col = 0; col < cols; col++)
					{
						// The array is centred on (PosX, PosY), not anchored at a corner; with an even size the centre falls between two elements.
						int x = PosX + (int)Math.Round((col - (cols - 1) / 2.0) * SpacingX);
						int y = PosY + (int)Math.Round((row - (rows - 1) / 2.0) * SpacingY);
						if (randomize && RandomizePosition && RandomizePositionAmount > 0)
						{
							x += SourceSettings.RandomInt(rng, RandomizePositionAmount);
							y += SourceSettings.RandomInt(rng, RandomizePositionAmount);
						}
						SourceElement element = MakeBaseElement(x, y);
						if (randomize && RandomizeFrequency && RandomizeFrequencyAmount > 0)
						{
							element.Level = SourceSettings.Clamp(Frequency + SourceSettings.RandomInt(rng, RandomizeFrequencyAmount), 1, 255);
						}
						if (randomize && RandomizePhase && RandomizePhaseAmount > 0)
						{
							int phaseDeg = SourceSettings.Clamp(PhaseDeg + SourceSettings.RandomInt(rng, RandomizePhaseAmount), -180, 180);
							element.PhaseRad = phaseDeg * Math.PI / 180.0;
						}
						if (randomize && RandomizeOffWidth && RandomizeOffWidthAmount > 0)
						{
							element.OffWidthCycles = Math.Max(1, OffStateCycles + SourceSettings.RandomInt(rng, RandomizeOffWidthAmount));
							element.RandomizeOffWidthAmount = RandomizeOffWidthAmount;
						}
						elements.Add(element);
					}
				}
			}
			Elements = elements;
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
				Level = Frequency,
				Amplitude = Amplitude / 100.0,
				PhaseRad = PhaseDeg * Math.PI / 180.0,
				Mode = Mode,
				PulseShape = PulseShape,
				OnWidthCycles = PulseWidthCycles,
				OffWidthCycles = OffStateCycles,
				OffWidthBase = OffStateCycles,
				RandomizeOffWidthAmount = 0,
				DetachAfterStep = DetachSource ? DetachAfterSteps : -1
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
			data["name"] = Name;
			data["main_type"] = ((MainType == SourceMainType.Composite) ? "composite" : "single");
			data["x"] = PosX;
			data["y"] = PosY;
			data["has_position"] = HasPosition;
			data["source_on"] = SourceOn;
			data["detach_source"] = DetachSource;
			data["detach_after_steps"] = DetachAfterSteps;
			data["frequency"] = Frequency;
			data["amplitude"] = Amplitude;
			data["phase_deg"] = PhaseDeg;
			data["mode"] = ((Mode == SourceMode.Pulsed) ? "pulsed" : "continuous");
			data["pulse_shape"] = ((PulseShape == SourcePulseShape.SmoothedSquare) ? "smoothed_square" : "gaussian");
			data["pulse_width_cycles"] = PulseWidthCycles;
			data["off_state_cycles"] = OffStateCycles;
			data["array_size_x"] = ArraySizeX;
			data["array_size_y"] = ArraySizeY;
			data["spacing_x"] = SpacingX;
			data["spacing_y"] = SpacingY;
			data["composite_mode"] = ((CompositeMode == SourceCompositeMode.InheritAndRandomize) ? "randomize" : "inherit");
			data["randomize_frequency"] = RandomizeFrequency;
			data["randomize_frequency_amount"] = RandomizeFrequencyAmount;
			data["randomize_phase"] = RandomizePhase;
			data["randomize_phase_amount"] = RandomizePhaseAmount;
			data["randomize_off_width"] = RandomizeOffWidth;
			data["randomize_off_width_amount"] = RandomizeOffWidthAmount;
			data["randomize_position"] = RandomizePosition;
			data["randomize_position_amount"] = RandomizePositionAmount;
			List<object> elements = new List<object>();
			foreach (SourceElement element in Elements)
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
		/// constructor defaults, except <c>has_position</c> which defaults to true (see the comments in the
		/// body for the keys that were added after the first projects were saved); unknown enum
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
			// a project saved before HasPosition existed has no "has_position" entry - default to true, since its
			// sources were positioned under the old rules (there was no "unset" state back then)
			s.HasPosition = SourceSettings.ReadBool(d, "has_position", true);
			// a project saved before SourceOn/DetachSource existed has no entries for them either - default to the
			// same behaviour it always had: on, and never detaching
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
						// a project saved before off-width re-randomisation existed has no "off_width_base" entry -
						// default to this element's own off_width_cycles (already read above), which is exactly its
						// anchor under the old, always-static behaviour
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
					// use the fallback
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
					// use the fallback
				}
			}
			return fallback;
		}

		/// <summary>Name of the project config section that holds the source list.</summary>
		public const string SectionName = "object_sources";
	}
}
