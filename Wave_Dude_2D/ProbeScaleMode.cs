namespace _2D_wave_sim_V01
{
	/// <summary>
	/// How the X (step) axis of a point probe's time-series graph is chosen; see
	/// <c>ProbeGraphPainter.ComputeRanges</c>. Line probes ignore it (their X axis is the
	/// distance along the line) and the UI disables the radio buttons for them.
	/// </summary>
	public enum ProbeScaleMode
	{
		/// <summary>Shows a sliding window of the last <see cref="ProbeSettings.RunningWindow"/> steps.</summary>
		Running,
		/// <summary>Shows the whole run: from the start step to the total step count (or to the samples so far when <see cref="ProbeSettings.AutoScaleX"/> is set).</summary>
		Full
	}
}
