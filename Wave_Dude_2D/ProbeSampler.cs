using System;
using System.Collections.Generic;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// What a simulation engine has to measure for the probes: precomputed per-probe pixel indices (into the
	/// padded field grid, see <see cref="IWaveEngine"/>) and what is read there, the field or the intensity.
	/// <list type="bullet">
	/// <item><description>Point probe: one pixel; the engine measures its value at every step (a series).</description></item>
	/// <item><description>Point, integrated: the same, but the values are added up over time (done by <see cref="ProbeCollector"/>).</description></item>
	/// <item><description>Line probe: the pixels on the line A-B; the engine gives the values along the line when asked (a profile).</description></item>
	/// <item><description>Line, integrated: the engine adds the intensity of every pixel of the line up over the steps.</description></item>
	/// </list>
	/// Built once per run by <see cref="Build"/> on the simulation worker thread from the run's probe list and
	/// canvas size, then handed to the engine (<see cref="IWaveEngine.SetProbes"/>) and to the
	/// <see cref="ProbeCollector"/>. All per-probe arrays are indexed by the probe's position in that list.
	/// Immutable after construction, so it may be read from any thread.
	/// </summary>
	internal sealed class ProbeSampler
	{
		/// <summary>Stores the per-probe arrays and derives the aggregate <see cref="NeedsIntensity"/>, <see cref="HasSeries"/> and <see cref="HasProfiles"/> flags.</summary>
		private ProbeSampler(int[][] pixels, bool[] isProfile, bool[] useIntensity, bool[] cumulative, bool[] integrate, double[] lengths, bool[] useStartAfterStep, int[] startAfterStep)
		{
			Count = pixels.Length;
			Pixels = pixels;
			IsProfile = isProfile;
			UseIntensity = useIntensity;
			Cumulative = cumulative;
			Integrate = integrate;
			Lengths = lengths;
			UseStartAfterStep = useStartAfterStep;
			StartAfterStep = startAfterStep;
			for (int i = 0; i < Count; i++)
			{
				if (useIntensity[i])
				{
					NeedsIntensity = true;
				}
				if (isProfile[i])
				{
					HasProfiles = true;
				}
				else
				{
					HasSeries = true;
				}
			}
		}

		/// <summary>
		/// Builds the sampler for a canvas of <paramref name="w"/> x <paramref name="h"/> pixels. Probe positions
		/// are clamped into the canvas; point probes get a single pixel index, line probes the Bresenham path from
		/// A to B. The padded grid stride is <paramref name="w"/> + 2.
		/// </summary>
		/// <param name="probes">Run probe list; the per-probe arrays of the result use the same order.</param>
		/// <param name="w">Canvas width in pixels.</param>
		/// <param name="h">Canvas height in pixels.</param>
		public static ProbeSampler Build(IList<ProbeSettings> probes, int w, int h)
		{
			int count = probes.Count;
			int[][] pixels = new int[count][];
			bool[] isProfile = new bool[count];
			bool[] useIntensity = new bool[count];
			bool[] cumulative = new bool[count];
			bool[] integrate = new bool[count];
			double[] lengths = new double[count];
			bool[] useStartAfterStep = new bool[count];
			int[] startAfterStep = new int[count];
			int stride = w + 2;
			for (int i = 0; i < count; i++)
			{
				ProbeSettings probe = probes[i];
				int ax = ProbeSampler.Clamp(probe.PosAX, w - 1);
				int ay = ProbeSampler.Clamp(probe.PosAY, h - 1);
				bool isIntegrated = probe.Measurement == ProbeMeasurement.IntegratedIntensity;
				if (probe.Kind == ProbeKind.Line)
				{
					int bx = ProbeSampler.Clamp(probe.PosBX, w - 1);
					int by = ProbeSampler.Clamp(probe.PosBY, h - 1);
					pixels[i] = ProbeSampler.LinePixels(ax, ay, bx, by, stride);
					isProfile[i] = true;
					integrate[i] = isIntegrated;
					double dx = bx - ax;
					double dy = by - ay;
					lengths[i] = Math.Sqrt(dx * dx + dy * dy);
				}
				else
				{
					pixels[i] = new int[] { (ay + 1) * stride + ax + 1 };
					cumulative[i] = isIntegrated;
				}
				// Intensity and IntegratedIntensity both read the intensity buffer.
				useIntensity[i] = probe.Measurement != ProbeMeasurement.Amplitude;
				useStartAfterStep[i] = probe.UseStartAfterStep;
				startAfterStep[i] = probe.StartAfterStep;
			}
			return new ProbeSampler(pixels, isProfile, useIntensity, cumulative, integrate, lengths, useStartAfterStep, startAfterStep);
		}

		/// <summary>Clamps <paramref name="value"/> into [0, <paramref name="maxValue"/>].</summary>
		private static int Clamp(int value, int maxValue)
		{
			if (value < 0)
			{
				return 0;
			}
			if (value > maxValue)
			{
				return maxValue;
			}
			return value;
		}

		/// <summary>Bresenham walk from (x0, y0) to (x1, y1), both inclusive, returning padded-grid indices in walking order (A first).</summary>
		/// <param name="x0">Start x in canvas pixels.</param>
		/// <param name="y0">Start y in canvas pixels.</param>
		/// <param name="x1">End x in canvas pixels.</param>
		/// <param name="y1">End y in canvas pixels.</param>
		/// <param name="stride">Row length of the padded grid (canvas width + 2).</param>
		private static int[] LinePixels(int x0, int y0, int x1, int y1, int stride)
		{
			List<int> indices = new List<int>();
			int dx = Math.Abs(x1 - x0);
			int dy = -Math.Abs(y1 - y0);
			int stepX = (x0 < x1) ? 1 : -1;
			int stepY = (y0 < y1) ? 1 : -1;
			int err = dx + dy;
			while (true)
			{
				indices.Add((y0 + 1) * stride + x0 + 1);
				if (x0 == x1 && y0 == y1)
				{
					break;
				}
				int e2 = 2 * err;
				if (e2 >= dy)
				{
					err += dy;
					x0 += stepX;
				}
				if (e2 <= dx)
				{
					err += dx;
					y0 += stepY;
				}
			}
			return indices.ToArray();
		}

		/// <summary>
		/// Reads the current value of a point probe from a padded-grid buffer. Used by the CPU engine
		/// (<c>WaveSimulator.SampleProbes</c>) on the worker thread after every step.
		/// </summary>
		/// <param name="probeIndex">Index of a point probe.</param>
		/// <param name="data">Padded field or intensity buffer, chosen by the caller according to <see cref="UseIntensity"/>.</param>
		/// <returns>The value at the probe's first pixel, or 0 when <paramref name="data"/> is null.</returns>
		public float Compute(int probeIndex, float[] data)
		{
			return (data == null) ? 0f : data[Pixels[probeIndex][0]];
		}

		/// <summary>Number of probes; the length of every per-probe array.</summary>
		public readonly int Count;

		/// <summary>Per probe: indices into the padded field/intensity buffer. A single entry for point probes, the whole A-to-B pixel path for line probes.</summary>
		public readonly int[][] Pixels;

		/// <summary>Per probe: true for line probes (which deliver a profile), false for point probes (which deliver a time series).</summary>
		public readonly bool[] IsProfile;

		/// <summary>Per probe: true when the measurement is Intensity or IntegratedIntensity, i.e. the intensity buffer is sampled instead of the field.</summary>
		public readonly bool[] UseIntensity;

		/// <summary>Per probe: point probe with IntegratedIntensity; <see cref="ProbeCollector.Collect"/> turns its per-step intensity samples into a running sum.</summary>
		public readonly bool[] Cumulative;

		/// <summary>Per probe: line probe with IntegratedIntensity; the engine accumulates per-pixel intensity totals over the steps.</summary>
		public readonly bool[] Integrate;

		/// <summary>Per probe: Euclidean length of the (clamped) probe line in canvas pixels; 0 for point probes.</summary>
		public readonly double[] Lengths;

		/// <summary>True when any probe samples the intensity; the runner then forces intensity tracking on in the engine.</summary>
		public readonly bool NeedsIntensity;

		/// <summary>True when at least one point probe exists.</summary>
		public readonly bool HasSeries;

		/// <summary>True when at least one line probe exists.</summary>
		public readonly bool HasProfiles;

		/// <summary>
		/// Per probe: whether "start recording at step #" is on. Used to purge (never accumulate) the running
		/// total of an integrated (cumulative) probe from before <see cref="StartAfterStep"/>, so the total shown
		/// once recording starts is not inflated by intensity that arrived before it - see
		/// <see cref="ProbeCollector.Collect"/>, <see cref="WaveSimulator.SampleProbes"/> and
		/// <see cref="GpuWaveSimulator"/>'s <c>profile_accumulate</c> kernel.
		/// </summary>
		public readonly bool[] UseStartAfterStep;

		/// <summary>Per probe: the step at which recording starts (the total before it is purged); only meaningful when <see cref="UseStartAfterStep"/> is set.</summary>
		public readonly int[] StartAfterStep;
	}
}
