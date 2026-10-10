using System.Diagnostics;
using NLog;
using WaveDude.Engine;
using WaveDude.Model;
using WaveDude.Probes;
using WaveDude.Rendering;
using WaveDude.Runs;

namespace LibraryCheck;

// Drives the library end to end: CPU and (if available) GPU engines over the same scene and compares their
// fields, seed determinism, the model-to-emitter mapping, the frame store, the renderer, the colour scheme
// files and a headless run. Exit code 0 = all checks passed.
internal static class Program
{
	private static int _failures;

	private static void Check(bool ok, string what)
	{
		Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
		if (!ok)
		{
			_failures++;
		}
	}

	private static Emitter Make(int x, int y, int level, double amplitude, SourceMode mode = SourceMode.Continuous, PulseShape shape = PulseShape.Gaussian,
		double phase = 0, int onWidth = 10, int offWidth = 10, int detach = -1, int randomize = 0, uint seed = 1) => new()
	{
		X = x, Y = y, Level = level, Amplitude = amplitude, PhaseRadians = phase, Mode = mode, PulseShape = shape,
		OnWidthCycles = onWidth, OffWidthCycles = offWidth, OffWidthBase = offWidth, RandomizeOffWidthAmount = randomize, DetachAfterStep = detach, RngSeed = seed,
	};

	private static List<Emitter> Sources() =>
	[
		Make(20, 24, 120, 1.0),
		Make(44, 12, 200, 0.7, SourceMode.Pulsed, PulseShape.Gaussian, phase: 0.5, onWidth: 4, offWidth: 6),
		Make(44, 36, 90, 0.5, SourceMode.Pulsed, PulseShape.SmoothedSquare, onWidth: 5, offWidth: 3, detach: 120),
		Make(99, 99, 50, 9.0), // outside: must be dropped
	];

	private static float MaxAbs(ReadOnlySpan<float> a)
	{
		float m = 0f;
		foreach (float v in a)
		{
			m = Math.Max(m, Math.Abs(v));
		}
		return m;
	}

