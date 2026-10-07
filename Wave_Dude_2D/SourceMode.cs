using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Time behaviour of a point source. Chosen with the mode radio buttons on the source tab,
	/// copied into every generated <see cref="SourceElement"/> and evaluated by the wave engines
	/// on each simulation step.
	/// </summary>
	public enum SourceMode
	{
		/// <summary>The source emits a steady sine wave for the whole run (envelope is always 1).</summary>
		Continuous,
		/// <summary>The sine wave is gated by a repeating on/off envelope whose shape is a <see cref="SourcePulseShape"/> and whose on and off lengths are given in cycles.</summary>
		Pulsed
	}
}
