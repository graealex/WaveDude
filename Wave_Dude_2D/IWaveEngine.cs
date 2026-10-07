using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// A wave-field solver (CPU <see cref="WaveSimulator"/> or OpenCL <see cref="GpuWaveSimulator"/>), created by
	/// <see cref="WaveEngineFactory"/> and driven by <see cref="SimulationRunner"/> on its worker thread. The field and
	/// intensity buffers are padded by one cell on each side, so row y of the scene starts at index (y + 1) * Stride + 1.
	/// Only <see cref="PrepareFrame"/> and the data properties are also used from the UI thread, and only while the
	/// runner is paused (see <see cref="SimulationRunner.RenderCurrentFrame"/>).
	/// </summary>
	internal interface IWaveEngine : IDisposable
	{
		/// <summary>Short name for the status bar and result: "CPU (n threads)" or "GPU: device name (OpenCL)".</summary>
		string Description { get; }

		/// <summary>Scene width in cells (= canvas pixels), without padding.</summary>
		int Width { get; }

		/// <summary>Scene height in cells (= canvas pixels), without padding.</summary>
		int Height { get; }

		/// <summary>Row pitch of <see cref="FieldData"/> and <see cref="IntensityData"/>: <see cref="Width"/> + 2.</summary>
		int Stride { get; }

		/// <summary>
		/// Current field amplitude, <see cref="Stride"/> * (<see cref="Height"/> + 2) floats. Valid for reading after
		/// <see cref="PrepareFrame"/>; the CPU engine exposes its live buffer (a different array instance after every
		/// sub-step, so re-read the property per frame), the GPU engine a host copy.
		/// </summary>
		float[] FieldData { get; }

		/// <summary>Accumulated intensity, same layout as <see cref="FieldData"/>; null when intensity tracking was not requested.</summary>
		float[] IntensityData { get; }

		/// <summary>Advances the simulation by one time step (internally several sub-steps with the 4th-order Laplacian).</summary>
		void AdvanceOneStep();

		/// <summary>
		/// Makes the requested buffers reflect the current state so the renderer can read them: a no-op on the CPU,
		/// a device-to-host copy on the GPU. Call before touching <see cref="FieldData"/> / <see cref="IntensityData"/>.
		/// </summary>
		void PrepareFrame(bool needField, bool needIntensity);

		/// <summary>
		/// Installs the probe definitions for this run (the GPU engine compiles its probe kernel here).
		/// A null or empty sampler installs nothing.
		/// </summary>
		/// <exception cref="InvalidOperationException">A probe needs the intensity but it is not tracked, or the GPU probe kernel cannot be built.</exception>
		void SetProbes(ProbeSampler sampler);

		/// <summary>Records one sample per series probe for the current step and accumulates integrating profiles; call after each <see cref="AdvanceOneStep"/>.</summary>
		void SampleProbes();

		/// <summary>
		/// Returns and clears the series samples gathered since the last call, one array per probe (empty for profile probes).
		/// </summary>
		/// <returns>Empty outer array when no probes are installed.</returns>
		float[][] TakeProbeSamples();

		/// <summary>Reads the current values along each profile probe, one array per profile probe.</summary>
		/// <returns>Null entries for non-profile probes; empty outer array when no probes are installed.</returns>
		float[][] ReadProbeProfiles();
	}
}
