using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Whether a <see cref="SourceSettings"/> entry describes one point source or a rectangular
	/// array of identical point sources. Selected with the "main type" combo box on the source
	/// tab and stored in the project as <c>"single"</c> / <c>"composite"</c>.
	/// </summary>
	public enum SourceMainType
	{
		/// <summary>A single point source at (PosX, PosY); generates exactly one <see cref="SourceElement"/>.</summary>
		Simple,
		/// <summary>An ArraySizeX x ArraySizeY grid of point sources centred on (PosX, PosY), optionally randomised per element.</summary>
		Composite
	}
}
