namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Geometry of a probe. Stored in <see cref="ProbeSettings.Kind"/>; the numeric value is the
	/// index of the "probe type" combo box on the Probes tab, so the order must not change.
	/// </summary>
	public enum ProbeKind
	{
		/// <summary>Samples the single canvas pixel at position A once per simulation step, producing a time series.</summary>
		Point,
		/// <summary>Samples every pixel on the Bresenham line from A to B, producing a profile along the line (no time axis).</summary>
		Line
	}
}
