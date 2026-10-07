using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// CPU implementation of <see cref="IWaveEngine"/>: an explicit leapfrog finite-difference solver for the
	/// scalar 2D wave equation on a grid padded by one cell on each side. Created by
	/// <see cref="WaveEngineFactory"/> on the <see cref="SimulationRunner"/> worker thread, which is the only
	/// thread that advances it; the UI thread reads <see cref="FieldData"/> / <see cref="IntensityData"/> only
	/// while the run is paused.
	/// </summary>
	/// <remarks>
	/// Units: grid spacing and time step are both 1, so the wave speed c is in cells (canvas pixels) per step,
	/// the per-cell <c>c2</c> is c² = 1/n² for refractive index n, and <see cref="Time"/> counts steps. Source
	/// frequencies are angular frequencies in radians per step (<see cref="_levelFrequency"/>).
	/// Per step: the sources are blended into the field once (<see cref="ApplySources"/>), then the field is
	/// advanced by <see cref="_subSteps"/> leapfrog sub-steps (<see cref="UpdateField"/>), the last of which also
	/// updates the running intensity. The 4th-order stencil needs a shorter time step to stay stable, hence
	/// the sub-stepping; the 9-point stencil always runs a single sub-step.
	/// </remarks>
	internal sealed class WaveSimulator : IWaveEngine
	{
		/// <summary>
		/// Copies the scene's c² and damping maps into the padded grid, derives the sub-step count (4th order
		/// only) and the matching per-sub-step damping and c² corrections, indexes the image and object sources,
		/// and sets the intensity averaging constant from the slowest source level in use.
		/// </summary>
		/// <param name="scene">Per-cell c² and damping maps plus the image (green-channel) sources.</param>
		/// <param name="sourceFrequencyScale">Multiplier in the level-to-angular-frequency mapping; see <see cref="_levelFrequency"/>.</param>
		/// <param name="sourceAmplitude">Peak amplitude of the image sources (object sources carry their own).</param>
		/// <param name="trackIntensity">Whether to allocate and update the intensity buffers; when false <see cref="IntensityData"/> stays null.</param>
		/// <param name="use4thOrderLaplacian">Use the 4th-order 5-point-per-axis stencil (with sub-stepping) instead of the 9-point one.</param>
		/// <param name="timeDecayFactor">Intensity averaging time in units of 100/level steps; see <see cref="_intensityAlpha"/>.</param>
		/// <param name="intensityScale">Multiplier applied to the intensity output.</param>
		/// <param name="objectSources">Interactive sources; those positioned outside the canvas are dropped.</param>
		public WaveSimulator(SceneData scene, double sourceFrequencyScale, double sourceAmplitude, bool trackIntensity, bool use4thOrderLaplacian, double timeDecayFactor, double intensityScale, List<SourceElement> objectSources = null)
		{
			this._levelFrequency = new double[256];
			this._levelUsed = new bool[256];
			this._levelValue = new float[256];
			this._objRandom = new Random();
			this._probeValues = new List<List<float>>();
			this._sourceAmplitude = sourceAmplitude;
			this._use4thOrderLaplacian = use4thOrderLaplacian;
			this._intensityScale = (float)intensityScale;
			this._width = scene.Width;
			this._height = scene.Height;
			this._stride = this._width + 2;
			int cellCount = this._stride * (this._height + 2);
			this._u = new float[cellCount];
			this._uPrev = new float[cellCount];
			this._c2 = new float[cellCount];
			this._d = new float[cellCount];
			if (trackIntensity)
			{
				this._intensity = new float[cellCount];
				this._intensityStage1 = new float[cellCount];
				this._intensityOut = new float[cellCount];
			}
			for (int y = 0; y < this._height; y++)
			{
				Array.Copy(scene.C2, y * this._width, this._c2, (y + 1) * this._stride + 1, this._width);
				Array.Copy(scene.D, y * this._width, this._d, (y + 1) * this._stride + 1, this._width);
			}
			if (use4thOrderLaplacian)
			{
				float maxC2 = 0f;
				for (int i = 0; i < this._c2.Length; i++)
				{
					if (this._c2[i] > maxC2)
					{
						maxC2 = this._c2[i];
					}
				}
				this._subSteps = WaveSimulator.ComputeSubSteps(maxC2);
			}
			else
			{
				this._subSteps = 1;
			}
			if (this._subSteps > 1)
			{
				// Damping is applied once per sub-step, so take the sub-step root to keep the per-step decay unchanged.
				float[] dEffective = new float[this._d.Length];
				double exponent = 1.0 / this._subSteps;
				for (int i = 0; i < this._d.Length; i++)
				{
					dEffective[i] = (float)Math.Pow(this._d[i], exponent);
				}
				this._dEffective = dEffective;
				// A sub-step is 1/subSteps long, so c²·dt² shrinks by 1/subSteps².
				this._c2Mult = 1f / (float)(this._subSteps * this._subSteps);
			}
			else
			{
				this._dEffective = this._d;
				this._c2Mult = 1f;
			}
			int sourceCount = scene.SourceCount;
			this._sourceIndex = new int[sourceCount];
			this._sourceLevel = new byte[sourceCount];
			for (int i = 0; i < sourceCount; i++)
			{
				this._sourceIndex[i] = (scene.SourceY[i] + 1) * this._stride + scene.SourceX[i] + 1;
				this._sourceLevel[i] = scene.SourceLevel[i];
				this._levelUsed[scene.SourceLevel[i]] = true;
			}
			for (int level = 0; level <= 255; level++)
			{
				this._levelFrequency[level] = level / 255.0 * 0.5 * sourceFrequencyScale;
			}
			if (objectSources == null)
			{
				this._objSources = Array.Empty<SourceElement>();
				this._objIndex = Array.Empty<int>();
			}
			else
			{
				List<SourceElement> insideSources = new List<SourceElement>();
				List<int> insideIndices = new List<int>();
				foreach (SourceElement source in objectSources)
				{
					if (source.PosX >= 0 && source.PosX < this._width && source.PosY >= 0 && source.PosY < this._height)
					{
						insideSources.Add(source);
						insideIndices.Add((source.PosY + 1) * this._stride + source.PosX + 1);
					}
				}
				this._objSources = insideSources.ToArray();
				this._objIndex = insideIndices.ToArray();
			}
			// The intensity decay time is scaled by the lowest (slowest) source level in use.
			int lowestLevel = 0;
			for (int level = 1; level <= 255; level++)
			{
				if (this._levelUsed[level])
				{
					lowestLevel = level;
					break;
				}
			}
			foreach (SourceElement source in this._objSources)
			{
				if (source.Level > 0 && (lowestLevel == 0 || source.Level < lowestLevel))
				{
					lowestLevel = source.Level;
				}
			}
			if (lowestLevel == 0)
			{
				lowestLevel = NominalDecayLevel;
			}
			double decaySteps = Math.Max(1.0, timeDecayFactor * 100.0 / lowestLevel);
			this._intensityAlpha = (float)Math.Exp(-1.0 / decaySteps);
			this._objCycleAnchor = new double[this._objSources.Length];
			this._objCurrentOffWidth = new int[this._objSources.Length];
			for (int i = 0; i < this._objSources.Length; i++)
			{
				this._objCurrentOffWidth[i] = this._objSources[i].OffWidthCycles;
			}
		}

		/// <summary>Scene width in cells (canvas pixels), excluding the padding.</summary>
		public int Width
		{
			get
			{
				return this._width;
			}
		}

		/// <summary>Scene height in cells (canvas pixels), excluding the padding.</summary>
		public int Height
		{
			get
			{
				return this._height;
			}
		}

		/// <summary>Row pitch of the padded buffers: <see cref="Width"/> + 2.</summary>
		public int Stride
		{
			get
			{
				return this._stride;
			}
		}

		/// <summary>
		/// The current field u (padded layout). The array instance changes every sub-step because the two
		/// field buffers are swapped, so callers must re-read this property per frame rather than cache the array.
		/// </summary>
		public float[] FieldData
		{
			get
			{
				return this._u;
			}
		}

		/// <summary>Scaled running intensity (padded layout), or null when the engine was created without intensity tracking.</summary>
		public float[] IntensityData
		{
			get
			{
				return this._intensityOut;
			}
		}

		/// <summary>Engine label for the UI/result: "CPU (n threads)" with the logical processor count.</summary>
		public string Description
		{
			get
			{
				return "CPU (" + Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture) + " threads)";
			}
		}

		/// <summary>No-op: the CPU buffers are always host-resident and up to date.</summary>
		public void PrepareFrame(bool needField, bool needIntensity)
		{
		}

		/// <summary>No-op: the engine owns only managed arrays.</summary>
		public void Dispose()
		{
		}

		/// <summary>
		/// Installs the probe layout (or clears it when <paramref name="sampler"/> is null or empty), discarding any
		/// pending samples and integrated totals, and allocates the per-pixel totals for integrated line probes.
		/// </summary>
		/// <exception cref="InvalidOperationException">The sampler needs intensity but this engine does not track it.</exception>
		public void SetProbes(ProbeSampler sampler)
		{
			this._probeValues.Clear();
			this._profileTotals = null;
			if (sampler == null || sampler.Count == 0)
			{
				this._sampler = null;
				return;
			}
			if (sampler.NeedsIntensity && this._intensityOut == null)
			{
				throw new InvalidOperationException("the intensity is not tracked");
			}
			this._sampler = sampler;
			this._profileTotals = new double[sampler.Count][];
			for (int i = 0; i < sampler.Count; i++)
			{
				this._probeValues.Add(new List<float>());
				if (sampler.Integrate[i])
				{
					this._profileTotals[i] = new double[sampler.Pixels[i].Length];
				}
			}
		}

		/// <summary>
		/// Called by the runner after every step: appends one field or intensity value per point probe, and adds
		/// the current intensity to the per-pixel totals of integrated line probes (once <see cref="Time"/> has
		/// reached their start step). Non-integrated line probes are read live by <see cref="ReadProbeProfiles"/>.
		/// </summary>
		public void SampleProbes()
		{
			if (this._sampler == null)
			{
				return;
			}
			int step = (int)Math.Round(this._time);
			for (int i = 0; i < this._sampler.Count; i++)
			{
				if (!this._sampler.IsProfile[i])
				{
					float[] source = this._sampler.UseIntensity[i] ? this._intensityOut : this._u;
					this._probeValues[i].Add(this._sampler.Compute(i, source));
				}
				else if (this._sampler.Integrate[i])
				{
					if (this._sampler.UseStartAfterStep[i] && step < this._sampler.StartAfterStep[i])
					{
						continue;
					}
					int[] pixels = this._sampler.Pixels[i];
					double[] totals = this._profileTotals[i];
					float[] intensityOut = this._intensityOut;
					for (int j = 0; j < pixels.Length; j++)
					{
						totals[j] += intensityOut[pixels[j]];
					}
				}
			}
		}

		/// <summary>Returns and clears the point-probe values collected since the previous call.</summary>
		/// <returns>One array per probe, one value per step (empty for line probes); an empty outer array when no sampler is set.</returns>
		public float[][] TakeProbeSamples()
		{
			if (this._sampler == null)
			{
				return new float[0][];
			}
			float[][] samples = new float[this._sampler.Count][];
			for (int i = 0; i < this._sampler.Count; i++)
			{
				samples[i] = this._probeValues[i].ToArray();
				this._probeValues[i].Clear();
			}
			return samples;
		}

		/// <summary>Reads the current values along every line probe; nothing is cleared.</summary>
		/// <returns>
		/// One array per probe in pixel order along the line, holding the integrated intensity totals or the live
		/// field/intensity values; null entries for point probes; an empty outer array when no sampler is set.
		/// </returns>
		public float[][] ReadProbeProfiles()
		{
			if (this._sampler == null)
			{
				return new float[0][];
			}
			float[][] profiles = new float[this._sampler.Count][];
			for (int i = 0; i < this._sampler.Count; i++)
			{
				if (!this._sampler.IsProfile[i])
				{
					continue;
				}
				int[] pixels = this._sampler.Pixels[i];
				float[] profile = new float[pixels.Length];
				if (this._sampler.Integrate[i])
				{
					double[] totals = this._profileTotals[i];
					for (int j = 0; j < pixels.Length; j++)
					{
						profile[j] = (float)totals[j];
					}
				}
				else
				{
					float[] source = this._sampler.UseIntensity[i] ? this._intensityOut : this._u;
					for (int j = 0; j < pixels.Length; j++)
					{
						profile[j] = source[pixels[j]];
					}
				}
				profiles[i] = profile;
			}
			return profiles;
		}

		/// <summary>Elapsed simulation time in steps (one per <see cref="AdvanceOneStep"/>).</summary>
		public double Time
		{
			get
			{
				return this._time;
			}
		}

		/// <summary>
		/// Number of sub-steps per time step needed to keep the 4th-order stencil stable for the largest c² in the
		/// scene: the smallest n with c²·λmax/n² ≤ <see cref="SubStepTargetLimit"/>, at least 1.
		/// </summary>
		private static int ComputeSubSteps(float maxC2)
		{
			if (maxC2 <= 0f)
			{
				return 1;
			}
			double subSteps = Math.Ceiling(Math.Sqrt((double)maxC2 * FourthOrderLambdaMax / SubStepTargetLimit));
			return Math.Max(1, (int)Math.Round(subSteps));
		}

		/// <summary>
		/// Advances the simulation by one step: blends the sources in once, runs <see cref="_subSteps"/> leapfrog
		/// sub-steps (the intensity is updated on the last one only) and increments <see cref="Time"/>.
		/// </summary>
		public void AdvanceOneStep()
		{
			this.ApplySources();
			for (int i = 1; i <= this._subSteps; i++)
			{
				this.UpdateField(this._dEffective, this._c2Mult, i == this._subSteps);
			}
			this._time += 1.0;
		}

		/// <summary>
		/// Soft-blends the current source values into the field (<c>u = 0.9·u + 0.1·value</c>, see
		/// <see cref="SourceOpacity"/>): image sources driven by their level's frequency, then object sources
		/// with their own phase, amplitude, pulse envelope and detach fade. Object sources that have been
		/// detached for <see cref="SourceElement.DetachRampSteps"/> steps or longer are skipped.
		/// </summary>
		private void ApplySources()
		{
			float[] u = this._u;
			if (this._sourceIndex.Length > 0)
			{
				for (int level = 1; level <= 255; level++)
				{
					if (this._levelUsed[level])
					{
						this._levelValue[level] = (float)(Math.Sin(this._levelFrequency[level] * this._time) * this._sourceAmplitude);
					}
				}
				for (int i = 0; i < this._sourceIndex.Length; i++)
				{
					int idx = this._sourceIndex[i];
					u[idx] = u[idx] * SourceOpacity + this._levelValue[this._sourceLevel[i]] * (1f - SourceOpacity);
				}
			}
			for (int i = 0; i < this._objSources.Length; i++)
			{
				SourceElement source = this._objSources[i];
				float fade = 1f;
				if (source.DetachAfterStep >= 0)
				{
					// Linear fade to zero over 30 steps (SourceElement.DetachRampSteps) after the detach step.
					double sinceDetach = this._time - source.DetachAfterStep;
					if (sinceDetach >= 30.0)
					{
						continue;
					}
					if (sinceDetach > 0.0)
					{
						fade = (float)(1.0 - sinceDetach / 30.0);
					}
				}
				double freq = this._levelFrequency[source.Level];
				double envelope;
				if (source.Mode == SourceMode.Continuous)
				{
					envelope = 1.0;
				}
				else
				{
					int offWidth = 0;
					bool isFirstCycle = false;
					double pulsePos = this.ComputePulsePosition(i, source, freq, ref offWidth, ref isFirstCycle);
					envelope = this.PulseEnvelope(source, pulsePos, offWidth, isFirstCycle);
				}
				float value = (float)(Math.Sin(source.PhaseRad + freq * this._time) * source.Amplitude * envelope) * fade;
				int idx = this._objIndex[i];
				u[idx] = u[idx] * SourceOpacity + value * (1f - SourceOpacity);
			}
		}

		/// <summary>
		/// Position (in cycles of the source frequency) of a pulsed source within its current on/off period. Also
		/// reports the off-width in effect for that period and whether the source is still in its first period.
		/// With a fixed off-width the period is phase-shifted by the off-width so the on-phase starts at step 0.
		/// With a randomized off-width the position is measured from the anchor of the current period (which
		/// begins with its off-phase); whenever a period completes the anchor moves on and a fresh off-width is
		/// drawn from <c>OffWidthBase ± RandomizeOffWidthAmount</c> (at least 1), at most
		/// <see cref="CycleAdvanceGuard"/> periods at a time.
		/// </summary>
		/// <param name="i">Index of the source in <see cref="_objSources"/> (selects its anchor/off-width slot).</param>
		/// <param name="el">The source whose pulse timing is evaluated.</param>
		/// <param name="freq">Angular frequency of the source in radians per step.</param>
		/// <param name="offWidth">Receives the off-width (cycles) in effect for the current period.</param>
		/// <param name="isFirstCycle">Receives whether the source is still in its first period.</param>
		private double ComputePulsePosition(int i, SourceElement el, double freq, ref int offWidth, ref bool isFirstCycle)
		{
			double cycles = freq * this._time / (2.0 * Math.PI);
			if (el.RandomizeOffWidthAmount <= 0)
			{
				offWidth = el.OffWidthCycles;
				int period = el.OnWidthCycles + offWidth;
				if (period <= 0)
				{
					isFirstCycle = true;
					return 0.0;
				}
				double shifted = cycles + offWidth;
				isFirstCycle = Math.Floor(shifted / period) == 0.0;
				return shifted - period * Math.Floor(shifted / period);
			}
			else
			{
				offWidth = this._objCurrentOffWidth[i];
				int period = el.OnWidthCycles + offWidth;
				if (period <= 0)
				{
					isFirstCycle = true;
					return 0.0;
				}
				double sinceAnchor = cycles - this._objCycleAnchor[i];
				int guard = 0;
				while (sinceAnchor >= period && guard < CycleAdvanceGuard)
				{
					this._objCycleAnchor[i] += period;
					sinceAnchor -= period;
					offWidth = Math.Max(1, el.OffWidthBase + SourceSettings.RandomInt(this._objRandom, el.RandomizeOffWidthAmount));
					this._objCurrentOffWidth[i] = offWidth;
					period = el.OnWidthCycles + offWidth;
					guard++;
				}
				if (sinceAnchor < 0.0)
				{
					sinceAnchor = 0.0;
				}
				isFirstCycle = this._objCycleAnchor[i] == 0.0;
				return sinceAnchor;
			}
		}

		/// <summary>
		/// Amplitude envelope (0..1) of a pulsed source at the given position within its on/off period (a
		/// non-positive period yields 1). Gaussian: zero during the off-phase, then a bell centred in the on-window
		/// with σ = on-width/6. Smoothed square: the product of an arctan rise at the start and an arctan fall at
		/// the end of the on-window (softness 0.35 cycles); positions are wrapped so the tails of neighbouring
		/// periods blend smoothly, except in the first period so the pulse does not start mid-tail.
		/// </summary>
		/// <param name="el">The source; supplies the pulse shape and on-width.</param>
		/// <param name="pos">Position within the period in cycles, as returned by <see cref="ComputePulsePosition"/>.</param>
		/// <param name="offWidth">Off-width (cycles) in effect for this period.</param>
		/// <param name="isFirstCycle">Whether this is the first period (suppresses the tail wrapping of the smoothed square).</param>
		private double PulseEnvelope(SourceElement el, double pos, int offWidth, bool isFirstCycle)
		{
			int onWidthCycles = el.OnWidthCycles;
			int period = onWidthCycles + offWidth;
			if (period <= 0)
			{
				return 1.0;
			}
			if (el.PulseShape == SourcePulseShape.Gaussian)
			{
				if (pos < offWidth)
				{
					return 0.0;
				}
				double onPos = pos - offWidth;
				double center = onWidthCycles / 2.0;
				double sigma = Math.Max(onWidthCycles / 6.0, 1E-06);
				double z = (onPos - center) / sigma;
				return Math.Exp(-0.5 * z * z);
			}
			else
			{
				double onPos = pos - offWidth;
				if (onPos < 0.0 && !isFirstCycle)
				{
					onPos += period;
				}
				double fallArg = onPos - onWidthCycles;
				double riseArg = onPos;
				if (riseArg > period / 2.0)
				{
					riseArg -= period;
				}
				double rise = 0.5 + Math.Atan(riseArg / 0.35) / Math.PI;
				double fall = 0.5 - Math.Atan(fallArg / 0.35) / Math.PI;
				return rise * fall;
			}
		}

		/// <summary>
		/// One leapfrog sub-step over all interior cells, parallelised over row bands:
		/// <c>next = u + (u - uPrev)·d + lap·c2·c2Mult</c>, where d is the per-cell damping (1 = lossless, applied
		/// to the velocity term) and lap is the Laplacian: the 9-point stencil (corners <see cref="KernelCorner"/>,
		/// edges <see cref="KernelEdge"/>, centre -1), or for cells at least two cells away from the border and
		/// when enabled, the 4th-order 5-point-per-axis stencil (<see cref="FourthOrderFar"/>,
		/// <see cref="FourthOrderNear"/>, <see cref="FourthOrderCenter"/>). Each cell reads only <see cref="_u"/>
		/// and overwrites its own slot in <see cref="_uPrev"/>, so the bands do not race; afterwards the two
		/// buffers are swapped. Border cells are never written and stay 0.
		/// When tracking, the intensity is a cascade of two exponential moving averages of u² (retention
		/// <see cref="_intensityAlpha"/>); the output mixes both stages (<see cref="IntensityBlend"/>) times
		/// <see cref="_intensityScale"/>.
		/// </summary>
		/// <param name="dArray">Damping map to use, already corrected for the sub-step length.</param>
		/// <param name="c2Mult">Factor on c² for the sub-step length (1/subSteps²).</param>
		/// <param name="updateIntensity">Whether to update the intensity buffers in this sub-step (last sub-step only).</param>
		private void UpdateField(float[] dArray, float c2Mult, bool updateIntensity)
		{
			float[] u = this._u;
			float[] uPrev = this._uPrev;
			float[] c2 = this._c2;
			float[] d = dArray;
			float[] intensity = this._intensity;
			float[] stage1 = this._intensityStage1;
			float[] intensityOut = this._intensityOut;
			bool track = intensity != null && updateIntensity;
			int stride = this._stride;
			int w = this._width;
			int h = this._height;
			bool use4th = this._use4thOrderLaplacian;
			float mult = c2Mult;
			float alpha = this._intensityAlpha;
			float scale = this._intensityScale;
			int bands = Math.Min(h, Environment.ProcessorCount * 4);

			Parallel.For(0, bands, band =>
			{
				int rowStart = (int)((long)h * band / bands);
				int rowEnd = (int)((long)h * (band + 1) / bands) - 1;
				for (int y = rowStart; y <= rowEnd; y++)
				{
					int idx = (y + 1) * stride + 1;
					// The 4th-order stencil reaches two cells out, but the padding is only one cell wide.
					bool rowAllows4th = use4th && y >= 1 && y <= h - 2;
					for (int x = 0; x < w; x++)
					{
						float center = u[idx];
						float laplacian;
						if (rowAllows4th && x >= 1 && x <= w - 2)
						{
							laplacian = FourthOrderFar * (u[idx - 2] + u[idx + 2] + u[idx - 2 * stride] + u[idx + 2 * stride])
								+ FourthOrderNear * (u[idx - 1] + u[idx + 1] + u[idx - stride] + u[idx + stride])
								+ FourthOrderCenter * center;
						}
						else
						{
							laplacian = KernelCorner * (u[idx - stride - 1] + u[idx - stride + 1] + u[idx + stride - 1] + u[idx + stride + 1])
								+ KernelEdge * (u[idx - 1] + u[idx + 1] + u[idx - stride] + u[idx + stride])
								- center;
						}
						float next = center + (center - uPrev[idx]) * d[idx] + laplacian * c2[idx] * mult;
						uPrev[idx] = next;
						if (track)
						{
							float s1 = stage1[idx] * alpha + next * next * (1f - alpha);
							stage1[idx] = s1;
							float s2 = intensity[idx] * alpha + s1 * (1f - alpha);
							intensity[idx] = s2;
							intensityOut[idx] = (s1 * (1f - IntensityBlend) + s2 * IntensityBlend) * scale;
						}
						idx++;
					}
				}
			});

			// uPrev now holds the new field; swap buffers.
			this._uPrev = u;
			this._u = uPrev;
		}

		/// <summary>Weight of the four diagonal neighbours in the 9-point Laplacian.</summary>
		private const float KernelCorner = 0.066f;

		/// <summary>Weight of the four edge neighbours in the 9-point Laplacian (4·corner + 4·edge = 1 balances the centre weight of -1).</summary>
		private const float KernelEdge = 0.184f;

		/// <summary>Fraction of the old field value kept when a source value is blended in (soft source injection).</summary>
		private const float SourceOpacity = 0.9f;

		/// <summary>Source level assumed for the intensity decay time when no source has a level above 0.</summary>
		private const int NominalDecayLevel = 100;

		/// <summary>Mix of the two intensity averaging stages in the output: 0 = first stage only, 1 = second stage only.</summary>
		private const float IntensityBlend = 0.5f;

		/// <summary>4th-order stencil weight of the nearest neighbours (4/3).</summary>
		private const float FourthOrderNear = 1.3333334f;

		/// <summary>4th-order stencil weight of the second-nearest neighbours (-1/12).</summary>
		private const float FourthOrderFar = -0.083333336f;

		/// <summary>4th-order stencil centre weight (2 × -5/2 for the two axes).</summary>
		private const float FourthOrderCenter = -5f;

		/// <summary>Largest eigenvalue magnitude of the 2D 4th-order stencil (2 × 16/3), for the stability estimate in <see cref="ComputeSubSteps"/>.</summary>
		private const float FourthOrderLambdaMax = 10.666667f;

		/// <summary>Allowed c²·λmax·dt² per sub-step; 2 keeps a margin below the leapfrog stability bound of 4.</summary>
		private const float SubStepTargetLimit = 2f;

		/// <summary>Scene width in cells.</summary>
		private readonly int _width;

		/// <summary>Scene height in cells.</summary>
		private readonly int _height;

		/// <summary>Padded row pitch (<see cref="_width"/> + 2).</summary>
		private readonly int _stride;

		/// <summary>Whether interior cells use the 4th-order stencil.</summary>
		private readonly bool _use4thOrderLaplacian;

		/// <summary>Leapfrog sub-steps per simulation step; 1 unless the 4th-order stencil is on.</summary>
		private readonly int _subSteps;

		/// <summary>Damping map applied per sub-step: <see cref="_d"/> raised to 1/<see cref="_subSteps"/>, or <see cref="_d"/> itself without sub-stepping.</summary>
		private readonly float[] _dEffective;

		/// <summary>1/<see cref="_subSteps"/>², scaling c² for the shorter sub-step.</summary>
		private readonly float _c2Mult;

		/// <summary>Per-step retention of the intensity averages: exp(-1/decaySteps) with decaySteps = timeDecayFactor·100/lowest source level.</summary>
		private readonly float _intensityAlpha;

		/// <summary>Multiplier on the intensity output.</summary>
		private readonly float _intensityScale;

		/// <summary>Current field (padded); exchanged with <see cref="_uPrev"/> after every sub-step.</summary>
		private float[] _u;

		/// <summary>Previous field (padded); receives the new field during a sub-step and then becomes <see cref="_u"/>.</summary>
		private float[] _uPrev;

		/// <summary>Per-cell c² = 1/n² (padded; border cells 0).</summary>
		private readonly float[] _c2;

		/// <summary>Per-cell damping from the scene (padded), 1 = lossless; source of <see cref="_dEffective"/>.</summary>
		private readonly float[] _d;

		/// <summary>Second-stage running average of u² (padded), or null when intensity is not tracked.</summary>
		private readonly float[] _intensity;

		/// <summary>First-stage running average of u² (padded), or null when intensity is not tracked.</summary>
		private readonly float[] _intensityStage1;

		/// <summary>Blended and scaled intensity exposed as <see cref="IntensityData"/>, or null when not tracked.</summary>
		private readonly float[] _intensityOut;

		/// <summary>Padded cell index of every image source.</summary>
		private readonly int[] _sourceIndex;

		/// <summary>Green level (1..255) of every image source, indexing <see cref="_levelFrequency"/>.</summary>
		private readonly byte[] _sourceLevel;

		/// <summary>Angular frequency in radians per step for each level 0..255: level/255 × 0.5 × sourceFrequencyScale.</summary>
		private readonly double[] _levelFrequency;

		/// <summary>Which levels have at least one image source; only those get a value computed per step.</summary>
		private readonly bool[] _levelUsed;

		/// <summary>Image source value for the current step, per level.</summary>
		private readonly float[] _levelValue;

		/// <summary>Peak amplitude of the image sources.</summary>
		private readonly double _sourceAmplitude;

		/// <summary>Object sources that lie inside the canvas.</summary>
		private readonly SourceElement[] _objSources;

		/// <summary>Padded cell index of every entry in <see cref="_objSources"/>.</summary>
		private readonly int[] _objIndex;

		/// <summary>Random generator for randomized off-widths; used on the worker thread only.</summary>
		private readonly Random _objRandom;

		/// <summary>Per object source: start (in cycles) of its current period; used in randomized off-width mode only.</summary>
		private readonly double[] _objCycleAnchor;

		/// <summary>Per object source: off-width (cycles) in effect for its current period.</summary>
		private readonly int[] _objCurrentOffWidth;

		/// <summary>Upper bound on the periods <see cref="ComputePulsePosition"/> advances at once (guards against a tiny period).</summary>
		private const int CycleAdvanceGuard = 1000;

		/// <summary>Elapsed time in steps.</summary>
		private double _time;

		/// <summary>Probe layout in use, or null when no probes are set.</summary>
		private ProbeSampler _sampler;

		/// <summary>Point-probe samples gathered since the last <see cref="TakeProbeSamples"/>, one list per probe.</summary>
		private readonly List<List<float>> _probeValues;

		/// <summary>Running per-pixel intensity sums for integrated line probes (null entries for other probes); null without a sampler.</summary>
		private double[][] _profileTotals;
	}
}
