using System.Globalization;

namespace WaveDude.Rendering;

/// <summary>
/// A color scheme: a lookup table of exactly <see cref="EntryCount"/> (255) RGB colors, the same table
/// size the Python simulator uses, optionally backed by <see cref="StopCount"/> editable gradient stops.
/// A scheme can be used for the field view or for the intensity view.
/// </summary>
/// <remarks>
/// Entry 0 is the start of the scale (field -1 / intensity 0), entry 254 the end (+1 / max), entry 127
/// the centre of a field scheme. Schemes made in the stop editor also keep their anchor colors
/// ("stops") so they can be edited again. The stops sit at fixed, equally spaced positions on the
/// scale: stop 1 is the first entry, the last stop is the last entry and, with an odd number of stops,
/// the middle stop is exactly the centre entry 127 (the zero-field color); with 9 stops that is
/// Color 5. All other entries are interpolated linearly in RGB between neighbouring stops. Imported
/// schemes (matplotlib, seaborn, Python tables) have no stops and are used as they are.
/// Instances are created by <see cref="ColorSchemeStore.Load"/> and a scheme editor, live on the UI
/// thread, and are turned into an <c>int[]</c> palette by <see cref="ColorLut.Build"/> before a frame is
/// rendered; the rendering threads never touch them. The <see cref="Entries"/> and <see cref="Stops"/>
/// getters return defensive copies, so the only way to change the table is <see cref="SetStop"/>.
/// </remarks>
public class ColorScheme
{
	/// <summary>Display name; also the JSON "name" and the basis of the file name (see <see cref="ColorSchemeStore.SafeFileName"/>).</summary>
	public string Name { get; set; }

	/// <summary>Free-text provenance of the colors (JSON "origin"), e.g. the matplotlib map it was sampled from.</summary>
	public string Origin { get; set; } = "";

	/// <summary>Free-text grouping such as "sequential" or "custom" (JSON "category"); empty when unknown.</summary>
	public string Category { get; set; } = "";

	/// <summary>
	/// What the scheme is meant for: <see cref="SchemeTypeField"/>, <see cref="SchemeTypeIntensity"/>
	/// or "" when not specified. This is only a label saved in the file (it decides which list offers the
	/// scheme); any scheme can technically be used for either.
	/// </summary>
	public string SchemeType { get; set; } = "";

	/// <summary>Full path of the JSON file the scheme was loaded from or last saved to; "" for an unsaved or a built-in scheme.</summary>
	public string FilePath { get; set; } = "";

	/// <summary>Whether the scheme ships with the program (embedded in the library); such a scheme is not written back anywhere.</summary>
	public bool IsBuiltIn { get; set; }

	/// <summary>
	/// Creates a table-only scheme (no stops) by resampling <paramref name="colors"/> to
	/// <see cref="EntryCount"/> entries; the text properties start out empty.
	/// </summary>
	/// <param name="schemeName">Value for <see cref="Name"/>.</param>
	/// <param name="colors">At least two colors, in scale order; copied, not kept.</param>
	/// <exception cref="ArgumentException">Fewer than two colors.</exception>
	public ColorScheme(string schemeName, IReadOnlyList<Rgb> colors)
	{
		Name = schemeName;
		_entries = Resample(colors, EntryCount);
	}

	/// <summary>A fresh copy of the full <see cref="EntryCount"/>-entry table (allocates on every call; use <see cref="GetEntry"/> in loops).</summary>
	public Rgb[] Entries => (Rgb[])_entries.Clone();

	/// <summary>Returns table entry <paramref name="index"/> (0..254) without copying the table.</summary>
	public Rgb GetEntry(int index) => _entries[index];

	/// <summary>True when the scheme carries <see cref="StopCount"/> editable stops, i.e. <see cref="SetStop"/> may be called.</summary>
	public bool HasStops => _stops != null;

	/// <summary>A fresh copy of the <see cref="StopCount"/> stops, or null for a table-only scheme.</summary>
	public Rgb[]? Stops => (Rgb[]?)_stops?.Clone();

