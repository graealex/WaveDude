namespace _2D_wave_sim_V01
{
	/// <summary>
	/// How a simulation run ended. Set by <see cref="SimulationRunner"/> on its worker thread in
	/// <see cref="SimulationResult.Status"/> and read by <c>Form1.OnSimulationFinished</c> on the UI thread.
	/// </summary>
	internal enum SimulationStatus
	{
		/// <summary>All requested steps were computed.</summary>
		Completed,
		/// <summary>The user stopped the run (or closed the program) before the last step.</summary>
		Cancelled,
		/// <summary>An exception ended the run early; the reason is in <see cref="SimulationResult.Message"/>.</summary>
		Failed
	}
}
