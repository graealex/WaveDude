namespace _2D_wave_sim_V01
{
	/// <summary>
	/// One batch of probe results handed from the simulation worker thread to the UI. Created by
	/// <see cref="ProbeCollector.TakeForUi"/> on the worker, raised through <c>SimulationRunner.ProbesUpdated</c>
	/// and applied to the form's own <see cref="ProbeTrace"/>s on the UI thread (marshalled via <c>PostToUi</c>).
	/// Never modified after construction, so no locking is needed.
	/// </summary>
	internal sealed class ProbeUpdate
	{
		public ProbeUpdate(float[][] samples, float[][] profiles, double[] lengths, int stepsDone)
		{
			Samples = samples;
			Profiles = profiles;
			Lengths = lengths;
			StepsDone = stepsDone;
		}

		/// <summary>
		/// Per probe (index parallel to the run's probe list): the series values sampled since the previous batch,
		/// in step order. Freshly allocated per batch; empty (never null) for line probes or when nothing new arrived.
		/// </summary>
		public readonly float[][] Samples;

		/// <summary>
		/// Per probe: the new line profile when it was refreshed since the previous batch, otherwise null. The array
		/// is the very instance the worker's trace holds; it is never written to after the engine produced it, so
		/// sharing it is safe.
		/// </summary>
		public readonly float[][] Profiles;

		/// <summary>Per probe: line length in canvas pixels (0 for point probes). This is the <see cref="ProbeSampler.Lengths"/> array itself, shared read-only by all batches.</summary>
		public readonly double[] Lengths;

		/// <summary>Simulation steps completed when the batch was taken; becomes <see cref="ProbeTrace.StepsSampled"/> on the UI side.</summary>
		public readonly int StepsDone;
	}
}
