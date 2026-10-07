using System;
using System.Collections.Generic;
using System.Text;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// OpenCL implementation of <see cref="IWaveEngine"/>: the same leapfrog scheme as <see cref="WaveSimulator"/>
	/// (padded grid, 9-point or sub-stepped 4th-order Laplacian, soft source blending, two-stage intensity
	/// average), with the field, the scene maps, the source tables and the probe buffers all resident on the
	/// device. Created through <see cref="TryCreate"/> by <see cref="WaveEngineFactory"/> and driven by
	/// <see cref="SimulationRunner"/> on its worker thread, which is the only thread that enqueues work; the UI
	/// thread touches only <see cref="PrepareFrame"/>, <see cref="FieldData"/> and <see cref="IntensityData"/>
	/// while the runner is paused.
	/// </summary>
	/// <remarks>
	/// The field lives in two ping-pong buffers (<see cref="_field"/>); every kernel that reads the field exists
	/// twice (index 0/1) with the buffers bound in opposite order, so a step merely picks the kernel for
	/// <see cref="_current"/> instead of re-binding arguments. Kernel launches and uploads are asynchronous;
	/// read-backs are blocking. All OpenCL handles are owned by this instance and released by
	/// <see cref="Dispose"/> (or by the constructor when building fails); the managed buffers
	/// (<see cref="FieldData"/>, <see cref="IntensityData"/>) are plain host copies.
	/// </remarks>
	internal sealed class GpuWaveSimulator : IWaveEngine
	{
		/// <summary>
		/// Tries to build an engine on each OpenCL GPU reported by <see cref="GpuDeviceInfo.FindAll"/> in turn and
		/// returns the first that succeeds. Parameters are those of the <see cref="WaveSimulator"/> constructor.
		/// </summary>
		/// <param name="scene">Per-cell c² and damping maps plus the image (green-channel) sources.</param>
		/// <param name="sourceFrequencyScale">Multiplier in the level-to-angular-frequency mapping; see <see cref="_frequencyBuffer"/>.</param>
		/// <param name="sourceAmplitude">Peak amplitude of the image sources (object sources carry their own).</param>
		/// <param name="trackIntensity">Whether to allocate and update the intensity buffers; when false <see cref="IntensityData"/> stays null.</param>
		/// <param name="use4thOrderLaplacian">Use the 4th-order 5-point-per-axis stencil (with sub-stepping) instead of the 9-point one.</param>
		/// <param name="timeDecayFactor">Intensity averaging time in units of 100/level steps; see <see cref="_intensityAlpha"/>.</param>
		/// <param name="intensityScale">Multiplier applied to the intensity output.</param>
		/// <param name="problems">Receives "" on success, otherwise why no device worked ("no GPU found …" or the
		/// per-device failure messages joined by "; ").</param>
		/// <param name="objectSources">Interactive sources; those outside the canvas are dropped.</param>
		/// <returns>The engine, or null when no device could be used (see <paramref name="problems"/>).</returns>
		public static GpuWaveSimulator TryCreate(SceneData scene, double sourceFrequencyScale, double sourceAmplitude, bool trackIntensity, bool use4thOrderLaplacian, double timeDecayFactor, double intensityScale, ref string problems, List<SourceElement> objectSources = null)
		{
			problems = "";
			List<GpuDeviceInfo> devices = GpuDeviceInfo.FindAll();
			if (devices.Count == 0)
			{
				problems = "no GPU found through OpenCL" + ((IntPtr.Size == 4) ? " (this is a 32-bit process; run the 64-bit build)" : "");
				return null;
			}
			StringBuilder errors = new StringBuilder();
			foreach (GpuDeviceInfo device in devices)
			{
				try
				{
					return new GpuWaveSimulator(device, scene, sourceFrequencyScale, sourceAmplitude, trackIntensity, use4thOrderLaplacian, timeDecayFactor, intensityScale, objectSources);
				}
				catch (Exception ex)
				{
					if (errors.Length > 0)
					{
						errors.Append("; ");
					}
					errors.Append(device.Name).Append(": ").Append(ex.Message);
				}
			}
			problems = errors.ToString();
			return null;
		}

		/// <summary>
		/// Records the scene geometry, rejects scenes whose per-cell buffer would exceed the device's maximum
		/// allocation, keeps only the object sources inside the canvas and then runs <see cref="Build"/>; if that
		/// throws, everything allocated so far is released before the exception propagates.
		/// </summary>
		/// <exception cref="InvalidOperationException">The scene is too large for the device, or the OpenCL program could not be built.</exception>
		private GpuWaveSimulator(GpuDeviceInfo device, SceneData scene, double sourceFrequencyScale, double sourceAmplitude, bool trackIntensity, bool use4thOrderLaplacian, double timeDecayFactor, double intensityScale, List<SourceElement> objectSources = null)
		{
			this._field = new IntPtr[2];
			this._stepKernel = new IntPtr[2];
			this._sourceKernel = new IntPtr[2];
			this._objSourceKernel = new IntPtr[2];
			this._objGrid1D = new IntPtr[1];
			this._grid2D = new IntPtr[2];
			this._grid1D = new IntPtr[1];
			this._probeKernel = new IntPtr[2];
			this._probeGrid = new IntPtr[1];
			this._probeValues = new List<List<float>>();
			this._gatherKernel = new IntPtr[2];
			this._profileGrid = new IntPtr[1];
			this._description = "GPU: " + device.Name + " (OpenCL)";
			this._width = scene.Width;
			this._height = scene.Height;
			this._stride = this._width + 2;
			this._cells = this._stride * (this._height + 2);
			this._track = trackIntensity;
			this._sourceCount = scene.SourceCount;
			this._amplitude = (float)sourceAmplitude;
			this._use4thOrderLaplacian = use4thOrderLaplacian;
			if (device.MaxAllocation > 0L && (long)this._cells * 4L > device.MaxAllocation)
			{
				throw new InvalidOperationException("the scene is too large for this GPU");
			}
			List<SourceElement> sourcesInScene = new List<SourceElement>();
			if (objectSources != null)
			{
				foreach (SourceElement source in objectSources)
				{
					if (source.PosX >= 0 && source.PosX < this._width && source.PosY >= 0 && source.PosY < this._height)
					{
						sourcesInScene.Add(source);
					}
				}
			}
			this._objSourceCount = sourcesInScene.Count;
			try
			{
				this.Build(device, scene, sourceFrequencyScale, timeDecayFactor, intensityScale, sourcesInScene);
			}
			catch
			{
				this.ReleaseAll();
				throw;
			}
		}

		/// <summary>
		/// Creates the context and queue, compiles <see cref="KernelSource"/>, uploads every buffer for the scene
		/// and binds the kernel arguments. Mirrors the <see cref="WaveSimulator"/> constructor: c² and damping are
		/// copied into the padded layout, the sub-step count and the per-sub-step damping / c² corrections are
		/// derived for the 4th-order stencil, the sources are indexed, and the intensity averaging constant is set
		/// from the lowest source level in use. When intensity is not tracked the three intensity buffers are
		/// 1-float dummies so the kernel arguments are still valid. The kernel arguments that change per step
		/// (time, per-sub-step track flag) are left to <see cref="AdvanceOneStep"/>.
		/// </summary>
		/// <param name="device">The OpenCL device (and its platform) to create the context on.</param>
		/// <param name="scene">Per-cell c² and damping maps plus the image sources.</param>
		/// <param name="sourceFrequencyScale">Multiplier in the level-to-angular-frequency mapping; see <see cref="_frequencyBuffer"/>.</param>
		/// <param name="timeDecayFactor">Intensity averaging time in units of 100/level steps; see <see cref="_intensityAlpha"/>.</param>
		/// <param name="intensityScale">Multiplier applied to the intensity output.</param>
		/// <param name="objectSources">Object sources already filtered to those inside the canvas.</param>
		/// <exception cref="InvalidOperationException">The OpenCL program could not be built (message includes the build log).</exception>
		private void Build(GpuDeviceInfo device, SceneData scene, double sourceFrequencyScale, double timeDecayFactor, double intensityScale, List<SourceElement> objectSources)
		{
			int err = 0;
			this._deviceId = device.DeviceId;
			IntPtr[] contextProperties = new IntPtr[]
			{
				new IntPtr(OpenClApi.ContextPlatformProperty),
				device.PlatformId,
				IntPtr.Zero
			};
			IntPtr[] deviceIds = new IntPtr[] { device.DeviceId };
			this._context = OpenClApi.clCreateContext(contextProperties, 1U, deviceIds, IntPtr.Zero, IntPtr.Zero, ref err);
			OpenClApi.Check(err, "clCreateContext");
			this._queue = OpenClApi.clCreateCommandQueue(this._context, device.DeviceId, 0UL, ref err);
			OpenClApi.Check(err, "clCreateCommandQueue");
			string[] sources = new string[] { GpuWaveSimulator.KernelSource(this._use4thOrderLaplacian) };
			this._program = OpenClApi.clCreateProgramWithSource(this._context, 1U, sources, null, ref err);
			OpenClApi.Check(err, "clCreateProgramWithSource");
			string buildOptions = "";
			err = OpenClApi.clBuildProgram(this._program, 1U, deviceIds, ref buildOptions, IntPtr.Zero, IntPtr.Zero);
			if (err != 0)
			{
				string buildLog = OpenClApi.BuildLog(this._program, device.DeviceId);
				throw new InvalidOperationException("the program for the GPU could not be built (" + OpenClApi.ErrorName(err) + ")" + ((buildLog.Length > 0) ? (": " + buildLog) : ""));
			}
			float[] c2 = new float[this._cells];
			float[] damping = new float[this._cells];
			for (int y = 0; y < this._height; y++)
			{
				Array.Copy(scene.C2, y * this._width, c2, (y + 1) * this._stride + 1, this._width);
				Array.Copy(scene.D, y * this._width, damping, (y + 1) * this._stride + 1, this._width);
			}
			float maxC2 = 0f;
			if (this._use4thOrderLaplacian)
			{
				for (int i = 0; i < c2.Length; i++)
				{
					if (c2[i] > maxC2)
					{
						maxC2 = c2[i];
					}
				}
			}
			this._subSteps = (this._use4thOrderLaplacian ? GpuWaveSimulator.ComputeSubSteps(maxC2) : 1);
			float[] dampingPerSubStep = damping;
			if (this._subSteps > 1)
			{
				// Damping is applied once per sub-step, so take the sub-step root to keep the per-step decay unchanged.
				float[] adjusted = new float[damping.Length];
				double exponent = 1.0 / this._subSteps;
				for (int i = 0; i < damping.Length; i++)
				{
					adjusted[i] = (float)Math.Pow(damping[i], exponent);
				}
				dampingPerSubStep = adjusted;
				// A sub-step is 1/subSteps long, so c²·dt² shrinks by 1/subSteps².
				this._c2Mult = 1f / (float)(this._subSteps * this._subSteps);
			}
			else
			{
				this._c2Mult = 1f;
			}
			this._c2Buffer = this.CreateFloatBuffer(c2);
			this._dBuffer = this.CreateFloatBuffer(dampingPerSubStep);
			float[] zeros = new float[this._cells];
			this._field[0] = this.CreateFloatBuffer(zeros);
			this._field[1] = this.CreateFloatBuffer(zeros);
			if (this._track)
			{
				this._intensityBuffer = this.CreateFloatBuffer(zeros);
				this._intensityStage1Buffer = this.CreateFloatBuffer(zeros);
				this._intensityOutBuffer = this.CreateFloatBuffer(zeros);
			}
			else
			{
				// Not tracked: the kernels still need valid buffer arguments, and they never touch them.
				this._intensityBuffer = this.CreateFloatBuffer(new float[1]);
				this._intensityStage1Buffer = this.CreateFloatBuffer(new float[1]);
				this._intensityOutBuffer = this.CreateFloatBuffer(new float[1]);
			}
			if (this._sourceCount > 0 || this._objSourceCount > 0)
			{
				float[] frequencies = new float[256];
				for (int level = 0; level <= 255; level++)
				{
					frequencies[level] = (float)(level / 255.0 * 0.5 * sourceFrequencyScale);
				}
				this._frequencyBuffer = this.CreateFloatBuffer(frequencies);
			}
			// The intensity decay time is scaled by the lowest (slowest) source level in use; 100 when none has a level.
			int minLevel = 0;
			for (int i = 0; i < this._sourceCount; i++)
			{
				int level = scene.SourceLevel[i];
				if (level > 0 && (minLevel == 0 || level < minLevel))
				{
					minLevel = level;
				}
			}
			foreach (SourceElement source in objectSources)
			{
				if (source.Level > 0 && (minLevel == 0 || source.Level < minLevel))
				{
					minLevel = source.Level;
				}
			}
			if (minLevel == 0)
			{
				minLevel = 100;
			}
			double decaySteps = Math.Max(1.0, timeDecayFactor * 100.0 / (double)minLevel);
			this._intensityAlpha = (float)Math.Exp(-1.0 / decaySteps);
			this._intensityScale = (float)intensityScale;
			if (this._sourceCount > 0)
			{
				int[] indices = new int[this._sourceCount];
				int[] levels = new int[this._sourceCount];
				for (int i = 0; i < this._sourceCount; i++)
				{
					indices[i] = (scene.SourceY[i] + 1) * this._stride + scene.SourceX[i] + 1;
					levels[i] = scene.SourceLevel[i];
				}
				this._indexBuffer = this.CreateIntBuffer(indices);
				this._levelBuffer = this.CreateIntBuffer(levels);
			}
			if (this._objSourceCount > 0)
			{
				int[] objIndices = new int[this._objSourceCount];
				int[] objLevels = new int[this._objSourceCount];
				float[] objAmplitudes = new float[this._objSourceCount];
				float[] objPhases = new float[this._objSourceCount];
				int[] objModes = new int[this._objSourceCount];
				int[] objPulseShapes = new int[this._objSourceCount];
				int[] objOnWidths = new int[this._objSourceCount];
				int[] objOffWidths = new int[this._objSourceCount];
				int[] objDetachSteps = new int[this._objSourceCount];
				int[] objOffWidthBases = new int[this._objSourceCount];
				int[] objRandomizeOffWidthAmounts = new int[this._objSourceCount];
				float[] objCycleAnchors = new float[this._objSourceCount];
				int[] objRngStates = new int[this._objSourceCount];
				Random random = new Random();
				for (int i = 0; i < this._objSourceCount; i++)
				{
					SourceElement source = objectSources[i];
					objIndices[i] = (source.PosY + 1) * this._stride + source.PosX + 1;
					objLevels[i] = source.Level;
					objAmplitudes[i] = (float)source.Amplitude;
					objPhases[i] = (float)source.PhaseRad;
					objModes[i] = ((source.Mode == SourceMode.Pulsed) ? 1 : 0);
					objPulseShapes[i] = ((source.PulseShape == SourcePulseShape.SmoothedSquare) ? 1 : 0);
					objOnWidths[i] = source.OnWidthCycles;
					objOffWidths[i] = source.OffWidthCycles;
					objDetachSteps[i] = source.DetachAfterStep;
					objOffWidthBases[i] = source.OffWidthBase;
					objRandomizeOffWidthAmounts[i] = source.RandomizeOffWidthAmount;
					objCycleAnchors[i] = 0f;
					// xorshift32 never leaves state 0, so a zero seed would make the off-width constant.
					int seed = random.Next(int.MinValue, int.MaxValue);
					if (seed == 0)
					{
						seed = 1;
					}
					objRngStates[i] = seed;
				}
				this._objIndexBuffer = this.CreateIntBuffer(objIndices);
				this._objLevelBuffer = this.CreateIntBuffer(objLevels);
				this._objAmplitudeBuffer = this.CreateFloatBuffer(objAmplitudes);
				this._objPhaseBuffer = this.CreateFloatBuffer(objPhases);
				this._objModeBuffer = this.CreateIntBuffer(objModes);
				this._objPulseShapeBuffer = this.CreateIntBuffer(objPulseShapes);
				this._objOnWidthBuffer = this.CreateIntBuffer(objOnWidths);
				this._objOffWidthBuffer = this.CreateIntBuffer(objOffWidths);
				this._objDetachStepBuffer = this.CreateIntBuffer(objDetachSteps);
				this._objOffWidthBaseBuffer = this.CreateIntBuffer(objOffWidthBases);
				this._objRandomizeOffWidthAmountBuffer = this.CreateIntBuffer(objRandomizeOffWidthAmounts);
				this._objCycleAnchorBuffer = this.CreateFloatBuffer(objCycleAnchors);
				this._objRngStateBuffer = this.CreateIntBuffer(objRngStates);
			}
			// Kernel k reads field[k] and writes field[1 - k]; argument 4 (sources) / 11 (object sources) is the
			// time and argument 8 of wave_step the track flag, both set per launch in AdvanceOneStep.
			for (int k = 0; k <= 1; k++)
			{
				this._stepKernel[k] = this.CreateKernel("wave_step");
				GpuWaveSimulator.SetMem(this._stepKernel[k], 0U, this._field[k]);
				GpuWaveSimulator.SetMem(this._stepKernel[k], 1U, this._field[1 - k]);
				GpuWaveSimulator.SetMem(this._stepKernel[k], 2U, this._c2Buffer);
				GpuWaveSimulator.SetMem(this._stepKernel[k], 3U, this._dBuffer);
				GpuWaveSimulator.SetMem(this._stepKernel[k], 4U, this._intensityBuffer);
				GpuWaveSimulator.SetInt(this._stepKernel[k], 5U, this._width);
				GpuWaveSimulator.SetInt(this._stepKernel[k], 6U, this._height);
				GpuWaveSimulator.SetInt(this._stepKernel[k], 7U, this._stride);
				GpuWaveSimulator.SetInt(this._stepKernel[k], 8U, this._track ? 1 : 0);
				GpuWaveSimulator.SetFloat(this._stepKernel[k], 9U, this._c2Mult);
				GpuWaveSimulator.SetFloat(this._stepKernel[k], 10U, this._intensityAlpha);
				GpuWaveSimulator.SetMem(this._stepKernel[k], 11U, this._intensityStage1Buffer);
				GpuWaveSimulator.SetMem(this._stepKernel[k], 12U, this._intensityOutBuffer);
				GpuWaveSimulator.SetFloat(this._stepKernel[k], 13U, this._intensityScale);
				if (this._sourceCount > 0)
				{
					this._sourceKernel[k] = this.CreateKernel("apply_sources");
					GpuWaveSimulator.SetMem(this._sourceKernel[k], 0U, this._field[k]);
					GpuWaveSimulator.SetMem(this._sourceKernel[k], 1U, this._indexBuffer);
					GpuWaveSimulator.SetMem(this._sourceKernel[k], 2U, this._levelBuffer);
					GpuWaveSimulator.SetMem(this._sourceKernel[k], 3U, this._frequencyBuffer);
					GpuWaveSimulator.SetFloat(this._sourceKernel[k], 5U, this._amplitude);
					GpuWaveSimulator.SetInt(this._sourceKernel[k], 6U, this._sourceCount);
				}
				if (this._objSourceCount > 0)
				{
					this._objSourceKernel[k] = this.CreateKernel("apply_object_sources");
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 0U, this._field[k]);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 1U, this._objIndexBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 2U, this._objLevelBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 3U, this._objAmplitudeBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 4U, this._objPhaseBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 5U, this._objModeBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 6U, this._objPulseShapeBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 7U, this._objOnWidthBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 8U, this._objOffWidthBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 9U, this._objDetachStepBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 10U, this._frequencyBuffer);
					GpuWaveSimulator.SetInt(this._objSourceKernel[k], 12U, this._objSourceCount);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 13U, this._objOffWidthBaseBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 14U, this._objRandomizeOffWidthAmountBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 15U, this._objCycleAnchorBuffer);
					GpuWaveSimulator.SetMem(this._objSourceKernel[k], 16U, this._objRngStateBuffer);
				}
			}
			this._grid2D[0] = new IntPtr(this._width);
			this._grid2D[1] = new IntPtr(this._height);
			this._grid1D[0] = new IntPtr(Math.Max(1, this._sourceCount));
			this._objGrid1D[0] = new IntPtr(Math.Max(1, this._objSourceCount));
			this._hostField = new float[this._cells];
			if (this._track)
			{
				this._hostIntensity = new float[this._cells];
			}
			this._current = 0;
		}

		/// <summary>
		/// Number of sub-steps per step needed to keep the 4th-order stencil stable for the largest c² in the
		/// scene: the smallest n with c²·λmax/n² ≤ 2, at least 1. Same formula as
		/// <see cref="WaveSimulator"/> (λmax = 32/3 for the 2D 4th-order stencil, 2 leaves a margin below the
		/// leapfrog bound of 4).
		/// </summary>
		private static int ComputeSubSteps(float maxC2)
		{
			if (maxC2 <= 0f)
			{
				return 1;
			}
			// 10.666666984558105 is (double)10.666667f, i.e. the CPU engine's FourthOrderLambdaMax.
			double steps = Math.Ceiling(Math.Sqrt(maxC2 * 10.666666984558105 / 2.0));
			return Math.Max(1, (int)Math.Round(steps));
		}

		/// <summary>
		/// OpenCL source of the simulation program: <c>wave_step</c>, <c>apply_sources</c> and
		/// <c>apply_object_sources</c>, each a GPU transcription of the corresponding <see cref="WaveSimulator"/>
		/// routine. The stencil is chosen at compile time through the <c>USE_4TH_ORDER</c> define.
		/// </summary>
		private static string KernelSource(bool use4thOrderLaplacian)
		{
			StringBuilder source = new StringBuilder();
			source.AppendLine("#define USE_4TH_ORDER " + (use4thOrderLaplacian ? "1" : "0"));
			source.AppendLine();
			// wave_step: one leapfrog sub-step, one work item per interior cell (x, y in 0..w-1 / 0..h-1).
			//   u      current field, padded, stride floats per row; the cell is i = (y + 1) * stride + x + 1
			//   up     previous field on entry, overwritten in place with the new value (leapfrog), same layout
			//   c2, d  per-cell c² and per-sub-step damping, same layout
			//   lap    4th-order 5-point-per-axis stencil for cells at least two away from the border (needs
			//          USE_4TH_ORDER), otherwise the 9-point stencil (corners 0.066, edges 0.184, centre -1)
			//   nv     = u + (u - up)·d + lap·c2·c2Scale, with c2Scale = 1/subSteps²
			//   inten1 / inten  first and second stage exponential averages of nv², retention intensityAlpha
			//   intenOut        (stage1 + stage2)/2 · intensityScale; the three are only touched when track != 0
			source.AppendLine("__kernel void wave_step(__global const float* u, __global float* up, __global const float* c2,");
			source.AppendLine("                        __global const float* d, __global float* inten,");
			source.AppendLine("                        const int w, const int h, const int stride, const int track, const float c2Scale,");
			source.AppendLine("                        const float intensityAlpha, __global float* inten1, __global float* intenOut,");
			source.AppendLine("                        const float intensityScale)");
			source.AppendLine("{");
			source.AppendLine("    int x = get_global_id(0);");
			source.AppendLine("    int y = get_global_id(1);");
			source.AppendLine("    if (x >= w || y >= h) return;");
			source.AppendLine("    int i = (y + 1) * stride + x + 1;");
			source.AppendLine("    float uc = u[i];");
			source.AppendLine("    float lap;");
			source.AppendLine("#if USE_4TH_ORDER");
			source.AppendLine("    if (x >= 1 && x <= w - 2 && y >= 1 && y <= h - 2) {");
			source.AppendLine("        lap = (-1.0f / 12.0f) * (u[i - 2] + u[i + 2] + u[i - 2 * stride] + u[i + 2 * stride])");
			source.AppendLine("            + (4.0f / 3.0f) * (u[i - 1] + u[i + 1] + u[i - stride] + u[i + stride])");
			source.AppendLine("            - 5.0f * uc;");
			source.AppendLine("    } else {");
			source.AppendLine("        lap = 0.066f * (u[i - stride - 1] + u[i - stride + 1] + u[i + stride - 1] + u[i + stride + 1])");
			source.AppendLine("            + 0.184f * (u[i - 1] + u[i + 1] + u[i - stride] + u[i + stride]) - uc;");
			source.AppendLine("    }");
			source.AppendLine("#else");
			source.AppendLine("    lap = 0.066f * (u[i - stride - 1] + u[i - stride + 1] + u[i + stride - 1] + u[i + stride + 1])");
			source.AppendLine("        + 0.184f * (u[i - 1] + u[i + 1] + u[i - stride] + u[i + stride]) - uc;");
			source.AppendLine("#endif");
			source.AppendLine("    float nv = uc + (uc - up[i]) * d[i] + lap * c2[i] * c2Scale;");
			source.AppendLine("    up[i] = nv;");
			source.AppendLine("    if (track != 0) {");
			source.AppendLine("        float s1 = inten1[i] * intensityAlpha + nv * nv * (1.0f - intensityAlpha);");
			source.AppendLine("        inten1[i] = s1;");
			source.AppendLine("        float s2 = inten[i] * intensityAlpha + s1 * (1.0f - intensityAlpha);");
			source.AppendLine("        inten[i] = s2;");
			source.AppendLine("        intenOut[i] = (s1 * 0.5f + s2 * 0.5f) * intensityScale;");
			source.AppendLine("    }");
			source.AppendLine("}");
			source.AppendLine();
			// apply_sources: soft-blends the image sources into the field, one work item per source k < n.
			//   idx[k]   padded cell index of the source; level[k] its green level (index into freq)
			//   freq     256 angular frequencies (radians per step); t the current step
			//   u[idx]   = 0.9·u + 0.1·sin(freq[level]·t)·amp   (same 0.9/0.1 blend as the CPU SourceOpacity)
			source.AppendLine("__kernel void apply_sources(__global float* u, __global const int* idx, __global const int* level,");
			source.AppendLine("                            __global const float* freq, const float t, const float amp, const int n)");
			source.AppendLine("{");
			source.AppendLine("    int k = get_global_id(0);");
			source.AppendLine("    if (k >= n) return;");
			source.AppendLine("    int p = idx[k];");
			source.AppendLine("    u[p] = u[p] * 0.9f + sin(freq[level[k]] * t) * amp * 0.1f;");
			source.AppendLine("}");
			source.AppendLine();
			// xorshift32: the per-source random generator for randomized off-widths (state must never be 0).
			source.AppendLine("inline uint xorshift32(uint x)");
			source.AppendLine("{");
			source.AppendLine("    x ^= x << 13;");
			source.AppendLine("    x ^= x >> 17;");
			source.AppendLine("    x ^= x << 5;");
			source.AppendLine("    return x;");
			source.AppendLine("}");
			source.AppendLine();
			// apply_object_sources: soft-blends the object sources into the field, one work item per source k < n.
			// All per-source tables are indexed by k:
			//   idx, level, amp, phase       cell index, frequency level (into freq), amplitude, phase (radians)
			//   mode (0 continuous/1 pulsed), pulseShape (0 Gaussian/1 smoothed square), onWidth (cycles)
			//   offWidth                     off-width in effect; rewritten here when it is randomized
			//   detachStep                   step after which the source fades out linearly over 30 steps (-1 = never);
			//                                work items past the fade return without writing
			//   offWidthBase ± offWidthRandomizeAmount   range of a randomized off-width (amount <= 0 = fixed)
			//   cycleAnchor, rngState        kernel-owned state for the randomized mode: start (cycles) of the
			//                                current period and the xorshift32 state
			// The envelope logic is WaveSimulator.ComputePulsePosition + PulseEnvelope; the result is
			//   u[idx] = 0.9·u + 0.1·sin(phase + f·t)·amp·envelope·detachFade.
			source.AppendLine("__kernel void apply_object_sources(__global float* u, __global const int* idx, __global const int* level,");
			source.AppendLine("                                   __global const float* amp, __global const float* phase, __global const int* mode,");
			source.AppendLine("                                   __global const int* pulseShape, __global const int* onWidth, __global int* offWidth,");
			source.AppendLine("                                   __global const int* detachStep,");
			source.AppendLine("                                   __global const float* freq, const float t, const int n,");
			source.AppendLine("                                   __global const int* offWidthBase, __global const int* offWidthRandomizeAmount,");
			source.AppendLine("                                   __global float* cycleAnchor, __global int* rngState)");
			source.AppendLine("{");
			source.AppendLine("    int k = get_global_id(0);");
			source.AppendLine("    if (k >= n) return;");
			source.AppendLine("    float detachFade = 1.0f;");
			source.AppendLine("    if (detachStep[k] >= 0) {");
			// The 30 below is SourceElement.DetachRampSteps, spliced into the kernel text.
			source.AppendLine("        float stepsSinceDetach = t - (float)detachStep[k];");
			source.AppendLine("        if (stepsSinceDetach >= " + 30.ToString() + ".0f) return;");
			source.AppendLine("        if (stepsSinceDetach > 0.0f) detachFade = 1.0f - stepsSinceDetach / " + 30.ToString() + ".0f;");
			source.AppendLine("    }");
			source.AppendLine("    float f = freq[level[k]];");
			source.AppendLine("    float envelope = 1.0f;");
			source.AppendLine("    if (mode[k] != 0) {");
			source.AppendLine("        int ow = offWidth[k];");
			source.AppendLine("        int period = onWidth[k] + ow;");
			source.AppendLine("        if (period > 0) {");
			source.AppendLine("            float cycleClock = f * t / (2.0f * M_PI_F);");
			source.AppendLine("            float pos;");
			source.AppendLine("            int isFirstCycle;");
			source.AppendLine("            if (offWidthRandomizeAmount[k] <= 0) {");
			source.AppendLine("                float adjustedClock = cycleClock + (float)ow;");
			source.AppendLine("                isFirstCycle = (floor(adjustedClock / (float)period) == 0.0f) ? 1 : 0;");
			source.AppendLine("                pos = adjustedClock - period * floor(adjustedClock / (float)period);");
			source.AppendLine("            } else {");
			source.AppendLine("                pos = cycleClock - cycleAnchor[k];");
			source.AppendLine("                int guard = 0;");
			source.AppendLine("                while (pos >= (float)period && guard < 1000) {");
			source.AppendLine("                    cycleAnchor[k] += (float)period;");
			source.AppendLine("                    pos -= (float)period;");
			source.AppendLine("                    uint state = xorshift32((uint)rngState[k]);");
			source.AppendLine("                    rngState[k] = (int)state;");
			source.AppendLine("                    int amt = offWidthRandomizeAmount[k];");
			source.AppendLine("                    int span = 2 * amt + 1;");
			source.AppendLine("                    int r = (int)(state % (uint)span) - amt;");
			source.AppendLine("                    ow = offWidthBase[k] + r;");
			source.AppendLine("                    if (ow < 1) ow = 1;");
			source.AppendLine("                    offWidth[k] = ow;");
			source.AppendLine("                    period = onWidth[k] + ow;");
			source.AppendLine("                    guard++;");
			source.AppendLine("                }");
			source.AppendLine("                if (pos < 0.0f) pos = 0.0f;");
			source.AppendLine("                isFirstCycle = (cycleAnchor[k] == 0.0f) ? 1 : 0;");
			source.AppendLine("            }");
			source.AppendLine("            if (pulseShape[k] == 0) {");
			source.AppendLine("                if (pos < (float)ow) {");
			source.AppendLine("                    envelope = 0.0f;");
			source.AppendLine("                } else {");
			source.AppendLine("                    float onPos = pos - (float)ow;");
			source.AppendLine("                    float center = onWidth[k] / 2.0f;");
			source.AppendLine("                    float sigma = max(onWidth[k] / 6.0f, 1.0e-6f);");
			source.AppendLine("                    float dd = (onPos - center) / sigma;");
			source.AppendLine("                    envelope = exp(-0.5f * dd * dd);");
			source.AppendLine("                }");
			source.AppendLine("            } else {");
			source.AppendLine("                // a flat-topped pulse (envelope 1 while on, 0 while off) with a short, FIXED-width");
			source.AppendLine("                // (in cycles) smoothed edge at the rise and the fall - fixed, so a long off span");
			source.AppendLine("                // sits at (very close to) 0 for nearly all of it instead of lingering. The math is");
			source.AppendLine("                // exactly the on-first formula, fed a coordinate shifted so the on span starts at 0.");
			source.AppendLine("                float shifted = pos - (float)ow;");
			source.AppendLine("                if (shifted < 0.0f && !isFirstCycle) shifted += (float)period;");
			source.AppendLine("                float edgeWidth = 0.35f;");
			source.AppendLine("                float dFall = shifted - (float)onWidth[k];");
			source.AppendLine("                float dRise = shifted;");
			source.AppendLine("                if (dRise > (float)period / 2.0f) dRise -= (float)period;");
			source.AppendLine("                float rising = 0.5f + atan(dRise / edgeWidth) / M_PI_F;");
			source.AppendLine("                float falling = 0.5f - atan(dFall / edgeWidth) / M_PI_F;");
			source.AppendLine("                envelope = rising * falling;");
			source.AppendLine("            }");
			source.AppendLine("        }");
			source.AppendLine("    }");
			source.AppendLine("    float v = sin(phase[k] + f * t) * amp[k] * envelope * detachFade;");
			source.AppendLine("    int p = idx[k];");
			source.AppendLine("    u[p] = u[p] * 0.9f + v * 0.1f;");
			source.AppendLine("}");
			return source.ToString();
		}

		/// <summary>Creates a read/write device buffer the size of <paramref name="data"/> and uploads it (blocking). The caller owns the handle and must release it with <see cref="ReleaseMem"/>.</summary>
		private IntPtr CreateFloatBuffer(float[] data)
		{
			IntPtr size = new IntPtr((long)data.Length * 4L);
			int err = 0;
			IntPtr buffer = OpenClApi.clCreateBuffer(this._context, OpenClApi.MemReadWrite, size, IntPtr.Zero, ref err);
			OpenClApi.Check(err, "clCreateBuffer");
			OpenClApi.Check(OpenClApi.clEnqueueWriteBufferFloat(this._queue, buffer, 1U, IntPtr.Zero, size, data, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueWriteBuffer");
			return buffer;
		}

		/// <summary>Int counterpart of <see cref="CreateFloatBuffer"/>.</summary>
		private IntPtr CreateIntBuffer(int[] data)
		{
			IntPtr size = new IntPtr((long)data.Length * 4L);
			int err = 0;
			IntPtr buffer = OpenClApi.clCreateBuffer(this._context, OpenClApi.MemReadWrite, size, IntPtr.Zero, ref err);
			OpenClApi.Check(err, "clCreateBuffer");
			OpenClApi.Check(OpenClApi.clEnqueueWriteBufferInt(this._queue, buffer, 1U, IntPtr.Zero, size, data, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueWriteBuffer");
			return buffer;
		}

		/// <summary>Creates a kernel of the simulation program (<see cref="_program"/>).</summary>
		private IntPtr CreateKernel(string name)
		{
			return this.CreateKernelFrom(this._program, name);
		}

		/// <summary>Creates the kernel <paramref name="name"/> from <paramref name="program"/>; the caller releases it with <see cref="ReleaseKernel"/>.</summary>
		private IntPtr CreateKernelFrom(IntPtr program, string name)
		{
			int err = 0;
			IntPtr kernel = OpenClApi.clCreateKernel(program, ref name, ref err);
			OpenClApi.Check(err, "clCreateKernel " + name);
			return kernel;
		}

		/// <summary>Binds a device buffer to kernel argument <paramref name="index"/>.</summary>
		private static void SetMem(IntPtr kernel, uint index, IntPtr value)
		{
			OpenClApi.Check(OpenClApi.clSetKernelArgMem(kernel, index, new IntPtr(IntPtr.Size), ref value), "clSetKernelArg");
		}

		/// <summary>Sets an <c>int</c> kernel argument.</summary>
		private static void SetInt(IntPtr kernel, uint index, int value)
		{
			OpenClApi.Check(OpenClApi.clSetKernelArgInt(kernel, index, new IntPtr(4), ref value), "clSetKernelArg");
		}

		/// <summary>Sets a <c>float</c> kernel argument.</summary>
		private static void SetFloat(IntPtr kernel, uint index, float value)
		{
			OpenClApi.Check(OpenClApi.clSetKernelArgFloat(kernel, index, new IntPtr(4), ref value), "clSetKernelArg");
		}

		/// <summary>Engine label for the UI/result: "GPU: &lt;device name&gt; (OpenCL)".</summary>
		public string Description
		{
			get
			{
				return this._description;
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
		/// Host copy of the field (padded layout). Unlike the CPU engine the array instance never changes, but
		/// its contents are only as fresh as the last <see cref="PrepareFrame"/> with <c>needField</c>.
		/// </summary>
		public float[] FieldData
		{
			get
			{
				return this._hostField;
			}
		}

		/// <summary>Host copy of the scaled intensity (padded layout) as of the last <see cref="PrepareFrame"/> with <c>needIntensity</c>; null when the engine was created without intensity tracking.</summary>
		public float[] IntensityData
		{
			get
			{
				return this._hostIntensity;
			}
		}

		/// <summary>
		/// Enqueues one simulation step (nothing is waited for): the source kernels bound to the current field
		/// blend the sources in once, then <c>wave_step</c> runs <see cref="_subSteps"/> times, alternating
		/// between the two kernel instances so each sub-step reads the newest buffer and overwrites the older one
		/// (the leapfrog "previous" field). The track flag is set so only the last sub-step updates the intensity
		/// (with a single sub-step the flag keeps the value bound in <see cref="Build"/>). Afterwards
		/// <see cref="_current"/> points at the newest buffer and <see cref="_time"/> advances by one step.
		/// Every 16 steps the queue is drained with <c>clFinish</c> so unbounded asynchronous enqueueing cannot
		/// exhaust the driver.
		/// </summary>
		public void AdvanceOneStep()
		{
			int current = this._current;
			if (this._sourceCount > 0)
			{
				GpuWaveSimulator.SetFloat(this._sourceKernel[current], 4U, (float)this._time);
				OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(this._queue, this._sourceKernel[current], 1U, null, this._grid1D, null, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (sources)");
			}
			if (this._objSourceCount > 0)
			{
				GpuWaveSimulator.SetFloat(this._objSourceKernel[current], 11U, (float)this._time);
				OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(this._queue, this._objSourceKernel[current], 1U, null, this._objGrid1D, null, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (object sources)");
			}
			if (this._subSteps <= 1)
			{
				OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(this._queue, this._stepKernel[current], 2U, null, this._grid2D, null, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (step)");
				current = 1 - current;
			}
			else
			{
				for (int i = 1; i <= this._subSteps; i++)
				{
					int trackThisSubStep = ((i == this._subSteps && this._track) ? 1 : 0);
					GpuWaveSimulator.SetInt(this._stepKernel[current], 8U, trackThisSubStep);
					OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(this._queue, this._stepKernel[current], 2U, null, this._grid2D, null, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (step)");
					current = 1 - current;
				}
			}
			this._current = current;
			this._time += 1.0;
			this._sinceFinish++;
			if (this._sinceFinish >= 16)
			{
				this._sinceFinish = 0;
				OpenClApi.Check(OpenClApi.clFinish(this._queue), "clFinish");
			}
		}

		/// <summary>
		/// Blocking device-to-host copies of the newest field into <see cref="FieldData"/> and/or of the intensity
		/// into <see cref="IntensityData"/> (the latter only when tracked). Called by the runner before rendering,
		/// and from the UI thread while paused. As the reads drain the queue, the clFinish counter is reset.
		/// </summary>
		public void PrepareFrame(bool needField, bool needIntensity)
		{
			IntPtr size = new IntPtr((long)this._cells * 4L);
			if (needField)
			{
				OpenClApi.Check(OpenClApi.clEnqueueReadBufferFloat(this._queue, this._field[this._current], 1U, IntPtr.Zero, size, this._hostField, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueReadBuffer");
			}
			if (needIntensity && this._track)
			{
				OpenClApi.Check(OpenClApi.clEnqueueReadBufferFloat(this._queue, this._intensityOutBuffer, 1U, IntPtr.Zero, size, this._hostIntensity, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueReadBuffer");
			}
			this._sinceFinish = 0;
		}

		/// <summary>
		/// OpenCL source of the probe program (built lazily in <see cref="SetProbes"/>): <c>probe_sample</c> for
		/// point probes, <c>profile_accumulate</c> and <c>profile_gather</c> for line probes. Together they are
		/// the device side of <see cref="WaveSimulator.SampleProbes"/> / <see cref="WaveSimulator.ReadProbeProfiles"/>.
		/// </summary>
		private static string ProbeKernelSource()
		{
			StringBuilder source = new StringBuilder();
			// probe_sample: one work item per probe p < n (n = all probes, line probes included).
			//   u, inten   newest field and the intensity output, padded layout
			//   pix        pixel index lists of all point probes concatenated
			//   info       4 ints per probe: [start in pix, count, 1 = sample inten instead of u, 1 = divide by count];
			//              all 0 for line probes, which therefore write 0
			//   hist       ProbeRows × n floats, row-major: hist[row * n + p] receives this step's value
			source.AppendLine("__kernel void probe_sample(__global const float* u, __global const float* inten, __global const int* pix,");
			source.AppendLine("                           __global const int* info, __global float* hist, const int row, const int n)");
			source.AppendLine("{");
			source.AppendLine("    int p = get_global_id(0);");
			source.AppendLine("    if (p >= n) return;");
			source.AppendLine("    int start = info[p * 4];");
			source.AppendLine("    int count = info[p * 4 + 1];");
			source.AppendLine("    int useI = info[p * 4 + 2];");
			source.AppendLine("    int mean = info[p * 4 + 3];");
			source.AppendLine("    float s = 0.0f;");
			source.AppendLine("    if (useI != 0) {");
			source.AppendLine("        for (int k = 0; k < count; k++) s += inten[pix[start + k]];");
			source.AppendLine("    } else {");
			source.AppendLine("        for (int k = 0; k < count; k++) s += u[pix[start + k]];");
			source.AppendLine("    }");
			source.AppendLine("    if (mean != 0 && count > 0) s = s / (float)count;");
			source.AppendLine("    hist[row * n + p] = s;");
			source.AppendLine("}");
			source.AppendLine();
			// profile_accumulate: one work item per line-probe pixel k < n (all line probes flattened):
			//   ppix[k]  padded cell index; pmode[k] 0 = field, 1 = intensity, 2 = integrated intensity
			//   total[k] += inten[ppix[k]] for mode 2 only, and only once step >= pStartStep[k] when pStartFlag[k] is set
			source.AppendLine("__kernel void profile_accumulate(__global const float* inten, __global const int* ppix,");
			source.AppendLine("                                 __global const int* pmode, __global const int* pStartFlag, __global const int* pStartStep,");
			source.AppendLine("                                 __global float* total, const int step, const int n)");
			source.AppendLine("{");
			source.AppendLine("    int k = get_global_id(0);");
			source.AppendLine("    if (k >= n) return;");
			source.AppendLine("    if (pmode[k] != 2) return;");
			source.AppendLine("    if (pStartFlag[k] != 0 && step < pStartStep[k]) return;");
			source.AppendLine("    total[k] += inten[ppix[k]];");
			source.AppendLine("}");
			source.AppendLine();
			// profile_gather: one work item per line-probe pixel k < n; same ppix/pmode layout as above.
			//   outv[k] = total[k] (mode 2), inten[ppix[k]] (mode 1) or u[ppix[k]] (mode 0)
			source.AppendLine("__kernel void profile_gather(__global const float* u, __global const float* inten, __global const int* ppix,");
			source.AppendLine("                             __global const int* pmode, __global const float* total, __global float* outv, const int n)");
			source.AppendLine("{");
			source.AppendLine("    int k = get_global_id(0);");
			source.AppendLine("    if (k >= n) return;");
			source.AppendLine("    int m = pmode[k];");
			source.AppendLine("    float v;");
			source.AppendLine("    if (m == 2) v = total[k];");
			source.AppendLine("    else if (m == 1) v = inten[ppix[k]];");
			source.AppendLine("    else v = u[ppix[k]];");
			source.AppendLine("    outv[k] = v;");
			source.AppendLine("}");
			return source.ToString();
		}

		/// <summary>
		/// Installs the probe layout: builds the probe program (<see cref="ProbeKernelSource"/>), then uploads the
		/// pixel tables and creates the kernels for point probes (<see cref="SetUpSeriesProbes"/>) and line probes
		/// (<see cref="SetUpProfileProbes"/>). A null or empty sampler installs nothing and leaves a previously
		/// installed one in place. Meant to be called once per engine: a second call overwrites the probe handles
		/// without releasing the earlier ones.
		/// </summary>
		/// <exception cref="InvalidOperationException">The sampler needs intensity but this engine does not track it, or the probe program / buffers could not be created (inner exception has the OpenCL error).</exception>
		public void SetProbes(ProbeSampler sampler)
		{
			if (sampler == null || sampler.Count == 0)
			{
				return;
			}
			try
			{
				if (sampler.NeedsIntensity && !this._track)
				{
					throw new InvalidOperationException("the intensity is not tracked");
				}
				int err = 0;
				IntPtr[] deviceIds = new IntPtr[] { this._deviceId };
				string[] sources = new string[] { GpuWaveSimulator.ProbeKernelSource() };
				this._probeProgram = OpenClApi.clCreateProgramWithSource(this._context, 1U, sources, null, ref err);
				OpenClApi.Check(err, "clCreateProgramWithSource (probes)");
				string buildOptions = "";
				err = OpenClApi.clBuildProgram(this._probeProgram, 1U, deviceIds, ref buildOptions, IntPtr.Zero, IntPtr.Zero);
				if (err != 0)
				{
					string buildLog = OpenClApi.BuildLog(this._probeProgram, this._deviceId);
					throw new InvalidOperationException("the probe program for the GPU could not be built (" + OpenClApi.ErrorName(err) + ")" + ((buildLog.Length > 0) ? (": " + buildLog) : ""));
				}
				if (sampler.HasSeries)
				{
					this.SetUpSeriesProbes(sampler);
				}
				if (sampler.HasProfiles)
				{
					this.SetUpProfileProbes(sampler);
				}
				this._probeSampler = sampler;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("the probes could not be set up on the GPU: " + ex.Message, ex);
			}
		}

		/// <summary>
		/// Packs the pixel lists of all point (non-profile) probes into <see cref="_pixelBuffer"/>, describes each
		/// probe in <see cref="_infoBuffer"/> (point probes average their pixels, so a single-pixel probe yields
		/// that pixel's value like <see cref="ProbeSampler.Compute"/>), allocates the <see cref="ProbeRows"/>-row
		/// history buffer and creates the two <c>probe_sample</c> kernels. Resets the host-side sample lists.
		/// </summary>
		private void SetUpSeriesProbes(ProbeSampler sampler)
		{
			int count = sampler.Count;
			int err = 0;
			int totalPixels = 0;
			for (int i = 0; i < count; i++)
			{
				if (!sampler.IsProfile[i])
				{
					totalPixels += sampler.Pixels[i].Length;
				}
			}
			int[] pixels = new int[Math.Max(totalPixels, 1)];
			int[] info = new int[count * 4];
			int offset = 0;
			for (int i = 0; i < count; i++)
			{
				if (!sampler.IsProfile[i])
				{
					int[] probePixels = sampler.Pixels[i];
					Array.Copy(probePixels, 0, pixels, offset, probePixels.Length);
					info[i * 4] = offset;
					info[i * 4 + 1] = probePixels.Length;
					info[i * 4 + 2] = (sampler.UseIntensity[i] ? 1 : 0);
					info[i * 4 + 3] = 1;
					offset += probePixels.Length;
				}
			}
			this._pixelBuffer = this.CreateIntBuffer(pixels);
			this._infoBuffer = this.CreateIntBuffer(info);
			// Never uploaded: the kernel writes every row before ReadProbeHistory reads it.
			IntPtr historySize = new IntPtr((long)GpuWaveSimulator.ProbeRows * count * 4L);
			this._historyBuffer = OpenClApi.clCreateBuffer(this._context, OpenClApi.MemReadWrite, historySize, IntPtr.Zero, ref err);
			OpenClApi.Check(err, "clCreateBuffer (probes)");
			for (int k = 0; k <= 1; k++)
			{
				this._probeKernel[k] = this.CreateKernelFrom(this._probeProgram, "probe_sample");
				GpuWaveSimulator.SetMem(this._probeKernel[k], 0U, this._field[k]);
				GpuWaveSimulator.SetMem(this._probeKernel[k], 1U, this._intensityOutBuffer);
				GpuWaveSimulator.SetMem(this._probeKernel[k], 2U, this._pixelBuffer);
				GpuWaveSimulator.SetMem(this._probeKernel[k], 3U, this._infoBuffer);
				GpuWaveSimulator.SetMem(this._probeKernel[k], 4U, this._historyBuffer);
				GpuWaveSimulator.SetInt(this._probeKernel[k], 5U, 0);
				GpuWaveSimulator.SetInt(this._probeKernel[k], 6U, count);
			}
			this._probeGrid[0] = new IntPtr(count);
			this._probeValues.Clear();
			for (int i = 0; i < count; i++)
			{
				this._probeValues.Add(new List<float>());
			}
			this._probeRow = 0;
		}

		/// <summary>
		/// Flattens the pixels of all line probes into per-pixel buffers (index, mode, start flag/step), records
		/// where each probe starts in <see cref="_profileStart"/>, zero-initialises the totals and output buffers
		/// and creates the two <c>profile_gather</c> kernels. The start-step buffers and the
		/// <c>profile_accumulate</c> kernel are only created when at least one probe integrates.
		/// </summary>
		private void SetUpProfileProbes(ProbeSampler sampler)
		{
			int count = sampler.Count;
			int totalPixels = 0;
			for (int i = 0; i < count; i++)
			{
				if (sampler.IsProfile[i])
				{
					totalPixels += sampler.Pixels[i].Length;
				}
			}
			int[] pixels = new int[totalPixels];
			int[] modes = new int[totalPixels];
			int[] startFlags = new int[totalPixels];
			int[] startSteps = new int[totalPixels];
			this._profileStart = new int[count];
			this._profileIntegrates = false;
			int offset = 0;
			for (int i = 0; i < count; i++)
			{
				this._profileStart[i] = offset;
				if (sampler.IsProfile[i])
				{
					int[] probePixels = sampler.Pixels[i];
					int mode = (sampler.Integrate[i] ? 2 : (sampler.UseIntensity[i] ? 1 : 0));
					if (mode == 2)
					{
						this._profileIntegrates = true;
					}
					int startFlag = (sampler.UseStartAfterStep[i] ? 1 : 0);
					int startStep = sampler.StartAfterStep[i];
					for (int k = 0; k < probePixels.Length; k++)
					{
						pixels[offset + k] = probePixels[k];
						modes[offset + k] = mode;
						startFlags[offset + k] = startFlag;
						startSteps[offset + k] = startStep;
					}
					offset += probePixels.Length;
				}
			}
			this._profileCount = totalPixels;
			this._profilePixelBuffer = this.CreateIntBuffer(pixels);
			this._profileModeBuffer = this.CreateIntBuffer(modes);
			this._profileTotalBuffer = this.CreateFloatBuffer(new float[totalPixels]);
			this._profileOutBuffer = this.CreateFloatBuffer(new float[totalPixels]);
			for (int k = 0; k <= 1; k++)
			{
				this._gatherKernel[k] = this.CreateKernelFrom(this._probeProgram, "profile_gather");
				GpuWaveSimulator.SetMem(this._gatherKernel[k], 0U, this._field[k]);
				GpuWaveSimulator.SetMem(this._gatherKernel[k], 1U, this._intensityOutBuffer);
				GpuWaveSimulator.SetMem(this._gatherKernel[k], 2U, this._profilePixelBuffer);
				GpuWaveSimulator.SetMem(this._gatherKernel[k], 3U, this._profileModeBuffer);
				GpuWaveSimulator.SetMem(this._gatherKernel[k], 4U, this._profileTotalBuffer);
				GpuWaveSimulator.SetMem(this._gatherKernel[k], 5U, this._profileOutBuffer);
				GpuWaveSimulator.SetInt(this._gatherKernel[k], 6U, totalPixels);
			}
			if (this._profileIntegrates)
			{
				this._profileStartFlagBuffer = this.CreateIntBuffer(startFlags);
				this._profileStartStepBuffer = this.CreateIntBuffer(startSteps);
				this._accumulateKernel = this.CreateKernelFrom(this._probeProgram, "profile_accumulate");
				GpuWaveSimulator.SetMem(this._accumulateKernel, 0U, this._intensityOutBuffer);
				GpuWaveSimulator.SetMem(this._accumulateKernel, 1U, this._profilePixelBuffer);
				GpuWaveSimulator.SetMem(this._accumulateKernel, 2U, this._profileModeBuffer);
				GpuWaveSimulator.SetMem(this._accumulateKernel, 3U, this._profileStartFlagBuffer);
				GpuWaveSimulator.SetMem(this._accumulateKernel, 4U, this._profileStartStepBuffer);
				GpuWaveSimulator.SetMem(this._accumulateKernel, 5U, this._profileTotalBuffer);
				GpuWaveSimulator.SetInt(this._accumulateKernel, 7U, totalPixels);
			}
			this._profileGrid[0] = new IntPtr(totalPixels);
		}

		/// <summary>
		/// Called by the runner after every step: enqueues <c>probe_sample</c> for the newest field into the next
		/// history row (first draining the history to the host when all <see cref="ProbeRows"/> rows are full),
		/// and <c>profile_accumulate</c> with the current step number when any line probe integrates. Nothing is
		/// waited for.
		/// </summary>
		public void SampleProbes()
		{
			if (this._probeSampler == null)
			{
				return;
			}
			int current = this._current;
			if (this._probeSampler.HasSeries)
			{
				if (this._probeRow >= GpuWaveSimulator.ProbeRows)
				{
					this.ReadProbeHistory();
				}
				GpuWaveSimulator.SetInt(this._probeKernel[current], 5U, this._probeRow);
				OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(this._queue, this._probeKernel[current], 1U, null, this._probeGrid, null, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (probes)");
				this._probeRow++;
			}
			if (this._profileIntegrates)
			{
				GpuWaveSimulator.SetInt(this._accumulateKernel, 6U, (int)Math.Round(this._time));
				OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(this._queue, this._accumulateKernel, 1U, null, this._profileGrid, null, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (totals)");
			}
		}

		/// <summary>
		/// Blocking download of the <see cref="_probeRow"/> rows sampled so far from <see cref="_historyBuffer"/>,
		/// appending each row's point-probe values to <see cref="_probeValues"/> (line-probe columns are skipped)
		/// and rewinding the row counter. No-op when no probes are set or no row is pending.
		/// </summary>
		private void ReadProbeHistory()
		{
			if (this._probeSampler == null || this._probeRow == 0)
			{
				return;
			}
			int count = this._probeSampler.Count;
			float[] history = new float[this._probeRow * count];
			OpenClApi.Check(OpenClApi.clEnqueueReadBufferFloat(this._queue, this._historyBuffer, 1U, IntPtr.Zero, new IntPtr((long)history.Length * 4L), history, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueReadBuffer (probes)");
			for (int row = 0; row < this._probeRow; row++)
			{
				for (int p = 0; p < count; p++)
				{
					if (!this._probeSampler.IsProfile[p])
					{
						this._probeValues[p].Add(history[row * count + p]);
					}
				}
			}
			this._probeRow = 0;
			this._sinceFinish = 0;
		}

		/// <summary>Drains the pending history rows from the device, then returns and clears the point-probe values collected since the previous call.</summary>
		/// <returns>One array per probe, one value per step (empty for line probes); an empty outer array when no sampler is set.</returns>
		public float[][] TakeProbeSamples()
		{
			if (this._probeSampler == null)
			{
				return new float[0][];
			}
			int count = this._probeSampler.Count;
			float[][] samples = new float[count][];
			if (this._probeSampler.HasSeries)
			{
				this.ReadProbeHistory();
			}
			for (int i = 0; i < count; i++)
			{
				if (this._probeSampler.IsProfile[i])
				{
					samples[i] = new float[0];
				}
				else
				{
					samples[i] = this._probeValues[i].ToArray();
					this._probeValues[i].Clear();
				}
			}
			return samples;
		}

		/// <summary>
		/// Runs <c>profile_gather</c> on the newest field and reads the flat result back (blocking), then slices it
		/// per line probe; nothing is cleared.
		/// </summary>
		/// <returns>
		/// One array per probe in pixel order along the line, holding the integrated intensity totals or the live
		/// field/intensity values; null entries for point probes; an empty outer array when no sampler is set.
		/// </returns>
		public float[][] ReadProbeProfiles()
		{
			if (this._probeSampler == null)
			{
				return new float[0][];
			}
			int count = this._probeSampler.Count;
			float[][] profiles = new float[count][];
			if (!this._probeSampler.HasProfiles)
			{
				return profiles;
			}
			int current = this._current;
			OpenClApi.Check(OpenClApi.clEnqueueNDRangeKernel(this._queue, this._gatherKernel[current], 1U, null, this._profileGrid, null, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueNDRangeKernel (profiles)");
			float[] values = new float[this._profileCount];
			OpenClApi.Check(OpenClApi.clEnqueueReadBufferFloat(this._queue, this._profileOutBuffer, 1U, IntPtr.Zero, new IntPtr((long)this._profileCount * 4L), values, 0U, IntPtr.Zero, IntPtr.Zero), "clEnqueueReadBuffer (profiles)");
			this._sinceFinish = 0;
			for (int i = 0; i < count; i++)
			{
				if (this._probeSampler.IsProfile[i])
				{
					int length = this._probeSampler.Pixels[i].Length;
					float[] profile = new float[length];
					Array.Copy(values, this._profileStart[i], profile, 0, length);
					profiles[i] = profile;
				}
			}
			return profiles;
		}

		/// <summary>Releases every OpenCL handle; called by the runner when the run ends. Safe to call more than once.</summary>
		public void Dispose()
		{
			this.ReleaseAll();
		}

		/// <summary>
		/// Releases all device resources exactly once (guarded by <see cref="_released"/>): drains the queue, then
		/// frees kernels and buffers (including the probe ones, if any), the two programs, the queue and finally
		/// the context, zeroing every handle. Unset handles are skipped and OpenCL failures are swallowed, so the
		/// method is also usable from the constructor after a partial <see cref="Build"/>.
		/// </summary>
		private void ReleaseAll()
		{
			if (this._released)
			{
				return;
			}
			this._released = true;
			try
			{
				if (this._queue != IntPtr.Zero)
				{
					OpenClApi.clFinish(this._queue);
				}
			}
			catch
			{
			}
			for (int k = 0; k <= 1; k++)
			{
				GpuWaveSimulator.ReleaseKernel(ref this._stepKernel[k]);
				GpuWaveSimulator.ReleaseKernel(ref this._sourceKernel[k]);
				GpuWaveSimulator.ReleaseKernel(ref this._objSourceKernel[k]);
				GpuWaveSimulator.ReleaseKernel(ref this._probeKernel[k]);
				GpuWaveSimulator.ReleaseKernel(ref this._gatherKernel[k]);
				GpuWaveSimulator.ReleaseMem(ref this._field[k]);
			}
			GpuWaveSimulator.ReleaseKernel(ref this._accumulateKernel);
			GpuWaveSimulator.ReleaseMem(ref this._pixelBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._infoBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._historyBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._profilePixelBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._profileModeBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._profileStartFlagBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._profileStartStepBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._profileTotalBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._profileOutBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._c2Buffer);
			GpuWaveSimulator.ReleaseMem(ref this._dBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._intensityBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._intensityStage1Buffer);
			GpuWaveSimulator.ReleaseMem(ref this._intensityOutBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._indexBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._levelBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._frequencyBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objIndexBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objLevelBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objAmplitudeBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objPhaseBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objModeBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objPulseShapeBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objOnWidthBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objOffWidthBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objDetachStepBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objOffWidthBaseBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objRandomizeOffWidthAmountBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objCycleAnchorBuffer);
			GpuWaveSimulator.ReleaseMem(ref this._objRngStateBuffer);
			try
			{
				if (this._probeProgram != IntPtr.Zero)
				{
					OpenClApi.clReleaseProgram(this._probeProgram);
				}
				if (this._program != IntPtr.Zero)
				{
					OpenClApi.clReleaseProgram(this._program);
				}
				if (this._queue != IntPtr.Zero)
				{
					OpenClApi.clReleaseCommandQueue(this._queue);
				}
				if (this._context != IntPtr.Zero)
				{
					OpenClApi.clReleaseContext(this._context);
				}
			}
			catch
			{
			}
			this._probeProgram = IntPtr.Zero;
			this._program = IntPtr.Zero;
			this._queue = IntPtr.Zero;
			this._context = IntPtr.Zero;
		}

		/// <summary>Releases a buffer handle unless it is <see cref="IntPtr.Zero"/>, ignoring errors, and zeroes it.</summary>
		private static void ReleaseMem(ref IntPtr handle)
		{
			try
			{
				if (handle != IntPtr.Zero)
				{
					OpenClApi.clReleaseMemObject(handle);
				}
			}
			catch
			{
			}
			handle = IntPtr.Zero;
		}

		/// <summary>Releases a kernel handle unless it is <see cref="IntPtr.Zero"/>, ignoring errors, and zeroes it.</summary>
		private static void ReleaseKernel(ref IntPtr handle)
		{
			try
			{
				if (handle != IntPtr.Zero)
				{
					OpenClApi.clReleaseKernel(handle);
				}
			}
			catch
			{
			}
			handle = IntPtr.Zero;
		}

		/// <summary>Engine label: "GPU: &lt;device name&gt; (OpenCL)".</summary>
		private readonly string _description;

		/// <summary>Scene width in cells (canvas pixels), without padding.</summary>
		private readonly int _width;

		/// <summary>Scene height in cells, without padding.</summary>
		private readonly int _height;

		/// <summary>Padded row pitch (<see cref="_width"/> + 2).</summary>
		private readonly int _stride;

		/// <summary>Number of floats in every padded per-cell buffer: <see cref="_stride"/> × (<see cref="_height"/> + 2).</summary>
		private readonly int _cells;

		/// <summary>Whether the intensity is tracked; when false the intensity buffers are 1-float dummies and <see cref="IntensityData"/> is null.</summary>
		private readonly bool _track;

		/// <summary>Number of image (green-channel) sources in the scene.</summary>
		private readonly int _sourceCount;

		/// <summary>Peak amplitude of the image sources (object sources carry their own).</summary>
		private readonly float _amplitude;

		/// <summary>Whether the program was compiled with the 4th-order stencil (<c>USE_4TH_ORDER</c>), which implies sub-stepping.</summary>
		private readonly bool _use4thOrderLaplacian;

		/// <summary>Leapfrog sub-steps per simulation step; 1 unless the 4th-order stencil is on. Set in <see cref="Build"/>.</summary>
		private int _subSteps;

		/// <summary>1/<see cref="_subSteps"/>², scaling c² for the shorter sub-step (kernel argument <c>c2Scale</c>).</summary>
		private float _c2Mult;

		/// <summary>Per-step retention of the two intensity averaging stages: exp(-1/decaySteps) with decaySteps = timeDecayFactor·100/lowest source level.</summary>
		private float _intensityAlpha;

		/// <summary>Multiplier on the intensity output.</summary>
		private float _intensityScale;

		/// <summary>OpenCL context on the chosen device; released last in <see cref="ReleaseAll"/>.</summary>
		private IntPtr _context;

		/// <summary>In-order command queue; every launch and transfer goes through it, from the worker thread.</summary>
		private IntPtr _queue;

		/// <summary>Program built from <see cref="KernelSource"/> (<c>wave_step</c>, <c>apply_sources</c>, <c>apply_object_sources</c>).</summary>
		private IntPtr _program;

		/// <summary>The two ping-pong field buffers (padded, <see cref="_cells"/> floats each); the one at <see cref="_current"/> holds the newest field, the other the previous one.</summary>
		private readonly IntPtr[] _field;

		/// <summary><c>wave_step</c> kernel per field index k: reads <see cref="_field"/>[k], writes <see cref="_field"/>[1 - k].</summary>
		private readonly IntPtr[] _stepKernel;

		/// <summary><c>apply_sources</c> kernel per field index k, bound to <see cref="_field"/>[k]; <see cref="IntPtr.Zero"/> when the scene has no image sources.</summary>
		private readonly IntPtr[] _sourceKernel;

		/// <summary>Index (0/1) of the field buffer holding the newest field; flips after every sub-step.</summary>
		private int _current;

		/// <summary>Per-cell c² = 1/n² (padded; border cells 0).</summary>
		private IntPtr _c2Buffer;

		/// <summary>Per-cell damping (padded), 1 = lossless; already raised to 1/<see cref="_subSteps"/> when sub-stepping.</summary>
		private IntPtr _dBuffer;

		/// <summary>Second-stage running average of u² (padded), or a 1-float dummy when not tracking.</summary>
		private IntPtr _intensityBuffer;

		/// <summary>First-stage running average of u² (padded), or a 1-float dummy when not tracking.</summary>
		private IntPtr _intensityStage1Buffer;

		/// <summary>Blended and scaled intensity (padded): read back into <see cref="_hostIntensity"/> and sampled by the probe kernels; a 1-float dummy when not tracking.</summary>
		private IntPtr _intensityOutBuffer;

		/// <summary>Padded cell index of every image source; unset when there are none.</summary>
		private IntPtr _indexBuffer;

		/// <summary>Green level (1..255) of every image source, indexing <see cref="_frequencyBuffer"/>; unset when there are none.</summary>
		private IntPtr _levelBuffer;

		/// <summary>256 angular frequencies in radians per step, one per level: level/255 × 0.5 × sourceFrequencyScale. Shared by both source kernels; unset when there are no sources at all.</summary>
		private IntPtr _frequencyBuffer;

		/// <summary>Number of object sources that lie inside the canvas.</summary>
		private readonly int _objSourceCount;

		/// <summary><c>apply_object_sources</c> kernel per field index k, bound to <see cref="_field"/>[k]; <see cref="IntPtr.Zero"/> when there are no object sources.</summary>
		private readonly IntPtr[] _objSourceKernel;

		/// <summary>Per object source: padded cell index.</summary>
		private IntPtr _objIndexBuffer;

		/// <summary>Per object source: level, indexing <see cref="_frequencyBuffer"/>.</summary>
		private IntPtr _objLevelBuffer;

		/// <summary>Per object source: peak amplitude.</summary>
		private IntPtr _objAmplitudeBuffer;

		/// <summary>Per object source: phase offset in radians.</summary>
		private IntPtr _objPhaseBuffer;

		/// <summary>Per object source: 0 = continuous, 1 = pulsed.</summary>
		private IntPtr _objModeBuffer;

		/// <summary>Per object source: 0 = Gaussian pulse, 1 = smoothed square.</summary>
		private IntPtr _objPulseShapeBuffer;

		/// <summary>Per object source: on-width of the pulse in cycles.</summary>
		private IntPtr _objOnWidthBuffer;

		/// <summary>Per object source: off-width in cycles currently in effect; rewritten by the kernel when the off-width is randomized.</summary>
		private IntPtr _objOffWidthBuffer;

		/// <summary>Per object source: step after which it detaches and fades out over <see cref="SourceElement.DetachRampSteps"/> steps, or -1 (never).</summary>
		private IntPtr _objDetachStepBuffer;

		/// <summary>Per object source: base off-width (cycles) a randomized off-width is drawn around.</summary>
		private IntPtr _objOffWidthBaseBuffer;

		/// <summary>Per object source: ± range (cycles) of the off-width randomization; 0 or less = fixed off-width.</summary>
		private IntPtr _objRandomizeOffWidthAmountBuffer;

		/// <summary>Per object source: start (in cycles) of its current period in randomized mode; kernel-owned state, uploaded as 0.</summary>
		private IntPtr _objCycleAnchorBuffer;

		/// <summary>Per object source: xorshift32 state for drawing off-widths; seeded on the host with a non-zero random int, advanced by the kernel.</summary>
		private IntPtr _objRngStateBuffer;

		/// <summary>Global work size of <c>apply_object_sources</c>: max(1, <see cref="_objSourceCount"/>).</summary>
		private readonly IntPtr[] _objGrid1D;

		/// <summary>Global work size of <c>wave_step</c>: <see cref="_width"/> × <see cref="_height"/> work items, one per interior cell.</summary>
		private readonly IntPtr[] _grid2D;

		/// <summary>Global work size of <c>apply_sources</c>: max(1, <see cref="_sourceCount"/>).</summary>
		private readonly IntPtr[] _grid1D;

		/// <summary>Host copy of the newest field, refreshed by <see cref="PrepareFrame"/>; exposed as <see cref="FieldData"/>.</summary>
		private float[] _hostField;

		/// <summary>Host copy of <see cref="_intensityOutBuffer"/>, refreshed by <see cref="PrepareFrame"/>; null when not tracking.</summary>
		private float[] _hostIntensity;

		/// <summary>Elapsed time in steps; passed to the source kernels as the float argument <c>t</c>.</summary>
		private double _time;

		/// <summary>Steps enqueued since the queue was last drained; <see cref="AdvanceOneStep"/> calls clFinish at 16. Reset by every blocking read, which drains the queue anyway.</summary>
		private int _sinceFinish;

		/// <summary>Set by <see cref="ReleaseAll"/> so a second release is a no-op.</summary>
		private bool _released;

		/// <summary>Rows (steps) the probe history buffer holds before it must be read back to the host.</summary>
		private const int ProbeRows = 2048;

		/// <summary>Device the context was created on; needed again to build the probe program.</summary>
		private IntPtr _deviceId;

		/// <summary>Probe layout installed by <see cref="SetProbes"/>, or null when no probes are set.</summary>
		private ProbeSampler _probeSampler;

		/// <summary>Program built from <see cref="ProbeKernelSource"/>; <see cref="IntPtr.Zero"/> until probes are set.</summary>
		private IntPtr _probeProgram;

		/// <summary><c>probe_sample</c> kernel per field index k, bound to <see cref="_field"/>[k]; unset without point probes.</summary>
		private readonly IntPtr[] _probeKernel;

		/// <summary>Pixel index lists of all point probes, concatenated (at least 1 int).</summary>
		private IntPtr _pixelBuffer;

		/// <summary>Per probe (all probes, 4 ints each): start offset in <see cref="_pixelBuffer"/>, pixel count, sample-intensity flag, average flag; all 0 for line probes.</summary>
		private IntPtr _infoBuffer;

		/// <summary>Sample history, <see cref="ProbeRows"/> rows × probe count floats, row-major; row r holds the samples of the r-th step since the last read-back.</summary>
		private IntPtr _historyBuffer;

		/// <summary>Next row of <see cref="_historyBuffer"/> to write, i.e. the number of rows waiting to be read back.</summary>
		private int _probeRow;

		/// <summary>Global work size of <c>probe_sample</c>: one work item per probe.</summary>
		private readonly IntPtr[] _probeGrid;

		/// <summary>Point-probe samples read back from the device and not yet taken by <see cref="TakeProbeSamples"/>, one list per probe (stays empty for line probes).</summary>
		private readonly List<List<float>> _probeValues;

		/// <summary><c>profile_gather</c> kernel per field index k, bound to <see cref="_field"/>[k]; unset without line probes.</summary>
		private readonly IntPtr[] _gatherKernel;

		/// <summary><c>profile_accumulate</c> kernel; created only when some line probe integrates (<see cref="_profileIntegrates"/>).</summary>
		private IntPtr _accumulateKernel;

		/// <summary>Padded cell index of every line-probe pixel, all line probes concatenated (<see cref="_profileCount"/> ints).</summary>
		private IntPtr _profilePixelBuffer;

		/// <summary>Per line-probe pixel: 0 = field, 1 = intensity, 2 = integrated intensity.</summary>
		private IntPtr _profileModeBuffer;

		/// <summary>Per line-probe pixel: whether integration only starts at <see cref="_profileStartStepBuffer"/>; created only when integrating.</summary>
		private IntPtr _profileStartFlagBuffer;

		/// <summary>Per line-probe pixel: step from which integration counts; created only when integrating.</summary>
		private IntPtr _profileStartStepBuffer;

		/// <summary>Per line-probe pixel: running intensity total, added to by <c>profile_accumulate</c>.</summary>
		private IntPtr _profileTotalBuffer;

		/// <summary>Per line-probe pixel: value written by <c>profile_gather</c> and read back by <see cref="ReadProbeProfiles"/>.</summary>
		private IntPtr _profileOutBuffer;

		/// <summary>Total number of line-probe pixels (length of the profile buffers).</summary>
		private int _profileCount;

		/// <summary>Whether any line probe integrates the intensity, i.e. <c>profile_accumulate</c> must run every step.</summary>
		private bool _profileIntegrates;

		/// <summary>Per probe: offset of its first pixel in the profile buffers (only meaningful for line probes).</summary>
		private int[] _profileStart;

		/// <summary>Global work size of the profile kernels: one work item per line-probe pixel.</summary>
		private readonly IntPtr[] _profileGrid;
	}
}