	/// <summary>Linearly interpolates between the table entries at a fractional position 0..254.</summary>
	/// <param name="position">Position on the entry scale; clamped to the first/last entry outside 0..254.</param>
	public Rgb SampleAt(double position)
	{
		if (position <= 0.0)
		{
			return _entries[0];
		}
		if (position >= EntryCount - 1)
		{
			return _entries[EntryCount - 1];
		}
		int index = (int)Math.Floor(position);
		return Lerp(_entries[index], _entries[index + 1], position - index);
	}

	/// <summary>Returns <see cref="Name"/>, which is what scheme lists display.</summary>
	public override string ToString() => Name;

	/// <summary>
	/// Creates a stop scheme from exactly <see cref="StopCount"/> stops, given in scale order (stop 1
	/// first, the middle stop in the centre, the last stop last): the table is interpolated from them,
	/// <see cref="Origin"/> says it was made in the editor and <see cref="Category"/> is "custom".
	/// </summary>
	/// <exception cref="ArgumentException"><paramref name="stops"/> is not <see cref="StopCount"/> long.</exception>
	public static ColorScheme FromStops(string schemeName, IReadOnlyList<Rgb> stops)
	{
		var scheme = new ColorScheme(schemeName, InterpolateStops(stops));
		scheme.AttachStops(stops);
		scheme.Origin = StopCount.ToString(CultureInfo.InvariantCulture) + "-stop gradient made in the color scheme editor";
		scheme.Category = "custom";
		return scheme;
	}

	/// <summary>
	/// Stores a copy of <paramref name="stops"/> as the editable stops without regenerating the
	/// table. Used by <see cref="ColorSchemeStore.Load"/> when a file has both "colors" and "stops", so the
	/// table may differ from the stops until the first <see cref="SetStop"/>.
	/// </summary>
	/// <exception cref="ArgumentException"><paramref name="stops"/> is not <see cref="StopCount"/> long.</exception>
	internal void AttachStops(IReadOnlyList<Rgb> stops)
	{
		if (stops.Count != StopCount)
		{
			throw new ArgumentException("Exactly " + StopCount.ToString(CultureInfo.InvariantCulture) + " stops are required.");
		}
		_stops = [.. stops];
	}

	/// <summary>Replaces stop <paramref name="stopIndex"/> with <paramref name="value"/> and rebuilds the whole table from the stops.</summary>
	/// <param name="stopIndex">0..<see cref="StopCount"/>-1.</param>
	/// <param name="value">New stop color.</param>
	/// <exception cref="InvalidOperationException">The scheme has no stops (<see cref="HasStops"/> is false).</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="stopIndex"/> is outside 0..<see cref="StopCount"/>-1.</exception>
	public void SetStop(int stopIndex, Rgb value)
	{
		if (_stops == null)
		{
			throw new InvalidOperationException("This scheme has no editable stops. Use ToStopScheme first.");
		}
		if (stopIndex < 0 || stopIndex >= StopCount)
		{
			throw new ArgumentOutOfRangeException(nameof(stopIndex));
		}
		_stops[stopIndex] = value;
		_entries = InterpolateStops(_stops);
	}

	/// <summary>
	/// Makes an editable <see cref="StopCount"/>-stop scheme from any scheme by sampling it at the stop
	/// positions (k/(StopCount-1) of the 0..254 range). This is lossy: the copy is a smooth
	/// approximation of the original. The result keeps this <see cref="SchemeType"/>, gets an
	/// <see cref="Origin"/> naming this scheme, <see cref="Category"/> "custom" and no file path.
	/// </summary>
	public ColorScheme ToStopScheme(string newName)
	{
		var stops = new Rgb[StopCount];
		for (int i = 0; i < StopCount; i++)
		{
			stops[i] = SampleAt((i * (EntryCount - 1)) / (double)(StopCount - 1));
		}
		ColorScheme scheme = FromStops(newName, stops);
		scheme.Origin = StopCount.ToString(CultureInfo.InvariantCulture) + "-stop copy of scheme '" + Name + "'";
		scheme.SchemeType = SchemeType;
		return scheme;
	}

	/// <summary>
	/// Deep copy with a new name: table, stops (if any), <see cref="Origin"/>, <see cref="Category"/> and
	/// <see cref="SchemeType"/> are copied; <see cref="FilePath"/> is not (the copy is "unsaved").
	/// </summary>
	public ColorScheme Clone(string newName)
	{
		var scheme = new ColorScheme(newName, _entries)
		{
			Origin = Origin,
			Category = Category,
			SchemeType = SchemeType,
		};
		if (_stops != null)
		{
			scheme.AttachStops(_stops);
		}
		return scheme;
	}

