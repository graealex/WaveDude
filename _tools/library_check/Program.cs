using System.Globalization;
using System.Diagnostics;
using System.Numerics.Tensors;
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
		new ProbeSpec("B", ProbeKind.Line, ProbeMeasurement.IntegratedValue, 5, 5, 40, 30, 50),
		new ProbeSpec("C", ProbeKind.Point, ProbeMeasurement.Value, 30, 20, 0, 0, 0),
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
		if (args.Length > 2 && args[0] == "simplify-schemes")
		{
			// one-off: slims every .wdscheme of a folder with more than the editor's 9 stops to its bends (2026-10-14)
			double tolerance = double.Parse(args[2], CultureInfo.InvariantCulture);
			foreach (string file in Directory.GetFiles(args[1], "*" + ColorSchemeStore.XmlExtension))
			{
				ColorScheme scheme = ColorSchemeStore.LoadXml(file);
				int before = scheme.Stops.Count;
				if (before <= ColorScheme.StopCount)
				{
					continue;
				}
				scheme.Simplify(tolerance);
				ColorSchemeStore.SaveXml(scheme, file);
				Console.WriteLine($"{Path.GetFileName(file)}: {before} -> {scheme.Stops.Count} stops");
			}
			return 0;
		}
		if (args.Length > 1 && args[0] == "convert-schemes")
		{
			// one-off: the author's JSON files of a folder become .wdscheme files beside them (2026-10-13)
			foreach (string file in Directory.GetFiles(args[1], "*.json"))
			{
				ColorScheme scheme = ColorSchemeStore.Load(file);
				string target = ColorSchemeStore.PathFor(args[1], Path.GetFileNameWithoutExtension(file));
				ColorSchemeStore.SaveXml(scheme, target);
				Console.WriteLine($"{Path.GetFileName(file)} -> {Path.GetFileName(target)} ({scheme.Stops.Count} stops)");
			}
			return 0;
		}
		if (args.Length > 0 && args[0] == "bench-export")
		{
			ExportChecks.Bench(args.Length > 1 ? int.Parse(args[1]) : 1920, args.Length > 2 ? int.Parse(args[2]) : 1080);
			return 0;
		}
		if (args.Length > 0 && args[0] == "export")
		{
			// just the export checks (they take a few seconds and need no engine)
			ExportChecks.Run(Check);
			Console.WriteLine(_failures == 0 ? "EXPORT OK" : _failures + " check(s) failed");
			return _failures == 0 ? 0 : 1;
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
		CheckProbeGraph();
		CheckImport();
		CheckRun();
		ExportChecks.Run(Check);

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
		using (var store = FrameStore.Create(path, scene.Width, scene.Height, FrameLayers.Field | FrameLayers.Intensity, 3, 2, ramBudget: 0))
		{
			Check(store.RamFrames == 0 && store.HasFile && File.Exists(path) && new FileInfo(path).Length == store.FileBytes && store.FileBytes == FrameStore.HeaderSize + 3 * store.FrameBytes, "no budget: the file is created at once and pre-sized (" + store.FileBytes + " bytes)");
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
		Check(FrameStore.FramesInBudget(1920, 1080, FrameLayers.Field | FrameLayers.Intensity, 1L << 30) == 129 && FrameStore.FramesInBudget(64, 48, FrameLayers.Field, 0) == 0, "FramesInBudget: 129 full-HD frames of both planes per GB");

		// the two tiers: a budget for two of five frames; the first two stay in memory, the other three go to the file
		path = Path.Combine(Path.GetTempPath(), "library_check-tiers-" + Guid.NewGuid().ToString("N") + FrameCache.Extension);
		using (var store = FrameStore.Create(path, scene.Width, scene.Height, FrameLayers.Field | FrameLayers.Intensity, 5, 2, ramBudget: 2L * 2 * scene.Width * scene.Height * 2))
		{
			Check(store.RamFrames == 2 && store.FirstSpilledFrame == 2 && store.HasFile && new FileInfo(path).Length == FrameStore.HeaderSize + 3 * store.FrameBytes, "budget for two frames: the file is sized for the other three");
			float[][] fields = new float[5][];
			for (int frame = 0; frame < 5; frame++)
			{
				Run(cpu, 2);
				fields[frame] = (float[])cpu.FieldData.Clone();
				store.Write(frame, cpu.FieldData, cpu.IntensityData, cpu.Stride);
			}
			Check(store.Count == 5 && store.FramesInMemory == 2 && store.FramesOnDisk == 3 && store.RamBytes == 2 * store.FrameBytes, "five frames: two in memory, three on disk");
			bool same = true;
			var copy = new Half[scene.Width * scene.Height];
			for (int frame = 0; frame < 5 && same; frame++)
			{
				same = store.CopyLayer(frame, FrameLayer.Field, copy);
				for (int y = 0; y < scene.Height && same; y++)
				{
					for (int x = 0; x < scene.Width; x++)
					{
						if (copy[y * scene.Width + x] != (Half)fields[frame][(y + 1) * cpu.Stride + x + 1])
						{
							same = false;
							break;
						}
					}
				}
			}
			Check(same, "every frame reads back from its tier");
		}
		Check(!File.Exists(path), "tiered store: file deleted on dispose");
		// a run that fits never touches the disk
		path = Path.Combine(Path.GetTempPath(), "library_check-ram-" + Guid.NewGuid().ToString("N") + FrameCache.Extension);
		using (var store = FrameStore.Create(path, scene.Width, scene.Height, FrameLayers.Field, 3, 1, ramBudget: 1L << 30))
		{
			store.Write(0, cpu.FieldData, null, cpu.Stride);
			Check(store.RamFrames == 3 && !store.HasFile && !File.Exists(path) && store.FileBytes == 0 && store.FirstSpilledFrame == 3, "a run within the budget creates no file");
		}
		Check(FrameMemory.PhysicalBytes() > 1L << 30 && FrameMemory.AvailableBytes() <= FrameMemory.PhysicalBytes() && FrameMemory.Budget(50) <= FrameMemory.PhysicalBytes() / 2 && FrameMemory.Budget(0) == 0,
			"memory: " + FrameMemory.Gigabytes(FrameMemory.PhysicalBytes()) + " physical, " + FrameMemory.Gigabytes(FrameMemory.AvailableBytes()) + " available, budget at 50 % " + FrameMemory.Gigabytes(FrameMemory.Budget(50)));

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
		// written the way the run does it: Half planes already converted (the engine's captures), no conversion in the store
		var bigFieldHalf = new Half[bigW * bigH];
		var bigIntensityHalf = new Half[bigW * bigH];
		for (int y = 0; y < bigH; y++)
		{
			TensorPrimitives.ConvertToHalf(bigField.AsSpan((y + 1) * (bigW + 2) + 1, bigW), bigFieldHalf.AsSpan(y * bigW, bigW));
			TensorPrimitives.ConvertToHalf(bigIntensity.AsSpan((y + 1) * (bigW + 2) + 1, bigW), bigIntensityHalf.AsSpan(y * bigW, bigW));
		}
		var clock = Stopwatch.StartNew();
		var big = FrameStore.Create(path, bigW, bigH, FrameLayers.Field | FrameLayers.Intensity, bigFrames, 1);
		for (int frame = 0; frame < bigFrames; frame++)
		{
			big.Write(frame, bigFieldHalf, bigIntensityHalf);
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
		Console.WriteLine($"  1 GB store in memory ({big.FramesInMemory} of {bigFrames} frames): {writeMs:0} ms writing ({big.TotalBytes / writeMs / 1000:0} MB/s, {writeMs / bigFrames:0.0} ms/frame), {readMs:0} ms reading ({readMs / bigFrames:0.0} ms/frame), {disposeMs:0} ms dispose");
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
		Check(fromFiles.Count > 0 && fromFiles.All(s => s.FilePath.EndsWith(ColorSchemeStore.XmlExtension, StringComparison.OrdinalIgnoreCase)), "the shipped schemes are XML (.wdscheme)");
		ColorScheme? field = ColorSchemeStore.Find(schemes, Display.DefaultFieldColorScheme);
		ColorScheme? intensity = ColorSchemeStore.Find(schemes, Display.DefaultIntensityColorScheme);
		Check(field != null && intensity != null, "default field and intensity schemes found (case-insensitive)");
		Check(field != null && field.IsStopScheme, "Default_Field is a 9-stop scheme (its table consolidated into the stops)");
		Check(intensity != null && intensity.Kind == ColorSchemeKind.Intensity, "Default_Intensity is typed intensity");
		Check(schemes.Count(s => s.IsStopScheme && s.IsEvenlySpaced) == 11 && schemes.All(s => s.Stops.Count <= 24), "11 shipped schemes have the author's 9 evenly spaced stops, the 12 sampled colormaps are slimmed to at most 24");
		// simplification: a ramp with one bend, sampled at 255 points, keeps its two ends and the bend only
		var ramp = new ColorScheme("ramp", Enumerable.Range(0, 255).Select(i => i <= 127 ? new Rgb((byte)(i * 2), 0, 0) : new Rgb(254, (byte)((i - 127) * 2), 0)));
		int sampled = ramp.Stops.Count;
		ramp.Simplify(1.0);
		Check(sampled == 255 && ramp.Stops.Count == 3 && Math.Abs(ramp.Stops[1].Position - 127 / 254.0) < 1e-9 && ramp.GetEntry(127) == new Rgb(254, 0, 0) && ramp.GetEntry(254) == new Rgb(254, 254, 0),
			"Simplify keeps only the bend of a two-segment ramp and the table still hits it");
		Check(Rgb.Parse("#FF8000") == new Rgb(255, 128, 0) && Rgb.Parse("[10, 20, 30]") == new Rgb(10, 20, 30) && new Rgb(1, 2, 3).ToString() == "#010203", "Rgb parses hex and triplets and prints #RRGGBB");
		if (field != null)
		{
			// the author's JSON layout still comes out of a 9-stop scheme (what his program reads)
			string temp = Path.Combine(Path.GetTempPath(), "library_check-scheme.json");
			ColorSchemeStore.Save(field.Clone(field.Name), temp);
			string json = File.ReadAllText(temp);
			File.Delete(temp);
			Check(json.Contains("\"stops\": [[253,253,253]") && json.Contains("\"scheme_type\": \"field\""), "JSON export writes the author's stops and type");
			// the program's own format: the shipped XML round-trips byte for byte, standalone and inside a project
			string xmlPath = Path.Combine(Path.GetTempPath(), "library_check-scheme" + ColorSchemeStore.XmlExtension);
			ColorSchemeStore.SaveXml(field.Clone(field.Name), xmlPath);
			string xml = File.ReadAllText(xmlPath);
			ColorScheme back = ColorSchemeStore.LoadXml(xmlPath);
			File.Delete(xmlPath);
			string shipped = File.ReadAllText(Path.Combine(folder, "Default_Field" + ColorSchemeStore.XmlExtension));
			Check(xml.ReplaceLineEndings() == shipped.ReplaceLineEndings(), "SaveXml reproduces the shipped .wdscheme file");
			Check(xml.Contains("<Stop At=\"0.125\" Color=\"#") && !xml.Contains("xmlns"), "XML writes one Stop element per stop with its position and #RRGGBB, no namespaces");
			Check(back.Name == field.Name && back.Kind == field.Kind && back.Stops.Select(s => (s.Position, s.Color)).SequenceEqual(field.Stops.Select(s => (s.Position, s.Color))), "XML round trip keeps name, kind and the 9 stops with positions");
			var uneven = new ColorScheme("uneven", [new Rgb(0, 0, 0)]);
			uneven.SetStops([new ColorStop(0.0, new Rgb(0, 0, 0)), new ColorStop(0.25, new Rgb(255, 255, 255)), new ColorStop(1.0, new Rgb(0, 0, 255))]);
			Check(uneven.GetEntry(0) == new Rgb(0, 0, 0) && uneven.GetEntry(127) == new Rgb(170, 170, 255) && uneven.GetEntry(254) == new Rgb(0, 0, 255) && uneven.GetEntry(32) == new Rgb(129, 129, 129), "stops at any position interpolate between their neighbours (0, 0.25, 1)");
			ColorStop added = uneven.AddStop();
			Check(uneven.Stops.Count == 4 && Math.Abs(added.Position - 0.625) < 1e-9 && uneven.Stops.IndexOf(added) == 2, "AddStop fills the widest gap, in position order");
			var project = new Project();
			project.ColorSchemes.Add(field.Clone("Mine"));
			project.Scene.ImageData = [1, 2, 3, 4, 250, 0, 255];
			project.Scene.Image = "scene.png";
			project.Scene.Mode = SceneMode.Image;
			using var stream = new MemoryStream();
			project.Save(stream);
			stream.Position = 0;
			Project loaded = Project.Load(stream);
			Check(loaded.ColorSchemes.Count == 1 && loaded.ColorSchemes[0].Name == "Mine" && loaded.ColorSchemes[0].Colors.SequenceEqual(field.Colors), "a project file embeds its own schemes");
			Check(loaded.Scene.ImageData != null && loaded.Scene.ImageData.SequenceEqual(project.Scene.ImageData) && loaded.Scene.Image == "scene.png", "a project file embeds the scene image bytes (base64)");
			var edited = loaded.ColorSchemes[0];
			int changes = 0;
			edited.PropertyChanged += (_, args) => changes += args.PropertyName == nameof(ColorScheme.Stops) ? 1 : 0;
			edited.Stops[4].Color = new Rgb(1, 2, 3);
			Check(changes == 1 && edited.GetEntry(127) == new Rgb(1, 2, 3), "editing one stop notifies once and rebuilds the table");
		}
		// the author's glowing edges: a vertical black|white step at x = 2 of a 5 x 5 image
		var image = new byte[5 * 5 * 4];
		for (int y = 0; y < 5; y++)
		{
			for (int x = 2; x < 5; x++)
			{
				int o = (y * 5 + x) * 4;
				image[o] = image[o + 1] = image[o + 2] = 255;
				image[o + 3] = 255;
			}
		}
		byte[] edges = GlowingEdges.Compute(image, 5, 5, 20);
		int At(int x, int y) => edges[(y * 5 + x) * 4];
		Check(At(1, 1) == 255 && At(2, 2) == 255 && At(3, 2) == 0 && At(0, 2) == 0 && At(2, 0) == 0 && edges[(2 * 5 + 2) * 4 + 3] == 255,
			"glowing edges: the two columns beside the step light up (Sobel x 0.25 clamps at 255), the flat area and the outer ring stay black");
		int[] frame = new int[25];
		Array.Fill(frame, unchecked((int)0xFF404040));
		GlowingEdges.BlendAdditive(frame, 5, 5, edges, 0.5);
		Check(frame[2 * 5 + 2] == unchecked((int)0xFFBFBFBF) && frame[2 * 5 + 3] == unchecked((int)0xFF404040), "additive blend adds edge x gain per channel and leaves flat pixels alone");
	}

	private static void CheckProbeGraph()
	{
		Console.WriteLine("--- probe graph geometry");
		var inv = CultureInfo.InvariantCulture;
		var probe = new Probe { Name = "P", BoxX = 1700, BoxY = 900 };
		Check(ProbeGraph.NominalBoxSize(ProbeBoxSize.Medium) == new BoxSize(400, 200) && ProbeGraph.NominalBoxSize(ProbeBoxSize.Tiny) == new BoxSize(200, 100), "nominal box sizes");
		var turned = new Source { Name = "Turned", X = 100, Y = 100, Array = new SourceArray { SizeX = 3, SizeY = 1, SpacingX = 10, Angle = 90 } };
		turned.GenerateElements(1);
		Check(turned.Elements.Select(e => (e.X, e.Y)).SequenceEqual([(100, 110), (100, 100), (100, 90)]), "a 3 x 1 array turned by 90 degrees stands vertical, counter-clockwise on the screen");
		turned.Array.Angle = 45;
		turned.GenerateElements(1);
		Check(turned.Elements.Select(e => (e.X, e.Y)).SequenceEqual([(93, 107), (100, 100), (107, 93)]), "45 degrees: the ends at (-7, +7) and (+7, -7)");
		turned.Array.Angle = 540;
		turned.Array.Normalize();
		bool wraps = turned.Array.Angle == 180;
		turned.Array.Angle = -180;
		turned.Array.Normalize();
		wraps &= turned.Array.Angle == 180;
		turned.Array.Angle = -190;
		turned.Array.Normalize();
		wraps &= turned.Array.Angle == 170;
		turned.Array.Angle = 190;
		turned.Array.Normalize();
		Check(wraps && turned.Array.Angle == -170, "an angle is normalised into (-180, 180]: 540 -> 180, -180 -> 180, -190 -> 170, 190 -> -170");
		var follower = new Probe { Name = "F" };
		follower.AX = 50;
		follower.AY = 60;
		Check(follower.IsBoxAttached && follower.BoxX == 50 && follower.BoxY == 60, "an attached graph box moves with the probe");
		follower.BoxX = 10;
		follower.AX = 70;
		follower.AY = 61;
		Check(!follower.IsBoxAttached && follower.BoxX == 10 && follower.BoxY == 60, "a box moved away is detached on both axes and stays put");
		follower.BoxX = 70;
		follower.BoxY = 61;
		follower.AX = 75;
		Check(follower.IsBoxAttached && follower.BoxX == 75 && follower.BoxY == 61, "a box put back on the probe is attached again");
		Check(ProbeGraph.BoxInCanvas(probe, 1920, 1080) == new BoxRect(1520, 880, 400, 200), "box clamped into a full-HD canvas");
		Check(ProbeGraph.BoxInCanvas(probe, 64, 48) == new BoxRect(32, 32, 32, 16), "box shrunk to half the width of a 64 x 48 canvas, aspect kept");
		probe.BoxSize = ProbeBoxSize.Custom;
		probe.BoxWidth = 500;
		probe.BoxHeight = 100;
		Check(ProbeGraph.BoxSizeOf(probe) == new BoxSize(500, 100) && ProbeGraph.BoxInCanvas(probe, 1920, 1080) == new BoxRect(1420, 900, 500, 100), "custom box size used as it is");
		Check(ProbeGraph.Snap(405, 195) == (ProbeBoxSize.Medium, new BoxSize(400, 200)) && ProbeGraph.Snap(690, 360) == (ProbeBoxSize.ExtraLarge, new BoxSize(700, 350)), "a dragged size near a preset snaps to it");
		Check(ProbeGraph.Snap(450, 230) == (ProbeBoxSize.Custom, new BoxSize(450, 230)) && ProbeGraph.Snap(10, 10) == (ProbeBoxSize.Custom, new BoxSize(60, 30)), "other sizes stay custom, clamped to the minimum");
		Check(ProbeGraph.NiceTicks(-1, 1, 4).SequenceEqual([-1.0, -0.5, 0.0, 0.5, 1.0]) && ProbeGraph.NiceTicks(0, 2500, 5).SequenceEqual([0.0, 500.0, 1000.0, 1500.0, 2000.0, 2500.0]), "nice ticks at 1/2/5 steps, zero exact");
		Check(ProbeGraph.NiceTicks(0.3, 0.3, 4).Count == 0 && ProbeGraph.NiceTicks(double.NaN, 1, 4).Count == 0, "no ticks for an empty or NaN range");
		Check(ProbeGraph.FormatTick(0.123456, inv) == "0.123" && ProbeGraph.FormatTick(0, inv) == "0" && ProbeGraph.FormatTick(0.5, new CultureInfo("de-DE")) == "0,5", "tick labels: 3 significant digits, locale decimal separator");
		Check(ProbeGraph.FormatPosition(1234.6, 2500, inv) == "1235" && ProbeGraph.FormatPosition(2.25, 5, inv) == "2.3", "position labels: integers over wide spans, one decimal otherwise");

		// a point probe's series: 120 samples, a sine between -0.5 and 0.5
		float[] values = new float[1024];
		for (int i = 0; i < 120; i++)
		{
			values[i] = 0.5f * MathF.Sin(i * 0.2f);
		}
		var point = new Probe { Name = "Point", Window = 500 };
		var data = new ProbeGraphData(values, 120, null, 0, 120);
		ProbeGraph.ComputeRanges(point, data, 2500, out double xMin, out double xMax, out double yMin, out double yMax);
		Check(xMin == 0 && xMax == 120, "running window, auto x: 0..samples shown (" + xMin + ".." + xMax + ")");
		Check(Math.Abs(yMin + 0.54) < 0.001 && Math.Abs(yMax - 0.54) < 0.001, "auto y: symmetric about zero, largest magnitude plus 8 % (" + yMin.ToString("0.000", inv) + ".." + yMax.ToString("0.000", inv) + ")");
		float[] lopsided = new float[16];
		lopsided[3] = -0.2f;
		lopsided[7] = 0.5f;
		ProbeGraph.ComputeRanges(point, new ProbeGraphData(lopsided, 16, null, 0, 16), 2500, out _, out _, out double lopMin, out double lopMax);
		Check(Math.Abs(lopMin + 0.54) < 0.001 && Math.Abs(lopMax - 0.54) < 0.001, "lopsided data still gets a symmetric axis (" + lopMin.ToString("0.00", inv) + ".." + lopMax.ToString("0.00", inv) + ")");
		ProbeGraph.ComputeRanges(point, new ProbeGraphData(new float[16], 16, null, 0, 16), 2500, out _, out _, out double zeroMin, out double zeroMax);
		Check(zeroMin == -1 && zeroMax == 1, "all-zero data keeps the fixed range instead of a 1e-6 fit");
		var intensityProbe = new Probe { Name = "I", Layer = ProbeLayer.Intensity, Measurement = ProbeMeasurement.Value };
		float[] positive = new float[16];
		positive[5] = 0.3f;
		ProbeGraph.ComputeRanges(intensityProbe, new ProbeGraphData(positive, 16, null, 0, 16), 2500, out _, out _, out double intMin, out double intMax);
		Check(intMin == 0 && Math.Abs(intMax - 0.324) < 0.001, "an intensity axis runs from zero to the largest value plus 8 %");
		point.AutoScaleX = false;
		ProbeGraph.ComputeRanges(point, data, 2500, out xMin, out xMax, out _, out _);
		Check(xMin == 0 && xMax == 500, "running window, fixed x: full window from the start");
		point.ScaleMode = ProbeScaleMode.Full;
		ProbeGraph.ComputeRanges(point, data, 2500, out xMin, out xMax, out _, out _);
		Check(xMin == 0 && xMax == 2500, "full scale, fixed x: the whole run");
		point.StartAfter = 100;
		point.AutoScaleX = true;
		ProbeGraph.ComputeRanges(point, data, 2500, out xMin, out xMax, out _, out _);
		Check(xMin == 100 && xMax == 120, "start after 100: x starts there");
		point.AutoScaleY = false;
		point.YMin = 2;
		point.YMax = 1;
		ProbeGraph.ComputeRanges(point, data, 2500, out _, out _, out yMin, out yMax);
		Check(yMin == -1 && yMax == 1, "inverted fixed y range falls back to -1..1");
		point.StartAfter = 0;
		point.AutoScaleY = true;
		point.ScaleMode = ProbeScaleMode.Running;
		// the text keeps the renderer's size (11 px, 14.3 px lines, 6.6 px per character here); the margins come from the labels
		ProbeGraphStyle style = ProbeGraphStyle.Estimate(11);
		ProbeGraphLayout layout = ProbeGraph.Layout(point, data, 2500, 400, 200, style, inv);
		float widest = layout.YTicks.Max(t => style.Measure(t.Label));
		Check(layout.IsVisible && layout.HasPlot && layout.ShowLabels && layout.FontSize == 11 && Math.Abs(layout.PlotX - (3 + widest + 4)) < 0.01 && Math.Abs(layout.PlotY - (14.3 + 6)) < 0.01
			&& Math.Abs(layout.PlotWidth - (400 - layout.PlotX - 8)) < 0.01 && Math.Abs(layout.PlotHeight - (200 - layout.PlotY - 14.3 - 4)) < 0.01,
			"400 x 200 layout: font 11, plot at (" + layout.PlotX.ToString("0.0", inv) + ", " + layout.PlotY.ToString("0.0", inv) + ") " + layout.PlotWidth.ToString("0.0", inv) + " x " + layout.PlotHeight.ToString("0.0", inv) + " from the measured labels");
		Check(layout.Curve.Length == 120 && layout.Curve[0].X > layout.PlotX && layout.Curve[^1].X <= layout.PlotX + layout.PlotWidth + 0.01 && layout.Title == "Point" && layout.Readout == "= " + ProbeGraph.FormatTick(values[119], inv),
			"one point per sample inside the plot, title and readout");
		Check(layout.YTicks.Any(t => t.IsZero && Math.Abs(t.Position - (layout.PlotY + layout.PlotHeight / 2)) < 2) && layout.XTicks.Count >= 4 && layout.XTicks[0].Label == "0", "zero line in the middle, step ticks from 0");
		Check(ProbeGraph.Layout(point, data, 2500, 30, 20, style, inv).IsVisible == false && ProbeGraph.Layout(point, data, 2500, 80, 40, style, inv) is { IsVisible: true, HasPlot: false }, "too small: nothing, or the box alone");
		ProbeGraphLayout bare = ProbeGraph.Layout(point, data, 2500, 120, 45, style, inv);
		Check(bare is { HasPlot: true, ShowLabels: false } && bare.YTicks.All(t => t.Label == "") && bare.PlotX == 6 && Math.Abs(bare.PlotHeight - (45 - 20.3 - 6)) < 0.01, "a box too low for labels keeps the plot and drops the labels");
		// the UI harness's 172 x 86 box at frame 49: three value labels 23 px apart fit a 47 px plot (asking for two ticks gave only the zero)
		float[] lowValues = new float[1024];
		for (int i = 40; i < 99; i++)
		{
			lowValues[i] = (i % 7 == 3 ? 0.0135f : 0.0126f) * MathF.Sin((i - 40) * 0.5f);
		}
		ProbeGraphLayout low = ProbeGraph.Layout(point, new ProbeGraphData(lowValues, 99, null, 0, 99), 100, 172, 86, new ProbeGraphStyle(11, 14.65f, t => t.Length * 6.2f), inv);
		Check(low.ShowLabels && low.YTicks.Count == 3 && low.YTicks.All(t => t.Label.Length > 0), "a low plot keeps three value labels while they are a line apart (" + string.Join(" ", low.YTicks.Select(t => t.Label)) + ")");
		ProbeGraphLayout narrow = ProbeGraph.Layout(point, data, 2500, 110, 80, style, inv);
		Check(narrow.ShowLabels && narrow.XTicks.Count <= 3 && narrow.XTicks.Sum(t => style.Measure(t.Label) + 8) - 8 <= narrow.PlotWidth, "a narrow plot gets fewer step labels so they do not collide (" + narrow.XTicks.Count + ")");
		var dense = new ProbeGraphData(Enumerable.Range(0, 1024).Select(i => (float)Math.Sin(i)).ToArray(), 1024, null, 0, 1024);
		point.ScaleMode = ProbeScaleMode.Full;
		ProbeGraphLayout denseLayout = ProbeGraph.Layout(point, dense, 1024, 200, 100, style, inv);
		Check(denseLayout.Curve.Length <= 2 * (int)denseLayout.PlotWidth && denseLayout.Curve.Length > 100, "dense series binned to two points per pixel column (" + denseLayout.Curve.Length + ")");

		// a line probe: 50 values along a 49-pixel line, gated by "start after step"
		float[] profile = Enumerable.Range(0, 50).Select(i => 0.1f * i).ToArray();
		var line = new Probe { Name = "Line", Kind = ProbeKind.Line, StartAfter = 100, Layer = ProbeLayer.Intensity };
		ProbeGraphLayout before = ProbeGraph.Layout(line, new ProbeGraphData(null, 0, profile, 49, 50), 2500, 400, 200, style, inv);
		ProbeGraphLayout after = ProbeGraph.Layout(line, new ProbeGraphData(null, 0, profile, 49, 100), 2500, 400, 200, style, inv);
		Check(before.Curve.Length == 0 && before.Readout == "" && before.XTicks[^1].Label == "1", "line probe blank before its start step");
		Check(after.Curve.Length == 50 && after.Readout == "max 4.9" && after.XTicks[^1].Label == "40" && after.YTicks[0].Label == "0", "line probe after its start step: profile over 0..49 pixels, intensity fitted from 0");
	}

	/// <summary>The repository root (the folder holding the author's examples), found by walking up from the harness binary; null when not found.</summary>
	private static string? FindRepoRoot()
	{
		for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
		{
			if (Directory.Exists(Path.Combine(dir.FullName, "Wave_Dude_Examples")))
			{
				return dir.FullName;
			}
		}
		return null;
	}

	private static void CheckImport()
	{
		Console.WriteLine("--- legacy project import");
		string? root = FindRepoRoot();
		if (root == null)
		{
			Console.WriteLine("  skip import: the Wave_Dude_Examples folder was not found above the harness");
			return;
		}
		var notes = new List<string>();
		Project p = LegacyProject.Import(Path.Combine(root, "Wave_Dude_Examples", "Example12_Dispersion", "configuration.json"), notes, out string name);
		Check(name == "Example12_Dispersion" && p.Description.StartsWith("This example shows the difference", StringComparison.Ordinal) && notes.Count == 0, "Example 12: name and description, no notes");
		Check(p.Scene.Mode == SceneMode.Image && p.Scene.Image == "frequency_race_track (2).png" && p.Scene.ImageData is { Length: 58074 } && p.Scene.Width == 1920 && p.Scene.Height == 1080, "scene: image mode, the image embedded (58074 bytes) under its file name, 1920 x 1080");
		Check(p.Scene.DampingEdge == 64 && !p.Scene.ImageSources && p.Scene.ObjectSources && p.Scene.RefractiveIndex == 1.0 && p.Scene.ImageSourceScale == 1.0, "scene: damping 64, object sources on, image sources off");
		Check(p.Simulation.Steps == 15000 && p.Simulation.VisualizeEvery == 2 && !p.Simulation.FourthOrderLaplacian && p.Simulation.WaveSpeed == 1.0 && p.Simulation.TimeDecay == 10.0 && p.Simulation.SourceFrequencyScale == 2.0 && p.Simulation.IntensityScale == 1.0, "simulation: 15000 steps every 2, 9-point stencil, c 1, decay 10");
		Check(p.Display.Field.Brightness == 80 && p.Display.Intensity.Brightness == 30 && p.Display.Field.BlackLevel == -0.05 && p.Display.Intensity.BlackLevel == 0 && p.Display.Field.ColorScheme == "Default_Field" && p.Display.Intensity.ColorScheme == "Default_intensity", "display: brightness 80 / 30, black levels, schemes");
		Check(p.Display.Field.Preview && p.Display.Intensity.Preview && p.Display.ProbeGraphsInExport && p.Display.Video.Fps == 60 && p.Display.Video.Quality == 70, "display: previews on, probe graphs in the export, 60 fps at the file's quality 70");
		Check(p.Sources.Count == 4 && p.Sources.Select(s => s.Name).SequenceEqual(["Source 1", "Source 2", "Source 3", "Source 4"]) && p.Sources.Select(s => s.Frequency).SequenceEqual([40, 100, 150, 240]) && p.Sources.Select(s => s.Amplitude).SequenceEqual([70, 80, 90, 100]), "4 single sources with their names, levels and amplitudes");
		Check(p.Sources.All(s => s.Enabled && s.Array == null && s.DetachAfter == Source.NeverDetach && s.Mode == SourceMode.Continuous && s.Elements.Count == 1) && p.Sources[0].X == 150 && p.Sources[0].Y == 140 && p.Sources[3].Y == 924, "sources enabled, single, never detaching, placed, with one emitter each");
		Check(p.Probes.Count == 2 && p.Probes.All(pr => !pr.Active && pr.Layer == ProbeLayer.Field && pr.Measurement == ProbeMeasurement.Amplitude && pr.BoxSize == ProbeBoxSize.Medium && pr.Window == 500 && pr.StartAfter == 0), "2 inactive field probes, medium boxes, window 500, no start step");
		Check(p.Probes[0].Kind == ProbeKind.Line && p.Probes[0].AX == 1713 && p.Probes[0].AY == 2 && p.Probes[0].BX == 1719 && p.Probes[0].BY == 1073 && p.Probes[0].BoxX == 1 && p.Probes[0].BoxY == 426 && p.Probes[0].Name == "Probe 1", "probe 1: a line from (1713, 2) to (1719, 1073), box at (1, 426)");
		Check(p.Probes[1].Kind == ProbeKind.Point && p.Probes[1].AX == 100 && p.Probes[1].BoxX == 450 && p.Probes[1].BoxY == 20 && p.Probes[1].Name == "Probe 2", "probe 2: the author's default point probe");
		using (var stream = new MemoryStream())
		{
			p.Save(stream);
			stream.Position = 0;
			Project back = Project.Load(stream);
			Check(back.Sources.Count == 4 && back.Probes.Count == 2 && back.Scene.ImageData!.SequenceEqual(p.Scene.ImageData!) && back.Simulation.Steps == 15000 && !back.Simulation.FourthOrderLaplacian, "the imported project round-trips through the XML format");
		}
		Check(LegacyProject.SuggestedFileName(Path.Combine(root, "Wave_Dude_Examples", "Example12_Dispersion", "configuration.json"), name) == Path.Combine(root, "Wave_Dude_Examples", "Example12_Dispersion", "Example12_Dispersion.wdproj"), "Save As proposes <name>.wdproj beside the original");

		// a composite, randomised, detaching, pulsed source; a missing image in canvas mode; a global probe switch
		notes.Clear();
		p = LegacyProject.Import(Path.Combine(root, "Wave_Dude_Examples", "Example5_Small_incoherent_source", "configuration.json"), notes, out _);
		Check(p.Scene.Mode == SceneMode.Canvas && p.Scene.ImageData == null && p.Scene.Image == "scene_images/spherrical_mirror_small_central_aperture.png" && notes.Any(n => n.Contains("not found")), "Example 5: canvas mode, the missing image's path kept and noted");
		Source composite = p.Sources[0];
		Check(composite.Array is { SizeX: 5, SizeY: 5, SpacingX: 3, SpacingY: 3, RandomFrequency: 50, RandomPhase: 90, RandomOffWidth: 19, RandomPosition: 1 } && composite.Elements.Count == 25, "a 5 x 5 array with its random amounts, 25 emitters regenerated");
		Check(composite.Mode == SourceMode.Pulsed && composite.PulseWidth == 5 && composite.OffWidth == 20 && composite.DetachAfter == 2000 && composite.Enabled && notes.Any(n => n.Contains("random offsets")), "pulsed 5 / 20 cycles, detaching after step 2000, the regeneration noted");
		Check(p.Simulation.WaveSpeed == 0.563 && p.Display.Field.ColorScheme == "Orange_symmetry" && p.Display.Intensity.ColorScheme == "Purple_rain" && p.Display.Field.Brightness == 50, "c 0.563 and the chosen schemes");
		Check(p.Probes[0] is { Active: true, Kind: ProbeKind.Point, AutoScaleY: false, YMin: -0.02, YMax: 0.02, BoxSize: ProbeBoxSize.ExtraLarge, BoxX: 14, BoxY: 14 } && p.Probes[1] is { Layer: ProbeLayer.Intensity, Measurement: ProbeMeasurement.Value, Kind: ProbeKind.Line, StartAfter: 0, YMax: 0.0001 }, "probes: fixed value ranges, the unused start step dropped, use_probes honoured");

		// start-after steps in use, an intensity probe, a detaching single source, the fallback decay
		notes.Clear();
		p = LegacyProject.Import(Path.Combine(root, "Wave_Dude_Examples", "Example3_Spherical_mirror", "configuration.json"), notes, out _);
		Check(p.Probes[0] is { StartAfter: 600, Window: 3000, Layer: ProbeLayer.Intensity, Measurement: ProbeMeasurement.Value } && p.Probes[1] is { StartAfter: 600, Measurement: ProbeMeasurement.IntegratedValue, Kind: ProbeKind.Line }, "Example 3: start-after steps kept when their flag is on, intensity measurements");
		Check(p.Sources[0] is { Array: null, DetachAfter: 670, PulseWidth: 70, OffWidth: 100, Frequency: 200 } && p.Simulation.TimeDecay == 8 && p.Display.Intensity.ColorScheme == "Copper", "a single detaching source (its unused array settings ignored), decay 8, Copper");
		Check(p.Scene.Mode == SceneMode.Image && p.Scene.ImageData == null && notes.Count == 1, "image mode with the image missing: one note");

		// a source the author never placed
		notes.Clear();
		p = LegacyProject.Import(Path.Combine(root, "Project", "Test", "configuration.json"), notes, out _);
		Check(p.Sources.Count == 1 && !p.Sources[0].Enabled && notes.Any(n => n.Contains("no position")) && p.Simulation.TimeDecay == 5, "Test: a source without a position comes in disabled and noted");
		bool refused = false;
		string newer = Path.Combine(Path.GetTempPath(), "library_check-newer.json");
		File.WriteAllText(newer, "{ \"format_version\": 2 }");
		try
		{
			LegacyProject.Import(newer, null, out _);
		}
		catch (InvalidDataException)
		{
			refused = true;
		}
		File.Delete(newer);
		Check(refused, "a newer format version is refused");
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
		Probe lineProbe = project.AddProbe();
		lineProbe.Kind = ProbeKind.Line;
		lineProbe.AX = 5;
		lineProbe.AY = 5;
		lineProbe.BX = 40;
		lineProbe.BY = 30;
		Probe integrated = project.AddProbe();
		integrated.Layer = ProbeLayer.Intensity;
		integrated.Measurement = ProbeMeasurement.IntegratedValue;
		integrated.Kind = ProbeKind.Line;
		integrated.AX = 10;
		integrated.AY = 30;
		integrated.BX = 50;
		integrated.BY = 30;
		integrated.StartAfter = 50;
		RunSpec spec = RunSpec.Create(project, null, preferGpu: false);
		Check(spec.FrameCount == 100 && spec.Layers == (FrameLayers.Field | FrameLayers.Intensity) && spec.Emitters.Length == 1 && spec.Probes.Length == 3, "spec: 100 frames, both layers (intensity preview on), 1 emitter, 3 probes");
		Check(ReferenceEquals(spec.Probes[0].Origin, probe) && ReferenceEquals(spec.Probes[1].Origin, lineProbe), "probe specs remember their model probe as the origin");

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
			Check(run.Traces[1].Profiles.Count == 100 && run.Traces[1].Profiles.All(p => p.Length == 36) && run.Traces[1].ProfileLength > 42.0 && run.Traces[1].Series.Count == 0,
				"line probe trace has one profile per frame (36 pixels, length " + run.Traces[1].ProfileLength.ToString("0.0", CultureInfo.InvariantCulture) + "), no series");
			Check(run.Traces[1].Profiles[0].All(v => v == 0f) && run.Traces[1].Profiles[99].Any(v => v != 0f) && !ReferenceEquals(run.Traces[1].Profiles[0], run.Traces[1].Profiles[99]), "profiles: frame 0 quiet, frame 99 has a wave, distinct arrays");
			ProbeGraphData at = ProbeGraphData.At(run.Traces[0], 40, 20);
			Check(at.Count == 41 && at.StepsSampled == 41 && at.Profile == null, "graph data at frame 20 / step 40: 41 samples");
			ProbeGraphData lineAt = ProbeGraphData.At(run.Traces[1], 40, 20);
			Check(ReferenceEquals(lineAt.Profile, run.Traces[1].Profiles[20]) && ReferenceEquals(ProbeGraphData.At(run.Traces[1], 400, 200).Profile, run.Traces[1].Profiles[99]), "graph data of the line probe: frame 20's profile; the newest stands in beyond the recorded frames");
			Check(run.Traces[2].Profiles.Count == 100 && run.Traces[2].Profiles[20].All(v => v == 0f) && run.Traces[2].Profiles[99].Any(v => v > 0f) && run.Traces[2].Profiles[99].All(v => v >= 0f), "integrated line probe: nothing before its start step, non-negative sums after");
			// the scratch runner records the same probes on its way to a frame (the graph is live before a run, 2026-10-14)
			using (var scratch = new ScratchRunner(null))
			using (var arrived = new ManualResetEventSlim())
			{
				ScratchResult? result = null;
				scratch.Completed += (_, r) =>
				{
					result = r;
					arrived.Set();
				};
				scratch.Request(spec, 20);
				Check(arrived.Wait(30000) && result is { Snapshot: not null, Frame: 20 }, "scratch frame 20 computed");
				Check(result!.Traces.Length == 3 && result.Traces[0].Series.Count == 41 && result.Traces[0].StepsSampled == 41, "scratch traces: the point probe's 41 samples up to step 40");
				Check(result.Traces[0].Series.Values.AsSpan(0, 41).SequenceEqual(run.Traces[0].Series.Values.AsSpan(0, 41)), "scratch series equals the run's first 41 samples bit for bit");
				Check(result.Traces[1].Profiles.Count == 1 && result.Traces[1].Profiles[0].AsSpan().SequenceEqual(run.Traces[1].Profiles[20]), "scratch profile equals the run's frame 20 profile");
				// a second request on the kept engine, past the integrated probe's start step: its sums must start from zero again
				arrived.Reset();
				scratch.Request(spec, 60);
				Check(arrived.Wait(30000) && result is { Frame: 60 } && result.Traces[2].Profiles.Count == 1 && result.Traces[2].Profiles[0].AsSpan().SequenceEqual(run.Traces[2].Profiles[60]), "a second scratch frame (60): the integrated profile equals the run's, the kept engine started its sums afresh");
			}
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
			if (run.EngineDescription.StartsWith("GPU", StringComparison.Ordinal) && run.FrameCount > 60)
			{
				// the GPU scratch path: two requests on one kept engine (the probe program compiled once), compared with the GPU run
				using var scratch = new ScratchRunner(null);
				using var arrived = new ManualResetEventSlim();
				ScratchResult? result = null;
				scratch.Completed += (_, r) =>
				{
					result = r;
					arrived.Set();
				};
				scratch.Request(spec, 10);
				bool first = arrived.Wait(30000) && result is { Snapshot: not null, Frame: 10 };
				arrived.Reset();
				var clock = Stopwatch.StartNew();
				scratch.Request(spec, 60);
				bool second = arrived.Wait(30000) && result is { Snapshot: not null, Frame: 60 };
				clock.Stop();
				Check(first && second, "GPU scratch frames 10 and 60 computed (" + clock.ElapsedMilliseconds + " ms for the second, probes re-installed without a compile)");
				Check(second && MaxDiff(result!.Traces[0].Series.Values.AsSpan(0, 121), run.Traces[0].Series.Values.AsSpan(0, 121)) <= 1e-5f * Math.Max(1e-3f, MaxAbs(run.Traces[0].Series.Values.AsSpan(0, 121))), "GPU scratch series matches the GPU run's first 121 samples");
				Check(second && MaxDiff(result!.Traces[2].Profiles[0], run.Traces[2].Profiles[60]) <= 1e-5f * Math.Max(1e-3f, MaxAbs(run.Traces[2].Profiles[60])), "GPU scratch integrated profile matches the GPU run's frame 60");
			}
		}
		Check(!File.Exists(path), "second store deleted");
	}
}
