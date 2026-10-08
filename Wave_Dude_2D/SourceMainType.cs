using System;

namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Whether a <see cref="SourceSettings"/> entry describes one point source (or line, in a future version)
	/// or a rectangular array of identical point sources. Selected with the "main type" combo box on the source
	/// tab (the numeric values are the combo-box indices) and stored in the project as <c>"single"</c> /
	/// <c>"composite"</c>.
	/// </summary>
	public enum SourceMainType
	{
		/// <summary>A single point source at (PosX, PosY); generates exactly one <see cref="SourceElement"/>. Named Simple (not Single) because Single is a reserved VB keyword (the floating-point type) in the original source.</summary>
		Simple = 0,
		/// <summary>An ArraySizeX x ArraySizeY grid of point sources centred on (PosX, PosY), optionally randomised per element.</summary>
		Composite = 1
	}
}