	private static float MaxDiff(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
	{
		float m = 0f;
		for (int i = 0; i < a.Length; i++)
		{
			m = Math.Max(m, Math.Abs(a[i] - b[i]));
		}
		return m;
	}

	private static float MaxDiff(float[]?[] a, float[]?[] b)
	{
		float m = 0f;
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] == null || b[i] == null)
			{
				if (a[i] != b[i])
				{
					return float.PositiveInfinity;
				}
				continue;
			}
			if (a[i]!.Length != b[i]!.Length)
			{
				return float.PositiveInfinity;
			}
			m = Math.Max(m, MaxDiff(a[i], b[i]));
		}
		return m;
	}

	private static ProbeSampler Probes(int w, int h) => ProbeSampler.Build(
	[
		new ProbeSpec("A", ProbeKind.Point, ProbeMeasurement.Amplitude, 20, 24, 0, 0, 0),
		new ProbeSpec("B", ProbeKind.Line, ProbeMeasurement.IntegratedIntensity, 5, 5, 40, 30, 50),
		new ProbeSpec("C", ProbeKind.Point, ProbeMeasurement.Intensity, 30, 20, 0, 0, 0),
	], w, h);

	private static void Run(IWaveEngine engine, int steps)
	{
		for (int i = 0; i < steps; i++)
		{
			engine.AdvanceOneStep();
			engine.SampleProbes();
		}
		engine.PrepareFrame(true, true);
	}

	/// <summary>A 64×48 BGRA scene: refractive index 1.00 everywhere, one green source pixel at (10, 10).</summary>
	private static (byte[] Bgra, int Stride) ScenePixels(int w, int h)
	{
		int stride = w * 4 + 8; // deliberately padded rows
		byte[] bgra = new byte[stride * h];
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				bgra[y * stride + x * 4 + 2] = 100; // R
				bgra[y * stride + x * 4 + 3] = 255; // A
			}
		}
		bgra[10 * stride + 10 * 4 + 1] = 160; // G at (10, 10)
		return (bgra, stride);
	}

	private static int Main(string[] args)
	{
		LogManager.Setup().LoadConfiguration(c => c.ForLogger().FilterMinLevel(LogLevel.Info).WriteToConsole(layout: "  log  ${level}: ${message}"));
		if (args.Length > 0 && args[0] == "bench")
		{
			Bench(args.Length > 1 ? int.Parse(args[1]) : 1920, args.Length > 2 ? int.Parse(args[2]) : 1080);
			return 0;
		}
		const int W = 64, H = 48, Steps = 200;
		(byte[] bgra, int stride) = ScenePixels(W, H);
		SceneData scene = SceneData.FromBgra(bgra, W, H, stride, true, 6);
		Check(scene.SourceCount == 1 && scene.SourceX[0] == 10 && scene.SourceLevel[0] == 160, "scene from BGRA has 1 image source at (10, 10) level 160");
		Check(SceneData.CountSources(bgra, W, H, stride) == 1, "CountSources agrees");
		Check(Math.Abs(scene.C2[0] - 1f) < 1e-6f && scene.D[0] == 0f && scene.D[7 * W + 7] == 1f, "c² = 1, damping edge of 6 (corner 0, inside 1)");

		CheckEngines(scene, W, H, Steps);
		CheckCaptureResetEmitters(scene, W, H);
		CheckDeterminism(scene);
		CheckEmitterMapping();
		CheckFrameStore(scene);
		CheckRenderer();
		CheckColorSchemes();
		CheckRun();

		Console.WriteLine(_failures == 0 ? "LIBRARY OK" : _failures + " check(s) failed");
		return _failures == 0 ? 0 : 1;
	}

	/// <summary>"bench [w h]": what a step costs on each engine at that size, pure stepping and with a frame readback every 2 steps.</summary>
	private static void Bench(int w, int h)
	{
		Console.WriteLine($"--- bench {w}x{h}");
		SceneData scene = SceneData.Empty(w, h, 64);
		List<Emitter> emitters = [Make(w / 3, h / 2, 120, 1.0), Make(2 * w / 3, h / 3, 200, 0.7, SourceMode.Pulsed, onWidth: 4, offWidth: 6)];
		foreach (bool fourth in new[] { false, true })
		{
			var options = new EngineOptions(2.0, 1.0, true, fourth, 10.0, 1.0);
			var clock = Stopwatch.StartNew();
			GpuWaveSimulator? gpu = GpuWaveSimulator.TryCreate(scene, options, emitters, out string problems);
			Console.WriteLine($"  GPU engine build (4th order {fourth}): {clock.ElapsedMilliseconds} ms{(gpu == null ? ", " + problems : "")}");
			if (gpu != null)
			{
				using (gpu)
				{
					Time(gpu, "GPU", 300, fourth);
				}
			}
			if (fourth)
			{
				continue;
			}
			int cpuSteps = (long)w * h > 500_000 ? 40 : 300;
			clock.Restart();
			var cpu = new WaveSimulator(scene, options, emitters);
			Console.WriteLine($"  CPU engine build: {clock.ElapsedMilliseconds} ms");
			Time(cpu, "CPU", cpuSteps, fourth);
		}
	}

	private static void Time(IWaveEngine engine, string name, int steps, bool fourth)
	{
		for (int i = 0; i < 5; i++)
		{
			engine.AdvanceOneStep();
		}
		engine.PrepareFrame(true, true);
		var clock = Stopwatch.StartNew();
		for (int i = 0; i < steps; i++)
		{
			engine.AdvanceOneStep();
		}
		engine.PrepareFrame(true, false);
		double pure = clock.Elapsed.TotalMilliseconds / steps;
		clock.Restart();
		for (int i = 0; i < steps; i++)
		{
			engine.AdvanceOneStep();
			if (i % 2 == 0)
			{
				engine.PrepareFrame(true, true);
			}
		}
		double withReadback = clock.Elapsed.TotalMilliseconds / steps;
		// the run's pattern: capture into a pool of snapshots, await one frame late
		var snapshots = new[] { new FrameSnapshot(engine.Width, engine.Height, true), new FrameSnapshot(engine.Width, engine.Height, true), new FrameSnapshot(engine.Width, engine.Height, true) };
		int next = 0;
		clock.Restart();
		for (int i = 0; i < steps; i++)
		{
			engine.AdvanceOneStep();
			if (i % 2 == 0)
			{
				FrameSnapshot s = snapshots[next];
				next = (next + 1) % snapshots.Length;
				engine.AwaitFrame(s);
				engine.CaptureFrame(s, true);
			}
		}
		foreach (FrameSnapshot s in snapshots)
		{
			engine.AwaitFrame(s);
		}
		double withCapture = clock.Elapsed.TotalMilliseconds / steps;
		Console.WriteLine($"  {name} 4th order {fourth}: {pure:0.00} ms/step pure, {withReadback:0.00} ms/step with both planes read back every 2 steps, {withCapture:0.00} ms/step with pipelined half captures every 2 steps ({steps} steps)");
	}

	private static void CheckEngines(SceneData scene, int W, int H, int Steps)
	{
		foreach (bool fourth in new[] { false, true })
		{
			Console.WriteLine("--- engines, 4th order: " + fourth);
			var options = new EngineOptions(1.0, 1.0, true, fourth, 10.0, 1.0);
			var cpu = new WaveSimulator(scene, options, Sources());
			cpu.SetProbes(Probes(W, H));
			Run(cpu, Steps);
			float[] cpuField = (float[])cpu.FieldData.Clone();
			float[] cpuInten = (float[])cpu.IntensityData!.Clone();
			float[][] cpuSamples = cpu.TakeProbeSamples();
			float[]?[] cpuProfiles = cpu.ReadProbeProfiles();
			Check(cpuSamples[0].Length == Steps && cpuSamples[2].Length == Steps && cpuSamples[1].Length == 0, "CPU point probes have one sample per step");
			Check(cpuProfiles[1] != null && cpuProfiles[1]!.Length > 30 && MaxAbs(cpuProfiles[1]) > 0f, "CPU integrated line profile populated");
			Check(cpu.TakeProbeSamples()[0].Length == 0, "CPU TakeProbeSamples clears");
			Check(MaxAbs(cpuField) > 0.01f, "CPU field is non-trivial (max |u| = " + MaxAbs(cpuField) + ")");
			Check(MaxAbs(cpuInten) > 0f, "CPU intensity accumulated");
			Check(Math.Abs(cpu.Time - Steps) < 1e-9, "CPU time counts steps");
			bool borderZero = true;
			for (int x = 0; x < cpu.Stride; x++)
			{
				borderZero &= cpuField[x] == 0f && cpuField[(H + 1) * cpu.Stride + x] == 0f;
			}
			Check(borderZero, "CPU padding rows untouched");

			GpuWaveSimulator? gpu = GpuWaveSimulator.TryCreate(scene, options, Sources(), out string problems);
			if (gpu == null)
			{
				Console.WriteLine("  skip GPU: " + problems);
				continue;
			}
			using (gpu)
			{
				Console.WriteLine("  " + gpu.Description);
				gpu.SetProbes(Probes(W, H));
				Run(gpu, Steps);
				float diffField = MaxDiff(cpuField, gpu.FieldData);
				float diffInten = MaxDiff(cpuInten, gpu.IntensityData!);
				Check(diffField < 1e-3f * Math.Max(1f, MaxAbs(cpuField)), "GPU field matches CPU (max diff " + diffField + ")");
				Check(diffInten < 1e-3f * Math.Max(1f, MaxAbs(cpuInten)), "GPU intensity matches CPU (max diff " + diffInten + ")");
				float[][] gpuSamples = gpu.TakeProbeSamples();
				float[]?[] gpuProfiles = gpu.ReadProbeProfiles();
				float diffSamples = MaxDiff(cpuSamples, gpuSamples);
				float diffProfiles = MaxDiff(cpuProfiles, gpuProfiles);
				Check(diffSamples < 1e-3f, "GPU point-probe samples match CPU (max diff " + diffSamples + ")");
				Check(diffProfiles < 1e-3f * Math.Max(1f, MaxAbs(cpuProfiles[1])), "GPU line profile matches CPU (max diff " + diffProfiles + ")");
				gpu.SetProbes(Probes(W, H));
				Run(gpu, 10);
				Check(gpu.TakeProbeSamples()[0].Length == 10, "GPU probes re-installed and sampling again");
				gpu.SetProbes(null);
				Check(gpu.TakeProbeSamples().Length == 0 && gpu.ReadProbeProfiles().Length == 0, "GPU: probes cleared -> empty results");
				gpu.Dispose();
				gpu.Dispose();
				Check(true, "GPU double dispose is harmless");
			}
		}
		Console.WriteLine("--- factory");
		IWaveEngine fallback = WaveEngineFactory.Create(scene, new EngineOptions(1.0, 1.0, false, false, 10.0, 1.0), false, null, out string note);
		Check(fallback is WaveSimulator && note == "" && fallback.IntensityData == null, "factory gives the CPU engine without intensity when asked");
		(fallback as IDisposable)?.Dispose();
	}

	private static float MaxDiff(ReadOnlySpan<float> padded, int stride, int w, int h, Half[] plane)
	{
		float m = 0f;
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				m = Math.Max(m, Math.Abs(padded[(y + 1) * stride + x + 1] - (float)plane[y * w + x]));
			}
		}
		return m;
	}

	/// <summary>CaptureFrame/AwaitFrame, Reset and SetEmitters on both engines, and a deferred await (the pipeline's pattern).</summary>
	private static void CheckCaptureResetEmitters(SceneData scene, int W, int H)
	{
		Console.WriteLine("--- capture, reset, emitters");
		var options = new EngineOptions(1.0, 1.0, true, false, 10.0, 1.0);
		List<Emitter> moved = [Make(30, 30, 120, 1.0), Make(44, 12, 200, 0.7, SourceMode.Pulsed, PulseShape.Gaussian, phase: 0.5, onWidth: 4, offWidth: 6)];
		var reference = new WaveSimulator(scene, options, Sources());
		Run(reference, 100);
		float[] at100 = (float[])reference.FieldData.Clone();
		float[] inten100 = (float[])reference.IntensityData!.Clone();
		Run(reference, 50);
		float[] at150 = (float[])reference.FieldData.Clone();
		var movedReference = new WaveSimulator(scene, options, moved);
		Run(movedReference, 100);
		float[] movedAt100 = (float[])movedReference.FieldData.Clone();
		Check(MaxDiff(at100, movedAt100) > 1e-3f, "moving a source changes the field (reference)");

		GpuWaveSimulator? gpu = GpuWaveSimulator.TryCreate(scene, options, Sources(), out string problems);
		if (gpu == null)
		{
			Console.WriteLine("  skip GPU: " + problems);
		}
		foreach ((IWaveEngine engine, string name) in new (IWaveEngine, string)[] { (new WaveSimulator(scene, options, Sources()), "CPU"), (gpu!, "GPU") })
		{
			if (engine == null)
			{
				continue;
			}
			var a = new FrameSnapshot(W, H, true);
			var b = new FrameSnapshot(W, H, true);
			Run(engine, 100);
			engine.CaptureFrame(a, true);
			// keep stepping before awaiting: the capture must still show step 100
			Run(engine, 50);
			engine.CaptureFrame(b, false);
			engine.AwaitFrame(a);
			engine.AwaitFrame(b);
			float tolerance = 2e-3f * Math.Max(1f, MaxAbs(at100));
			Check(a.HasIntensity && !b.HasIntensity, name + ": intensity captured only when asked");
			Check(MaxDiff(at100, engine.Stride, W, H, a.Field) < tolerance, name + ": deferred capture holds the field of its step (max diff " + MaxDiff(at100, engine.Stride, W, H, a.Field) + ")");
			Check(MaxDiff(inten100, engine.Stride, W, H, a.Intensity!) < 2e-3f * Math.Max(1f, MaxAbs(inten100)), name + ": captured intensity matches");
			Check(MaxDiff(at150, engine.Stride, W, H, b.Field) < tolerance, name + ": second capture holds the later step");
			engine.Reset();
			engine.CaptureFrame(a, true);
			engine.AwaitFrame(a);
			Check(MaxAbs(a.Field.Select(h => (float)h).ToArray()) == 0f && MaxAbs(a.Intensity!.Select(h => (float)h).ToArray()) == 0f, name + ": reset clears field and intensity");
			Run(engine, 100);
			engine.CaptureFrame(a, false);
			engine.AwaitFrame(a);
			Check(MaxDiff(at100, engine.Stride, W, H, a.Field) < tolerance, name + ": after reset the run reproduces (max diff " + MaxDiff(at100, engine.Stride, W, H, a.Field) + ")");
			engine.Reset();
			engine.SetEmitters(moved);
			Run(engine, 100);
			engine.CaptureFrame(a, false);
			engine.AwaitFrame(a);
			Check(MaxDiff(movedAt100, engine.Stride, W, H, a.Field) < tolerance, name + ": replaced emitters give the moved reference's field (max diff " + MaxDiff(movedAt100, engine.Stride, W, H, a.Field) + ")");
			engine.Reset();
			engine.SetEmitters(Sources());
			Run(engine, 100);
			engine.CaptureFrame(a, false);
			engine.AwaitFrame(a);
			Check(MaxDiff(at100, engine.Stride, W, H, a.Field) < tolerance, name + ": emitters restored, original field again");
			(engine as IDisposable)?.Dispose();
		}
	}

	private static float[] RunCpu(SceneData scene, List<Emitter> emitters, int steps)
	{
		var cpu = new WaveSimulator(scene, new EngineOptions(1.0, 1.0, false, false, 10.0, 1.0), emitters);
		Run(cpu, steps);
		return (float[])cpu.FieldData.Clone();
	}

	private static void CheckDeterminism(SceneData scene)
	{
		Console.WriteLine("--- determinism");
		Source Randomized(int seed)
		{
			var source = new Source
			{
				Name = "Jitter", X = 32, Y = 24, Frequency = 180, Amplitude = 80, Mode = SourceMode.Pulsed, PulseShape = PulseShape.SmoothedSquare,
				PulseWidth = 2, OffWidth = 2, Array = new SourceArray { SizeX = 3, SizeY = 1, SpacingX = 6, RandomOffWidth = 3 },
			};
			source.GenerateElements(seed);
			return source;
		}
		List<Emitter> a = Emitter.FromSource(Randomized(123), 123);
		List<Emitter> b = Emitter.FromSource(Randomized(123), 123);
		List<Emitter> c = Emitter.FromSource(Randomized(124), 124);
		Check(a.Count == 3 && a.All(e => e.RandomizeOffWidthAmount == 3 && e.RngSeed != 0), "randomized composite gives 3 emitters with non-zero seeds");
		Check(a.Select(e => e.RngSeed).SequenceEqual(b.Select(e => e.RngSeed)), "same project seed -> same emitter seeds");
		Check(!a.Select(e => e.RngSeed).SequenceEqual(c.Select(e => e.RngSeed)), "different project seed -> different emitter seeds");
		float[] fieldA = RunCpu(scene, a, 300);
		float[] fieldB = RunCpu(scene, b, 300);
		float[] fieldC = RunCpu(scene, c, 300);
		Check(fieldA.AsSpan().SequenceEqual(fieldB), "same seed -> bit-identical field after 300 steps");
		Check(MaxDiff(fieldA, fieldC) > 1e-4f, "different seed -> different field (max diff " + MaxDiff(fieldA, fieldC) + ")");
		var options = new EngineOptions(1.0, 1.0, false, false, 10.0, 1.0);
		GpuWaveSimulator? gpu = GpuWaveSimulator.TryCreate(scene, options, a, out string problems);
		if (gpu == null)
		{
			Console.WriteLine("  skip GPU: " + problems);
			return;
		}
		using (gpu)
		{
			Run(gpu, 300);
			float diff = MaxDiff(fieldA, gpu.FieldData);
			Check(diff < 1e-3f * Math.Max(1f, MaxAbs(fieldA)), "GPU agrees with CPU on the randomized gaps (max diff " + diff + ")");
		}
	}

	private static void CheckEmitterMapping()
	{
		Console.WriteLine("--- emitter mapping");
		var project = new Project();
		Source source = project.AddSource();
		source.X = 12;
		source.Y = 34;
		source.Frequency = 77;
		source.Amplitude = 50;
		source.Phase = 90;
		source.Mode = SourceMode.Pulsed;
		source.PulseShape = PulseShape.SmoothedSquare;
		source.PulseWidth = 7;
		source.OffWidth = 9;
		source.DetachAfter = 500;
		List<Emitter> emitters = Emitter.FromProject(project);
		Check(emitters.Count == 1, "one emitter for a single source");
		Emitter e = emitters[0];
		Check(e.X == 12 && e.Y == 34 && e.Level == 77, "position and level copied");
		Check(Math.Abs(e.Amplitude - 0.5) < 1e-12 && Math.Abs(e.PhaseRadians - Math.PI / 2) < 1e-12, "amplitude / 100 and phase in radians");
		Check(e.Mode == SourceMode.Pulsed && e.PulseShape == PulseShape.SmoothedSquare && e.OnWidthCycles == 7 && e.OffWidthCycles == 9 && e.OffWidthBase == 9 && e.RandomizeOffWidthAmount == 0 && e.DetachAfterStep == 500, "mode, shape, widths, detach copied");
		source.Enabled = false;
		Check(Emitter.FromProject(project).Count == 0, "disabled source gives no emitters");
		source.Enabled = true;
		project.Scene.ObjectSources = false;
		Check(Emitter.FromProject(project).Count == 0, "scene without object sources gives no emitters");
	}

	private static void CheckFrameStore(SceneData scene)
	{
		Console.WriteLine("--- frame store");
		string path = Path.Combine(Path.GetTempPath(), "library_check-" + Guid.NewGuid().ToString("N") + FrameCache.Extension);
		var cpu = new WaveSimulator(scene, new EngineOptions(1.0, 1.0, true, false, 10.0, 1.0), Sources());
		using (var store = FrameStore.Create(path, scene.Width, scene.Height, FrameLayers.Field | FrameLayers.Intensity, 3, 2))
		{
			Check(File.Exists(path) && new FileInfo(path).Length == store.TotalBytes, "file created and pre-sized (" + store.TotalBytes + " bytes)");
			Check(store.Count == 0 && store.LayerCount == 2 && store.Has(FrameLayer.Intensity) && store.StepOf(2) == 4, "empty store reports its layout");
			int written = 0;
			store.FrameWritten += n => written = n;
			float[][] fields = new float[3][];
			for (int frame = 0; frame < 3; frame++)
			{
				Run(cpu, 2);
				fields[frame] = (float[])cpu.FieldData.Clone();
				store.Write(frame, cpu.FieldData, cpu.IntensityData, cpu.Stride);
			}
			Check(store.Count == 3 && written == 3, "three frames written, event saw the count");
			bool same = true;
			var copy = new Half[scene.Width * scene.Height];
			for (int frame = 0; frame < 3 && same; frame++)
			{
				Check(store.CopyLayer(frame, FrameLayer.Field, copy), "CopyLayer frame " + frame);
				for (int y = 0; y < scene.Height && same; y++)
				{
					for (int x = 0; x < scene.Width; x++)
					{
						Half expected = (Half)fields[frame][(y + 1) * cpu.Stride + x + 1];
						if (copy[y * scene.Width + x] != expected)
						{
							same = false;
							break;
						}
					}
				}
			}
			Check(same, "CopyLayer returns the Half of every interior cell");
			Check(store.CopyLayer(2, FrameLayer.Intensity, copy) && MaxAbs(fields[2]) > 0 && (float)copy[24 * scene.Width + 20] > 0, "intensity plane populated");
			Check(!store.CopyLayer(2, FrameLayer.Intensity, copy) == false, "a read while alive succeeds");
			bool threw = false;
			try
			{
				store.Write(5, cpu.FieldData, cpu.IntensityData, cpu.Stride);
			}
			catch (ArgumentOutOfRangeException)
			{
				threw = true;
			}
			Check(threw, "out-of-order write is rejected");
			try
			{
				store.Write(3, cpu.FieldData, cpu.IntensityData, cpu.Stride);
				threw = false;
			}
			catch (ArgumentOutOfRangeException)
			{
				threw = true;
			}
			Check(threw, "write beyond the capacity is rejected");
		}
		Check(!File.Exists(path), "file deleted on dispose");
		Check(FrameStore.SizeOf(1920, 1080, FrameLayers.Field | FrameLayers.Intensity, 1250) == 64 + 1250L * 2 * 1920 * 1080 * 2, "SizeOf matches the layout");

		// a full-HD store of 1 GB: the writes must run at memory speed and the dispose must not flush the
		// dirty pages to disk (the file is deleted anyway; this used to take a minute for a 10 GB run)
		const int bigW = 1920, bigH = 1080, bigFrames = 64;
		path = Path.Combine(Path.GetTempPath(), "library_check-big-" + Guid.NewGuid().ToString("N") + FrameCache.Extension);
		float[] bigField = new float[(bigW + 2) * (bigH + 2)];
		float[] bigIntensity = new float[(bigW + 2) * (bigH + 2)];
		for (int i = 0; i < bigField.Length; i++)
		{
			bigField[i] = (i % 97) / 97f - 0.5f;
			bigIntensity[i] = (i % 89) / 89f;
		}
		var clock = Stopwatch.StartNew();
		var big = FrameStore.Create(path, bigW, bigH, FrameLayers.Field | FrameLayers.Intensity, bigFrames, 1);
		for (int frame = 0; frame < bigFrames; frame++)
		{
			big.Write(frame, bigField, bigIntensity, bigW + 2);
		}
		double writeMs = clock.Elapsed.TotalMilliseconds;
		clock.Restart();
		var plane = new Half[bigW * bigH];
		for (int frame = 0; frame < bigFrames; frame++)
		{
			big.CopyLayer(frame, FrameLayer.Field, plane);
			big.CopyLayer(frame, FrameLayer.Intensity, plane);
		}
		double readMs = clock.Elapsed.TotalMilliseconds;
		Check(plane[0] == (Half)bigIntensity[(bigW + 2) + 1] && plane[bigW * bigH - 1] == (Half)bigIntensity[bigH * (bigW + 2) + bigW], "big store reads back what was written");
		clock.Restart();
		big.Dispose();
		double disposeMs = clock.Elapsed.TotalMilliseconds;
		Console.WriteLine($"  1 GB store: {writeMs:0} ms writing ({big.TotalBytes / writeMs / 1000:0} MB/s, {writeMs / bigFrames:0.0} ms/frame), {readMs:0} ms reading ({readMs / bigFrames:0.0} ms/frame), {disposeMs:0} ms dispose");
		Check(!File.Exists(path), "big store deleted on dispose");
		Check(disposeMs < 1500, "disposing a 1 GB store does not flush it to disk (" + disposeMs.ToString("0") + " ms)");
	}

	private static void CheckRenderer()
	{
		Console.WriteLine("--- renderer");
		var values = new Half[4] { (Half)0f, (Half)0.5f, (Half)(-2f), (Half)1f };
		var pixels = new int[4];
		FrameRenderer.RenderField(values, 2, 2, null, 1f, pixels);
		Check(pixels[0] == unchecked((int)0xFF7F7F7F) && pixels[2] == unchecked((int)0xFF000000) && pixels[3] == unchecked((int)0xFFFEFEFE), "field without LUT: grey 127 / 0 / 254");
		int[] lut = new int[255];
		for (int i = 0; i < 255; i++)
		{
			lut[i] = i;
		}
		FrameRenderer.RenderField(values, 2, 2, lut, 1f, pixels);
		Check(pixels[1] == ColorLut.FieldIndex(0.5f, 1f) && pixels[1] == 190, "field with LUT uses FieldIndex (0.5 -> 190)");
		var intensities = new Half[4] { (Half)0f, (Half)(-1f), (Half)0.25f, (Half)9f };
		FrameRenderer.RenderIntensity(intensities, 2, 2, lut, 1f, pixels);
		Check(pixels[0] == 0 && pixels[1] == 0 && pixels[2] == 127 && pixels[3] == 254, "intensity: <= 0 -> 0, sqrt(0.25) -> 127, clamp -> 254");
		var stops = new Rgb[ColorScheme.StopCount];
		for (int i = 0; i < stops.Length; i++)
		{
			stops[i] = new Rgb((byte)(i * 30), 0, (byte)(255 - i * 30));
		}
		ColorScheme scheme = ColorScheme.FromStops("test", stops);
		ColorLut built = ColorLut.Build(scheme);
		Check(built.ColorAt(0) == stops[0] && built.ColorAt(254) == stops[^1] && built.ColorAt(127) == stops[4], "LUT from a stop scheme hits the stops at 0, 127 and 254");
		Check(ColorLut.Build(scheme, blackLevel: 0.5).ColorAt(0).G == 127, "black level 0.5 lifts a 0 channel to 127");
	}

	private static void CheckColorSchemes()
	{
		Console.WriteLine("--- colour schemes");
		string folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ColorSchemeList"));
		var errors = new List<string>();
		List<ColorScheme> schemes = ColorSchemeStore.LoadBuiltIn(errors);
		foreach (string error in errors)
		{
			Console.WriteLine("  " + error);
		}
		Check(errors.Count == 0, "all built-in schemes load from the assembly (" + schemes.Count + ")");
		Check(schemes.Count >= 20 && schemes.All(s => s.IsBuiltIn && s.FilePath == ""), "at least 20 schemes embedded, marked built-in");
		List<ColorScheme> fromFiles = ColorSchemeStore.LoadFolder(folder, null);
		Check(fromFiles.Count == schemes.Count && fromFiles.Select(s => s.Name).SequenceEqual(schemes.Select(s => s.Name)), "the embedded set equals the repo's ColorSchemeList folder");
		ColorScheme? field = ColorSchemeStore.Find(schemes, Display.DefaultFieldColorScheme);
		ColorScheme? intensity = ColorSchemeStore.Find(schemes, Display.DefaultIntensityColorScheme);
		Check(field != null && intensity != null, "default field and intensity schemes found (case-insensitive)");
		Check(field != null && field.HasStops && field.Stops!.Length == ColorScheme.StopCount, "Default_Field has 9 stops");
		Check(intensity != null && intensity.SchemeType == ColorScheme.SchemeTypeIntensity, "Default_Intensity is typed intensity");
		if (field != null)
		{
			string temp = Path.Combine(Path.GetTempPath(), "library_check-scheme.json");
			ColorSchemeStore.Save(field.Clone(field.Name), temp);
			string original = File.ReadAllText(Path.Combine(folder, "Default_Field.json"));
			string saved = File.ReadAllText(temp);
			File.Delete(temp);
			Check(saved.ReplaceLineEndings() == original.ReplaceLineEndings(), "Save reproduces the shipped file");
		}
	}

	private static void CheckRun()
	{
		Console.WriteLine("--- run");
		var project = new Project();
		project.Scene.Width = 64;
		project.Scene.Height = 48;
		project.Scene.DampingEdge = 4;
		project.Simulation.Steps = 200;
		project.Simulation.VisualizeEvery = 2;
		Source source = project.AddSource();
		source.X = 20;
		source.Y = 24;
		source.Frequency = 120;
		Probe probe = project.AddProbe();
		probe.AX = 20;
		probe.AY = 24;
		RunSpec spec = RunSpec.Create(project, null, preferGpu: false);
		Check(spec.FrameCount == 100 && spec.Layers == (FrameLayers.Field | FrameLayers.Intensity) && spec.Emitters.Length == 1 && spec.Probes.Length == 1, "spec: 100 frames, both layers (intensity preview on), 1 emitter, 1 probe");

		string path = Path.Combine(Path.GetTempPath(), "library_check-run-" + Guid.NewGuid().ToString("N") + FrameCache.Extension);
		using (var store = FrameStore.Create(path, spec.Width, spec.Height, spec.Layers, spec.FrameCount, spec.VisualizeEvery))
		{
			var run = new SimulationRun(spec, store, null);
			var states = new List<RunState>();
			run.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(SimulationRun.State))
				{
					states.Add(run.State);
				}
			};
			int finished = 0;
			run.Finished += (_, _) => finished++;
			var stopwatch = Stopwatch.StartNew();
			run.Start();
			Check(run.Completion.Wait(30000), "run completes within 30 s");
			Console.WriteLine("  " + run.EngineDescription + ", " + run.Elapsed.TotalMilliseconds.ToString("0") + " ms");
			Check(run.State == RunState.Completed && finished == 1 && run.Error == null, "state Completed, Finished raised once");
			Check(states.SequenceEqual([RunState.Starting, RunState.Running, RunState.Completed]), "states: Starting, Running, Completed");
			Check(run.StepsDone == 200 && run.FrameCount == 100 && store.Count == 100, "200 steps, 100 frames in the store");
			Check(run.Traces[0].Series.Count == 200 && run.Traces[0].StepsSampled == 200, "probe trace has one sample per step");
			Check(run.EngineDescription.StartsWith("CPU"), "CPU engine used");
			var lastFrame = new Half[64 * 48];
			Check(store.CopyLayer(99, FrameLayer.Field, lastFrame) && (float)lastFrame[24 * 64 + 20] != 0f, "last frame has a field at the source");
			run.Dispose();
		}
		Check(!File.Exists(path), "store deleted after the run");

		Console.WriteLine("--- run: pause, stop, GPU preferred");
		project.Simulation.Steps = 100000;
		spec = RunSpec.Create(project, null, preferGpu: true);
		path = Path.Combine(Path.GetTempPath(), "library_check-run-" + Guid.NewGuid().ToString("N") + FrameCache.Extension);
		using (var store = FrameStore.Create(path, spec.Width, spec.Height, spec.Layers, spec.FrameCount, spec.VisualizeEvery))
		using (var run = new SimulationRun(spec, store, null))
		{
			run.Start();
			var wait = Stopwatch.StartNew();
			while (run.State != RunState.Running && wait.ElapsedMilliseconds < 20000)
			{
				Thread.Sleep(10);
			}
			Check(run.State == RunState.Running, "second run is running (" + run.EngineDescription + (run.EngineNote.Length > 0 ? "; " + run.EngineNote : "") + ")");
			Thread.Sleep(200);
			run.Pause();
			wait.Restart();
			while (run.State != RunState.Paused && wait.ElapsedMilliseconds < 5000)
			{
				Thread.Sleep(10);
			}
			int atPause = run.StepsDone;
			Thread.Sleep(200);
			Check(run.State == RunState.Paused && run.StepsDone == atPause && atPause > 0 && atPause < spec.Steps, "paused at step " + atPause + " and holding");
			Check(run.IsPauseRequested, "pause is reported as requested");
			run.TogglePause();
			wait.Restart();
			while (run.State != RunState.Running && wait.ElapsedMilliseconds < 5000)
			{
				Thread.Sleep(10);
			}
			Check(run.State == RunState.Running, "resumed through TogglePause");
			run.Stop();
			Check(run.Completion.Wait(10000) && run.State == RunState.Cancelled && !run.IsActive, "stopped -> Cancelled, " + run.StepsDone + " steps, " + run.FrameCount + " frames");
			Check(run.FrameCount == store.Count && run.FrameCount > 0, "frame count published matches the store");
		}
		Check(!File.Exists(path), "second store deleted");
	}
}
