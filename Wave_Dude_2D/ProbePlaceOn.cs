namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Which simulation output a probe belongs to: it decides both the buffer that is sampled and
	/// the preview picture / exported video on which the probe markers and graph box are drawn.
	/// The numeric value is the index of the "place on" combo box on the Probes tab.
	/// </summary>
	public enum ProbePlaceOn
	{
		/// <summary>The wave field (amplitude) output. Only <see cref="ProbeMeasurement.Amplitude"/> is valid here.</summary>
		Field,
		/// <summary>The intensity output. Allows <see cref="ProbeMeasurement.Intensity"/> and <see cref="ProbeMeasurement.IntegratedIntensity"/>.</summary>
		Intensity
	}
}
