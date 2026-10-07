namespace _2D_wave_sim_V01
{
	/// <summary>
	/// The quantity a probe records. Which values are allowed depends on <see cref="ProbePlaceOn"/>
	/// (see <see cref="ProbeSettings.Normalize"/>). The engine treats every value above
	/// <see cref="Amplitude"/> as "read from the intensity buffer" (<c>ProbeSampler.UseIntensity</c>).
	/// </summary>
	public enum ProbeMeasurement
	{
		/// <summary>The instantaneous field value (wave amplitude, signed) at the probe pixel(s).</summary>
		Amplitude,
		/// <summary>The current value of the engine's running (exponentially decayed) intensity buffer at the probe pixel(s).</summary>
		Intensity,
		/// <summary>
		/// Intensity summed over simulation steps. For a point probe the running sum is formed in
		/// <c>ProbeCollector</c>; for a line probe the engine keeps per-pixel totals. Accumulation
		/// starts at <see cref="ProbeSettings.StartAfterStep"/> when that option is enabled.
		/// </summary>
		IntegratedIntensity
	}
}
