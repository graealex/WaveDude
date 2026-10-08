using System;
using System.Collections.Generic;
using System.Drawing;
using _2D_wave_sim_V01;

namespace EngineCheck
{
	// Drives the CPU and (if available) GPU engines over the same scene and compares their fields, so a
	// refactor of either engine is checked against the other. Exit code 0 = all checks passed.
	internal static class Program
	{
		private static int _failures;

		private static void Check(bool ok, string what)
		{
			Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
			if (!ok) _failures++;
		}

		private static List<SourceElement> Sources()
		{
			return new List<SourceElement>
			{
				new SourceElement { PosX = 20, PosY = 24, Level = 120, Amplitude = 1.0, Mode = SourceMode.Continuous },
				new SourceElement { PosX = 44, PosY = 12, Level = 200, Amplitude = 0.7, PhaseRad = 0.5, Mode = SourceMode.Pulsed, PulseShape = SourcePulseShape.Gaussian, OnWidthCycles = 4, OffWidthCycles = 6 },
				new SourceElement { PosX = 44, PosY = 36, Level = 90, Amplitude = 0.5, Mode = SourceMode.Pulsed, PulseShape = SourcePulseShape.SmoothedSquare, OnWidthCycles = 5, OffWidthCycles = 3, DetachAfterStep = 120 },
				new SourceElement { PosX = 99, PosY = 99, Level = 50, Amplitude = 9.0, Mode = SourceMode.Continuous }, // outside: must be dropped
			};
		}

		private static float MaxAbs(float[] a)
		{
			float m = 0f;
			foreach (float v in a) m = Math.Max(m, Math.Abs(v));
			return m;
		}

		private static float MaxDiff(float[] a, float[] b)
		{
			float m = 0f;
			for (int i = 0; i < a.Length; i++) m = Math.Max(m, Math.Abs(a[i] - b[i]));
			return m;
		}

		private static ProbeSampler Probes(int w, int h)
		{
			var probes = new List<ProbeSettings>
			{
				new ProbeSettings { Kind = ProbeKind.Point, Measurement = ProbeMeasurement.Amplitude, PosAX = 20, PosAY = 24, Active = true },
				new ProbeSettings { Kind = ProbeKind.Line, Measurement = ProbeMeasurement.IntegratedIntensity, PosAX = 5, PosAY = 5, PosBX = 40, PosBY = 30, Active = true, UseStartAfterStep = true, StartAfterStep = 50 },
				new ProbeSettings { Kind = ProbeKind.Point, Measurement = ProbeMeasurement.Intensity, PosAX = 30, PosAY = 20, Active = true },
			};
			return ProbeSampler.Build(probes, w, h);
		}

		private static float MaxDiff(float[][] a, float[][] b)
		{
			float m = 0f;
			for (int i = 0; i < a.Length; i++)
			{
				if (a[i] == null || b[i] == null) { if (a[i] != b[i]) return float.PositiveInfinity; continue; }
				if (a[i].Length != b[i].Length) return float.PositiveInfinity;
				m = Math.Max(m, MaxDiff(a[i], b[i]));
			}
			return m;
		}

		private static void Run(IWaveEngine engine, int steps)
		{
			for (int i = 0; i < steps; i++)
			{
				engine.AdvanceOneStep();
				engine.SampleProbes();
			}
			engine.PrepareFrame(true, true);
		}

		private static int Main()
		{
			const int W = 64, H = 48, Steps = 200;
			// Scene with an image source (green level 160) so both source kernels run.
			Bitmap image = new Bitmap(W, H);
			using (Graphics g = Graphics.FromImage(image)) g.Clear(Color.FromArgb(100, 0, 0)); // refractive index 1.00
			image.SetPixel(10, 10, Color.FromArgb(100, 160, 0));
			SceneData scene = SceneData.FromImage(image, true, 6);
			Check(scene.SourceCount == 1, "scene has 1 image source");

			foreach (bool fourth in new[] { false, true })
			{
				Console.WriteLine("--- 4th order: " + fourth);
				WaveSimulator cpu = new WaveSimulator(scene, 1.0, 1.0, true, fourth, 10.0, 1.0, Sources());
				cpu.SetProbes(Probes(W, H));
				Run(cpu, Steps);
				float[] cpuField = (float[])cpu.FieldData.Clone();
				float[] cpuInten = (float[])cpu.IntensityData.Clone();
				float[][] cpuSamples = cpu.TakeProbeSamples();
				float[][] cpuProfiles = cpu.ReadProbeProfiles();
				Check(cpuSamples[0].Length == Steps && cpuSamples[2].Length == Steps && cpuSamples[1].Length == 0, "CPU point probes have one sample per step");
				Check(cpuProfiles[1] != null && cpuProfiles[1].Length > 30 && MaxAbs(cpuProfiles[1]) > 0f, "CPU integrated line profile populated");
				Check(cpu.TakeProbeSamples()[0].Length == 0, "CPU TakeProbeSamples clears");
				Check(MaxAbs(cpuField) > 0.01f, "CPU field is non-trivial (max |u| = " + MaxAbs(cpuField) + ")");
				Check(MaxAbs(cpuInten) > 0f, "CPU intensity accumulated");
				Check(Math.Abs(cpu.Time - Steps) < 1e-9, "CPU time counts steps");
				// Border stays zero (never written).
				bool borderZero = true;
				for (int x = 0; x < cpu.Stride; x++) borderZero &= cpuField[x] == 0f && cpuField[(H + 1) * cpu.Stride + x] == 0f;
				Check(borderZero, "CPU padding rows untouched");

				string problems = "";
				GpuWaveSimulator gpu = GpuWaveSimulator.TryCreate(scene, 1.0, 1.0, true, fourth, 10.0, 1.0, ref problems, Sources());
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
					float diffInten = MaxDiff(cpuInten, gpu.IntensityData);
					Check(diffField < 1e-3f * Math.Max(1f, MaxAbs(cpuField)), "GPU field matches CPU (max diff " + diffField + ")");
					Check(diffInten < 1e-3f * Math.Max(1f, MaxAbs(cpuInten)), "GPU intensity matches CPU (max diff " + diffInten + ")");
					float[][] gpuSamples = gpu.TakeProbeSamples();
					float[][] gpuProfiles = gpu.ReadProbeProfiles();
					float diffSamples = MaxDiff(cpuSamples, gpuSamples);
					float diffProfiles = MaxDiff(cpuProfiles, gpuProfiles);
					Check(diffSamples < 1e-3f, "GPU point-probe samples match CPU (max diff " + diffSamples + ")");
					Check(diffProfiles < 1e-3f * Math.Max(1f, MaxAbs(cpuProfiles[1])), "GPU line profile matches CPU (max diff " + diffProfiles + ")");
					// Re-installing probes replaces the previous set (no leak, no stale state); clearing empties results.
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
			Console.WriteLine(_failures == 0 ? "ENGINES OK" : _failures + " check(s) failed");
			return _failures == 0 ? 0 : 1;
		}
	}
}
