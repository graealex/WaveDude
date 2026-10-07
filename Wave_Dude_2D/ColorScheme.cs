using System;
using System.Collections.Generic;
using System.Drawing;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// A 255-entry color table, optionally backed by 9 editable gradient stops.
	/// </summary>
	/// <remarks>
	/// Entry 0 is the start of the scale (field -1 / intensity 0), entry 254 the end (+1 / max), entry 127
	/// the centre of a field scheme. Instances are created by <see cref="ColorSchemeStore.Load"/> and the
	/// editor on the color scheme tab, live on the UI thread, and are turned into an <c>int[]</c> palette by
	/// <see cref="ColorLut.Build"/> before a simulation starts; the worker thread never touches them.
	/// The <see cref="Entries"/> and <see cref="Stops"/> getters return defensive copies, so the only way to
	/// change the table is <see cref="SetStop"/>. All colors are opaque RGB; alpha is ignored.
	/// </remarks>
	public class ColorScheme
	{
		/// <summary>Display name; also the JSON "name" and the basis of the file name (see <see cref="ColorSchemeStore.SafeFileName"/>).</summary>
		public string Name { get; set; }

		/// <summary>Free-text provenance of the colors (JSON "origin"), e.g. the matplotlib map it was sampled from.</summary>
		public string Origin { get; set; }

		/// <summary>Free-text grouping such as "sequential" or "custom" (JSON "category"); empty when unknown.</summary>
		public string Category { get; set; }

		/// <summary>
		/// What the scheme is meant to color: <see cref="SchemeTypeField"/>, <see cref="SchemeTypeIntensity"/>
		/// or "" when not specified. Decides which combo box on the simulation tab offers the scheme.
		/// </summary>
		public string SchemeType { get; set; }

		/// <summary>Full path of the JSON file the scheme was loaded from or last saved to; "" (or null) for an unsaved scheme.</summary>
		public string FilePath { get; set; }

		/// <summary>
		/// Creates a table-only scheme (no stops) by resampling <paramref name="colors"/> to
		/// <see cref="EntryCount"/> entries; the text properties start out empty.
		/// </summary>
		/// <param name="schemeName">Value for <see cref="Name"/>.</param>
		/// <param name="colors">At least two colors, in scale order; copied, not kept.</param>
		/// <exception cref="ArgumentNullException"><paramref name="colors"/> is null.</exception>
		/// <exception cref="ArgumentException">Fewer than two colors.</exception>
		public ColorScheme(string schemeName, IList<Color> colors)
		{
			if (colors == null)
			{
				throw new ArgumentNullException("colors");
			}
			this.Name = schemeName;
			this.Origin = "";
			this.Category = "";
			this.SchemeType = "";
			this.FilePath = "";
			this._entries = ColorScheme.Resample(colors, EntryCount);
		}

		/// <summary>A fresh copy of the full <see cref="EntryCount"/>-entry table (allocates on every call; use <see cref="GetEntry"/> in loops).</summary>
		public Color[] Entries
		{
			get
			{
				return (Color[])this._entries.Clone();
			}
		}

		/// <summary>Returns table entry <paramref name="index"/> (0..254) without copying the table.</summary>
		public Color GetEntry(int index)
		{
			return this._entries[index];
		}

		/// <summary>True when the scheme carries <see cref="StopCount"/> editable stops, i.e. <see cref="SetStop"/> may be called.</summary>
		public bool HasStops
		{
			get
			{
				return this._stops != null;
			}
		}

		/// <summary>A fresh copy of the <see cref="StopCount"/> stops, or null for a table-only scheme.</summary>
		public Color[] Stops
		{
			get
			{
				return this._stops == null ? null : (Color[])this._stops.Clone();
			}
		}

		/// <summary>Linearly interpolates between the table entries at a fractional position 0..254.</summary>
		/// <param name="position">Position on the entry scale; clamped to the first/last entry outside 0..254.</param>
		public Color SampleAt(double position)
		{
			if (position <= 0.0)
			{
				return this._entries[0];
			}
			if (position >= EntryCount - 1)
			{
				return this._entries[EntryCount - 1];
			}
			int index = (int)Math.Floor(position);
			return ColorScheme.Lerp(this._entries[index], this._entries[index + 1], position - index);
		}

		/// <summary>Returns <see cref="Name"/>, which is what the scheme combo boxes display.</summary>
		public override string ToString()
		{
			return this.Name;
		}

		/// <summary>
		/// Creates a stop scheme from exactly <see cref="StopCount"/> stops: the table is interpolated from
		/// them, <see cref="Origin"/> says it was made in the editor and <see cref="Category"/> is "custom".
		/// </summary>
		/// <exception cref="ArgumentException"><paramref name="stops"/> is null or not <see cref="StopCount"/> long.</exception>
		public static ColorScheme FromStops(string schemeName, IList<Color> stops)
		{
			ColorScheme scheme = new ColorScheme(schemeName, ColorScheme.InterpolateStops(stops));
			scheme.AttachStops(stops);
			scheme.Origin = StopCount.ToString() + "-stop gradient made in the color scheme editor";
			scheme.Category = "custom";
			return scheme;
		}

		/// <summary>
		/// Stores a copy of <paramref name="stops"/> as the editable stops without regenerating the
		/// table. Used by <see cref="ColorSchemeStore.Load"/> when a file has both "colors" and "stops", so the
		/// table may differ from the stops until the first <see cref="SetStop"/>.
		/// </summary>
		/// <exception cref="ArgumentException"><paramref name="stops"/> is null or not <see cref="StopCount"/> long.</exception>
		internal void AttachStops(IList<Color> stops)
		{
			if (stops == null || stops.Count != StopCount)
			{
				throw new ArgumentException("Exactly " + StopCount.ToString() + " stops are required.");
			}
			Color[] copy = new Color[StopCount];
			stops.CopyTo(copy, 0);
			this._stops = copy;
		}

		/// <summary>
		/// Replaces stop <paramref name="stopIndex"/> with the RGB part of <paramref name="value"/> (alpha dropped)
		/// and rebuilds the whole table from the stops.
		/// </summary>
		/// <param name="stopIndex">0..<see cref="StopCount"/>-1.</param>
		/// <param name="value">New stop color; only R, G and B are used.</param>
		/// <exception cref="InvalidOperationException">The scheme has no stops (<see cref="HasStops"/> is false).</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="stopIndex"/> is outside 0..8.</exception>
		public void SetStop(int stopIndex, Color value)
		{
			if (this._stops == null)
			{
				throw new InvalidOperationException("This scheme has no editable stops. Use ToStopScheme first.");
			}
			if (stopIndex < 0 || stopIndex >= StopCount)
			{
				throw new ArgumentOutOfRangeException("stopIndex");
			}
			this._stops[stopIndex] = Color.FromArgb(value.R, value.G, value.B);
			this._entries = ColorScheme.InterpolateStops(this._stops);
		}

		/// <summary>
		/// Creates a 9-stop approximation of this scheme by sampling it at evenly spaced positions
		/// (k/8 of the 0..254 range). The result keeps this <see cref="SchemeType"/>, gets an
		/// <see cref="Origin"/> naming this scheme, <see cref="Category"/> "custom" and no file path.
		/// </summary>
		public ColorScheme ToStopScheme(string newName)
		{
			Color[] stops = new Color[StopCount];
			for (int i = 0; i < StopCount; i++)
			{
				stops[i] = this.SampleAt((i * 254) / 8.0);
			}
			ColorScheme scheme = ColorScheme.FromStops(newName, stops);
			scheme.Origin = StopCount.ToString() + "-stop copy of scheme '" + this.Name + "'";
			scheme.SchemeType = this.SchemeType;
			return scheme;
		}

		/// <summary>
		/// Deep copy with a new name: table, stops (if any), <see cref="Origin"/>, <see cref="Category"/> and
		/// <see cref="SchemeType"/> are copied; <see cref="FilePath"/> is not (the copy is "unsaved").
		/// </summary>
		public ColorScheme Clone(string newName)
		{
			ColorScheme scheme = new ColorScheme(newName, this._entries);
			scheme.Origin = this.Origin;
			scheme.Category = this.Category;
			scheme.SchemeType = this.SchemeType;
			if (this._stops != null)
			{
				scheme.AttachStops(this._stops);
			}
			return scheme;
		}

		/// <summary>Expands 9 stops into the full 255-entry table by linear RGB interpolation; stop k lands on entry k*254/8.</summary>
		/// <exception cref="ArgumentException"><paramref name="stops"/> is null or not <see cref="StopCount"/> long.</exception>
		public static Color[] InterpolateStops(IList<Color> stops)
		{
			if (stops == null || stops.Count != StopCount)
			{
				throw new ArgumentException("Exactly " + StopCount.ToString() + " stops are required.");
			}
			Color[] entries = new Color[EntryCount];
			for (int i = 0; i < EntryCount; i++)
			{
				// Position of entry i in stop units (0..8); the last entry must stay in segment 7.
				double position = (i * 8) / 254.0;
				int segment = (int)Math.Floor(position);
				if (segment > 7)
				{
					segment = 7;
				}
				entries[i] = ColorScheme.Lerp(stops[segment], stops[segment + 1], position - segment);
			}
			return entries;
		}

		/// <summary>
		/// Resamples an arbitrary-length color list to exactly <paramref name="count"/> entries by linear RGB
		/// interpolation (a plain copy when the lengths already match).
		/// </summary>
		/// <param name="source">At least two colors.</param>
		/// <param name="count">Number of entries wanted; must be at least 2.</param>
		/// <exception cref="ArgumentException"><paramref name="source"/> is null or has fewer than two colors.</exception>
		public static Color[] Resample(IList<Color> source, int count)
		{
			if (source == null || source.Count < 2)
			{
				throw new ArgumentException("A color scheme needs at least 2 colors.");
			}
			Color[] result = new Color[count];
			if (source.Count == count)
			{
				source.CopyTo(result, 0);
				return result;
			}
			for (int i = 0; i < count; i++)
			{
				double position = (double)(i * (source.Count - 1)) / (count - 1);
				int index = (int)Math.Floor(position);
				if (index >= source.Count - 1)
				{
					result[i] = source[source.Count - 1];
				}
				else
				{
					result[i] = ColorScheme.Lerp(source[index], source[index + 1], position - index);
				}
			}
			return result;
		}

		/// <summary>Per-channel linear blend of two opaque colors; <paramref name="t"/> = 0 gives <paramref name="a"/>, 1 gives <paramref name="b"/>.</summary>
		private static Color Lerp(Color a, Color b, double t)
		{
			return Color.FromArgb(ColorScheme.LerpChannel(a.R, b.R, t), ColorScheme.LerpChannel(a.G, b.G, t), ColorScheme.LerpChannel(a.B, b.B, t));
		}

		/// <summary>Blends one 0..255 channel, rounding halves away from zero and clamping to 0..255.</summary>
		private static int LerpChannel(int a, int b, double t)
		{
			int value = (int)Math.Round(a + (b - a) * t, MidpointRounding.AwayFromZero);
			if (value < 0)
			{
				value = 0;
			}
			if (value > 255)
			{
				value = 255;
			}
			return value;
		}

		/// <summary>Number of entries in the color table (indices 0..254).</summary>
		public const int EntryCount = 255;

		/// <summary>Number of editable gradient stops of a stop scheme (8 segments).</summary>
		public const int StopCount = 9;

		/// <summary>The <see cref="EntryCount"/> table entries; replaced wholesale by <see cref="SetStop"/>, never null.</summary>
		private Color[] _entries;

		/// <summary>The <see cref="StopCount"/> editable stops, or null for a table-only scheme.</summary>
		private Color[] _stops;

		/// <summary><see cref="SchemeType"/> value for schemes that color the signed field (-1..+1).</summary>
		public const string SchemeTypeField = "field";

		/// <summary><see cref="SchemeType"/> value for schemes that color the accumulated intensity (0..max).</summary>
		public const string SchemeTypeIntensity = "intensity";
	}
}
