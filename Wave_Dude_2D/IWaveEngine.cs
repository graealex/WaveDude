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
	internal interface IWaveEngine
	{
		/// <summary>Where the simulation runs, for the status bar and result: for example "CPU (8 threads)" or "GPU: NVIDIA GeForce MX130 (OpenCL)".</summary>
		string Description { get; }

		/// <summary>Scene width in cells (= canvas pixels), without padding.</summary>
		int Width { get; }

		/// <summary>Scene height in cells (= canvas pixels), without padding.</summary>
		int Height { get; }

		/// <summary>Row pitch of <see cref="FieldData"/> and <see cref="IntensityData"/>: <see cref="Width"/> + 2. Pixel (x, y) is at (y + 1) * Stride + x + 1.</summary>
		int Stride { get; }

		/// <summary>
		/// The field of the last completed step, <see cref="Stride"/> * (<see cref="Height"/> + 2) floats. Valid for
		/// reading after <see cref="PrepareFrame"/>(true, ...); the CPU engine exposes its live buffer (a different
		/// array instance after every sub-step, so re-read the property per frame), the GPU engine a host copy.
		/// Do not change.
		/// </summary>
		float[] FieldData { get; }

		/// <summary>
		/// The averaged intensity, same layout as <see cref="FieldData"/>, valid after <see cref="PrepareFrame"/>(..., true);
		/// null when intensity tracking was not requested. Do not change.
		/// </summary>
		float[] IntensityData { get; }

		/// <summary>One time step: apply the sources, update the field (internally several sub-steps with the 4th-order Laplacian), update the intensity.</summary>
		void AdvanceOneStep();

		/// <summary>
		/// Makes the requested buffers reflect the current state so the renderer can read them: a no-op on the CPU;
		/// on the GPU the data is copied from the graphics memory, so only ask for what is going to be drawn. Call
		/// before touching <see cref="FieldData"/> / <see cref="IntensityData"/>.
		/// </summary>
		void PrepareFrame(bool needField, bool needIntensity);

		/// <summary>
		/// Installs the probe definitions for this run (the GPU engine compiles its probe kernel here).
		/// A null or empty sampler installs nothing.
		/// </summary>
		/// <exception cref="InvalidOperationException">A probe needs the intensity but it is not tracked, or the GPU probe kernel cannot be built.</exception>
		void SetProbes(ProbeSampler sampler);

		/// <summary>
		/// Measures the probes in the state after the last <see cref="AdvanceOneStep"/>: one value per step for every
		/// point probe, and the running totals of the line probes that integrate the intensity.
		/// </summary>
		void SampleProbes();

		/// <summary>
		/// Returns and clears the values measured for the point probes since the last call, one array per probe (empty for line probes).
		/// </summary>
		/// <returns>Empty outer array when no probes are installed.</returns>
		float[][] TakeProbeSamples();

		/// <summary>
		/// The values along the line of every line probe in the current state: the field, the intensity, or the
		/// intensity totals of the steps so far. On the GPU this waits for the card, so do not call it every step.
		/// </summary>
		/// <returns>Null entries for point probes; empty outer array when no probes are installed.</returns>
		float[][] ReadProbeProfiles();
	}
}
