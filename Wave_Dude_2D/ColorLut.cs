using System;
using System.Drawing;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// An immutable palette of <see cref="Size"/> opaque ARGB entries built from a
	/// <see cref="ColorScheme"/> by <see cref="Build"/>, plus the static value-to-index
	/// mappings used when colouring frames. Built on the UI thread when a simulation is
	/// started; the <see cref="Argb"/> array is then handed to <see cref="SimulationRunner"/>
	/// and read from <see cref="FrameRenderer"/>'s parallel loops without locking (it is
	/// never modified after construction).
	/// </summary>
	public sealed class ColorLut
	{
		private ColorLut(int[] argb)
		{
			this._argb = argb;
		}

		/// <summary>
		/// The palette as packed opaque ARGB ints (<see cref="Color.ToArgb"/> layout), indices
		/// 0..<see cref="Size"/>-1. Returns the internal array, not a copy; callers must not modify it.
		/// </summary>
		public int[] Argb
		{
			get
			{
				return this._argb;
			}
		}

		/// <summary>Returns entry <paramref name="index"/> (0..<see cref="Size"/>-1) as a <see cref="Color"/>.</summary>
		public Color ColorAt(int index)
		{
			return Color.FromArgb(this._argb[index]);
		}

		/// <summary>
		/// Builds a palette from the <see cref="Size"/> entries of <paramref name="scheme"/>.
		/// Transformations are applied in this order: <paramref name="reverse"/> (entry i is read
		/// from entry <c>Size-1-i</c>), <paramref name="invert"/> (each channel becomes
		/// <c>1 - channel</c>), <paramref name="makeSymmetric"/> (entry i is read from entry
		/// <c>2 * min(i, Size-1-i)</c>, so the palette runs from entry 0 at both ends up to entry
		/// 254 in the middle), then <paramref name="blackLevel"/> lifts every channel linearly so
		/// that 0 becomes <paramref name="blackLevel"/> and 1 stays 1, clamped to 0..1. Alpha is always 255.
		/// </summary>
		/// <param name="scheme">Source colours; must have at least <see cref="Size"/> entries.</param>
		/// <param name="invert">Invert every channel (negative image).</param>
		/// <param name="blackLevel">Minimum channel level 0..1 after lifting (0 = unchanged).</param>
		/// <param name="makeSymmetric">Mirror the palette around its centre index.</param>
		/// <param name="reverse">Read the scheme back to front.</param>
		/// <exception cref="ArgumentNullException"><paramref name="scheme"/> is null.</exception>
		public static ColorLut Build(ColorScheme scheme, bool invert = false, double blackLevel = 0.0, bool makeSymmetric = false, bool reverse = false)
		{
			if (scheme == null)
			{
				throw new ArgumentNullException("scheme");
			}
			double[,] rgb = new double[Size, 3];
			for (int i = 0; i < Size; i++)
			{
				Color entry = scheme.GetEntry(reverse ? (Size - 1 - i) : i);
				rgb[i, 0] = entry.R / 255.0;
				rgb[i, 1] = entry.G / 255.0;
				rgb[i, 2] = entry.B / 255.0;
			}
			if (invert)
			{
				for (int i = 0; i < Size; i++)
				{
					for (int channel = 0; channel < 3; channel++)
					{
						rgb[i, channel] = 1.0 - rgb[i, channel];
					}
				}
			}
			int[] argb = new int[Size];
			for (int i = 0; i < Size; i++)
			{
				int sourceIndex = i;
				if (makeSymmetric)
				{
					sourceIndex = 2 * Math.Min(i, Size - 1 - i);
				}
				int[] components = new int[3];
				for (int channel = 0; channel < 3; channel++)
				{
					double level = rgb[sourceIndex, channel] * (1.0 - blackLevel) + blackLevel;
					if (level < 0.0)
					{
						level = 0.0;
					}
					if (level > 1.0)
					{
						level = 1.0;
					}
					int component = (int)(level * 255.0);
					if (component > 255)
					{
						component = 255;
					}
					components[channel] = component;
				}
				argb[i] = Color.FromArgb(255, components[0], components[1], components[2]).ToArgb();
			}
			return new ColorLut(argb);
		}

		/// <summary>
		/// Maps a signed field sample to a palette index: <c>value * brightness</c> is clamped to
		/// -1..1 and mapped linearly to 0..254 with 0 at index 127. NaN gives 127. Called per
		/// pixel from <see cref="FrameRenderer.RenderField"/> on worker threads.
		/// </summary>
		/// <param name="value">Raw field amplitude from the engine.</param>
		/// <param name="brightness">Gain applied before clamping.</param>
		public static int FieldIndex(float value, float brightness)
		{
			float scaled = value * brightness;
			if (float.IsNaN(scaled))
			{
				return 127;
			}
			if (scaled > 1f)
			{
				scaled = 1f;
			}
			if (scaled < -1f)
			{
				scaled = -1f;
			}
			return (int)(scaled * 127f + 127f);
		}

		/// <summary>
		/// Maps a non-negative intensity to a palette index 0..254: <c>intensity ^ exponent</c>
		/// (square root by default) times <paramref name="brightness"/>, clamped to 0..1 and
		/// scaled to 254. NaN or non-positive intensity gives 0. Not currently called;
		/// <see cref="FrameRenderer.RenderIntensity"/> inlines the equivalent square-root mapping.
		/// </summary>
		/// <param name="intensity">Accumulated intensity from the engine.</param>
		/// <param name="brightness">Gain applied after compression.</param>
		/// <param name="exponent">Compression exponent; 0.5 = square root.</param>
		public static int IntensityIndex(float intensity, float brightness, float exponent = 0.5f)
		{
			if (float.IsNaN(intensity) || intensity <= 0f)
			{
				return 0;
			}
			double scaled = Math.Pow(intensity, exponent) * brightness;
			if (double.IsNaN(scaled) || scaled < 0.0)
			{
				return 0;
			}
			if (scaled > 1.0)
			{
				scaled = 1.0;
			}
			return (int)(scaled * 254.0);
		}

		/// <summary>Number of entries in a palette (255, matching <see cref="ColorScheme.EntryCount"/>).</summary>
		public const int Size = 255;

		/// <summary>The packed opaque ARGB entries; never modified after construction.</summary>
		private readonly int[] _argb;
	}
}