	/// <summary>
	/// Builds the <see cref="EntryCount"/> entries from the <see cref="StopCount"/> stops at fixed, equally
	/// spaced positions (linear in RGB); stop k lands on entry k*(EntryCount-1)/(StopCount-1).
	/// </summary>
	/// <exception cref="ArgumentException"><paramref name="stops"/> is not <see cref="StopCount"/> long.</exception>
	public static Rgb[] InterpolateStops(IReadOnlyList<Rgb> stops)
	{
		if (stops.Count != StopCount)
		{
			throw new ArgumentException("Exactly " + StopCount.ToString(CultureInfo.InvariantCulture) + " stops are required.");
		}
		var entries = new Rgb[EntryCount];
		for (int i = 0; i < EntryCount; i++)
		{
			// position on the stop scale: 0.0 (first stop) to StopCount - 1 (last stop);
			// with 9 stops entry 127 is exactly 4.0, the middle stop.
			// The last entry must stay in the last segment (StopCount - 2).
			double position = (i * (StopCount - 1)) / (double)(EntryCount - 1);
			int segment = Math.Min((int)Math.Floor(position), StopCount - 2);
			entries[i] = Lerp(stops[segment], stops[segment + 1], position - segment);
		}
		return entries;
	}

	/// <summary>
	/// Resamples an arbitrary-length color list to exactly <paramref name="count"/> entries by linear RGB
	/// interpolation (a plain copy when the lengths already match).
	/// </summary>
	/// <param name="source">At least two colors.</param>
	/// <param name="count">Number of entries wanted; must be at least 2.</param>
	/// <exception cref="ArgumentException"><paramref name="source"/> has fewer than two colors.</exception>
	public static Rgb[] Resample(IReadOnlyList<Rgb> source, int count)
	{
		if (source.Count < 2)
		{
			throw new ArgumentException("A color scheme needs at least 2 colors.");
		}
		var result = new Rgb[count];
		if (source.Count == count)
		{
			for (int i = 0; i < count; i++)
			{
				result[i] = source[i];
			}
			return result;
		}
		for (int i = 0; i < count; i++)
		{
			double position = (double)(i * (source.Count - 1)) / (count - 1);
			int index = (int)Math.Floor(position);
			result[i] = index >= source.Count - 1 ? source[^1] : Lerp(source[index], source[index + 1], position - index);
		}
		return result;
	}

	/// <summary>Per-channel linear blend of two colors; <paramref name="t"/> = 0 gives <paramref name="a"/>, 1 gives <paramref name="b"/>.</summary>
	private static Rgb Lerp(Rgb a, Rgb b, double t) =>
		Rgb.Clamped(LerpChannel(a.R, b.R, t), LerpChannel(a.G, b.G, t), LerpChannel(a.B, b.B, t));

	/// <summary>Blends one 0..255 channel, rounding halves away from zero.</summary>
	private static int LerpChannel(int a, int b, double t) => (int)Math.Round(a + (b - a) * t, MidpointRounding.AwayFromZero);

	/// <summary>Number of entries in the color table (indices 0..254).</summary>
	public const int EntryCount = 255;

	/// <summary>
	/// Number of fixed anchor colors in the stop editor: 9 (upgraded from 5 to 7 to 9 over time). Odd on
	/// purpose, so the middle stop is exactly the centre entry 127; see the class remarks.
	/// </summary>
	public const int StopCount = 9;

	/// <summary>The <see cref="EntryCount"/> table entries; replaced wholesale by <see cref="SetStop"/>.</summary>
	private Rgb[] _entries;

	/// <summary>The <see cref="StopCount"/> editable stops, or null for a table-only scheme.</summary>
	private Rgb[]? _stops;

	/// <summary><see cref="SchemeType"/> value for schemes that color the signed field (-1..+1).</summary>
	public const string SchemeTypeField = "field";

	/// <summary><see cref="SchemeType"/> value for schemes that color the accumulated intensity (0..max).</summary>
	public const string SchemeTypeIntensity = "intensity";
}
