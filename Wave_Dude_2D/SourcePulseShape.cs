using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Envelope shape of one "on" burst of a <see cref="SourceMode.Pulsed"/> source. Only used
	/// when the mode is pulsed; stored in the project as <c>"gaussian"</c> / <c>"smoothed_square"</c>.
	/// </summary>
	public enum SourcePulseShape
	{
		/// <summary>Gaussian bell centred in the on-window; its sigma is one sixth of the on-width, so the burst is essentially zero at both edges.</summary>
		Gaussian,
		/// <summary>Flat-topped burst with smoothed rising and falling edges that spans the whole on-width.</summary>
		SmoothedSquare
	}
}
