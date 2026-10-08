namespace _2D_wave_sim_V01
{
	/// <summary>
	/// Nominal size of a probe's graph box in canvas pixels (<c>ProbeGraphPainter.NominalBoxSize</c>).
	/// The box is shrunk to fit half the canvas width and the full canvas height
	/// (<c>ProbeGraphPainter.BoxInCanvas</c>). The numeric value is the index of the "box size"
	/// combo box on the Probes tab.
	/// </summary>
	public enum ProbeBoxSize
	{
		/// <summary>200 x 100 canvas pixels.</summary>
		Tiny = 0,
		/// <summary>300 x 150 canvas pixels.</summary>
		Small = 1,
		/// <summary>400 x 200 canvas pixels (default).</summary>
		Medium = 2,
		/// <summary>500 x 250 canvas pixels.</summary>
		Large = 3,
		/// <summary>700 x 350 canvas pixels.</summary>
		ExtraLarge = 4
	}
}
