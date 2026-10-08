using System;
using System.Collections.Generic;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Picks the wave engine for a run: the GPU (OpenCL) engine when requested and available, otherwise the CPU engine.
	/// Static-only; called by <see cref="SimulationRunner"/> on its worker thread once per run.
	/// </summary>
	internal sealed class WaveEngineFactory
	{
		/// <summary>Not instantiable.</summary>
		private WaveEngineFactory()
		{
		}

		/// <summary>
		/// Creates the engine for <paramref name="scene"/>. With <see cref="SimulationSettings.UseGpu"/> set it first tries
		/// <see cref="GpuWaveSimulator.TryCreate"/>, translating the usual OpenCL loader failures (missing, wrong-bitness or
		/// incomplete OpenCL.dll) into a readable reason; any failure falls back to the CPU <see cref="WaveSimulator"/>.
		/// Never throws for GPU problems, but CPU engine construction may still throw (e.g. out of memory).
		/// Object sources (the Sources tab) run on either engine, the same as the image sources.
		/// </summary>
		/// <param name="scene">Per-pixel speed, damping and image sources of the canvas; the engine copies it into its padded grid.</param>
		/// <param name="settings">Supplies <see cref="SimulationSettings.UseGpu"/> and the physics options passed to the engine (frequency scale, Laplacian order, time decay, intensity scale).</param>
		/// <param name="sourceAmplitude">Amplitude multiplier for the image-defined sources.</param>
		/// <param name="trackIntensity">Whether the engine must accumulate the intensity buffer (needed for intensity video, preview or probes).</param>
		/// <param name="note">
		/// Receives an empty string when the GPU was not requested or the GPU engine was created; "GPU not used: reason" when the GPU
		/// was requested but could not be used (the loader message above, or the per-device errors from <c>TryCreate</c>).
		/// The "the first choice failed - ..." form is only produced if <c>TryCreate</c> ever returns an engine together with a
		/// message, which it does not at present.
		/// </param>
		/// <param name="objectSources">The generated elements of the object sources (Sources tab) to drive in addition to the image sources; may be null.</param>
		/// <returns>A ready engine; the caller owns and must dispose it.</returns>
		public static IWaveEngine Create(SceneData scene, SimulationSettings settings, double sourceAmplitude, bool trackIntensity, ref string note, List<SourceElement> objectSources = null)
		{
			note = "";
			if (settings.UseGpu)
			{
				string gpuMessage = "";
				GpuWaveSimulator gpu = null;
				try
				{
					gpu = GpuWaveSimulator.TryCreate(scene, settings.SourceFrequencyScale, sourceAmplitude, trackIntensity, settings.Use4thOrderLaplacian, settings.TimeDecayFactor, settings.IntensityScale, ref gpuMessage, objectSources);
				}
				catch (DllNotFoundException)
				{
					gpuMessage = "no OpenCL driver (OpenCL.dll) found; install the driver of the graphics card";
				}
				catch (BadImageFormatException)
				{
					gpuMessage = "the OpenCL driver does not match this 32/64-bit program";
				}
				catch (EntryPointNotFoundException)
				{
					gpuMessage = "the OpenCL driver is incomplete";
				}
				catch (Exception ex)
				{
					gpuMessage = ex.Message;
				}
				if (gpu != null)
				{
					if (gpuMessage.Length > 0)
					{
						note = "the first choice failed - " + gpuMessage;
					}
					return gpu;
				}
				note = "GPU not used: " + gpuMessage;
			}
			return new WaveSimulator(scene, settings.SourceFrequencyScale, sourceAmplitude, trackIntensity, settings.Use4thOrderLaplacian, settings.TimeDecayFactor, settings.IntensityScale, objectSources);
		}
	}
}
